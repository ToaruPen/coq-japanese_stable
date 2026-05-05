using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace QudJP.Tools.StaticProducerInventoryScanner;

internal static class Program
{
    public static int Main(string[] args)
    {
        var parsed = CliArguments.Parse(args);
        if (parsed.ShowHelp)
        {
            Console.Out.WriteLine("Usage: StaticProducerInventoryScanner --source-root <dir> --output <json-path>");
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

        InventoryWriter.Write(parsed.SourceRoot!, parsed.OutputPath!);
        return 0;
    }
}

internal sealed class CliArguments
{
    public string? SourceRoot { get; private init; }
    public string? OutputPath { get; private init; }
    public string? Error { get; private init; }
    public bool ShowHelp { get; private init; }

    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        string? sourceRoot = null;
        string? outputPath = null;

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
                case "--output":
                    if (index + 1 >= args.Count)
                    {
                        return new CliArguments { Error = "Missing value for --output" };
                    }

                    outputPath = args[++index];
                    break;
                default:
                    return new CliArguments { Error = $"Unknown argument: {args[index]}" };
            }
        }

        if (sourceRoot is null || outputPath is null)
        {
            return new CliArguments { Error = "Missing required argument. Use --help." };
        }

        return new CliArguments
        {
            SourceRoot = ExpandHome(sourceRoot),
            OutputPath = ExpandHome(outputPath),
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
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static void Write(string sourceRoot, string outputPath)
    {
        var payload = StaticProducerScanner.Scan(sourceRoot);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, Options) + "\n", new UTF8Encoding(false));
    }
}

internal static class StaticProducerScanner
{
    private const string SchemaVersion = "1.0";
    private const string GameVersion = "2.0.4";
    internal static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    private static readonly IReadOnlyList<string> TargetSurfaces =
        new ReadOnlyCollection<string>(new[] { "EmitMessage", "Popup.Show*", "AddPlayerMessage" });

    private static readonly HashSet<string> ActionableStatuses = new(StringComparer.Ordinal)
    {
        ClosureStatus.MessagesCandidate,
        ClosureStatus.OwnerPatchRequired,
    };

    private static readonly HashSet<string> WrapperPaths = new(StringComparer.Ordinal)
    {
        "XRL.Messages/MessageQueue.cs",
        "XRL.UI/Popup.cs",
        "XRL.World/IComponent.cs",
        "XRL/IGameSystem.cs",
        "XRL.World.AI/GoalHandler.cs",
    };

    private static readonly HashSet<string> DebugExactPaths = new(StringComparer.Ordinal)
    {
        "XRL.World.Capabilities/Wishing.cs",
        "Qud.UI/DebugConsole.cs",
    };

    private static readonly string[] DiagnosticMarkers =
    {
        "Debug:",
        "&KDebug:",
        "Error generating",
        "please report this error",
        "Hotload complete",
        "Total xp:",
        "secret id",
        "(missing)",
        "(invalid)",
        "Wasn't found",
        "biome:",
    };

    private static readonly HashSet<string> SourceLikeNames = new(StringComparer.Ordinal)
    {
        "Actor",
        "Source",
        "ParentObject",
        "Object",
        "GO",
        "who",
        "target",
        "Target",
        "E.Actor",
        "E.Object",
        "The.Player",
    };

    private static readonly Dictionary<string, string> PopupRoleParameterAliases = new(StringComparer.Ordinal)
    {
        ["message"] = "Message",
        ["title"] = "Title",
        ["prompt"] = "Prompt",
        ["intro"] = "Intro",
        ["options"] = "Options",
        ["spacing_text"] = "SpacingText",
        ["buttons"] = "Buttons",
        ["preview_content"] = "PreviewContent",
    };

    private static readonly Dictionary<string, TextRole[]> PopupTextRoles = new(StringComparer.Ordinal)
    {
        ["Show"] = new[] { new TextRole("message", 0) },
        ["ShowAsync"] = new[] { new TextRole("message", 0) },
        ["ShowFail"] = new[] { new TextRole("message", 0) },
        ["ShowFailAsync"] = new[] { new TextRole("message", 0) },
        ["ShowKeybindAsync"] = new[] { new TextRole("message", 0) },
        ["ShowBlock"] = new[] { new TextRole("message", 0), new TextRole("title", 1) },
        ["ShowSpace"] = new[] { new TextRole("message", 0), new TextRole("title", 1) },
        ["ShowBlockPrompt"] = new[] { new TextRole("message", 0), new TextRole("prompt", 1) },
        ["ShowBlockSpace"] = new[] { new TextRole("message", 0), new TextRole("prompt", 1) },
        ["ShowBlockWithCopy"] = new[]
        {
            new TextRole("message", 0),
            new TextRole("prompt", 1),
            new TextRole("title", 2),
            new TextRole("copy_info", 3),
        },
        ["ShowConversation"] = new[]
        {
            new TextRole("title", 0),
            new TextRole("intro", 2),
            new TextRole("options", 3),
        },
        ["ShowOptionList"] = new[]
        {
            new TextRole("title", 0),
            new TextRole("options", 1),
            new TextRole("intro", 4),
            new TextRole("spacing_text", 9),
            new TextRole("buttons", 14),
        },
        ["ShowOptionListAsync"] = new[]
        {
            new TextRole("title", 0),
            new TextRole("options", 1),
            new TextRole("intro", 4),
            new TextRole("spacing_text", 9),
        },
        ["ShowColorPicker"] = new[]
        {
            new TextRole("title", 0),
            new TextRole("intro", 2),
            new TextRole("spacing_text", 7),
            new TextRole("preview_content", 11),
        },
        ["ShowColorPickerAsync"] = new[]
        {
            new TextRole("title", 0),
            new TextRole("intro", 2),
            new TextRole("spacing_text", 7),
            new TextRole("preview_content", 11),
        },
    };

    public static InventoryPayload Scan(string sourceRoot)
    {
        var fullSourceRoot = Path.GetFullPath(sourceRoot);
        var sourceFiles = EnumerateCSharpFiles(fullSourceRoot)
            .Select(path => SourceFile.Parse(fullSourceRoot, path))
            .ToList();
        var compilation = CreateCompilation(sourceFiles.Select(sourceFile => sourceFile.Tree));
        var semanticModels = sourceFiles.ToDictionary(
            sourceFile => sourceFile.Tree,
            sourceFile => compilation.GetSemanticModel(sourceFile.Tree));
        var callsites = sourceFiles
            .SelectMany(sourceFile => ScanFile(sourceFile, semanticModels[sourceFile.Tree]))
            .OrderBy(callsite => callsite.File, StringComparer.Ordinal)
            .ThenBy(callsite => callsite.Line)
            .ThenBy(callsite => callsite.Expression, StringComparer.Ordinal)
            .ToList();
        var families = BuildFamilies(callsites);

        return new InventoryPayload
        {
            SchemaVersion = SchemaVersion,
            GameVersion = GameVersion,
            TargetSurfaces = TargetSurfaces.ToList(),
            Totals = BuildTotals(callsites, families),
            Callsites = callsites,
            Families = families,
        };
    }

    private static IEnumerable<string> EnumerateCSharpFiles(string sourceRoot)
    {
        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => new
            {
                FullPath = Path.GetFullPath(path),
                RelativePath = RelativePath(sourceRoot, path),
            })
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .Select(entry => entry.FullPath);
    }

    private static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> syntaxTrees)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var referencePaths = trustedPlatformAssemblies is null
            ? AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                .Select(assembly => assembly.Location)
            : trustedPlatformAssemblies.Split(Path.PathSeparator);
        var references = referencePaths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(location => location, StringComparer.Ordinal)
            .Select(location => MetadataReference.CreateFromFile(location));
        return CSharpCompilation.Create(
            "QudJP.StaticProducerInventory.Input",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private static IEnumerable<CallsitePayload> ScanFile(SourceFile sourceFile, SemanticModel semanticModel)
    {
        var root = sourceFile.Tree.GetCompilationUnitRoot();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var callee = GetCallee(invocation);
            if (callee is null)
            {
                continue;
            }

            var semanticInfo = GetSemanticInfo(semanticModel, invocation);
            var (targetSurface, closureReason) = TargetSurface(
                sourceFile.RelativePath,
                callee.Method,
                callee.Receiver,
                semanticInfo);
            if (targetSurface is null)
            {
                continue;
            }

            var context = GetMemberContext(sourceFile.Tree, invocation);
            var line = sourceFile.Tree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
            var arguments = AssignFormalIndexes(
                callee.Method,
                invocation.ArgumentList.Arguments.Select(ToArgument).ToList(),
                semanticInfo.ParameterIndexes);
            var candidate = new InvocationCandidate(
                sourceFile.RelativePath,
                line,
                invocation.ToString().Trim(),
                callee.Receiver,
                callee.Method,
                targetSurface,
                arguments,
                context,
                closureReason,
                invocation.Expression.ToString().Trim(),
                invocation.ArgumentList.Arguments.Count,
                NamedArgumentNames(invocation.ArgumentList).OrderBy(name => name, StringComparer.Ordinal).ToList(),
                semanticInfo);

            yield return SerializeCallsite(candidate);
        }
    }

    private static SemanticCallInfo GetSemanticInfo(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
        var status = "resolved";
        if (methodSymbol is null)
        {
            var candidateSymbols = symbolInfo.CandidateSymbols
                .OfType<IMethodSymbol>()
                .OrderBy(symbol => symbol.ToDisplayString(SymbolDisplayFormats.MethodSignature), StringComparer.Ordinal)
                .ToList();
            methodSymbol = SelectCandidateMethod(invocation, candidateSymbols);
            status = methodSymbol is null ? "unresolved" : "candidate";
        }

        var receiverType = invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? semanticModel.GetTypeInfo(memberAccess.Expression).Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
            : null;
        return new SemanticCallInfo(
            status,
            methodSymbol?.ToDisplayString(SymbolDisplayFormats.MethodSignature),
            methodSymbol?.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            receiverType,
            methodSymbol is null ? new Dictionary<string, int>(StringComparer.Ordinal) : ParameterIndexes(methodSymbol));
    }

    private static IMethodSymbol? SelectCandidateMethod(
        InvocationExpressionSyntax invocation,
        IReadOnlyList<IMethodSymbol> candidateSymbols)
    {
        if (candidateSymbols.Count == 0)
        {
            return null;
        }

        var argumentCount = invocation.ArgumentList.Arguments.Count;
        var namedArguments = NamedArgumentNames(invocation.ArgumentList).ToHashSet(StringComparer.Ordinal);
        var compatibleCandidates = candidateSymbols
            .Where(symbol => symbol.Parameters.Length >= argumentCount)
            .Where(symbol => namedArguments.All(name => symbol.Parameters.Any(parameter => parameter.Name == name)))
            .ToList();
        if (compatibleCandidates.Count == 1)
        {
            return compatibleCandidates[0];
        }

        return candidateSymbols.Count == 1 ? candidateSymbols[0] : null;
    }

    private static IEnumerable<string> NamedArgumentNames(ArgumentListSyntax argumentList)
    {
        return argumentList.Arguments
            .Select(argument => argument.NameColon?.Name.Identifier.ValueText)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>();
    }

    private static Dictionary<string, int> ParameterIndexes(IMethodSymbol methodSymbol)
    {
        return methodSymbol.Parameters
            .Select((parameter, index) => new { parameter.Name, Index = index })
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.Ordinal);
    }

    private static CalleeInfo? GetCallee(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifier => new CalleeInfo(null, identifier.Identifier.ValueText),
            GenericNameSyntax generic => new CalleeInfo(null, generic.Identifier.ValueText),
            MemberAccessExpressionSyntax memberAccess => new CalleeInfo(
                memberAccess.Expression.ToString().Trim(),
                memberAccess.Name.Identifier.ValueText),
            MemberBindingExpressionSyntax memberBinding => new CalleeInfo(null, memberBinding.Name.Identifier.ValueText),
            _ => null,
        };
    }

    private static (string? TargetSurface, string? ClosureReason) TargetSurface(
        string relativePath,
        string method,
        string? receiver,
        SemanticCallInfo semanticInfo)
    {
        if (semanticInfo.ContainingTypeSymbol is not null)
        {
            return SemanticTargetSurface(method, semanticInfo.ContainingTypeSymbol);
        }

        if (method is "PickOption" or "AskString")
        {
            return (null, null);
        }

        if (method == "EmitMessage")
        {
            return ("EmitMessage", null);
        }

        if (method == "AddPlayerMessage")
        {
            return ("AddPlayerMessage", null);
        }

        if (!method.StartsWith("Show", StringComparison.Ordinal))
        {
            return (null, null);
        }

        if (ReceiverName(receiver) == "Popup" || (receiver is null && relativePath == "XRL.UI/Popup.cs"))
        {
            var reason = PopupTextRolesFor(method).Count == 0 ? "unknown_popup_show_signature" : null;
            return ("Popup.Show*", reason);
        }

        return (null, null);
    }

    private static (string? TargetSurface, string? ClosureReason) SemanticTargetSurface(
        string method,
        string containingTypeSymbol)
    {
        if (method is "PickOption" or "AskString")
        {
            return (null, null);
        }

        if (method == "EmitMessage")
        {
            return IsEmitMessageOwnerType(containingTypeSymbol) ? ("EmitMessage", null) : (null, null);
        }

        if (method == "AddPlayerMessage")
        {
            return IsAddPlayerMessageOwnerType(containingTypeSymbol) ? ("AddPlayerMessage", null) : (null, null);
        }

        if (method.StartsWith("Show", StringComparison.Ordinal) && containingTypeSymbol == "XRL.UI.Popup")
        {
            var reason = PopupTextRolesFor(method).Count == 0 ? "unknown_popup_show_signature" : null;
            return ("Popup.Show*", reason);
        }

        return (null, null);
    }

    private static bool IsEmitMessageOwnerType(string containingTypeSymbol)
    {
        return containingTypeSymbol == "XRL.World.Capabilities.Messaging"
            || containingTypeSymbol == "XRL.World.GameObject"
            || containingTypeSymbol.StartsWith("XRL.World.IComponent<", StringComparison.Ordinal);
    }

    private static bool IsAddPlayerMessageOwnerType(string containingTypeSymbol)
    {
        return containingTypeSymbol == "XRL.Messages.MessageQueue"
            || containingTypeSymbol == "XRL.IGameSystem"
            || containingTypeSymbol == "XRL.World.AI.GoalHandler"
            || containingTypeSymbol.StartsWith("XRL.World.IComponent<", StringComparison.Ordinal);
    }

    private static ArgumentRecord ToArgument(ArgumentSyntax argument)
    {
        return new ArgumentRecord(
            argument.NameColon?.Name.Identifier.ValueText,
            null,
            argument.Expression.ToString().Trim(),
            argument.Expression);
    }

    private static List<ArgumentRecord> AssignFormalIndexes(
        string method,
        IReadOnlyList<ArgumentRecord> arguments,
        IReadOnlyDictionary<string, int> semanticParameterIndexes)
    {
        var namedIndexes = NamedArgumentIndexes(method, arguments, semanticParameterIndexes);
        var usedFormalIndexes = new HashSet<int>();
        var nextPositionalIndex = 0;
        var assigned = new List<ArgumentRecord>();

        foreach (var argument in arguments)
        {
            int? formalIndex;
            if (argument.Name is null)
            {
                while (usedFormalIndexes.Contains(nextPositionalIndex))
                {
                    nextPositionalIndex++;
                }

                formalIndex = nextPositionalIndex;
                nextPositionalIndex++;
            }
            else if (!namedIndexes.TryGetValue(argument.Name, out var namedFormalIndex))
            {
                assigned.Add(argument with { FormalIndex = null });
                continue;
            }
            else
            {
                formalIndex = namedFormalIndex;
            }

            usedFormalIndexes.Add(formalIndex.Value);
            assigned.Add(argument with { FormalIndex = formalIndex });
        }

        return assigned;
    }

    private static Dictionary<string, int> NamedArgumentIndexes(
        string method,
        IReadOnlyList<ArgumentRecord> arguments,
        IReadOnlyDictionary<string, int> semanticParameterIndexes)
    {
        if (semanticParameterIndexes.Count > 0)
        {
            return new Dictionary<string, int>(semanticParameterIndexes, StringComparer.Ordinal);
        }

        if (method == "AddPlayerMessage")
        {
            return new Dictionary<string, int>(StringComparer.Ordinal) { ["Message"] = 0 };
        }

        if (method == "EmitMessage")
        {
            var sourceFirst = arguments.Count > 1
                && IsSourceLikeExpression(arguments[0].ExpressionText)
                && arguments.Any(argument => argument.Name is "Source" or "Msg");
            var messageIndex = sourceFirst ? 1 : 0;
            return new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Source"] = 0,
                ["Message"] = messageIndex,
                ["Msg"] = 1,
            };
        }

        var roleIndexes = PopupTextRolesFor(method)
            .ToDictionary(role => role.Name, role => role.FormalIndex, StringComparer.Ordinal);
        var named = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (roleName, formalName) in PopupRoleParameterAliases)
        {
            if (roleIndexes.TryGetValue(roleName, out var formalIndex))
            {
                named[formalName] = formalIndex;
            }
        }

        return named;
    }

    private static CallsitePayload SerializeCallsite(InvocationCandidate invocation)
    {
        var textRoles = TextArguments(invocation);
        var isDebug = IsDebugCallsite(invocation);
        var isWrapperSink = IsWrapperSink(invocation, textRoles);
        var serializedTextArguments = new List<TextArgumentPayload>();

        foreach (var role in textRoles)
        {
            var classification = ClassifyExpression(invocation, role);
            var closureStatus = TextArgumentClosure(invocation, classification, isDebug, isWrapperSink);
            serializedTextArguments.Add(new TextArgumentPayload
            {
                Role = role.Name,
                FormalIndex = role.FormalIndex,
                Expression = ArgumentAt(invocation.Arguments, role.FormalIndex)?.ExpressionText ?? "",
                ExpressionKind = classification,
                ClosureStatus = closureStatus,
            });
        }

        var (callsiteClosureStatus, callsiteClosureReason) = CallsiteClosure(
            invocation,
            serializedTextArguments,
            isDebug,
            isWrapperSink);

        return new CallsitePayload
        {
            File = invocation.File,
            Line = invocation.Line,
            TargetSurface = invocation.TargetSurface,
            Receiver = invocation.Receiver,
            Method = invocation.Method,
            Expression = invocation.Expression,
            Namespace = invocation.Context.Namespace,
            TypeName = invocation.Context.TypeName,
            MemberName = invocation.Context.MemberName,
            MemberKind = invocation.Context.MemberKind,
            MemberStartLine = invocation.Context.MemberStartLine,
            ProducerFamilyId = ProducerFamilyId(invocation),
            TextArguments = serializedTextArguments,
            ClosureStatus = callsiteClosureStatus,
            ClosureReason = callsiteClosureReason,
            ArgumentCount = invocation.ArgumentCount,
            ArgumentNames = invocation.ArgumentNames,
            CalleeExpression = invocation.CalleeExpression,
            RoslynSymbolStatus = invocation.SemanticInfo.Status,
            MethodSymbol = invocation.SemanticInfo.MethodSymbol,
            ContainingTypeSymbol = invocation.SemanticInfo.ContainingTypeSymbol,
            ReceiverTypeSymbol = invocation.SemanticInfo.ReceiverTypeSymbol,
        };
    }

    private static List<TextRole> TextArguments(InvocationCandidate invocation)
    {
        if (invocation.TargetSurface == "EmitMessage")
        {
            var index = EmitMessageTextIndex(invocation);
            return ArgumentAt(invocation.Arguments, index) is not null ? new List<TextRole> { new("message", index) } : new List<TextRole>();
        }

        if (invocation.TargetSurface == "AddPlayerMessage")
        {
            return ArgumentAt(invocation.Arguments, 0) is not null ? new List<TextRole> { new("message", 0) } : new List<TextRole>();
        }

        return PopupTextRolesFor(invocation.Method)
            .Where(role => ArgumentAt(invocation.Arguments, role.FormalIndex) is not null)
            .ToList();
    }

    private static int EmitMessageTextIndex(InvocationCandidate invocation)
    {
        var receiverName = ReceiverName(invocation.Receiver);
        if (receiverName is "Messaging" or "IComponent" || (invocation.Receiver ?? "").StartsWith("IComponent<", StringComparison.Ordinal))
        {
            return 1;
        }

        if (receiverName is not null)
        {
            return 0;
        }

        var firstArgument = ArgumentAt(invocation.Arguments, 0)?.ExpressionText;
        if (firstArgument is not null
            && IsSourceLikeExpression(firstArgument)
            && ArgumentAt(invocation.Arguments, 1) is not null)
        {
            return 1;
        }

        return 0;
    }

    private static List<TextRole> PopupTextRolesFor(string method)
    {
        if (PopupTextRoles.TryGetValue(method, out var roles))
        {
            return roles.ToList();
        }

        return method.StartsWith("ShowYesNo", StringComparison.Ordinal)
            ? new List<TextRole> { new("message", 0) }
            : new List<TextRole>();
    }

    private static string ClassifyExpression(InvocationCandidate invocation, TextRole role)
    {
        var argument = ArgumentAt(invocation.Arguments, role.FormalIndex);
        var rawExpression = argument?.ExpressionText ?? "";
        var expressionText = StripBalancedOuterParentheses(rawExpression.Trim());
        var expressionSyntax = StripParentheses(argument?.ExpressionSyntax);

        if (role.Name == "options")
        {
            return "collection_text";
        }

        if (IsWrapperPath(invocation.File) && IsForwardedParameter(expressionText, invocation.Context.ParameterNames))
        {
            return "forwarded_parameter";
        }

        if (IsStringLiteral(expressionSyntax))
        {
            return "static_literal";
        }

        if (IsLiteralTemplate(expressionText, expressionSyntax))
        {
            return "literal_template";
        }

        if (LooksLikeCollectionText(expressionText, expressionSyntax))
        {
            return "collection_text";
        }

        return "procedural_or_unknown";
    }

    private static string TextArgumentClosure(
        InvocationCandidate invocation,
        string classification,
        bool isDebug,
        bool isWrapperSink)
    {
        if (isDebug)
        {
            return ClosureStatus.DebugIgnore;
        }

        if (isWrapperSink)
        {
            return ClosureStatus.SinkObservedOnly;
        }

        if (invocation.TargetSurface == "AddPlayerMessage")
        {
            return classification is "static_literal" or "literal_template"
                ? ClosureStatus.OwnerPatchRequired
                : ClosureStatus.RuntimeRequired;
        }

        if (invocation.TargetSurface == "Popup.Show*")
        {
            return classification switch
            {
                "static_literal" => ClosureStatus.MessagesCandidate,
                "literal_template" => ClosureStatus.OwnerPatchRequired,
                _ => ClosureStatus.RuntimeRequired,
            };
        }

        return classification is "static_literal" or "literal_template"
            ? ClosureStatus.MessagesCandidate
            : ClosureStatus.RuntimeRequired;
    }

    private static (string ClosureStatus, string? ClosureReason) CallsiteClosure(
        InvocationCandidate invocation,
        IReadOnlyList<TextArgumentPayload> textArguments,
        bool isDebug,
        bool isWrapperSink)
    {
        if (isDebug)
        {
            return (ClosureStatus.DebugIgnore, "debug_override");
        }

        if (isWrapperSink)
        {
            return (ClosureStatus.SinkObservedOnly, "wrapper_forwarding");
        }

        if (textArguments.Count == 0)
        {
            return (ClosureStatus.RuntimeRequired, invocation.ClosureReason ?? "no_text_arguments");
        }

        var statuses = textArguments.Select(argument => argument.ClosureStatus).ToHashSet(StringComparer.Ordinal);
        if (statuses.SetEquals(new[] { ClosureStatus.DebugIgnore }))
        {
            return (ClosureStatus.DebugIgnore, "debug_override");
        }

        if (statuses.SetEquals(new[] { ClosureStatus.SinkObservedOnly }))
        {
            return (ClosureStatus.SinkObservedOnly, "wrapper_forwarding");
        }

        if (statuses.Contains(ClosureStatus.RuntimeRequired))
        {
            return (ClosureStatus.RuntimeRequired, null);
        }

        if (statuses.Contains(ClosureStatus.OwnerPatchRequired))
        {
            return (ClosureStatus.OwnerPatchRequired, null);
        }

        return (ClosureStatus.MessagesCandidate, null);
    }

    private static List<FamilyPayload> BuildFamilies(IReadOnlyList<CallsitePayload> callsites)
    {
        return callsites
            .GroupBy(callsite => callsite.ProducerFamilyId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => BuildFamilyPayload(group.Key, group.ToList()))
            .ToList();
    }

    private static FamilyPayload BuildFamilyPayload(string familyId, IReadOnlyList<CallsitePayload> familyCallsites)
    {
        var first = familyCallsites[0];
        var closureStatusCounts = CountStrings(FamilyClosureUnits(familyCallsites));
        var surfaceCounts = CountStrings(familyCallsites.Select(callsite => callsite.TargetSurface));
        return new FamilyPayload
        {
            ProducerFamilyId = familyId,
            File = first.File,
            Namespace = first.Namespace,
            TypeName = first.TypeName,
            MemberName = first.MemberName,
            MemberKind = first.MemberKind,
            MemberStartLine = first.MemberStartLine,
            CallsiteCount = familyCallsites.Count,
            TextArgumentCount = familyCallsites.Sum(callsite => callsite.TextArguments.Count),
            FamilyClosureStatus = FamilyClosure(closureStatusCounts.Keys.ToHashSet(StringComparer.Ordinal)),
            ClosureStatusCounts = closureStatusCounts,
            SurfaceCounts = surfaceCounts,
            RepresentativeCalls = RepresentativeCalls(familyCallsites),
        };
    }

    private static List<RepresentativeCallPayload> RepresentativeCalls(IEnumerable<CallsitePayload> callsites)
    {
        return callsites.Take(3).Select(callsite => new RepresentativeCallPayload
        {
            File = callsite.File,
            Line = callsite.Line,
            TargetSurface = callsite.TargetSurface,
            Method = callsite.Method,
            ClosureStatus = callsite.ClosureStatus,
            Expression = callsite.Expression,
        }).ToList();
    }

    private static IEnumerable<string> FamilyClosureUnits(IEnumerable<CallsitePayload> familyCallsites)
    {
        foreach (var callsite in familyCallsites)
        {
            if (callsite.TextArguments.Count == 0)
            {
                yield return callsite.ClosureStatus;
                continue;
            }

            foreach (var argument in callsite.TextArguments)
            {
                yield return argument.ClosureStatus;
            }
        }
    }

    private static string FamilyClosure(ISet<string> statuses)
    {
        var effective = statuses.Where(status => status != ClosureStatus.DebugIgnore).ToHashSet(StringComparer.Ordinal);
        if (effective.Count == 0)
        {
            return ClosureStatus.DebugIgnore;
        }

        if (effective.SetEquals(new[] { ClosureStatus.SinkObservedOnly }))
        {
            return ClosureStatus.SinkObservedOnly;
        }

        if (effective.Count == 1)
        {
            return effective.First();
        }

        if (effective.Contains(ClosureStatus.RuntimeRequired) && effective.Overlaps(ActionableStatuses))
        {
            return ClosureStatus.NeedsFamilyReview;
        }

        if (effective.Contains(ClosureStatus.MessagesCandidate) && effective.Contains(ClosureStatus.OwnerPatchRequired))
        {
            return ClosureStatus.NeedsFamilyReview;
        }

        if (effective.Contains(ClosureStatus.RuntimeRequired))
        {
            return ClosureStatus.RuntimeRequired;
        }

        if (effective.Contains(ClosureStatus.OwnerPatchRequired))
        {
            return ClosureStatus.OwnerPatchRequired;
        }

        return ClosureStatus.MessagesCandidate;
    }

    private static TotalsPayload BuildTotals(IReadOnlyList<CallsitePayload> callsites, IReadOnlyList<FamilyPayload> families)
    {
        var textArguments = callsites.SelectMany(callsite => callsite.TextArguments).ToList();
        return new TotalsPayload
        {
            Callsites = callsites.Count,
            Families = families.Count,
            TextArguments = textArguments.Count,
            CallsiteStatuses = CountStrings(callsites.Select(callsite => callsite.ClosureStatus)),
            CallsiteOnlyStatuses = CountStrings(
                callsites.Where(callsite => callsite.TextArguments.Count == 0).Select(callsite => callsite.ClosureStatus)),
            TextArgumentStatuses = CountStrings(textArguments.Select(argument => argument.ClosureStatus)),
            TextArgumentClassifications = CountStrings(textArguments.Select(argument => argument.ExpressionKind)),
            FamilyStatuses = CountStrings(families.Select(family => family.FamilyClosureStatus)),
        };
    }

    private static SortedDictionary<string, int> CountStrings(IEnumerable<string> values)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static string ProducerFamilyId(InvocationCandidate invocation)
    {
        return $"{invocation.File}::{invocation.Context.TypeName}.{invocation.Context.MemberName}";
    }

    private static ArgumentRecord? ArgumentAt(IEnumerable<ArgumentRecord> arguments, int formalIndex)
    {
        return arguments.FirstOrDefault(argument => argument.FormalIndex == formalIndex);
    }

    private static MemberContext GetMemberContext(SyntaxTree tree, InvocationExpressionSyntax invocation)
    {
        var namespaceName = NamespaceName(invocation);
        var typeDeclarations = invocation.Ancestors().OfType<BaseTypeDeclarationSyntax>().Reverse().ToList();
        var localTypeName = typeDeclarations.Count == 0
            ? "<top-level>"
            : string.Join(".", typeDeclarations.Select(TypeIdentifier));
        var typeName = namespaceName is null || localTypeName == "<top-level>"
            ? localTypeName
            : $"{namespaceName}.{localTypeName}";

        var member = NearestMember(invocation);
        if (member is null)
        {
            var fallbackMemberName = typeDeclarations.Count == 0 ? "<top-level>" : "<type-scope>";
            return new MemberContext(
                namespaceName,
                typeName,
                fallbackMemberName,
                "type",
                1,
                new HashSet<string>(StringComparer.Ordinal));
        }

        return member.Value.ToContext(namespaceName, typeName, tree);
    }

    private static string? NamespaceName(SyntaxNode node)
    {
        var namespaces = node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse()
            .Select(namespaceDeclaration => namespaceDeclaration.Name.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        return namespaces.Count == 0 ? null : string.Join(".", namespaces);
    }

    private static string TypeIdentifier(BaseTypeDeclarationSyntax typeDeclaration)
    {
        return typeDeclaration switch
        {
            TypeDeclarationSyntax type => type.Identifier.ValueText,
            _ => typeDeclaration.ToString(),
        };
    }

    private static MemberInfo? NearestMember(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax method:
                    return new MemberInfo(
                        method.Identifier.ValueText,
                        "method",
                        method.Identifier.Span,
                        method.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText));
                case LocalFunctionStatementSyntax localFunction:
                    return new MemberInfo(
                        localFunction.Identifier.ValueText,
                        "method",
                        localFunction.Identifier.Span,
                        localFunction.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText));
                case ConstructorDeclarationSyntax constructor:
                    return new MemberInfo(
                        constructor.Identifier.ValueText,
                        "method",
                        constructor.Identifier.Span,
                        constructor.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText));
                case PropertyDeclarationSyntax property:
                    return new MemberInfo(property.Identifier.ValueText, "property", property.Identifier.Span, Array.Empty<string>());
                case IndexerDeclarationSyntax indexer:
                    return new MemberInfo("this", "property", indexer.ThisKeyword.Span, Array.Empty<string>());
            }
        }

        return null;
    }

    private static bool IsSourceLikeExpression(string expression)
    {
        var normalized = Regex.Replace(expression, @"\s+", "");
        if (SourceLikeNames.Contains(normalized))
        {
            return true;
        }

        if (normalized.EndsWith(".GetBasisGameObject()", StringComparison.Ordinal))
        {
            return true;
        }

        return SourceLikeNames.Where(name => !name.Contains('.', StringComparison.Ordinal))
            .Any(name => normalized.EndsWith($".{name}", StringComparison.Ordinal));
    }

    private static bool IsWrapperSink(InvocationCandidate invocation, IReadOnlyList<TextRole> textArguments)
    {
        if (!IsWrapperPath(invocation.File))
        {
            return false;
        }

        if (textArguments.Count == 0)
        {
            return true;
        }

        return textArguments.All(role => IsForwardedParameter(
            ArgumentAt(invocation.Arguments, role.FormalIndex)?.ExpressionText ?? "",
            invocation.Context.ParameterNames));
    }

    private static bool IsWrapperPath(string relativePath)
    {
        return WrapperPaths.Contains(relativePath);
    }

    private static bool IsForwardedParameter(string expression, ISet<string> parameterNames)
    {
        return parameterNames.Contains(StripBalancedOuterParentheses(expression.Trim()));
    }

    private static bool IsDebugCallsite(InvocationCandidate invocation)
    {
        var basename = Path.GetFileName(invocation.File);
        if (DebugExactPaths.Contains(invocation.File) || invocation.File.StartsWith("XRL.Wish/", StringComparison.Ordinal))
        {
            return true;
        }

        if (Regex.IsMatch(basename, @"\AexDebug.*\.cs\z", RegexOptions.CultureInvariant))
        {
            return true;
        }

        return HasDiagnosticMarker(invocation.Expression);
    }

    private static bool HasDiagnosticMarker(string expression)
    {
        if (Regex.IsMatch(expression, @"\bdebug\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        return DiagnosticMarkers.Any(marker => expression.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStringLiteral(ExpressionSyntax? expression)
    {
        return expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression);
    }

    private static bool IsLiteralTemplate(string expressionText, ExpressionSyntax? expression)
    {
        if (expression is InterpolatedStringExpressionSyntax)
        {
            return true;
        }

        if (IsStringFormatCall(expression))
        {
            return true;
        }

        return expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.AddExpression)
            && ContainsStringLiteral(binary);
    }

    private static bool IsStringFormatCall(ExpressionSyntax? expression)
    {
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Name.Identifier.ValueText != "Format")
        {
            return false;
        }

        var receiver = memberAccess.Expression.ToString().Trim();
        return receiver is "string" or "String" or "System.String";
    }

    private static bool ContainsStringLiteral(SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<LiteralExpressionSyntax>()
            .Any(literal => literal.IsKind(SyntaxKind.StringLiteralExpression));
    }

    private static bool LooksLikeCollectionText(string expressionText, ExpressionSyntax? expression)
    {
        if (expression is InitializerExpressionSyntax or ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax)
        {
            return true;
        }

        if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            return LooksLikeStringCollectionType(objectCreation.Type.ToString());
        }

        var stripped = expressionText.Trim();
        return stripped.StartsWith("new[]", StringComparison.Ordinal)
            || stripped.StartsWith("new List<", StringComparison.Ordinal)
            || stripped.StartsWith("new string[]", StringComparison.Ordinal)
            || stripped.StartsWith("{", StringComparison.Ordinal)
            || stripped.EndsWith("[]", StringComparison.Ordinal)
            || Regex.IsMatch(stripped, @"\b(?:List|IList|IEnumerable)<\s*string\s*>", RegexOptions.CultureInvariant);
    }

    private static bool LooksLikeStringCollectionType(string typeText)
    {
        return typeText.StartsWith("List<", StringComparison.Ordinal)
            || typeText.StartsWith("IList<", StringComparison.Ordinal)
            || typeText.StartsWith("IEnumerable<", StringComparison.Ordinal)
            || typeText.Contains("List<string>", StringComparison.Ordinal)
            || typeText.Contains("IEnumerable<string>", StringComparison.Ordinal);
    }

    private static ExpressionSyntax? StripParentheses(ExpressionSyntax? expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static string StripBalancedOuterParentheses(string expression)
    {
        while (expression.StartsWith("(", StringComparison.Ordinal) && expression.EndsWith(")", StringComparison.Ordinal))
        {
            var inner = expression[1..^1].Trim();
            if (!IsBalancedExpression(inner))
            {
                break;
            }

            expression = inner;
        }

        return expression;
    }

    private static bool IsBalancedExpression(string expression)
    {
        var depth = 0;
        foreach (var character in expression)
        {
            switch (character)
            {
                case '(':
                case '[':
                case '{':
                    depth++;
                    break;
                case ')':
                case ']':
                case '}':
                    depth--;
                    if (depth < 0)
                    {
                        return false;
                    }

                    break;
            }
        }

        return depth == 0;
    }

    private static string? ReceiverName(string? receiver)
    {
        if (receiver is null)
        {
            return null;
        }

        var match = Regex.Match(receiver, @"([A-Za-z_][A-Za-z0-9_]*)\??\s*(?:<[^<>]*>)?\s*\z", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string RelativePath(string sourceRoot, string path)
    {
        return Path.GetRelativePath(sourceRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}

internal static class SymbolDisplayFormats
{
    public static readonly SymbolDisplayFormat MethodSignature = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeType,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
}

internal static class ClosureStatus
{
    public const string MessagesCandidate = "messages_candidate";
    public const string OwnerPatchRequired = "owner_patch_required";
    public const string RuntimeRequired = "runtime_required";
    public const string SinkObservedOnly = "sink_observed_only";
    public const string DebugIgnore = "debug_ignore";
    public const string NeedsFamilyReview = "needs_family_review";
}

internal sealed record SourceFile(string RelativePath, SyntaxTree Tree)
{
    public static SourceFile Parse(string sourceRoot, string sourcePath)
    {
        var text = File.ReadAllText(sourcePath, Encoding.UTF8);
        return new SourceFile(
            StaticProducerScanner.RelativePath(sourceRoot, sourcePath),
            CSharpSyntaxTree.ParseText(text, StaticProducerScanner.ParseOptions, path: sourcePath));
    }
}

internal sealed record CalleeInfo(string? Receiver, string Method);

internal sealed record TextRole(string Name, int FormalIndex);

internal sealed record ArgumentRecord(string? Name, int? FormalIndex, string ExpressionText, ExpressionSyntax ExpressionSyntax);

internal sealed record SemanticCallInfo(
    string Status,
    string? MethodSymbol,
    string? ContainingTypeSymbol,
    string? ReceiverTypeSymbol,
    IReadOnlyDictionary<string, int> ParameterIndexes);

internal sealed record MemberContext(
    string? Namespace,
    string TypeName,
    string MemberName,
    string MemberKind,
    int MemberStartLine,
    ISet<string> ParameterNames);

internal readonly record struct MemberInfo(string Name, string Kind, TextSpan IdentifierSpan, IEnumerable<string> ParameterNames)
{
    public MemberContext ToContext(string? namespaceName, string typeName, SyntaxTree tree)
    {
        return new MemberContext(
            namespaceName,
            typeName,
            Name,
            Kind,
            tree.GetLineSpan(IdentifierSpan).StartLinePosition.Line + 1,
            ParameterNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.Ordinal));
    }
}

internal sealed record InvocationCandidate(
    string File,
    int Line,
    string Expression,
    string? Receiver,
    string Method,
    string TargetSurface,
    IReadOnlyList<ArgumentRecord> Arguments,
    MemberContext Context,
    string? ClosureReason,
    string CalleeExpression,
    int ArgumentCount,
    IReadOnlyList<string> ArgumentNames,
    SemanticCallInfo SemanticInfo);

internal sealed class InventoryPayload
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "";

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = "";

    [JsonPropertyName("target_surfaces")]
    public List<string> TargetSurfaces { get; set; } = new();

    [JsonPropertyName("totals")]
    public TotalsPayload Totals { get; set; } = new();

    [JsonPropertyName("callsites")]
    public List<CallsitePayload> Callsites { get; set; } = new();

    [JsonPropertyName("families")]
    public List<FamilyPayload> Families { get; set; } = new();
}

internal sealed class TextArgumentPayload
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("formal_index")]
    public int FormalIndex { get; set; }

    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";

    [JsonPropertyName("expression_kind")]
    public string ExpressionKind { get; set; } = "";

    [JsonPropertyName("closure_status")]
    public string ClosureStatus { get; set; } = "";
}

internal sealed class CallsitePayload
{
    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("target_surface")]
    public string TargetSurface { get; set; } = "";

    [JsonPropertyName("receiver")]
    public string? Receiver { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    [JsonPropertyName("type_name")]
    public string TypeName { get; set; } = "";

    [JsonPropertyName("member_name")]
    public string MemberName { get; set; } = "";

    [JsonPropertyName("member_kind")]
    public string MemberKind { get; set; } = "";

    [JsonPropertyName("member_start_line")]
    public int MemberStartLine { get; set; }

    [JsonPropertyName("producer_family_id")]
    public string ProducerFamilyId { get; set; } = "";

    [JsonPropertyName("text_arguments")]
    public List<TextArgumentPayload> TextArguments { get; set; } = new();

    [JsonPropertyName("closure_status")]
    public string ClosureStatus { get; set; } = "";

    [JsonPropertyName("closure_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClosureReason { get; set; }

    [JsonPropertyName("argument_count")]
    public int ArgumentCount { get; set; }

    [JsonPropertyName("argument_names")]
    public IReadOnlyList<string> ArgumentNames { get; set; } = Array.Empty<string>();

    [JsonPropertyName("callee_expression")]
    public string CalleeExpression { get; set; } = "";

    [JsonPropertyName("roslyn_symbol_status")]
    public string RoslynSymbolStatus { get; set; } = "syntax_only";

    [JsonPropertyName("method_symbol")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MethodSymbol { get; set; }

    [JsonPropertyName("containing_type_symbol")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContainingTypeSymbol { get; set; }

    [JsonPropertyName("receiver_type_symbol")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReceiverTypeSymbol { get; set; }
}

internal sealed class RepresentativeCallPayload
{
    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("target_surface")]
    public string TargetSurface { get; set; } = "";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("closure_status")]
    public string ClosureStatus { get; set; } = "";

    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";
}

internal sealed class FamilyPayload
{
    [JsonPropertyName("producer_family_id")]
    public string ProducerFamilyId { get; set; } = "";

    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    [JsonPropertyName("type_name")]
    public string TypeName { get; set; } = "";

    [JsonPropertyName("member_name")]
    public string MemberName { get; set; } = "";

    [JsonPropertyName("member_kind")]
    public string MemberKind { get; set; } = "";

    [JsonPropertyName("member_start_line")]
    public int MemberStartLine { get; set; }

    [JsonPropertyName("callsite_count")]
    public int CallsiteCount { get; set; }

    [JsonPropertyName("text_argument_count")]
    public int TextArgumentCount { get; set; }

    [JsonPropertyName("family_closure_status")]
    public string FamilyClosureStatus { get; set; } = "";

    [JsonPropertyName("closure_status_counts")]
    public SortedDictionary<string, int> ClosureStatusCounts { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("surface_counts")]
    public SortedDictionary<string, int> SurfaceCounts { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("representative_calls")]
    public List<RepresentativeCallPayload> RepresentativeCalls { get; set; } = new();
}

internal sealed class TotalsPayload
{
    [JsonPropertyName("callsites")]
    public int Callsites { get; set; }

    [JsonPropertyName("families")]
    public int Families { get; set; }

    [JsonPropertyName("text_arguments")]
    public int TextArguments { get; set; }

    [JsonPropertyName("callsite_statuses")]
    public SortedDictionary<string, int> CallsiteStatuses { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("callsite_only_statuses")]
    public SortedDictionary<string, int> CallsiteOnlyStatuses { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("text_argument_statuses")]
    public SortedDictionary<string, int> TextArgumentStatuses { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("text_argument_classifications")]
    public SortedDictionary<string, int> TextArgumentClassifications { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("family_statuses")]
    public SortedDictionary<string, int> FamilyStatuses { get; set; } = new(StringComparer.Ordinal);
}
