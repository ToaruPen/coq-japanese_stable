using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

string? sourceRoot = null;
string? output = null;
string? summaryOutput = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--source-root":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Missing value for --source-root");
                return 2;
            }
            sourceRoot = args[++i];
            break;
        case "--output":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Missing value for --output");
                return 2;
            }
            output = args[++i];
            break;
        case "--summary-output":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Missing value for --summary-output");
                return 2;
            }
            summaryOutput = args[++i];
            break;
        case "--help":
            Console.Out.WriteLine(
                "Usage: TextConstructionInventory --source-root <dir> --output <json-path> [--summary-output <md-path>]");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 2;
    }
}

if (sourceRoot is null || output is null)
{
    Console.Error.WriteLine("Missing required argument. Use --help.");
    return 2;
}

if (!Directory.Exists(sourceRoot))
{
    Console.Error.WriteLine($"--source-root does not exist: {sourceRoot}");
    return 1;
}

var inventory = InventoryBuilder.Scan(sourceRoot);
var outputDirectory = Path.GetDirectoryName(output);
if (!string.IsNullOrEmpty(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
File.WriteAllText(output, JsonSerializer.Serialize(inventory, options) + "\n", new UTF8Encoding(false));
Console.Out.WriteLine($"[roslyn-text-inventory] wrote {inventory.Families.Count} family record(s) to {output}");
if (summaryOutput is not null)
{
    var summaryDirectory = Path.GetDirectoryName(summaryOutput);
    if (!string.IsNullOrEmpty(summaryDirectory))
    {
        Directory.CreateDirectory(summaryDirectory);
    }

    File.WriteAllText(summaryOutput, SummaryMarkdownRenderer.Render(inventory), new UTF8Encoding(false));
    Console.Out.WriteLine($"[roslyn-text-inventory] wrote summary to {summaryOutput}");
}

return 0;

internal static class InventoryBuilder
{
    private const string SchemaVersion = "1.0";
    private const string GameVersion = "1.0.4";

    public static InventoryDocument Scan(string sourceRoot)
    {
        var root = Path.GetFullPath(sourceRoot);
        var sites = new List<TextSite>();
        var parseErrorFiles = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(ShouldInclude)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, path: relativePath);
            var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (diagnostics.Count > 0)
            {
                parseErrorFiles.Add(relativePath);
                continue;
            }

            var collector = new TextConstructionCollector(relativePath, tree);
            collector.Visit(tree.GetRoot());
            sites.AddRange(collector.Sites);
        }

        var families = BuildFamilies(sites);
        return new InventoryDocument
        {
            SchemaVersion = SchemaVersion,
            GameVersion = GameVersion,
            Generation = new GenerationInfo
            {
                Tool = "scripts/tools/TextConstructionInventory",
                Parser = "Microsoft.CodeAnalysis.CSharp",
                IncludesRawSourceText = false,
                IncludesRawEnglishText = false,
                ParseErrorFileCount = parseErrorFiles.Count,
                ParseErrorFiles = parseErrorFiles,
            },
            Totals = BuildTotals(sites, families),
            Families = families,
        };
    }

    private static bool ShouldInclude(string path)
    {
        var normalized = path.Replace(Path.DirectorySeparatorChar, '/');
        return !normalized.EndsWith(".retry.cs", StringComparison.Ordinal)
            && !normalized.EndsWith(".msgprobe.cs", StringComparison.Ordinal)
            && !normalized.Contains("/bin/", StringComparison.Ordinal)
            && !normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static List<FamilyRecord> BuildFamilies(List<TextSite> sites)
    {
        return sites
            .GroupBy(site => site.FamilyId, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.OrderBy(site => site.Line).First();
                return new FamilyRecord
                {
                    FamilyId = group.Key,
                    File = first.File,
                    Namespace = first.Namespace,
                    TypeName = first.TypeName,
                    MemberName = first.MemberName,
                    MemberSignature = first.MemberSignature,
                    MemberKind = first.MemberKind,
                    MemberStartLine = first.MemberStartLine,
                    TextConstructionCount = group.Count(),
                    ShapeCounts = CountBy(group, site => site.Shape),
                    ContextCounts = CountBy(group, site => site.ContextKind),
                    SurfaceCounts = CountMany(group, site => site.Surfaces),
                    FirstLines = group.Select(site => site.Line).Distinct().Order().Take(10).ToList(),
                };
            })
            .OrderBy(family => family.File, StringComparer.Ordinal)
            .ThenBy(family => family.MemberStartLine)
            .ThenBy(family => family.FamilyId, StringComparer.Ordinal)
            .ToList();
    }

    private static TotalsRecord BuildTotals(List<TextSite> sites, List<FamilyRecord> families)
    {
        return new TotalsRecord
        {
            FilesWithTextConstructions = sites.Select(site => site.File).Distinct(StringComparer.Ordinal).Count(),
            Families = families.Count,
            TextConstructions = sites.Count,
            ShapeCounts = CountBy(sites, site => site.Shape),
            ContextCounts = CountBy(sites, site => site.ContextKind),
            SurfaceCounts = CountMany(sites, site => site.Surfaces),
        };
    }

    private static Dictionary<string, int> CountBy<T>(IEnumerable<T> rows, Func<T, string> selector)
    {
        return rows
            .GroupBy(selector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static Dictionary<string, int> CountMany<T>(IEnumerable<T> rows, Func<T, IEnumerable<string>> selector)
    {
        return rows
            .SelectMany(selector)
            .GroupBy(value => value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }
}

internal sealed class TextConstructionCollector : CSharpSyntaxWalker
{
    private readonly string file;
    private readonly SyntaxTree tree;

    public TextConstructionCollector(string file, SyntaxTree tree)
    {
        this.file = file;
        this.tree = tree;
    }

    public List<TextSite> Sites { get; } = [];

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.StringLiteralExpression) && !IsNestedInsideTrackedExpression(node))
        {
            AddSite(node, "static_literal", literalPartCount: 1, expressionPartCount: 0);
        }

        base.VisitLiteralExpression(node);
    }

    public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        if (!IsNestedInsideTrackedExpression(node))
        {
            var literalParts = node.Contents.OfType<InterpolatedStringTextSyntax>().Count();
            var expressionParts = node.Contents.OfType<InterpolationSyntax>().Count();
            AddSite(node, "interpolation", literalParts, expressionParts);
        }

        base.VisitInterpolatedStringExpression(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (IsTopLevelStringConcatenation(node))
        {
            var counts = CountConcatParts(node);
            AddSite(node, "concatenation", counts.LiteralParts, counts.ExpressionParts);
        }

        base.VisitBinaryExpression(node);
    }

    private void AddSite(ExpressionSyntax expression, string shape, int literalPartCount, int expressionPartCount)
    {
        var lineSpan = tree.GetLineSpan(expression.Span);
        var type = expression.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        var member = expression.FirstAncestorOrSelf<MemberDeclarationSyntax>(IsMember);
        var context = ContextClassifier.Classify(expression);
        var memberName = MemberName(member);
        var memberSignature = MemberSignature(member);
        var memberKind = MemberKind(member);
        var memberStartLine = member is null ? 0 : tree.GetLineSpan(member.Span).StartLinePosition.Line + 1;
        var namespaceName = expression.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString();
        var typeName = type?.Identifier.ValueText ?? "<global>";
        var familyId = $"{file}::{typeName}.{memberSignature}";

        Sites.Add(new TextSite
        {
            File = file,
            Line = lineSpan.StartLinePosition.Line + 1,
            Column = lineSpan.StartLinePosition.Character + 1,
            Namespace = namespaceName,
            TypeName = typeName,
            MemberName = memberName,
            MemberSignature = memberSignature,
            MemberKind = memberKind,
            MemberStartLine = memberStartLine,
            FamilyId = familyId,
            Shape = shape,
            ContextKind = context.ContextKind,
            Surfaces = context.Surfaces,
            LiteralPartCount = literalPartCount,
            ExpressionPartCount = expressionPartCount,
        });
    }

    private static bool IsMember(SyntaxNode node)
    {
        return node is BaseMethodDeclarationSyntax
            or PropertyDeclarationSyntax
            or FieldDeclarationSyntax
            or EventFieldDeclarationSyntax;
    }

    private static string MemberName(MemberDeclarationSyntax? member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            DestructorDeclarationSyntax destructor => destructor.Identifier.ValueText,
            OperatorDeclarationSyntax op => op.OperatorToken.ValueText,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type.ToString(),
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            FieldDeclarationSyntax field => string.Join(",", field.Declaration.Variables.Select(v => v.Identifier.ValueText)),
            EventFieldDeclarationSyntax field => string.Join(",", field.Declaration.Variables.Select(v => v.Identifier.ValueText)),
            _ => "<member>",
        };
    }

    private static string MemberKind(MemberDeclarationSyntax? member)
    {
        return member switch
        {
            MethodDeclarationSyntax => "method",
            ConstructorDeclarationSyntax => "constructor",
            DestructorDeclarationSyntax => "destructor",
            OperatorDeclarationSyntax => "operator",
            ConversionOperatorDeclarationSyntax => "conversion_operator",
            PropertyDeclarationSyntax => "property",
            FieldDeclarationSyntax => "field",
            EventFieldDeclarationSyntax => "event_field",
            _ => "unknown",
        };
    }

    private static string MemberSignature(MemberDeclarationSyntax? member)
    {
        return member switch
        {
            MethodDeclarationSyntax method =>
                $"{method.Identifier.ValueText}{TypeParameterShape(method.TypeParameterList)}({ParameterShape(method.ParameterList)})",
            ConstructorDeclarationSyntax constructor => $"{constructor.Identifier.ValueText}({ParameterShape(constructor.ParameterList)})",
            DestructorDeclarationSyntax destructor => $"~{destructor.Identifier.ValueText}()",
            OperatorDeclarationSyntax op => $"operator {op.OperatorToken.ValueText}({ParameterShape(op.ParameterList)})",
            ConversionOperatorDeclarationSyntax conversion => $"operator {conversion.Type}({ParameterShape(conversion.ParameterList)})",
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            FieldDeclarationSyntax field => string.Join(",", field.Declaration.Variables.Select(v => v.Identifier.ValueText)),
            EventFieldDeclarationSyntax field => string.Join(",", field.Declaration.Variables.Select(v => v.Identifier.ValueText)),
            _ => "<member>",
        };
    }

    private static string TypeParameterShape(TypeParameterListSyntax? typeParameterList)
    {
        if (typeParameterList is null || typeParameterList.Parameters.Count == 0)
        {
            return "";
        }

        return $"<{string.Join(",", typeParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText))}>";
    }

    private static string ParameterShape(ParameterListSyntax parameterList)
    {
        return string.Join(
            ",",
            parameterList.Parameters.Select(parameter =>
            {
                var modifiers = parameter.Modifiers.Count == 0
                    ? ""
                    : string.Concat(parameter.Modifiers.Select(modifier => modifier.ValueText + " "));
                var type = parameter.Type?.ToString().Replace(" ", "", StringComparison.Ordinal) ?? "<unknown>";
                return modifiers + type;
            }));
    }

    private static bool IsNestedInsideTrackedExpression(ExpressionSyntax expression)
    {
        var parentExpression = expression.Parent as ExpressionSyntax;
        while (parentExpression is not null)
        {
            if (parentExpression is InterpolatedStringExpressionSyntax)
            {
                return true;
            }

            if (parentExpression is BinaryExpressionSyntax binaryExpression && IsStringConcatenation(binaryExpression))
            {
                return true;
            }

            parentExpression = parentExpression.Parent as ExpressionSyntax;
        }

        return false;
    }

    private static bool IsTopLevelStringConcatenation(BinaryExpressionSyntax node)
    {
        return IsStringConcatenation(node)
            && (node.Parent is not BinaryExpressionSyntax parent || !IsStringConcatenation(parent));
    }

    private static bool IsStringConcatenation(BinaryExpressionSyntax node)
    {
        return node.IsKind(SyntaxKind.AddExpression)
            && (ContainsTrackedStringShape(node.Left) || ContainsTrackedStringShape(node.Right));
    }

    private static bool ContainsTrackedStringShape(ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
            InterpolatedStringExpressionSyntax => true,
            BinaryExpressionSyntax binary => IsStringConcatenation(binary),
            _ => false,
        };
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static (int LiteralParts, int ExpressionParts) CountConcatParts(ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);
        if (expression is BinaryExpressionSyntax binary && IsStringConcatenation(binary))
        {
            var left = CountConcatParts(binary.Left);
            var right = CountConcatParts(binary.Right);
            return (left.LiteralParts + right.LiteralParts, left.ExpressionParts + right.ExpressionParts);
        }

        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return (1, 0);
        }

        return (0, 1);
    }
}

internal static class ContextClassifier
{
    public static TextContext Classify(ExpressionSyntax expression)
    {
        var surfaces = new List<string>();
        var argument = expression.FirstAncestorOrSelf<ArgumentSyntax>();
        if (argument?.Parent?.Parent is InvocationExpressionSyntax invocation)
        {
            AddSurface(surfaces, SurfaceClassifier.Classify(invocation));
            AddOuterSurfaces(expression, surfaces);
            return new TextContext("invocation_argument", surfaces);
        }

        if (expression.FirstAncestorOrSelf<AttributeArgumentSyntax>() is not null)
        {
            AddSurface(surfaces, "Attribute");
            AddOuterSurfaces(expression, surfaces);
            return new TextContext("attribute_argument", surfaces);
        }

        if (expression.FirstAncestorOrSelf<ReturnStatementSyntax>() is { } returnStatement
            && returnStatement.Expression is not null
            && returnStatement.Expression.Span.Contains(expression.Span))
        {
            AddSurface(surfaces, ReturnSurface(expression));
            return new TextContext("return_expression", surfaces);
        }

        if (expression.FirstAncestorOrSelf<ArrowExpressionClauseSyntax>() is { } arrow
            && arrow.Expression.Span.Contains(expression.Span))
        {
            AddSurface(surfaces, ReturnSurface(expression));
            return new TextContext("return_expression", surfaces);
        }

        if (expression.FirstAncestorOrSelf<AssignmentExpressionSyntax>() is { } assignment
            && assignment.Right.Span.Contains(expression.Span))
        {
            AddSurface(surfaces, AssignmentSurface(assignment.Left));
            return new TextContext("assignment_rhs", surfaces);
        }

        if (expression.FirstAncestorOrSelf<EqualsValueClauseSyntax>() is not null)
        {
            AddSurface(surfaces, "Initializer");
            return new TextContext("initializer", surfaces);
        }

        AddSurface(surfaces, "Other");
        return new TextContext("other", surfaces);
    }

    private static void AddOuterSurfaces(ExpressionSyntax expression, List<string> surfaces)
    {
        if (expression.FirstAncestorOrSelf<ReturnStatementSyntax>() is { } returnStatement
            && returnStatement.Expression is not null
            && returnStatement.Expression.Span.Contains(expression.Span))
        {
            AddSurface(surfaces, ReturnSurface(expression));
        }

        if (expression.FirstAncestorOrSelf<ArrowExpressionClauseSyntax>() is { } arrow
            && arrow.Expression.Span.Contains(expression.Span))
        {
            AddSurface(surfaces, ReturnSurface(expression));
        }

        if (expression.FirstAncestorOrSelf<AssignmentExpressionSyntax>() is { } assignment
            && assignment.Right.Span.Contains(expression.Span))
        {
            AddSurface(surfaces, AssignmentSurface(assignment.Left));
        }

        if (expression.FirstAncestorOrSelf<EqualsValueClauseSyntax>() is not null)
        {
            AddSurface(surfaces, "Initializer");
        }
    }

    private static void AddSurface(List<string> surfaces, string surface)
    {
        if (!surfaces.Contains(surface, StringComparer.Ordinal))
        {
            surfaces.Add(surface);
        }
    }

    private static string ReturnSurface(ExpressionSyntax expression)
    {
        var member = expression.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.ValueText
            ?? expression.FirstAncestorOrSelf<PropertyDeclarationSyntax>()?.Identifier.ValueText;
        return member switch
        {
            "GetDescription" or "GetDetails" => "EffectDescriptionReturn",
            "GetShortDescription" or "GetLongDescription" => "DescriptionReturn",
            "GetDisplayName" or "GetReferenceDisplayName" => "DisplayNameReturn",
            "GetDisplayText" => "DisplayTextReturn",
            _ => "Return",
        };
    }

    private static string AssignmentSurface(ExpressionSyntax left)
    {
        var target = left.ToString();
        if (IsConversationChoiceTagAssignment(left))
        {
            return "ConversationChoiceTag";
        }

        if (target.EndsWith(".text", StringComparison.Ordinal)
            || target.EndsWith(".Text", StringComparison.Ordinal))
        {
            return "DirectTextAssignment";
        }

        if (target.Contains("DisplayName", StringComparison.Ordinal))
        {
            return "DisplayNameAssignment";
        }

        if (target.Contains("Description", StringComparison.Ordinal)
            || target.EndsWith(".Short", StringComparison.Ordinal)
            || target.EndsWith(".Long", StringComparison.Ordinal))
        {
            return "DescriptionAssignment";
        }

        return "Assignment";
    }

    private static bool IsConversationChoiceTagAssignment(ExpressionSyntax left)
    {
        if (left is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "Tag", StringComparison.Ordinal)
            || memberAccess.Expression is not IdentifierNameSyntax receiver)
        {
            return false;
        }

        var method = left.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null
            || !string.Equals(method.Identifier.ValueText, "HandleEvent", StringComparison.Ordinal)
            || method.ParameterList.Parameters.Count != 1)
        {
            return false;
        }

        var parameter = method.ParameterList.Parameters[0];
        return string.Equals(parameter.Identifier.ValueText, receiver.Identifier.ValueText, StringComparison.Ordinal)
            && string.Equals(parameter.Type?.ToString(), "GetChoiceTagEvent", StringComparison.Ordinal);
    }
}

internal static class SurfaceClassifier
{
    private static readonly HashSet<string> MessageFrameMethods =
        new(StringComparer.Ordinal) { "DidX", "DidXToY", "DidXToYWithZ", "XDidY", "XDidYToZ", "WDidXToYWithZ" };

    public static string Classify(InvocationExpressionSyntax invocation)
    {
        var method = MethodName(invocation.Expression);
        var receiver = ReceiverName(invocation.Expression);
        var receiverExpression = ReceiverExpression(invocation.Expression);

        if (string.Equals(method, "SetText", StringComparison.Ordinal))
        {
            return "SetText";
        }

        if (IsConversationTextReceiver(receiverExpression))
        {
            if (IsStringBuilderAppend(method))
            {
                return "ConversationTextAppend";
            }

            if (string.Equals(method, "Replace", StringComparison.Ordinal))
            {
                return "ConversationTextReplace";
            }
        }

        if (IsStringBuilderAppend(method))
        {
            return "StringBuilderAppend";
        }

        if (IsStringFormat(receiver, method))
        {
            return "StringFormat";
        }

        if (string.Equals(method, "Replace", StringComparison.Ordinal))
        {
            return "ReplaceChain";
        }

        if (string.Equals(method, "AddMyActivatedAbility", StringComparison.Ordinal)
            || string.Equals(method, "SetMyActivatedAbilityDisplayName", StringComparison.Ordinal))
        {
            return "ActivatedAbility";
        }

        if (string.Equals(method, "AddPlayerMessage", StringComparison.Ordinal))
        {
            return "AddPlayerMessage";
        }

        if (string.Equals(receiver, "Popup", StringComparison.Ordinal) && IsPopupMethod(method))
        {
            return "Popup";
        }

        if (string.Equals(receiver, "TutorialManager", StringComparison.Ordinal) && method.StartsWith("Show", StringComparison.Ordinal))
        {
            return "TutorialManagerPopup";
        }

        if (MessageFrameMethods.Contains(method)
            || (string.Equals(receiver, "Messaging", StringComparison.Ordinal) && MessageFrameMethods.Contains(method)))
        {
            return "MessageFrame";
        }

        if (string.Equals(method, "GetDisplayName", StringComparison.Ordinal))
        {
            return "GetDisplayName";
        }

        if (string.Equals(method, "Does", StringComparison.Ordinal))
        {
            return "Does";
        }

        if (string.Equals(method, "EmitMessage", StringComparison.Ordinal)
            || (string.Equals(receiver, "Messaging", StringComparison.Ordinal) && string.Equals(method, "EmitMessage", StringComparison.Ordinal)))
        {
            return "EmitMessage";
        }

        if (string.Equals(method, "GetShortDescription", StringComparison.Ordinal)
            || string.Equals(method, "GetLongDescription", StringComparison.Ordinal))
        {
            return "Description";
        }

        if (string.Equals(receiver, "JournalAPI", StringComparison.Ordinal) && method.StartsWith("Add", StringComparison.Ordinal))
        {
            return "JournalAPI";
        }

        if (string.Equals(receiver, "HistoricStringExpander", StringComparison.Ordinal)
            && string.Equals(method, "ExpandString", StringComparison.Ordinal))
        {
            return "HistoricStringExpander";
        }

        if (string.Equals(method, "StartReplace", StringComparison.Ordinal))
        {
            return "ReplaceBuilder";
        }

        return "OtherInvocation";
    }

    private static bool IsStringBuilderAppend(string method)
    {
        return string.Equals(method, "Append", StringComparison.Ordinal)
            || string.Equals(method, "AppendLine", StringComparison.Ordinal)
            || string.Equals(method, "AppendFormat", StringComparison.Ordinal);
    }

    private static bool IsStringFormat(string? receiver, string method)
    {
        return string.Equals(method, "Format", StringComparison.Ordinal)
            && (string.Equals(receiver, "string", StringComparison.Ordinal)
                || string.Equals(receiver, "String", StringComparison.Ordinal)
                || string.Equals(receiver, "System.String", StringComparison.Ordinal));
    }

    private static bool IsPopupMethod(string method)
    {
        return method.StartsWith("Show", StringComparison.Ordinal)
            || method.StartsWith("Ask", StringComparison.Ordinal)
            || method.StartsWith("Pick", StringComparison.Ordinal)
            || method.StartsWith("Render", StringComparison.Ordinal)
            || method.StartsWith("Warn", StringComparison.Ordinal)
            || method.Contains("Popup", StringComparison.Ordinal);
    }

    private static string MethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => expression.ToString(),
        };
    }

    private static string? ReceiverName(ExpressionSyntax expression)
    {
        if (expression is MemberAccessExpressionSyntax member)
        {
            return member.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                MemberAccessExpressionSyntax nested => nested.Name.Identifier.ValueText,
                _ => member.Expression.ToString(),
            };
        }

        return null;
    }

    private static string? ReceiverExpression(ExpressionSyntax expression)
    {
        return expression is MemberAccessExpressionSyntax member ? member.Expression.ToString() : null;
    }

    private static bool IsConversationTextReceiver(string? receiverExpression)
    {
        return string.Equals(receiverExpression, "E.Text", StringComparison.Ordinal);
    }
}

internal static class SummaryMarkdownRenderer
{
    public static string Render(InventoryDocument inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Roslyn Text Construction Inventory Summary");
        builder.AppendLine();
        builder.AppendLine("This is the CodeRabbit-facing summary for the raw-free Roslyn text construction");
        builder.AppendLine($"inventory generated from local decompiled Caves of Qud `{inventory.GameVersion}` C# sources.");
        builder.AppendLine();
        builder.AppendLine("The full generated family inventory is intentionally not committed and should");
        builder.AppendLine("not be added to CodeRabbit knowledge-base file patterns. It is a large");
        builder.AppendLine("symbol-level audit artifact. Maintainers may regenerate it locally when a PR");
        builder.AppendLine("claims broad runtime text coverage.");
        builder.AppendLine();
        builder.AppendLine("Committed CodeRabbit knowledge should use this summary and the route/token/");
        builder.AppendLine("glossary review rules instead of the full JSON.");
        builder.AppendLine();
        builder.AppendLine("Complementary review knowledge:");
        builder.AppendLine();
        builder.AppendLine("- `CODERABBIT.md`");
        builder.AppendLine("- `docs/coderabbit/owner-route-coverage.md`");
        builder.AppendLine("- `docs/coderabbit/color-token-review.md`");
        builder.AppendLine("- `docs/coderabbit/glossary-variance-review.md`");
        builder.AppendLine();
        builder.AppendLine("Generator:");
        builder.AppendLine();
        builder.AppendLine("- `scripts/tools/TextConstructionInventory/`");
        builder.AppendLine();
        builder.AppendLine("Reproduction:");
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine("just text-construction-inventory \\");
        builder.AppendLine("  \"$HOME/dev/coq-decompiled_stable\" \\");
        builder.AppendLine("  /tmp/roslyn-text-construction-inventory.json \\");
        builder.AppendLine("  docs/coderabbit/roslyn-text-construction-inventory-summary.md");
        builder.AppendLine("```");
        builder.AppendLine();
        AppendScope(builder);
        AppendTotals(builder, inventory);
        AppendCountSection(builder, "Shape Counts", "Shape", inventory.Totals.ShapeCounts);
        AppendCountSection(builder, "Context Counts", "Context", inventory.Totals.ContextCounts);
        AppendCountSection(builder, "Route-Surface Counts", "Surface", inventory.Totals.SurfaceCounts);
        AppendReviewUse(builder);
        return builder.ToString();
    }

    private static void AppendScope(StringBuilder builder)
    {
        builder.AppendLine("## Scope");
        builder.AppendLine();
        builder.AppendLine("The inventory is built with `Microsoft.CodeAnalysis.CSharp` and records derived");
        builder.AppendLine("metadata only. It does not include raw source text, raw English strings,");
        builder.AppendLine("decompiled method bodies, or absolute source-root paths. The full local JSON");
        builder.AppendLine("includes relative `parse_error_files` so skipped files are auditable without");
        builder.AppendLine("exposing source content.");
        builder.AppendLine();
        builder.AppendLine("The scanner captures string construction shapes across parsed C# files:");
        builder.AppendLine();
        builder.AppendLine("- string literals;");
        builder.AppendLine("- interpolated strings;");
        builder.AppendLine("- string concatenations;");
        builder.AppendLine("- invocation arguments;");
        builder.AppendLine("- assignment right-hand sides;");
        builder.AppendLine("- return expressions;");
        builder.AppendLine("- expression-bodied return expressions;");
        builder.AppendLine("- initializers;");
        builder.AppendLine("- attribute arguments.");
        builder.AppendLine();
    }

    private static void AppendTotals(StringBuilder builder, InventoryDocument inventory)
    {
        builder.AppendLine("## Totals");
        builder.AppendLine();
        builder.AppendLine("| Metric | Count |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| Files with text constructions | {inventory.Totals.FilesWithTextConstructions} |");
        builder.AppendLine($"| Producer/member families | {inventory.Totals.Families} |");
        builder.AppendLine($"| Text constructions | {inventory.Totals.TextConstructions} |");
        builder.AppendLine($"| Parse-error files skipped | {inventory.Generation.ParseErrorFileCount} |");
        builder.AppendLine();
        builder.AppendLine("Skipped parse-error files:");
        builder.AppendLine();
        if (inventory.Generation.ParseErrorFiles.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var file in inventory.Generation.ParseErrorFiles)
            {
                builder.AppendLine($"- `{file}`");
            }
        }

        builder.AppendLine();
    }

    private static void AppendCountSection(
        StringBuilder builder,
        string title,
        string columnName,
        Dictionary<string, int> counts)
    {
        builder.AppendLine($"### {title}");
        builder.AppendLine();
        builder.AppendLine($"| {columnName} | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var (name, count) in counts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| `{name}` | {count} |");
        }

        builder.AppendLine();
    }

    private static void AppendReviewUse(StringBuilder builder)
    {
        builder.AppendLine("## Review Use");
        builder.AppendLine();
        builder.AppendLine("Use this inventory as a completeness map, not as proof that a route is already");
        builder.AppendLine("localized. A family being present means Roslyn found text construction in that");
        builder.AppendLine("member. It does not mean QudJP owns the route. Route-surface counts may exceed");
        builder.AppendLine("text-construction counts because nested constructions record both inner surfaces");
        builder.AppendLine("such as `StringFormat` and outer route clues such as `DisplayNameAssignment`.");
        builder.AppendLine();
        builder.AppendLine("When reviewing QudJP changes:");
        builder.AppendLine();
        builder.AppendLine("- require owner-route reasoning for any family surfaced by the inventory;");
        builder.AppendLine("- treat `AddPlayerMessage`, `SetText`, `DirectTextAssignment`, and renderer-like");
        builder.AppendLine("  surfaces as sink-adjacent until producer ownership is proven;");
        builder.AppendLine("- require tests before accepting changes that claim a family is covered;");
        builder.AppendLine("- flag PR claims of \"complete runtime text coverage\" unless the Roslyn summary,");
        builder.AppendLine("  local regeneration evidence, and the QudJP owner/test matrix were all updated.");
        builder.AppendLine();
        builder.AppendLine("## Limits");
        builder.AppendLine();
        builder.AppendLine("This inventory is Roslyn syntax based. It does not build a full semantic");
        builder.AppendLine("compilation, infer runtime receiver types for every call, inspect XML/JSON game");
        builder.AppendLine("data, or prove UI rendering behavior. Runtime logs and L2/L2G/L3 evidence remain");
        builder.AppendLine("required only when static evidence cannot settle final behavior.");
    }
}

internal sealed class InventoryDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "";

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = "";

    [JsonPropertyName("generation")]
    public GenerationInfo Generation { get; set; } = new();

    [JsonPropertyName("totals")]
    public TotalsRecord Totals { get; set; } = new();

    [JsonPropertyName("families")]
    public List<FamilyRecord> Families { get; set; } = [];
}

internal sealed class GenerationInfo
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "";

    [JsonPropertyName("parser")]
    public string Parser { get; set; } = "";

    [JsonPropertyName("includes_raw_source_text")]
    public bool IncludesRawSourceText { get; set; }

    [JsonPropertyName("includes_raw_english_text")]
    public bool IncludesRawEnglishText { get; set; }

    [JsonPropertyName("parse_error_file_count")]
    public int ParseErrorFileCount { get; set; }

    [JsonPropertyName("parse_error_files")]
    public List<string> ParseErrorFiles { get; set; } = [];
}

internal sealed class TotalsRecord
{
    [JsonPropertyName("files_with_text_constructions")]
    public int FilesWithTextConstructions { get; set; }

    [JsonPropertyName("families")]
    public int Families { get; set; }

    [JsonPropertyName("text_constructions")]
    public int TextConstructions { get; set; }

    [JsonPropertyName("shape_counts")]
    public Dictionary<string, int> ShapeCounts { get; set; } = [];

    [JsonPropertyName("context_counts")]
    public Dictionary<string, int> ContextCounts { get; set; } = [];

    [JsonPropertyName("surface_counts")]
    public Dictionary<string, int> SurfaceCounts { get; set; } = [];
}

internal sealed class FamilyRecord
{
    [JsonPropertyName("family_id")]
    public string FamilyId { get; set; } = "";

    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    [JsonPropertyName("type_name")]
    public string TypeName { get; set; } = "";

    [JsonPropertyName("member_name")]
    public string MemberName { get; set; } = "";

    [JsonPropertyName("member_signature")]
    public string MemberSignature { get; set; } = "";

    [JsonPropertyName("member_kind")]
    public string MemberKind { get; set; } = "";

    [JsonPropertyName("member_start_line")]
    public int MemberStartLine { get; set; }

    [JsonPropertyName("text_construction_count")]
    public int TextConstructionCount { get; set; }

    [JsonPropertyName("shape_counts")]
    public Dictionary<string, int> ShapeCounts { get; set; } = [];

    [JsonPropertyName("context_counts")]
    public Dictionary<string, int> ContextCounts { get; set; } = [];

    [JsonPropertyName("surface_counts")]
    public Dictionary<string, int> SurfaceCounts { get; set; } = [];

    [JsonPropertyName("first_lines")]
    public List<int> FirstLines { get; set; } = [];
}

internal sealed class TextSite
{
    public string File { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string? Namespace { get; set; }
    public string TypeName { get; set; } = "";
    public string MemberName { get; set; } = "";
    public string MemberSignature { get; set; } = "";
    public string MemberKind { get; set; } = "";
    public int MemberStartLine { get; set; }
    public string FamilyId { get; set; } = "";
    public string Shape { get; set; } = "";
    public string ContextKind { get; set; } = "";
    public List<string> Surfaces { get; set; } = [];
    public int LiteralPartCount { get; set; }
    public int ExpressionPartCount { get; set; }
}

internal readonly record struct TextContext(string ContextKind, List<string> Surfaces);
