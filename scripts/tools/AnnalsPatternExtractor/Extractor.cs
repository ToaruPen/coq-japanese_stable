using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace QudJP.Tools.AnnalsPatternExtractor;

internal sealed class Extractor
{
    private readonly List<CandidateEntry> candidates = new();
    private readonly List<string> diagnostics = new();

    public IReadOnlyList<CandidateEntry> Candidates => candidates;
    public IReadOnlyList<string> Diagnostics => diagnostics;

    public void ProcessFile(string sourcePath)
    {
        var sourceText = File.ReadAllText(sourcePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetCompilationUnitRoot();
        var fileName = Path.GetFileName(sourcePath);
        var className = Path.GetFileNameWithoutExtension(sourcePath);

        var generateMethod = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == "Generate");
        if (generateMethod is null)
        {
            diagnostics.Add($"{fileName}: no Generate() method found, skipping");
            return;
        }

        // Build local-variable initializer table (expression-level resolution within Generate())
        var localInitializers = CollectResolvableLocals(generateMethod);

        // Find SetEventProperty calls
        var setterCalls = generateMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsSetEventPropertyCall)
            .ToList();

        for (var i = 0; i < setterCalls.Count; i++)
        {
            var invocation = setterCalls[i];
            var (eventProperty, valueExpr) = ParseSetterArgs(invocation);
            if (eventProperty is null || valueExpr is null) continue;
            if (eventProperty != "gospel" && eventProperty != "tombInscription") continue;

            // Include event_property in the id so a single Generate() that sets both
            // "gospel" and "tombInscription" produces distinct ids (no downstream collision).
            var candidateId = $"{className}#{eventProperty}";
            // PR1 does not handle switch/case; if the call is inside a SwitchSectionSyntax, mark needs_manual.
            var switchSection = invocation.Ancestors().OfType<SwitchSectionSyntax>().FirstOrDefault();
            if (switchSection is not null)
            {
                candidates.Add(NeedsManual(
                    id: $"{className}#{eventProperty}#switch{i}",
                    sourceFile: fileName,
                    annalClass: className,
                    switchCase: ExtractSwitchLabel(switchSection),
                    eventProperty: eventProperty,
                    reason: "switch/case decomposition is out of scope for PR1 (deferred to #422)"));
                continue;
            }

            // Other unsupported shapes also degrade.
            if (IsStringFormatCall(valueExpr))
            {
                candidates.Add(NeedsManual(
                    id: candidateId,
                    sourceFile: fileName,
                    annalClass: className,
                    switchCase: "default",
                    eventProperty: eventProperty,
                    reason: "string.Format(...) extraction is out of scope for PR1 (deferred to #422)"));
                continue;
            }

            var resolution = ResolveValueExpression(valueExpr, localInitializers);
            if (!resolution.Resolved)
            {
                candidates.Add(NeedsManual(
                    id: candidateId,
                    sourceFile: fileName,
                    annalClass: className,
                    switchCase: "default",
                    eventProperty: eventProperty,
                    reason: resolution.Reason));
                continue;
            }

            var candidate = BuildCandidate(
                id: candidateId,
                sourceFile: fileName,
                annalClass: className,
                switchCase: "default",
                eventProperty: eventProperty,
                resolved: resolution);

            candidates.Add(candidate);
        }
    }

    private static bool IsSetEventPropertyCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is IdentifierNameSyntax id) return id.Identifier.ValueText == "SetEventProperty";
        if (invocation.Expression is MemberAccessExpressionSyntax m) return m.Name.Identifier.ValueText == "SetEventProperty";
        return false;
    }

    private static (string? property, ExpressionSyntax? value) ParseSetterArgs(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count < 2) return (null, null);
        if (invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax keyLiteral) return (null, null);
        if (!keyLiteral.IsKind(SyntaxKind.StringLiteralExpression)) return (null, null);
        return (keyLiteral.Token.ValueText, invocation.ArgumentList.Arguments[1].Expression);
    }

    private static bool IsStringFormatCall(ExpressionSyntax expr)
    {
        if (expr is not InvocationExpressionSyntax invoc) return false;
        if (invoc.Expression is MemberAccessExpressionSyntax m
            && m.Name.Identifier.ValueText == "Format")
        {
            // `string` keyword parses as PredefinedTypeSyntax in Roslyn, not IdentifierNameSyntax.
            if (m.Expression is PredefinedTypeSyntax pt && pt.Keyword.ValueText == "string") return true;
            if (m.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "string") return true;
        }
        return false;
    }

    private static string? ExtractSwitchLabel(SwitchSectionSyntax section)
    {
        var label = section.Labels.FirstOrDefault();
        return label switch
        {
            CaseSwitchLabelSyntax csl => csl.Value.ToString(),
            DefaultSwitchLabelSyntax => "default",
            _ => null,
        };
    }

    /// <summary>
    /// Collect locals that are safe to inline: exactly one declarator in the method,
    /// non-null initializer, and never reassigned after declaration.
    /// Returns a map from local name → initializer expression (not pre-resolved).
    /// </summary>
    private static IReadOnlyDictionary<string, ExpressionSyntax> CollectResolvableLocals(MethodDeclarationSyntax method)
    {
        // Count declarators per name — skip multi-declarator names (e.g. `string a, b = "x"`)
        var declaratorsByName = new Dictionary<string, List<VariableDeclaratorSyntax>>(StringComparer.Ordinal);
        foreach (var declarator in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            var name = declarator.Identifier.ValueText;
            if (!declaratorsByName.TryGetValue(name, out var list))
            {
                list = new List<VariableDeclaratorSyntax>();
                declaratorsByName[name] = list;
            }
            list.Add(declarator);
        }

        // Collect names that are reassigned anywhere (simple assignment: `name = ...`)
        var reassigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && assignment.Left is IdentifierNameSyntax lhs)
            {
                reassigned.Add(lhs.Identifier.ValueText);
            }
        }

        var dict = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        foreach (var (name, declarators) in declaratorsByName)
        {
            // Must have exactly one declarator and must not be reassigned
            if (declarators.Count != 1) continue;
            if (reassigned.Contains(name)) continue;
            var initValue = declarators[0].Initializer?.Value;
            if (initValue is null) continue;
            dict[name] = initValue;
        }
        return dict;
    }

    private sealed class ResolutionResult
    {
        public bool Resolved { get; init; }
        public string Reason { get; init; } = "";
        public string SampleSource { get; init; } = "";
        public List<SlotEntry> Slots { get; init; } = new();
    }

    private static ResolutionResult ResolveValueExpression(
        ExpressionSyntax valueExpr,
        IReadOnlyDictionary<string, ExpressionSyntax> localInitializers)
    {
        // Required PR1 shapes:
        //   a) single string literal
        //   b) BinaryExpression (+ concat) of string literals and identifier references whose initializer is a literal or concat
        var pieces = new List<string>();
        var slots = new List<SlotEntry>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        if (!FlattenConcat(valueExpr, localInitializers, pieces, slots, visited, out var unsupportedReason))
        {
            return new ResolutionResult { Resolved = false, Reason = unsupportedReason };
        }

        var sample = string.Concat(pieces);
        return new ResolutionResult
        {
            Resolved = true,
            SampleSource = sample,
            Slots = slots,
        };
    }

    private static bool FlattenConcat(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        List<string> pieces,
        List<SlotEntry> slots,
        HashSet<string> visited,
        out string unsupportedReason)
    {
        unsupportedReason = "";
        switch (expr)
        {
            case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                pieces.Add(lit.Token.ValueText);
                return true;

            case IdentifierNameSyntax id:
            {
                var name = id.Identifier.ValueText;
                if (locals.TryGetValue(name, out var initExpr) && !visited.Contains(name))
                {
                    // Recurse into the initializer expression with cycle protection.
                    // Snapshot list counts before recursion so we can roll back any partial
                    // state written by a partially-succeeded binary sub-expression (e.g.
                    // `"lit" + SomeUnsupported()` writes "lit" to pieces before failing).
                    var piecesSnapshot = pieces.Count;
                    var slotsSnapshot = slots.Count;
                    visited.Add(name);
                    try
                    {
                        if (FlattenConcat(initExpr, locals, pieces, slots, visited, out _))
                        {
                            return true;
                        }
                        // Recursion failed: roll back partial state, then degrade to a
                        // single slot for the whole variable rather than propagating failure.
                        pieces.RemoveRange(piecesSnapshot, pieces.Count - piecesSnapshot);
                        slots.RemoveRange(slotsSnapshot, slots.Count - slotsSnapshot);
                        AddSlot(slots, name, type: "unresolved-local");
                        pieces.Add($"{{{slots.Count - 1}}}");
                        return true;
                    }
                    finally
                    {
                        visited.Remove(name);
                    }
                }
                // Not in resolvable locals, or would cycle: degrade to a slot
                AddSlot(slots, name, type: "unresolved-local");
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;
            }

            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression):
                if (!FlattenConcat(bin.Left, locals, pieces, slots, visited, out unsupportedReason)) return false;
                if (!FlattenConcat(bin.Right, locals, pieces, slots, visited, out unsupportedReason)) return false;
                return true;

            case InvocationExpressionSyntax invoc when IsEntityGetProperty(invoc):
                AddSlot(slots, $"entity.GetProperty({GetFirstStringArg(invoc)})", type: "entity-property");
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;

            case InvocationExpressionSyntax invoc when IsRandomCall(invoc):
                AddSlot(slots, "Random(...)", type: "string-format-arg");
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;

            default:
                unsupportedReason =
                    $"unsupported expression for PR1 AST subset: {expr.Kind()} '{expr.ToString()}'";
                return false;
        }
    }

    // NOTE: Receiver type is intentionally not validated here. PR1's Resheph 16
    // candidate set contains no false-positive cases (all `GetProperty` callers
    // in scope are entity accessors), so SemanticModel-based receiver resolution
    // is deferred to PR2+ when the tool gains a full Compilation / SemanticModel.
    private static bool IsEntityGetProperty(InvocationExpressionSyntax invoc)
    {
        if (invoc.Expression is MemberAccessExpressionSyntax m
            && m.Name.Identifier.ValueText == "GetProperty")
        {
            return true;
        }
        return false;
    }

    private static bool IsRandomCall(InvocationExpressionSyntax invoc)
    {
        if (invoc.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "Random") return true;
        return false;
    }

    private static string GetFirstStringArg(InvocationExpressionSyntax invoc)
    {
        if (invoc.ArgumentList.Arguments.Count == 0) return "?";
        if (invoc.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit) return $"\"{lit.Token.ValueText}\"";
        return invoc.ArgumentList.Arguments[0].Expression.ToString();
    }

    private static void AddSlot(List<SlotEntry> slots, string raw, string type)
    {
        slots.Add(new SlotEntry
        {
            Index = slots.Count,
            Type = type,
            Raw = raw,
            Default = $"{{t{slots.Count}}}",
        });
    }

    private static CandidateEntry BuildCandidate(
        string id,
        string sourceFile,
        string annalClass,
        string switchCase,
        string eventProperty,
        ResolutionResult resolved)
    {
        var sample = resolved.SampleSource;
        var pattern = BuildAnchoredRegex(sample, resolved.Slots);
        var c = new CandidateEntry
        {
            Id = id,
            SourceFile = sourceFile,
            AnnalClass = annalClass,
            SwitchCase = switchCase,
            EventProperty = eventProperty,
            SampleSource = sample,
            ExtractedPattern = pattern,
            Slots = resolved.Slots,
            Status = "pending",
            Reason = "",
            JaTemplate = "",
            ReviewNotes = "",
            Route = "annals",
        };
        c.EnTemplateHash = HashHelper.ComputeEnTemplateHash(c);
        return c;
    }

    private static string BuildAnchoredRegex(string sample, List<SlotEntry> slots)
    {
        // Replace each "{N}" placeholder in the sample with a non-greedy capture group, escape literals.
        var sb = new StringBuilder("^");
        var i = 0;
        while (i < sample.Length)
        {
            if (sample[i] == '{' && i + 2 < sample.Length && char.IsDigit(sample[i + 1]))
            {
                var close = sample.IndexOf('}', i);
                if (close > i)
                {
                    var inner = sample[(i + 1)..close];
                    if (inner.Length > 0 && inner.All(char.IsDigit))
                    {
                        sb.Append("(.+?)");
                        i = close + 1;
                        continue;
                    }
                }
            }
            sb.Append(Regex.Escape(sample[i].ToString()));
            i++;
        }
        sb.Append('$');
        return sb.ToString();
    }

    private static CandidateEntry NeedsManual(
        string id,
        string sourceFile,
        string annalClass,
        string? switchCase,
        string eventProperty,
        string reason)
    {
        var c = new CandidateEntry
        {
            Id = id,
            SourceFile = sourceFile,
            AnnalClass = annalClass,
            SwitchCase = switchCase,
            EventProperty = eventProperty,
            SampleSource = "",
            ExtractedPattern = "",
            Slots = new List<SlotEntry>(),
            Status = "needs_manual",
            Reason = reason,
            JaTemplate = "",
            ReviewNotes = "",
            Route = "annals",
        };
        c.EnTemplateHash = HashHelper.ComputeEnTemplateHash(c);
        return c;
    }
}
