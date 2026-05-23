using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace QudJP.Tools.UnusedCodeInventoryScanner;

internal static class Program
{
    public static int Main(string[] args)
    {
        var parsed = CliArguments.Parse(args);
        if (parsed.ShowHelp)
        {
            Console.Out.WriteLine(
                "Usage: UnusedCodeInventoryScanner --source-root <dir> --config <json-path> --output <json-path> [--fail-on-candidates]");
            return 0;
        }

        if (parsed.Error is not null)
        {
            Console.Error.WriteLine(parsed.Error);
            return 2;
        }

        if (!Directory.Exists(parsed.SourceRoot))
        {
            Console.Error.WriteLine($"source root does not exist or is not a directory: {parsed.SourceRoot}");
            return 1;
        }

        if (!File.Exists(parsed.ConfigPath))
        {
            Console.Error.WriteLine($"config file does not exist: {parsed.ConfigPath}");
            return 1;
        }

        var inventory = UnusedCodeScanner.Scan(parsed.SourceRoot!, parsed.ConfigPath!);
        InventoryWriter.Write(inventory, parsed.OutputPath!);
        Console.Out.WriteLine(
            $"[unused-code-inventory] wrote {inventory.Totals.Candidates} candidate(s) to {parsed.OutputPath}");
        if (parsed.FailOnCandidates && inventory.Totals.Candidates > 0)
        {
            Console.Error.WriteLine($"unused code candidates found: {inventory.Totals.Candidates}");
            return 3;
        }

        return 0;
    }
}

internal sealed class CliArguments
{
    public string? SourceRoot { get; private init; }
    public string? ConfigPath { get; private init; }
    public string? OutputPath { get; private init; }
    public string? Error { get; private init; }
    public bool FailOnCandidates { get; private init; }
    public bool ShowHelp { get; private init; }

    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        string? sourceRoot = null;
        string? configPath = null;
        string? outputPath = null;
        var failOnCandidates = false;

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--help":
                    return new CliArguments { ShowHelp = true };
                case "--source-root":
                    if (index + 1 >= args.Count)
                    {
                        return new CliArguments { Error = "Missing value for --source-root" };
                    }

                    sourceRoot = args[++index];
                    break;
                case "--config":
                    if (index + 1 >= args.Count)
                    {
                        return new CliArguments { Error = "Missing value for --config" };
                    }

                    configPath = args[++index];
                    break;
                case "--output":
                    if (index + 1 >= args.Count)
                    {
                        return new CliArguments { Error = "Missing value for --output" };
                    }

                    outputPath = args[++index];
                    break;
                case "--fail-on-candidates":
                    failOnCandidates = true;
                    break;
                default:
                    return new CliArguments { Error = $"Unknown argument: {args[index]}" };
            }
        }

        if (sourceRoot is null || configPath is null || outputPath is null)
        {
            return new CliArguments { Error = "Missing required argument. Use --help." };
        }

        return new CliArguments
        {
            SourceRoot = ExpandHome(sourceRoot),
            ConfigPath = ExpandHome(configPath),
            OutputPath = ExpandHome(outputPath),
            FailOnCandidates = failOnCandidates,
        };
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }

        return path;
    }
}

internal static class InventoryWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Write(InventoryDocument inventory, string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(inventory, Options) + "\n", new UTF8Encoding(false));
    }
}

internal static class UnusedCodeScanner
{
    private const string SchemaVersion = "1.0";
    private const string GameVersion = "1.0.4";
    public static InventoryDocument Scan(string sourceRoot, string configPath)
    {
        var root = Path.GetFullPath(sourceRoot);
        var config = ScannerConfig.Load(configPath);
        var syntaxTrees = LoadSyntaxTrees(root, config);
        var compilation = CSharpCompilation.Create(
            "QudJP.UnusedCodeInventory",
            syntaxTrees,
            ReferenceResolver.ResolveReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var declarations = DeclarationCollector.Collect(compilation, config);
        var references = ReferenceCollector.Collect(compilation, declarations.Symbols);
        var classified = CandidateClassifier.Classify(declarations, references, config);
        return BuildInventory(config, syntaxTrees, classified);
    }

    private static List<SyntaxTree> LoadSyntaxTrees(string sourceRoot, ScannerConfig config)
    {
        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => new SourceFile(sourceRoot, path))
            .Where(file => config.ShouldInclude(file.RelativePath))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => CSharpSyntaxTree.ParseText(
                File.ReadAllText(file.FullPath),
                CSharpParseOptions.Default
                    .WithLanguageVersion(LanguageVersion.Preview)
                    .WithPreprocessorSymbols(config.PreprocessorSymbols),
                file.RelativePath))
            .ToList();
    }

    private static InventoryDocument BuildInventory(
        ScannerConfig config,
        IReadOnlyList<SyntaxTree> syntaxTrees,
        IReadOnlyList<ClassifiedDeclaration> classified)
    {
        var candidates = classified
            .Where(row => row.Status == CandidateStatus.Candidate)
            .Select(row => row.ToRecord())
            .OrderBy(record => record.File, StringComparer.Ordinal)
            .ThenBy(record => record.Line)
            .ThenBy(record => record.SymbolId, StringComparer.Ordinal)
            .ToList();
        var roots = classified
            .Where(row => row.Status == CandidateStatus.Rooted)
            .Select(row => row.ToRecord())
            .OrderBy(record => record.File, StringComparer.Ordinal)
            .ThenBy(record => record.Line)
            .ThenBy(record => record.SymbolId, StringComparer.Ordinal)
            .ToList();
        var used = classified.Count(row => row.Status == CandidateStatus.Used);
        var excluded = classified.Count(row => row.Status == CandidateStatus.Excluded);
        var diagnostics = syntaxTrees
            .SelectMany(tree => tree.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Location.GetLineSpan().Path)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        return new InventoryDocument
        {
            SchemaVersion = SchemaVersion,
            GameVersion = GameVersion,
            ConfigSchemaVersion = config.SchemaVersion,
            Generation = new GenerationInfo
            {
                Tool = "scripts/tools/UnusedCodeInventoryScanner",
                Parser = "Microsoft.CodeAnalysis.CSharp",
                IncludesRawSourceText = false,
                ParseErrorFileCount = diagnostics.Count,
                ParseErrorFiles = diagnostics,
            },
            Totals = new TotalsRecord
            {
                FilesScanned = syntaxTrees.Count,
                ReportableDeclarations = classified.Count,
                Candidates = candidates.Count,
                UsedDeclarations = used,
                RootedDeclarations = roots.Count,
                ExcludedDeclarations = excluded,
                CandidateKinds = CountBy(candidates, record => record.Kind),
                CandidateAccessibilities = CountBy(candidates, record => record.Accessibility),
            },
            Candidates = candidates,
            RootedDeclarations = roots,
        };
    }

    private static Dictionary<string, int> CountBy<T>(IEnumerable<T> rows, Func<T, string> selector)
    {
        return rows
            .GroupBy(selector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }
}

internal sealed class SourceFile
{
    public SourceFile(string sourceRoot, string fullPath)
    {
        FullPath = fullPath;
        RelativePath = NormalizePath(Path.GetRelativePath(sourceRoot, fullPath));
    }

    public string FullPath { get; }
    public string RelativePath { get; }

    private static string NormalizePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');
}

internal sealed class ScannerConfig
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("include_path_prefixes")]
    public IReadOnlyList<string> IncludePathPrefixes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("exclude_path_contains")]
    public IReadOnlyList<string> ExcludePathContains { get; init; } = Array.Empty<string>();

    [JsonPropertyName("report_path_prefixes")]
    public IReadOnlyList<string> ReportPathPrefixes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("candidate_accessibilities")]
    public IReadOnlyList<string> CandidateAccessibilities { get; init; } = Array.Empty<string>();

    [JsonPropertyName("root_attribute_type_suffixes")]
    public IReadOnlyList<string> RootAttributeTypeSuffixes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("root_member_names_in_attribute_rooted_types")]
    public IReadOnlyList<string> RootMemberNamesInAttributeRootedTypes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("exclude_symbol_patterns")]
    public IReadOnlyList<string> ExcludeSymbolPatterns { get; init; } = Array.Empty<string>();

    [JsonPropertyName("exclude_declaration_name_suffixes")]
    public IReadOnlyList<string> ExcludeDeclarationNameSuffixes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("preprocessor_symbols")]
    public IReadOnlyList<string> PreprocessorSymbols { get; init; } = Array.Empty<string>();

    private IReadOnlyList<Regex>? excludeSymbolRegexes;

    public static ScannerConfig Load(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<ScannerConfig>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException($"empty scanner config: {path}");
        if (config.SchemaVersion != "1.0")
        {
            throw new InvalidOperationException($"unsupported config schema_version: {config.SchemaVersion}");
        }

        return config;
    }

    public bool ShouldInclude(string relativePath)
    {
        return MatchesPrefix(relativePath, IncludePathPrefixes)
            && !ExcludePathContains.Any(part => relativePath.Contains(part, StringComparison.Ordinal));
    }

    public bool ShouldReport(string relativePath) => MatchesPrefix(relativePath, ReportPathPrefixes);

    public bool IsCandidateAccessibility(Accessibility accessibility)
    {
        return CandidateAccessibilities.Contains(accessibility.ToString().ToLowerInvariant(), StringComparer.Ordinal);
    }

    public bool IsExcludedSymbol(ISymbol symbol)
    {
        return ExcludeDeclarationNameSuffixes.Any(suffix => symbol.Name.EndsWith(suffix, StringComparison.Ordinal))
            || ExcludeRegexes().Any(regex => regex.IsMatch(symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    public bool HasRootAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(attribute =>
        {
            var name = attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            return name is not null && RootAttributeTypeSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal));
        });
    }

    public bool IsRootMemberNameInAttributeRootedType(string memberName)
    {
        return RootMemberNamesInAttributeRootedTypes.Contains(memberName, StringComparer.Ordinal);
    }

    private static bool MatchesPrefix(string relativePath, IReadOnlyList<string> prefixes)
    {
        return prefixes.Count == 0 || prefixes.Any(prefix => relativePath.StartsWith(prefix, StringComparison.Ordinal));
    }

    private IReadOnlyList<Regex> ExcludeRegexes()
    {
        return excludeSymbolRegexes ??= ExcludeSymbolPatterns.Select(WildcardToRegex).ToList();
    }

    private static Regex WildcardToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal);
        return new Regex($"^{escaped}$", RegexOptions.CultureInvariant);
    }
}

internal sealed class DeclarationSet
{
    public DeclarationSet(IReadOnlyList<DeclarationInfo> declarations)
    {
        Declarations = declarations;
        Symbols = declarations.Select(declaration => declaration.Symbol).ToHashSet(SymbolEqualityComparer.Default);
    }

    public IReadOnlyList<DeclarationInfo> Declarations { get; }
    public ISet<ISymbol> Symbols { get; }
}

internal static class DeclarationCollector
{
    public static DeclarationSet Collect(Compilation compilation, ScannerConfig config)
    {
        var declarations = new List<DeclarationInfo>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                AddDeclaration(model, node, config, declarations);
            }
        }

        return new DeclarationSet(declarations);
    }

    private static void AddDeclaration(
        SemanticModel model,
        SyntaxNode node,
        ScannerConfig config,
        List<DeclarationInfo> declarations)
    {
        switch (node)
        {
            case BaseTypeDeclarationSyntax typeDeclaration:
                AddSymbol(model.GetDeclaredSymbol(typeDeclaration), typeDeclaration, config, declarations);
                break;
            case DelegateDeclarationSyntax delegateDeclaration:
                AddSymbol(model.GetDeclaredSymbol(delegateDeclaration), delegateDeclaration, config, declarations);
                break;
            case MethodDeclarationSyntax methodDeclaration:
                AddSymbol(model.GetDeclaredSymbol(methodDeclaration), methodDeclaration, config, declarations);
                break;
            case PropertyDeclarationSyntax propertyDeclaration:
                AddSymbol(model.GetDeclaredSymbol(propertyDeclaration), propertyDeclaration, config, declarations);
                break;
            case EventDeclarationSyntax eventDeclaration:
                AddSymbol(model.GetDeclaredSymbol(eventDeclaration), eventDeclaration, config, declarations);
                break;
            case VariableDeclaratorSyntax variableDeclarator
                when variableDeclarator.Parent?.Parent is FieldDeclarationSyntax:
                AddSymbol(model.GetDeclaredSymbol(variableDeclarator), variableDeclarator, config, declarations);
                break;
        }
    }

    private static void AddSymbol(
        ISymbol? symbol,
        SyntaxNode node,
        ScannerConfig config,
        List<DeclarationInfo> declarations)
    {
        if (symbol is null || symbol.IsImplicitlyDeclared)
        {
            return;
        }

        var file = node.SyntaxTree.FilePath;
        if (!config.ShouldReport(file) || !config.IsCandidateAccessibility(symbol.DeclaredAccessibility))
        {
            return;
        }

        if (symbol is IMethodSymbol method
            && (method.MethodKind != MethodKind.Ordinary || method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0))
        {
            return;
        }

        if (symbol is IPropertySymbol property
            && (property.IsOverride || property.ExplicitInterfaceImplementations.Length > 0))
        {
            return;
        }

        declarations.Add(DeclarationInfo.Create(symbol, node, config));
    }
}

internal sealed class DeclarationInfo
{
    private DeclarationInfo(
        ISymbol symbol,
        string file,
        int line,
        string kind,
        bool isRoot,
        bool isExcluded)
    {
        Symbol = symbol;
        File = file;
        Line = line;
        Kind = kind;
        IsRoot = isRoot;
        IsExcluded = isExcluded;
    }

    public ISymbol Symbol { get; }
    public string File { get; }
    public int Line { get; }
    public string Kind { get; }
    public bool IsRoot { get; }
    public bool IsExcluded { get; }
    public string SymbolId => Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    public string Accessibility => Symbol.DeclaredAccessibility.ToString().ToLowerInvariant();

    public static DeclarationInfo Create(ISymbol symbol, SyntaxNode node, ScannerConfig config)
    {
        var normalizedSymbol = NormalizeDeclaredSymbol(symbol);
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        return new DeclarationInfo(
            normalizedSymbol,
            node.SyntaxTree.FilePath,
            lineSpan.StartLinePosition.Line + 1,
            KindFor(normalizedSymbol),
            IsRootDeclaration(normalizedSymbol, node, config),
            config.IsExcludedSymbol(normalizedSymbol));
    }

    private static ISymbol NormalizeDeclaredSymbol(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => method.OriginalDefinition,
            IPropertySymbol property => property.OriginalDefinition,
            IFieldSymbol field => field.OriginalDefinition,
            IEventSymbol eventSymbol => eventSymbol.OriginalDefinition,
            INamedTypeSymbol type => type.OriginalDefinition,
            _ => symbol.OriginalDefinition,
        };
    }

    private static string KindFor(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol => "type",
            IMethodSymbol => "method",
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IEventSymbol => "event",
            _ => symbol.Kind.ToString().ToLowerInvariant(),
        };
    }

    private static bool IsRootDeclaration(ISymbol symbol, SyntaxNode node, ScannerConfig config)
    {
        return config.HasRootAttribute(symbol)
            || HasRootAttributeSyntax(node, config)
            || IsAttributeRootedHarmonyMember(symbol, node, config)
            || IsAnalyzerEntryPoint(symbol);
    }

    private static bool IsAttributeRootedHarmonyMember(ISymbol symbol, SyntaxNode node, ScannerConfig config)
    {
        if (symbol is not IMethodSymbol method || symbol.ContainingType is null)
        {
            return false;
        }

        return config.IsRootMemberNameInAttributeRootedType(method.Name)
            && (config.HasRootAttribute(symbol.ContainingType) || ContainingTypeHasRootAttributeSyntax(node, config));
    }

    private static bool ContainingTypeHasRootAttributeSyntax(SyntaxNode node, ScannerConfig config)
    {
        var containingType = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        return containingType is not null && HasRootAttributeSyntax(containingType, config);
    }

    private static bool HasRootAttributeSyntax(SyntaxNode node, ScannerConfig config)
    {
        return AttributeLists(node).SelectMany(list => list.Attributes).Any(attribute =>
        {
            var name = attribute.Name.ToString();
            return config.RootAttributeTypeSuffixes.Any(suffix =>
                name.EndsWith(suffix, StringComparison.Ordinal)
                || (suffix.EndsWith("Attribute", StringComparison.Ordinal)
                    && name.EndsWith(suffix[..^"Attribute".Length], StringComparison.Ordinal)));
        });
    }

    private static SyntaxList<AttributeListSyntax> AttributeLists(SyntaxNode node)
    {
        return node switch
        {
            BaseTypeDeclarationSyntax type => type.AttributeLists,
            DelegateDeclarationSyntax declaration => declaration.AttributeLists,
            MethodDeclarationSyntax declaration => declaration.AttributeLists,
            PropertyDeclarationSyntax declaration => declaration.AttributeLists,
            EventDeclarationSyntax declaration => declaration.AttributeLists,
            VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax declaration } => declaration.AttributeLists,
            _ => default,
        };
    }

    private static bool IsAnalyzerEntryPoint(ISymbol symbol)
    {
        return symbol is IMethodSymbol { Name: "Initialize", ContainingType: { } type }
            && InheritsFrom(type, "Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer");
    }

    private static bool InheritsFrom(INamedTypeSymbol type, string baseTypeName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == baseTypeName)
            {
                return true;
            }
        }

        return false;
    }
}

internal static class ReferenceCollector
{
    public static Dictionary<ISymbol, HashSet<ISymbol>> Collect(Compilation compilation, ISet<ISymbol> targets)
    {
        var references = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                AddReference(model, node, targets, references);
            }
        }

        return references;
    }

    private static void AddReference(
        SemanticModel model,
        SyntaxNode node,
        ISet<ISymbol> targets,
        Dictionary<ISymbol, HashSet<ISymbol>> references)
    {
        if (!CanReferenceSymbol(node))
        {
            return;
        }

        var enclosing = NormalizeEnclosingSymbol(model.GetEnclosingSymbol(node.SpanStart));
        foreach (var symbol in ReferencedSymbols(model, node))
        {
            if (!targets.Contains(symbol))
            {
                continue;
            }

            if (enclosing is not null && SymbolEqualityComparer.Default.Equals(symbol, enclosing))
            {
                continue;
            }

            if (!references.TryGetValue(symbol, out var referrers))
            {
                referrers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                references.Add(symbol, referrers);
            }

            if (enclosing is not null)
            {
                referrers.Add(enclosing);
            }
        }
    }

    private static IEnumerable<ISymbol> ReferencedSymbols(SemanticModel model, SyntaxNode node)
    {
        var info = model.GetSymbolInfo(node);
        var symbol = NormalizeReferencedSymbol(info.Symbol);
        if (symbol is not null)
        {
            yield return symbol;
        }

        foreach (var candidate in info.CandidateSymbols.Select(NormalizeReferencedSymbol).OfType<ISymbol>())
        {
            yield return candidate;
        }
    }

    private static bool CanReferenceSymbol(SyntaxNode node)
    {
        return node is IdentifierNameSyntax
            or GenericNameSyntax
            or MemberAccessExpressionSyntax
            or QualifiedNameSyntax
            or ObjectCreationExpressionSyntax
            or AttributeSyntax;
    }

    private static ISymbol? NormalizeReferencedSymbol(ISymbol? symbol)
    {
        return symbol switch
        {
            IMethodSymbol { MethodKind: MethodKind.Constructor, ContainingType: { } type } => type.OriginalDefinition,
            IMethodSymbol method => method.OriginalDefinition,
            IPropertySymbol property => property.OriginalDefinition,
            IFieldSymbol field => field.OriginalDefinition,
            IEventSymbol eventSymbol => eventSymbol.OriginalDefinition,
            INamedTypeSymbol type => type.OriginalDefinition,
            _ => symbol?.OriginalDefinition,
        };
    }

    private static ISymbol? NormalizeEnclosingSymbol(ISymbol? symbol)
    {
        return symbol switch
        {
            IMethodSymbol { AssociatedSymbol: { } associated } => associated.OriginalDefinition,
            IMethodSymbol method => method.OriginalDefinition,
            IPropertySymbol property => property.OriginalDefinition,
            IFieldSymbol field => field.OriginalDefinition,
            IEventSymbol eventSymbol => eventSymbol.OriginalDefinition,
            INamedTypeSymbol type => type.OriginalDefinition,
            _ => symbol?.OriginalDefinition,
        };
    }
}

internal static class CandidateClassifier
{
    public static IReadOnlyList<ClassifiedDeclaration> Classify(
        DeclarationSet declarations,
        IReadOnlyDictionary<ISymbol, HashSet<ISymbol>> references,
        ScannerConfig config)
    {
        var used = declarations.Declarations
            .Where(declaration => declaration.IsRoot || IsUsedByReference(declaration.Symbol, references))
            .Select(declaration => declaration.Symbol)
            .ToHashSet(SymbolEqualityComparer.Default);
        PropagateUsedToContainingTypes(used);

        return declarations.Declarations.Select(declaration =>
        {
            if (declaration.IsExcluded)
            {
                return new ClassifiedDeclaration(declaration, CandidateStatus.Excluded, "excluded_by_config");
            }

            if (declaration.IsRoot)
            {
                return new ClassifiedDeclaration(declaration, CandidateStatus.Rooted, "rooted_by_config_or_framework");
            }

            if (used.Contains(declaration.Symbol))
            {
                return new ClassifiedDeclaration(declaration, CandidateStatus.Used, "referenced_by_scanned_source");
            }

            return new ClassifiedDeclaration(declaration, CandidateStatus.Candidate, "no_scanned_reference");
        }).ToList();
    }

    private static bool IsUsedByReference(
        ISymbol symbol,
        IReadOnlyDictionary<ISymbol, HashSet<ISymbol>> references)
    {
        return references.TryGetValue(symbol, out var referrers) && referrers.Count > 0;
    }

    private static void PropagateUsedToContainingTypes(HashSet<ISymbol> used)
    {
        var pending = used.ToList();
        foreach (var symbol in pending)
        {
            for (var containing = symbol.ContainingType; containing is not null; containing = containing.ContainingType)
            {
                used.Add(containing.OriginalDefinition);
            }
        }
    }
}

internal sealed class ClassifiedDeclaration
{
    public ClassifiedDeclaration(DeclarationInfo declaration, CandidateStatus status, string reason)
    {
        Declaration = declaration;
        Status = status;
        Reason = reason;
    }

    public DeclarationInfo Declaration { get; }
    public CandidateStatus Status { get; }
    public string Reason { get; }

    public DeclarationRecord ToRecord()
    {
        return new DeclarationRecord
        {
            SymbolId = Declaration.SymbolId,
            File = Declaration.File,
            Line = Declaration.Line,
            Kind = Declaration.Kind,
            Accessibility = Declaration.Accessibility,
            Status = Status.Value,
            Reason = Reason,
        };
    }
}

internal sealed class CandidateStatus
{
    public static readonly CandidateStatus Candidate = new("candidate");
    public static readonly CandidateStatus Used = new("used");
    public static readonly CandidateStatus Rooted = new("rooted");
    public static readonly CandidateStatus Excluded = new("excluded");

    private CandidateStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool operator ==(CandidateStatus left, CandidateStatus right) => left.Value == right.Value;
    public static bool operator !=(CandidateStatus left, CandidateStatus right) => left.Value != right.Value;
    public override bool Equals(object? obj) => obj is CandidateStatus other && other.Value == Value;
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
}

internal static class ReferenceResolver
{
    public static IReadOnlyList<MetadataReference> ResolveReferences()
    {
        var trustedPlatformAssemblies =
            (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?.Split(Path.PathSeparator)
            ?? Array.Empty<string>();
        return trustedPlatformAssemblies
            .Where(File.Exists)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();
    }
}

internal sealed class InventoryDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "";

    [JsonPropertyName("game_version")]
    public string GameVersion { get; init; } = "";

    [JsonPropertyName("config_schema_version")]
    public string ConfigSchemaVersion { get; init; } = "";

    [JsonPropertyName("generation")]
    public GenerationInfo Generation { get; init; } = new();

    [JsonPropertyName("totals")]
    public TotalsRecord Totals { get; init; } = new();

    [JsonPropertyName("candidates")]
    public IReadOnlyList<DeclarationRecord> Candidates { get; init; } = new ReadOnlyCollection<DeclarationRecord>([]);

    [JsonPropertyName("rooted_declarations")]
    public IReadOnlyList<DeclarationRecord> RootedDeclarations { get; init; } = new ReadOnlyCollection<DeclarationRecord>([]);
}

internal sealed class GenerationInfo
{
    [JsonPropertyName("tool")]
    public string Tool { get; init; } = "";

    [JsonPropertyName("parser")]
    public string Parser { get; init; } = "";

    [JsonPropertyName("includes_raw_source_text")]
    public bool IncludesRawSourceText { get; init; }

    [JsonPropertyName("parse_error_file_count")]
    public int ParseErrorFileCount { get; init; }

    [JsonPropertyName("parse_error_files")]
    public IReadOnlyList<string> ParseErrorFiles { get; init; } = Array.Empty<string>();
}

internal sealed class TotalsRecord
{
    [JsonPropertyName("files_scanned")]
    public int FilesScanned { get; init; }

    [JsonPropertyName("reportable_declarations")]
    public int ReportableDeclarations { get; init; }

    [JsonPropertyName("candidates")]
    public int Candidates { get; init; }

    [JsonPropertyName("used_declarations")]
    public int UsedDeclarations { get; init; }

    [JsonPropertyName("rooted_declarations")]
    public int RootedDeclarations { get; init; }

    [JsonPropertyName("excluded_declarations")]
    public int ExcludedDeclarations { get; init; }

    [JsonPropertyName("candidate_kinds")]
    public IReadOnlyDictionary<string, int> CandidateKinds { get; init; } = new Dictionary<string, int>();

    [JsonPropertyName("candidate_accessibilities")]
    public IReadOnlyDictionary<string, int> CandidateAccessibilities { get; init; } = new Dictionary<string, int>();
}

internal sealed class DeclarationRecord
{
    [JsonPropertyName("symbol_id")]
    public string SymbolId { get; init; } = "";

    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "";

    [JsonPropertyName("accessibility")]
    public string Accessibility { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "";
}
