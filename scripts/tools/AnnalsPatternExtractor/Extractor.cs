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
        var appendsByLocal = CollectCompoundAppends(generateMethod);

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
            var switchSection = invocation.Ancestors().OfType<SwitchSectionSyntax>().FirstOrDefault();
            var switchLabel = switchSection is null ? "default" : (ExtractSwitchLabel(switchSection) ?? "default");
            var idPrefix = switchSection is null
                ? $"{className}#{eventProperty}"
                : $"{className}#{eventProperty}#case:{switchLabel}";
            var ifBranchSuffix = ResolveIfBranchSuffix(invocation, setterCalls, eventProperty);
            if (ifBranchSuffix is not null) idPrefix += $"#if:{ifBranchSuffix}";

            var setterLocals = BuildSetterScopedLocals(invocation, localInitializers);
            var armings = ExpandSwitchExpressionArms(valueExpr, setterLocals);
            foreach (var (armLabel, armLocals) in armings)
            {
                var armSwitchCase = armLabel is null ? switchLabel : armLabel;
                var armId = armLabel is null ? idPrefix : $"{idPrefix}#arm:{armLabel}";

                var setterScopedAppends = FilterAppendsBeforeSetter(appendsByLocal, invocation);
                var optExpansions = ExpandOptionalAppends(valueExpr, armLocals, FilterAppendsToLocals(armLocals, setterScopedAppends));
                foreach (var (optLabel, optLocals) in optExpansions)
                {
                    var optId = optLabel is null ? armId : $"{armId}#opt:{optLabel}";
                    var resolution = ResolveValueExpression(valueExpr, optLocals);
                    if (!resolution.Resolved)
                    {
                        candidates.Add(NeedsManual(
                            id: optId,
                            sourceFile: fileName,
                            annalClass: className,
                            switchCase: armSwitchCase,
                            eventProperty: eventProperty,
                            reason: resolution.Reason));
                        continue;
                    }

                    candidates.Add(BuildCandidate(
                        id: optId,
                        sourceFile: fileName,
                        annalClass: className,
                        switchCase: armSwitchCase,
                        eventProperty: eventProperty,
                        resolved: resolution));
                }
            }
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

    /// <summary>
    /// When two SetEventProperty calls for the same event property appear in distinct branches of
    /// the same `if/else` statement, return a `then`/`else`/`elseN` suffix so the extractor can
    /// emit one candidate per branch (each with its own shape). If only one setter for the event
    /// exists in this if (i.e., a `tombInscription` fallback like `If.Chance(80) tomb=long; else
    /// tomb=short;`), still suffix to keep the runtime variants distinct.
    /// </summary>
    private static string? ResolveIfBranchSuffix(
        InvocationExpressionSyntax setter,
        List<InvocationExpressionSyntax> allSetters,
        string eventProperty)
    {
        var ifStmt = setter.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
        if (ifStmt is null) return null;
        // Only suffix when at least one other setter for this same event property exists in a
        // distinct branch of the *same* if statement.
        var siblingsInSameIf = allSetters.Where(other =>
        {
            if (other == setter) return false;
            if (ParseSetterArgs(other).property != eventProperty) return false;
            var otherIf = other.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
            return otherIf == ifStmt;
        }).ToList();
        if (siblingsInSameIf.Count == 0) return null;

        // Determine which branch this setter lives in.
        if (IsInThenBranch(setter, ifStmt)) return "then";
        if (IsInElseBranch(setter, ifStmt)) return "else";
        return null;
    }

    private static bool IsInThenBranch(SyntaxNode node, IfStatementSyntax ifStmt)
    {
        return ifStmt.Statement.Span.Contains(node.Span);
    }

    private static bool IsInElseBranch(SyntaxNode node, IfStatementSyntax ifStmt)
    {
        return ifStmt.Else is not null && ifStmt.Else.Statement.Span.Contains(node.Span);
    }

    /// <summary>
    /// Add per-setter overrides for locals that were excluded from method-scope locals because of
    /// SimpleAssignmentExpression reassignment, when a flow-sensitive lookup at the setter's
    /// location identifies a single dominating assignment (the most recent assignment within the
    /// nearest enclosing block, e.g. a switch case body or an if branch). This recovers `value`
    /// in patterns like `string value; if (cond) { value = ...; SetEventProperty(_, value); }`.
    /// </summary>
    private static IReadOnlyDictionary<string, ExpressionSyntax> BuildSetterScopedLocals(
        InvocationExpressionSyntax setter,
        IReadOnlyDictionary<string, ExpressionSyntax> methodLocals)
    {
        var overrides = new Dictionary<string, ExpressionSyntax>(methodLocals, StringComparer.Ordinal);
        var alreadyOverridden = new HashSet<string>(StringComparer.Ordinal);
        // Walk enclosing statement-containers (BlockSyntax or SwitchSectionSyntax) from innermost
        // to outermost; within each container, scan preceding sibling statements for simple
        // assignments to names that aren't already in the method-scope locals table.
        SyntaxNode? cursor = setter;
        while (cursor?.Parent is not null)
        {
            cursor = cursor.Parent;
            IEnumerable<StatementSyntax>? statements = cursor switch
            {
                BlockSyntax b => b.Statements,
                SwitchSectionSyntax s => s.Statements,
                _ => null,
            };
            if (statements is null) continue;
            foreach (var stmt in statements)
            {
                if (stmt.SpanStart >= setter.SpanStart) break;
                // Skip stmts that *contain* the setter — they're an ancestor of the setter and
                // are handled at deeper cursor levels. Their descendants include assignments
                // that lexically follow the setter (e.g. inside the same if-branch), which must
                // not influence the setter's value resolution.
                if (stmt.Span.Contains(setter.Span)) continue;
                foreach (var assign in stmt.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>())
                {
                    if (!assign.IsKind(SyntaxKind.SimpleAssignmentExpression)) continue;
                    if (assign.Left is not IdentifierNameSyntax lhs) continue;
                    var name = lhs.Identifier.ValueText;
                    if (alreadyOverridden.Contains(name)) continue;
                    if (methodLocals.ContainsKey(name)) continue;
                    overrides[name] = assign.Right;
                    alreadyOverridden.Add(name);
                }
            }
        }
        return overrides;
    }

    /// <summary>
    /// Drop compound-append rhs entries whose owning append statement starts at or after the
    /// setter's span. Appends that execute after the setter never influence the value the runtime
    /// stores, so they must not be fanned out as `#opt:withN` candidates.
    /// </summary>
    private static IReadOnlyDictionary<string, List<ExpressionSyntax>> FilterAppendsBeforeSetter(
        IReadOnlyDictionary<string, List<ExpressionSyntax>> appendsByLocal,
        InvocationExpressionSyntax setter)
    {
        var dict = new Dictionary<string, List<ExpressionSyntax>>(StringComparer.Ordinal);
        foreach (var (name, appends) in appendsByLocal)
        {
            var kept = new List<ExpressionSyntax>();
            foreach (var rhs in appends)
            {
                // The rhs is the right-hand side of `local += rhs`; walk up to the owning
                // assignment expression (its parent) and use its span as the append's span.
                var assignSpanStart = rhs.Parent?.SpanStart ?? rhs.SpanStart;
                if (assignSpanStart < setter.SpanStart) kept.Add(rhs);
            }
            if (kept.Count > 0) dict[name] = kept;
        }
        return dict;
    }

    private static IReadOnlyDictionary<string, List<ExpressionSyntax>> FilterAppendsToLocals(
        IReadOnlyDictionary<string, ExpressionSyntax> activeLocals,
        IReadOnlyDictionary<string, List<ExpressionSyntax>> appendsByLocal)
    {
        var dict = new Dictionary<string, List<ExpressionSyntax>>(StringComparer.Ordinal);
        foreach (var (name, appends) in appendsByLocal)
        {
            if (activeLocals.ContainsKey(name)) dict[name] = appends;
        }
        return dict;
    }

    /// <summary>
    /// Collect compound `+=` reassignments (`local += expr`) per local. These typically come from
    /// `if (flag) local += suffix;` blocks and represent optional-append branches we want to fan
    /// out as additional candidates.
    /// </summary>
    private static IReadOnlyDictionary<string, List<ExpressionSyntax>> CollectCompoundAppends(MethodDeclarationSyntax method)
    {
        var dict = new Dictionary<string, List<ExpressionSyntax>>(StringComparer.Ordinal);
        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assignment.IsKind(SyntaxKind.AddAssignmentExpression)) continue;
            if (assignment.Left is not IdentifierNameSyntax lhs) continue;
            var name = lhs.Identifier.ValueText;
            if (!dict.TryGetValue(name, out var list))
            {
                list = new List<ExpressionSyntax>();
                dict[name] = list;
            }
            list.Add(assignment.Right);
        }
        return dict;
    }

    /// <summary>
    /// If the value expression depends on a local that has compound `+=` appends, fan out one
    /// (label="base", initializer-only) entry plus one (label="withN", initializer + N-th append)
    /// entry per append. Otherwise returns a single (label=null, original-locals) pair.
    /// Only the first such local found is expanded; nested cross-products are not emitted (target
    /// files have at most one optionally-appended local in the value-expression tree).
    /// </summary>
    private static List<(string? optLabel, IReadOnlyDictionary<string, ExpressionSyntax> locals)>
        ExpandOptionalAppends(
            ExpressionSyntax valueExpr,
            IReadOnlyDictionary<string, ExpressionSyntax> locals,
            IReadOnlyDictionary<string, List<ExpressionSyntax>> appendsByLocal)
    {
        var result = new List<(string?, IReadOnlyDictionary<string, ExpressionSyntax>)>();
        if (appendsByLocal.Count == 0)
        {
            result.Add((null, locals));
            return result;
        }
        var (appendLocalName, appends) = FindAppendableLocal(valueExpr, locals, appendsByLocal, new HashSet<string>(StringComparer.Ordinal));
        if (appendLocalName is null || appends is null)
        {
            result.Add((null, locals));
            return result;
        }
        if (!locals.TryGetValue(appendLocalName, out var baseInit))
        {
            result.Add((null, locals));
            return result;
        }

        result.Add(("base", BuildAppendOverride(locals, appendLocalName, baseInit)));
        for (var i = 0; i < appends.Count; i++)
        {
            var combined = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.BinaryExpression(
                SyntaxKind.AddExpression, baseInit, appends[i]);
            result.Add(($"with{i + 1}", BuildAppendOverride(locals, appendLocalName, combined)));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, ExpressionSyntax> BuildAppendOverride(
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        string name,
        ExpressionSyntax newInit)
    {
        var copy = new Dictionary<string, ExpressionSyntax>(locals, StringComparer.Ordinal)
        {
            [name] = newInit,
        };
        return copy;
    }

    private static (string? name, List<ExpressionSyntax>? appends) FindAppendableLocal(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        IReadOnlyDictionary<string, List<ExpressionSyntax>> appendsByLocal,
        HashSet<string> visited)
    {
        foreach (var id in expr.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var name = id.Identifier.ValueText;
            if (visited.Contains(name)) continue;
            if (appendsByLocal.TryGetValue(name, out var appends) && appends.Count > 0
                && locals.ContainsKey(name))
            {
                return (name, appends);
            }
            if (locals.TryGetValue(name, out var init))
            {
                visited.Add(name);
                var nested = FindAppendableLocal(init, locals, appendsByLocal, visited);
                if (nested.name is not null) return nested;
            }
        }
        return (null, null);
    }

    /// <summary>
    /// If the value expression depends on a local whose initializer is a SwitchExpressionSyntax,
    /// fan out one entry per arm (the local is overridden with the arm's expression). Otherwise
    /// returns a single (label=null, original-locals) pair so callers can use a single code path.
    /// Only the first switch-expression local found is expanded; nested cross-products are not
    /// emitted by design (PR2a target files use one switch expression at a time).
    /// </summary>
    private static List<(string? armLabel, IReadOnlyDictionary<string, ExpressionSyntax> locals)>
        ExpandSwitchExpressionArms(
            ExpressionSyntax valueExpr,
            IReadOnlyDictionary<string, ExpressionSyntax> locals)
    {
        var result = new List<(string?, IReadOnlyDictionary<string, ExpressionSyntax>)>();
        var (switchLocalName, switchExpr) = FindSwitchExpressionLocal(valueExpr, locals, new HashSet<string>(StringComparer.Ordinal));
        if (switchLocalName is null || switchExpr is null)
        {
            result.Add((null, locals));
            return result;
        }
        foreach (var arm in switchExpr.Arms)
        {
            var label = SwitchExpressionArmLabel(arm);
            var overrideLocals = new Dictionary<string, ExpressionSyntax>(locals, StringComparer.Ordinal)
            {
                [switchLocalName] = arm.Expression,
            };
            result.Add((label, overrideLocals));
        }
        return result;
    }

    private static (string? name, SwitchExpressionSyntax? expr) FindSwitchExpressionLocal(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        HashSet<string> visited)
    {
        foreach (var id in expr.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var name = id.Identifier.ValueText;
            if (visited.Contains(name)) continue;
            if (!locals.TryGetValue(name, out var init)) continue;
            if (init is SwitchExpressionSyntax sw) return (name, sw);
            visited.Add(name);
            var nested = FindSwitchExpressionLocal(init, locals, visited);
            if (nested.name is not null) return nested;
        }
        return (null, null);
    }

    private static string SwitchExpressionArmLabel(SwitchExpressionArmSyntax arm)
    {
        return arm.Pattern switch
        {
            ConstantPatternSyntax c => c.Expression.ToString(),
            DiscardPatternSyntax => "_",
            _ => arm.Pattern.ToString(),
        };
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
        // Strategy: scan the joined character stream, treating each existing slot reference
        // (`{N}`) as a single atomic "metachar" so cross-piece HistoryKit tokens
        // (e.g. `<spice.elements.{0}.babeTrait.!random>` formed by `"<...." + local + "....>"`
        // get collapsed into a single `historykit-token` slot.
        var oldSlots = new List<SlotEntry>(slots);
        var streamItems = new List<StreamItem>();
        foreach (var piece in pieces)
        {
            ParsePieceIntoStream(piece, oldSlots, streamItems);
        }

        slots.Clear();
        pieces.Clear();

        // Two-pass: scan streamItems for `<...>` runs; emit slots/pieces per run.
        var i = 0;
        var sb = new StringBuilder();
        while (i < streamItems.Count)
        {
            var item = streamItems[i];
            if (item.Kind == StreamItemKind.Literal && item.Char == '<')
            {
                // Find matching '>' in subsequent items
                var close = -1;
                for (var j = i + 1; j < streamItems.Count; j++)
                {
                    if (streamItems[j].Kind == StreamItemKind.Literal && streamItems[j].Char == '>')
                    {
                        close = j;
                        break;
                    }
                }
                if (close >= 0)
                {
                    if (sb.Length > 0)
                    {
                        pieces.Add(sb.ToString());
                        sb.Clear();
                    }
                    var (rawSpan, isAssignment) = BuildTokenRaw(streamItems, i, close, oldSlots);
                    if (!isAssignment)
                    {
                        AddSlot(slots, rawSpan, type: HistoryKitTokenType);
                        pieces.Add($"{{{slots.Count - 1}}}");
                    }
                    i = close + 1;
                    continue;
                }
            }
            if (item.Kind == StreamItemKind.SlotRef)
            {
                if (sb.Length > 0)
                {
                    pieces.Add(sb.ToString());
                    sb.Clear();
                }
                var slot = oldSlots[item.SlotIndex];
                slot.Index = slots.Count;
                slot.Default = $"{{t{slots.Count}}}";
                slots.Add(slot);
                pieces.Add($"{{{slot.Index}}}");
                i++;
                continue;
            }
            sb.Append(item.Char);
            i++;
        }
        if (sb.Length > 0) pieces.Add(sb.ToString());
    }

    private enum StreamItemKind { Literal, SlotRef }

    private readonly struct StreamItem
    {
        public StreamItemKind Kind { get; init; }
        public char Char { get; init; }
        public int SlotIndex { get; init; }
    }

    private static void ParsePieceIntoStream(string piece, List<SlotEntry> slots, List<StreamItem> items)
    {
        var i = 0;
        while (i < piece.Length)
        {
            if (piece[i] == '{')
            {
                var close = piece.IndexOf('}', i);
                if (close > i
                    && int.TryParse(piece.AsSpan(i + 1, close - i - 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var idx)
                    && (uint)idx < (uint)slots.Count)
                {
                    items.Add(new StreamItem { Kind = StreamItemKind.SlotRef, SlotIndex = idx });
                    i = close + 1;
                    continue;
                }
            }
            items.Add(new StreamItem { Kind = StreamItemKind.Literal, Char = piece[i] });
            i++;
        }
    }

    private static (string raw, bool isAssignment) BuildTokenRaw(
        List<StreamItem> stream,
        int openIdx,
        int closeIdx,
        List<SlotEntry> oldSlots)
    {
        var sb = new StringBuilder();
        var hasEquals = false;
        for (var k = openIdx; k <= closeIdx; k++)
        {
            var item = stream[k];
            if (item.Kind == StreamItemKind.Literal)
            {
                sb.Append(item.Char);
                if (k > openIdx + 1 && item.Char == '=') hasEquals = true;
            }
            else
            {
                sb.Append('{').Append(oldSlots[item.SlotIndex].Raw).Append('}');
            }
        }
        var raw = sb.ToString();
        var isAssignment = raw.Length > 3 && raw[1] == '$' && hasEquals;
        return (raw, isAssignment);
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

            case InvocationExpressionSyntax invoc when IsExpandStringWrapper(invoc) && IsTokenShapedExpression(invoc.ArgumentList.Arguments[0].Expression, locals):
            {
                AddSlot(slots, RenderTokenRaw(invoc.ArgumentList.Arguments[0].Expression), type: HistoryKitTokenType);
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;
            }

            case InvocationExpressionSyntax invoc when IsPatternPreservingWrapper(invoc):
                return FlattenConcat(invoc.ArgumentList.Arguments[0].Expression, locals, pieces, slots, visited, out unsupportedReason);

            case InvocationExpressionSyntax invoc when IsEntityGetProperty(invoc):
                AddSlot(slots, $"entity.GetProperty({GetFirstStringArg(invoc)})", type: "entity-property");
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;

            case InvocationExpressionSyntax invoc when IsRandomCall(invoc):
                AddSlot(slots, "Random(...)", type: "string-format-arg");
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;

            case InvocationExpressionSyntax invoc:
                AddSlot(slots, ClassifyHelperCallRaw(invoc), type: ClassifyHelperCallSlotType(invoc));
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

    /// <summary>
    /// Render a HistoryKit-token-shaped expression's raw text by concatenating its leaf literals
    /// (unquoted) and any non-literal sub-expressions wrapped in `{...}` placeholders. This keeps
    /// the raw stable across re-runs and avoids embedding C# string literal quotes.
    /// </summary>
    private static string RenderTokenRaw(ExpressionSyntax expr)
    {
        var sb = new StringBuilder();
        AppendTokenRaw(expr, sb);
        return sb.ToString();
    }

    private static void AppendTokenRaw(ExpressionSyntax expr, StringBuilder sb)
    {
        switch (expr)
        {
            case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                sb.Append(lit.Token.ValueText);
                break;
            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression):
                AppendTokenRaw(bin.Left, sb);
                AppendTokenRaw(bin.Right, sb);
                break;
            default:
                sb.Append('{').Append(ClassifyHelperCallRaw(expr)).Append('}');
                break;
        }
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

    // True when the wrapper invocation is `ExpandString(...)` (free function or member).
    private static bool IsExpandStringWrapper(InvocationExpressionSyntax invoc)
    {
        if (invoc.ArgumentList.Arguments.Count < 1) return false;
        if (invoc.Expression is IdentifierNameSyntax bareId && bareId.Identifier.ValueText == "ExpandString") return true;
        if (invoc.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.ValueText == "ExpandString") return true;
        return false;
    }

    /// <summary>
    /// True when the expression is a concat that begins with a `<` literal and ends with a `>`
    /// literal — a HistoryKit token spanning multiple pieces. The runtime ExpandString collapses
    /// the whole assembled string into a single arbitrary-text expansion, so we model it as a
    /// single `historykit-token` slot rather than letting the inner structure leak through as
    /// brittle literal-and-slot fragments.
    /// </summary>
    private static bool IsTokenShapedExpression(ExpressionSyntax expr, IReadOnlyDictionary<string, ExpressionSyntax> locals)
    {
        var leftmost = LeftmostLiteral(expr, locals, new HashSet<string>(StringComparer.Ordinal));
        var rightmost = RightmostLiteral(expr, locals, new HashSet<string>(StringComparer.Ordinal));
        if (leftmost is null || rightmost is null) return false;
        return leftmost.Length > 0 && leftmost[0] == '<' && rightmost.Length > 0 && rightmost[^1] == '>';
    }

    private static string? LeftmostLiteral(ExpressionSyntax expr, IReadOnlyDictionary<string, ExpressionSyntax> locals, HashSet<string> visited)
    {
        return expr switch
        {
            LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression) => lit.Token.ValueText,
            BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression) => LeftmostLiteral(bin.Left, locals, visited),
            IdentifierNameSyntax id when locals.TryGetValue(id.Identifier.ValueText, out var init) && visited.Add(id.Identifier.ValueText)
                => LeftmostLiteral(init, locals, visited),
            _ => null,
        };
    }

    private static string? RightmostLiteral(ExpressionSyntax expr, IReadOnlyDictionary<string, ExpressionSyntax> locals, HashSet<string> visited)
    {
        return expr switch
        {
            LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression) => lit.Token.ValueText,
            BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression) => RightmostLiteral(bin.Right, locals, visited),
            IdentifierNameSyntax id when locals.TryGetValue(id.Identifier.ValueText, out var init) && visited.Add(id.Identifier.ValueText)
                => RightmostLiteral(init, locals, visited),
            _ => null,
        };
    }

    // Wrappers whose return value matches their first argument modulo capitalization /
    // HSE expansion — both invisible to a runtime regex match. Unwrapping lets us see
    // the inner pattern (e.g. `Grammar.InitCap(string.Format(...))`).
    private static bool IsPatternPreservingWrapper(InvocationExpressionSyntax invoc)
    {
        if (invoc.ArgumentList.Arguments.Count < 1) return false;
        if (invoc.Expression is IdentifierNameSyntax bareId && bareId.Identifier.ValueText == "ExpandString") return true;
        if (invoc.Expression is MemberAccessExpressionSyntax m)
        {
            var receiver = m.Expression.ToString();
            var name = m.Name.Identifier.ValueText;
            if (name == "ExpandString") return true;
            if (receiver == "Grammar")
            {
                return name is "InitCap" or "InitialCap" or "MakeTitleCase" or "MakeTitleCaseWithArticle";
            }
        }
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
