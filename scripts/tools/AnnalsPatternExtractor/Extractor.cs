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

        ApplyHistoryKitTokenLexer(pieces, slots);
        ApplyHseExpansion(pieces, slots);

        var sample = string.Concat(pieces);
        return new ResolutionResult
        {
            Resolved = true,
            SampleSource = sample,
            Slots = slots,
        };
    }

    // HistoryKit `<...>` tokens are expanded by HistoricStringExpander.ExpandString to arbitrary
    // text (or empty for assignment-form `<$var=...>`). We slot them so the runtime regex matches
    // the post-expansion stored value, not the pre-expansion source literal.
    private const string HistoryKitTokenType = "historykit-token";

    private static void ApplyHistoryKitTokenLexer(List<string> pieces, List<SlotEntry> slots)
    {
        // Renumbering is required because we splice new slots into the middle of the slot list.
        // Strategy: rebuild pieces+slots from scratch, scanning every existing piece. Existing
        // slot references (e.g. `{0}` already produced by FlattenConcat) are remapped to the new
        // index space.
        var oldSlots = new List<SlotEntry>(slots);
        var oldPieces = new List<string>(pieces);
        slots.Clear();
        pieces.Clear();

        foreach (var piece in oldPieces)
        {
            if (piece.Length >= 3 && piece[0] == '{' && piece[^1] == '}'
                && int.TryParse(piece.AsSpan(1, piece.Length - 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var oldIndex)
                && (uint)oldIndex < (uint)oldSlots.Count)
            {
                var slot = oldSlots[oldIndex];
                slot.Index = slots.Count;
                slot.Default = $"{{t{slots.Count}}}";
                slots.Add(slot);
                pieces.Add($"{{{slot.Index}}}");
                continue;
            }

            // Literal piece: scan for `<...>` tokens.
            var i = 0;
            var sb = new StringBuilder();
            while (i < piece.Length)
            {
                if (piece[i] != '<')
                {
                    sb.Append(piece[i]);
                    i++;
                    continue;
                }
                var close = piece.IndexOf('>', i + 1);
                if (close < 0)
                {
                    sb.Append(piece[i]);
                    i++;
                    continue;
                }
                var token = piece.Substring(i, close - i + 1);
                if (IsHistoryKitAssignmentToken(token))
                {
                    // Zero-width: assignment-form tokens (`<$var=...>`) emit "" at runtime.
                    i = close + 1;
                    continue;
                }
                if (sb.Length > 0)
                {
                    pieces.Add(sb.ToString());
                    sb.Clear();
                }
                AddSlot(slots, token, type: HistoryKitTokenType);
                pieces.Add($"{{{slots.Count - 1}}}");
                i = close + 1;
            }
            if (sb.Length > 0) pieces.Add(sb.ToString());
        }
    }

    private static bool IsHistoryKitAssignmentToken(string token)
    {
        // Form: `<$name=...>` — the first character after `<` is `$` and the body contains `=`.
        if (token.Length < 4 || token[0] != '<' || token[^1] != '>') return false;
        if (token[1] != '$') return false;
        // Search only inside the angle brackets.
        return token.AsSpan(2, token.Length - 3).IndexOf('=') >= 0;
    }

    // HistoricStringExpander rewrites %name% in the runtime value before the regex matches.
    private const string HseExpansionType = "hse-expansion";

    private sealed record HseExpansion(string SampleSuffix, string RegexSuffix);

    private static readonly IReadOnlyDictionary<string, HseExpansion> HseExpansions =
        new Dictionary<string, HseExpansion>(StringComparer.Ordinal)
        {
            ["year"] = new HseExpansion(SampleSuffix: " AR", RegexSuffix: "\\ (?:BR|AR)"),
        };

    private static void ApplyHseExpansion(List<string> pieces, List<SlotEntry> slots)
    {
        for (var i = 0; i + 2 < pieces.Count; i++)
        {
            var left = pieces[i];
            var middle = pieces[i + 1];
            var right = pieces[i + 2];
            if (!left.EndsWith("%", StringComparison.Ordinal)) continue;
            if (!right.StartsWith("%", StringComparison.Ordinal)) continue;
            if (!TryGetSlotIndex(middle, out var slotIndex)) continue;
            if ((uint)slotIndex >= (uint)slots.Count) continue;
            var slot = slots[slotIndex];
            if (!HseExpansions.TryGetValue(slot.Raw, out var expansion)) continue;

            pieces[i] = left[..^1];
            pieces[i + 2] = expansion.SampleSuffix + right[1..];
            slot.Type = HseExpansionType;
        }
    }

    private static bool TryGetSlotIndex(ReadOnlySpan<char> piece, out int index)
    {
        index = -1;
        if (piece.Length < 3) return false;
        if (piece[0] != '{' || piece[^1] != '}') return false;
        return int.TryParse(piece[1..^1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index);
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

            case InvocationExpressionSyntax invoc when IsStringFormatCall(invoc):
                return FlattenStringFormat(invoc, locals, pieces, slots, visited, out unsupportedReason);

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

    private static bool FlattenStringFormat(
        InvocationExpressionSyntax invoc,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        List<string> pieces,
        List<SlotEntry> slots,
        HashSet<string> visited,
        out string unsupportedReason)
    {
        unsupportedReason = "";
        var args = invoc.ArgumentList.Arguments;
        if (args.Count < 1)
        {
            unsupportedReason = "string.Format with no arguments";
            return false;
        }
        var fmtExpr = args[0].Expression;
        if (fmtExpr is not LiteralExpressionSyntax fmtLit || !fmtLit.IsKind(SyntaxKind.StringLiteralExpression))
        {
            unsupportedReason = $"string.Format format string is not a literal: {fmtExpr.Kind()}";
            return false;
        }
        var format = fmtLit.Token.ValueText;

        // Walk the format string. Each `{N}` (with optional `,W` or `:fmt`) substitutes args[N+1].
        var i = 0;
        var literalSb = new StringBuilder();
        while (i < format.Length)
        {
            var c = format[i];
            if (c == '{' && i + 1 < format.Length && format[i + 1] == '{')
            {
                literalSb.Append('{');
                i += 2;
                continue;
            }
            if (c == '}' && i + 1 < format.Length && format[i + 1] == '}')
            {
                literalSb.Append('}');
                i += 2;
                continue;
            }
            if (c == '{')
            {
                var close = format.IndexOf('}', i);
                if (close < 0)
                {
                    unsupportedReason = $"string.Format format string has unclosed '{{': {format}";
                    return false;
                }
                var holderBody = format.Substring(i + 1, close - i - 1);
                var sepIdx = holderBody.IndexOfAny(new[] { ',', ':' });
                var indexStr = sepIdx < 0 ? holderBody : holderBody[..sepIdx];
                if (!int.TryParse(indexStr, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var argIdx))
                {
                    unsupportedReason = $"string.Format placeholder index is not an integer: {{{holderBody}}}";
                    return false;
                }
                if (argIdx + 1 >= args.Count)
                {
                    unsupportedReason = $"string.Format placeholder {{{argIdx}}} has no matching argument";
                    return false;
                }
                if (literalSb.Length > 0)
                {
                    pieces.Add(literalSb.ToString());
                    literalSb.Clear();
                }
                if (!FlattenFormatArgument(args[argIdx + 1].Expression, locals, pieces, slots, visited))
                {
                    return false;
                }
                i = close + 1;
                continue;
            }
            literalSb.Append(c);
            i++;
        }
        if (literalSb.Length > 0) pieces.Add(literalSb.ToString());
        return true;
    }

    private static bool FlattenFormatArgument(
        ExpressionSyntax argExpr,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        List<string> pieces,
        List<SlotEntry> slots,
        HashSet<string> visited)
    {
        // Try the structural concat/literal/local resolver first; if it fails, fall back to a
        // single helper-call slot so the format isn't aborted by one un-resolvable argument.
        var piecesSnapshot = pieces.Count;
        var slotsSnapshot = slots.Count;
        if (FlattenConcat(argExpr, locals, pieces, slots, visited, out _))
        {
            return true;
        }
        pieces.RemoveRange(piecesSnapshot, pieces.Count - piecesSnapshot);
        slots.RemoveRange(slotsSnapshot, slots.Count - slotsSnapshot);
        AddSlot(slots, ClassifyHelperCallRaw(argExpr), type: ClassifyHelperCallSlotType(argExpr));
        pieces.Add($"{{{slots.Count - 1}}}");
        return true;
    }

    private static string ClassifyHelperCallRaw(ExpressionSyntax expr)
    {
        // Strip whitespace/newlines so multi-line helper invocations produce stable raw strings.
        return string.Join(" ", expr.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
    }

    private static string ClassifyHelperCallSlotType(ExpressionSyntax expr)
    {
        // Helper-call slot type for invocations from the known game-helper namespaces, format-arg
        // for everything else (literals already resolved by FlattenConcat, so this hits unknown
        // identifiers or expressions).
        if (expr is InvocationExpressionSyntax invoc)
        {
            if (invoc.Expression is MemberAccessExpressionSyntax m)
            {
                var receiver = m.Expression.ToString();
                if (receiver is "Grammar" or "QudHistoryHelpers" or "Faction" or "NameMaker") return "helper-call";
            }
        }
        return "format-arg";
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
        var sb = new StringBuilder("^");
        var i = 0;
        while (i < sample.Length)
        {
            if (sample[i] == '{')
            {
                var close = sample.IndexOf('}', i);
                if (close > i && TryGetSlotIndex(sample.AsSpan(i, close - i + 1), out var slotIndex))
                {
                    sb.Append("(.+?)");
                    i = close + 1;
                    if ((uint)slotIndex < (uint)slots.Count
                        && slots[slotIndex].Type == HseExpansionType
                        && HseExpansions.TryGetValue(slots[slotIndex].Raw, out var expansion)
                        && sample.AsSpan(i).StartsWith(expansion.SampleSuffix))
                    {
                        sb.Append(expansion.RegexSuffix);
                        i += expansion.SampleSuffix.Length;
                    }
                    continue;
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
