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

internal static class CandidateIdSuffix
{
    public const string Case = "#case:";
    public const string Arm = "#arm:";
    public const string Opt = "#opt:";
    public const string If = "#if:";
}

internal sealed class Extractor
{
    private static readonly HashSet<string> KnownHelperReceivers =
        new(StringComparer.Ordinal) { "Grammar", "QudHistoryHelpers", "Faction", "NameMaker" };

    private static readonly char[] FormatAlignmentSeparators = { ',', ':' };

    // Sentinels that survive ParsePieceIntoStream / BuildAnchoredRegex without colliding with the
    // slot-ref braces (`{N}`) that the same passes use as control characters. FlattenStringFormat
    // emits these in place of `{`/`}` produced by `{{`/`}}` escapes; the regex builder translates
    // them to `\{`/`\}` and BuildCandidate restores them to literal braces in the human-facing
    // sample_source. Using PUA codepoints keeps them unambiguous and well outside any source text.
    private const char LiteralBraceOpenSentinel = '';
    private const char LiteralBraceCloseSentinel = '';

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
                : $"{className}#{eventProperty}{CandidateIdSuffix.Case}{switchLabel}";
            var ifBranchSuffix = ResolveIfBranchSuffix(invocation, setterCalls, eventProperty);
            if (ifBranchSuffix is not null) idPrefix += CandidateIdSuffix.If + ifBranchSuffix;

            // BuildSetterScopedLocals returns one entry when the local resolves unconditionally
            // (IfBranchLabel = null) and two entries (`then`/`else`) when a setter-scope local
            // has branch-distinct SimpleAssignments in a pre-setter `if/else` sibling. The
            // branch-fanout case is mutually exclusive with `ResolveIfBranchSuffix` (setter
            // INSIDE an if-branch with a sibling setter for the same property in the other
            // branch); when that fires, we suppress branch-fanout to avoid `#if:then#if:else`
            // double-suffixing.
            var suppressBranchFanout = ifBranchSuffix is not null;
            var setterLocalsBranches = BuildSetterScopedLocals(invocation, localInitializers, generateMethod, suppressBranchFanout);
            var setterScopedAppends = FilterAppendsBeforeSetter(appendsByLocal, invocation);
            foreach (var (branchLabel, setterLocals) in setterLocalsBranches)
            {
                var branchedIdPrefix = branchLabel is null ? idPrefix : idPrefix + CandidateIdSuffix.If + branchLabel;
                var armings = ExpandSwitchExpressionArms(valueExpr, setterLocals);
                foreach (var (armLabel, armLocals) in armings)
                {
                    var armSwitchCase = armLabel is null ? switchLabel : armLabel;
                    var armId = armLabel is null ? branchedIdPrefix : branchedIdPrefix + CandidateIdSuffix.Arm + armLabel;

                    var optExpansions = ExpandOptionalAppends(valueExpr, armLocals, FilterAppendsToLocals(armLocals, setterScopedAppends));
                    foreach (var (optLabel, optLocals) in optExpansions)
                    {
                        var optId = optLabel is null ? armId : armId + CandidateIdSuffix.Opt + optLabel;
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
            if (m.Expression is IdentifierNameSyntax id
                && (id.Identifier.ValueText == "string" || id.Identifier.ValueText == "String"))
            {
                return true;
            }
            // Fully-qualified `System.String.Format(...)`.
            if (m.Expression is MemberAccessExpressionSyntax inner
                && inner.Name.Identifier.ValueText == "String"
                && inner.Expression is IdentifierNameSyntax sys
                && sys.Identifier.ValueText == "System")
            {
                return true;
            }
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
    /// <remarks>
    /// CR R8: For `else if` chains, the C# parser models `else if (...) { ... }` as the outer
    /// if's `Else.Statement` being a NESTED `IfStatementSyntax`. `Ancestors().OfType&lt;
    /// IfStatementSyntax&gt;().FirstOrDefault()` therefore returns the INNER if for setters in
    /// the inner branch, and the OUTER if for setters in the outer-then branch — siblings in
    /// the same chain stop matching, and no `#if:` suffix is emitted, leading to id collision
    /// at dedupe time. We normalize the resolved if to the OUTERMOST `IfStatementSyntax` in the
    /// chain (walking up `Parent.Parent` through `ElseClauseSyntax` → `IfStatementSyntax` links)
    /// for both the sibling check and the then/else branch test.
    ///
    /// Limitation: 3+ branch chains (e.g. `if A else if B else C`) collapse the trailing `else`
    /// into the outermost-if's else and would label B as `else` (because B lives in the
    /// outer-else span) and C also as `else`, causing collision. PR1/PR2a target files do not
    /// contain 3+ branch chains, so the normalize-to-outermost approach is sufficient for the
    /// current scope; a richer per-arm labelling is deferred.
    /// </remarks>
    private static string? ResolveIfBranchSuffix(
        InvocationExpressionSyntax setter,
        List<InvocationExpressionSyntax> allSetters,
        string eventProperty)
    {
        var nearestIf = setter.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
        if (nearestIf is null) return null;
        var ifStmt = NormalizeToChainRoot(nearestIf);
        // Only suffix when at least one other setter for this same event property exists in a
        // distinct branch of the *same* if-chain (using the chain root for both sides).
        var siblingsInSameIf = allSetters.Where(other =>
        {
            if (other == setter) return false;
            if (ParseSetterArgs(other).property != eventProperty) return false;
            var otherNearest = other.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
            if (otherNearest is null) return false;
            return NormalizeToChainRoot(otherNearest) == ifStmt;
        }).ToList();
        if (siblingsInSameIf.Count == 0) return null;

        // Determine which branch this setter lives in (relative to the chain root).
        if (IsInThenBranch(setter, ifStmt)) return "then";
        if (IsInElseBranch(setter, ifStmt)) return "else";
        return null;
    }

    /// <summary>
    /// Walk up an else-if chain to the OUTERMOST `IfStatementSyntax`. C# models `else if` as
    /// `IfStatementSyntax.Else.Statement` being a nested `IfStatementSyntax`; this method
    /// follows the `Parent`/`Parent.Parent` chain until it reaches the chain root.
    /// </summary>
    private static IfStatementSyntax NormalizeToChainRoot(IfStatementSyntax ifStmt)
    {
        var current = ifStmt;
        while (current.Parent is ElseClauseSyntax elseClause
            && elseClause.Parent is IfStatementSyntax outer)
        {
            current = outer;
        }
        return current;
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
    /// <remarks>
    /// Returns one entry (label = null) for the unconditional case. When a pre-setter
    /// `IfStatementSyntax` sibling holds branch-distinct SimpleAssignments to a single live
    /// local, returns two entries (`then` / `else`) so the caller emits one candidate per
    /// branch. Decisions:
    /// <list type="bullet">
    ///   <item>
    ///     <b>Cross-product policy (PR2a)</b>: at most ONE branched local drives fanout. When
    ///     multiple if-stmt siblings or multiple locals would qualify, only the FIRST encountered
    ///     branched name (in reverse-sibling-then-DFS order) fans out; other locals fold into
    ///     the unconditional source-latest override.
    ///   </item>
    ///   <item>
    ///     <b>Single-branch-assign</b>: when only ONE branch assigns the local, the other branch
    ///     keeps the local's prior state — runtime-faithfully a distinct value. We still emit
    ///     two candidates; the unassigned branch resolves the local from the declared
    ///     initializer (when present) or falls back to the unconditional `overrides[name]`
    ///     (which today degrades to an `unresolved-local` slot).
    ///   </item>
    ///   <item>
    ///     <b>Identical-rhs in both branches</b>: not branch-distinct — folds unconditionally
    ///     via the existing source-latest reverse-iter rule.
    ///   </item>
    ///   <item>
    ///     <b>Mutual exclusion with `ResolveIfBranchSuffix`</b>: when the setter is INSIDE an
    ///     if-branch and a sibling setter exists in the other branch (driving an `#if:then` /
    ///     `#if:else` suffix), branch-fanout is suppressed by the caller via
    ///     <paramref name="suppressBranchFanout"/> so the id never carries two `#if:` segments.
    ///   </item>
    /// </list>
    /// </remarks>
    private static IReadOnlyList<(string? IfBranchLabel, IReadOnlyDictionary<string, ExpressionSyntax> Overrides)>
        BuildSetterScopedLocals(
            InvocationExpressionSyntax setter,
            IReadOnlyDictionary<string, ExpressionSyntax> methodLocals,
            MethodDeclarationSyntax method,
            bool suppressBranchFanout)
    {
        var overrides = new Dictionary<string, ExpressionSyntax>(methodLocals, StringComparer.Ordinal);
        var alreadyOverridden = new HashSet<string>(StringComparer.Ordinal);

        // First branched local found (innermost sibling, reverse source order). Once non-null,
        // further branched candidates are ignored — the spec limits fanout to a single local.
        string? branchedName = null;
        ExpressionSyntax? branchedThen = null;
        ExpressionSyntax? branchedElse = null;

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
            // Walk pre-setter siblings in REVERSE source order so the most-recent assignment
            // (closest to the setter) wins per local. With forward iteration the first-seen-wins
            // logic of `alreadyOverridden` would lock the EARLIEST assignment, which is the
            // opposite of the runtime semantic.
            foreach (var stmt in statements.Reverse())
            {
                // `continue` (not `break`) for post-setter stmts: in reverse order they appear
                // first, but earlier siblings still need to be visited.
                if (stmt.SpanStart >= setter.SpanStart) continue;
                // Skip stmts that *contain* the setter — they're an ancestor of the setter and
                // are handled at deeper cursor levels. Their descendants include assignments
                // that lexically follow the setter (e.g. inside the same if-branch), which must
                // not influence the setter's value resolution.
                if (stmt.Span.Contains(setter.Span)) continue;

                // Branched-local detection — only fires for top-level if-stmt siblings (not
                // nested ifs inside other stmts) and only for the FIRST qualifying name. A
                // `value = "post-if"` assignment closer to the setter shadows the if-stmt via
                // `alreadyOverridden`, matching today's source-latest-wins semantic.
                if (!suppressBranchFanout && branchedName is null && stmt is IfStatementSyntax ifStmt)
                {
                    var detected = TryDetectBranchedLocal(ifStmt, methodLocals, alreadyOverridden);
                    if (detected.Name is not null)
                    {
                        branchedName = detected.Name;
                        branchedThen = detected.ThenRhs;
                        branchedElse = detected.ElseRhs;
                        // Shield the branched name from the unconditional fold below: its
                        // value is owned by the per-branch override dicts.
                        alreadyOverridden.Add(detected.Name);
                    }
                }

                // Iterate descendants in reverse source order so multiple assignments to the
                // same local *within a single sibling stmt* (e.g. `if (cond) { x="a"; x="b"; }`)
                // yield first-seen-wins = source-last-wins. CR R8: also consider
                // VariableDeclaratorSyntax with non-null Initializer — a `string value = "base"`
                // sibling holds the local's declared value as `Initializer.Value`, NOT as a
                // SimpleAssignmentExpression, so without this an excluded-from-method-locals
                // local (one reassigned later) would resolve to `unresolved-local` even when
                // the declared initializer is the only pre-setter source. Both kinds are
                // unified by SpanStart so source-latest still wins via `alreadyOverridden`.
                var contributors = new List<(string Name, ExpressionSyntax Right, int SpanStart)>();
                foreach (var assign in stmt.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>())
                {
                    if (!assign.IsKind(SyntaxKind.SimpleAssignmentExpression)) continue;
                    if (assign.Left is not IdentifierNameSyntax lhs) continue;
                    contributors.Add((lhs.Identifier.ValueText, assign.Right, assign.SpanStart));
                }
                foreach (var declarator in stmt.DescendantNodesAndSelf().OfType<VariableDeclaratorSyntax>())
                {
                    if (declarator.Initializer?.Value is not { } init) continue;
                    contributors.Add((declarator.Identifier.ValueText, init, declarator.SpanStart));
                }
                foreach (var (name, right, _) in contributors.OrderByDescending(c => c.SpanStart))
                {
                    if (alreadyOverridden.Contains(name)) continue;
                    if (methodLocals.ContainsKey(name)) continue;
                    overrides[name] = right;
                    alreadyOverridden.Add(name);
                }
            }
        }

        if (branchedName is null)
        {
            return new[] { ((string?)null, (IReadOnlyDictionary<string, ExpressionSyntax>)overrides) };
        }

        // Resolve missing branches via the local's declared initializer. When neither branch
        // assigns AND no initializer exists, fall back to the unconditional override (whatever
        // outer-scope assignment happened to land in `overrides[name]` — typically nothing,
        // leaving the name to degrade to an `unresolved-local` slot at FlattenConcat time).
        var initializerFallback = LookupDeclaredInitializer(branchedName, method);
        var thenValue = branchedThen ?? initializerFallback;
        var elseValue = branchedElse ?? initializerFallback;

        var thenOverrides = new Dictionary<string, ExpressionSyntax>(overrides, StringComparer.Ordinal);
        if (thenValue is not null) thenOverrides[branchedName] = thenValue;
        var elseOverrides = new Dictionary<string, ExpressionSyntax>(overrides, StringComparer.Ordinal);
        if (elseValue is not null) elseOverrides[branchedName] = elseValue;

        return new[]
        {
            ((string?)"then", (IReadOnlyDictionary<string, ExpressionSyntax>)thenOverrides),
            ((string?)"else", (IReadOnlyDictionary<string, ExpressionSyntax>)elseOverrides),
        };
    }

    /// <summary>
    /// Inspect the THEN/ELSE branches of <paramref name="ifStmt"/> for SimpleAssignments to live
    /// locals (names not in <paramref name="methodLocals"/> and not already overridden by a
    /// closer-to-setter assignment) and return the first qualifying branched local. Returns
    /// (null, null, null) when no branch-distinct local is present.
    /// </summary>
    /// <remarks>
    /// Per-branch picks the SOURCE-LATEST assignment (mirrors the within-branch reverse-iter
    /// rule). A name qualifies as "branched" when:
    /// <list type="bullet">
    ///   <item>Both branches assign it with DIFFERENT rhs nodes; or</item>
    ///   <item>Only ONE branch assigns it — the other branch retains the local's prior state,
    ///   which is runtime-faithfully a distinct value worth a separate candidate.</item>
    /// </list>
    /// Search order is the if-stmt's source-DFS assignment order so the FIRST qualifying name
    /// wins deterministically.
    /// </remarks>
    private static (string? Name, ExpressionSyntax? ThenRhs, ExpressionSyntax? ElseRhs) TryDetectBranchedLocal(
        IfStatementSyntax ifStmt,
        IReadOnlyDictionary<string, ExpressionSyntax> methodLocals,
        HashSet<string> alreadyOverridden)
    {
        // ELSE may be null (`if (cond) { ... }`). An empty else map drives the single-branch
        // case: any THEN-branch assignment qualifies as branched (else-branch keeps prior state).
        var thenAssigns = CollectBranchAssignments(ifStmt.Statement);
        var elseAssigns = ifStmt.Else is null
            ? new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal)
            : CollectBranchAssignments(ifStmt.Else.Statement);

        // Walk if-stmt-internal SimpleAssignments in source order; first qualifying name wins.
        // Skip post-setter assigns? Not applicable — caller filters via stmt.Span checks.
        foreach (var assign in ifStmt.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assign.IsKind(SyntaxKind.SimpleAssignmentExpression)) continue;
            if (assign.Left is not IdentifierNameSyntax lhs) continue;
            var name = lhs.Identifier.ValueText;
            if (methodLocals.ContainsKey(name)) continue;
            if (alreadyOverridden.Contains(name)) continue;

            thenAssigns.TryGetValue(name, out var t);
            elseAssigns.TryGetValue(name, out var e);
            if (t is null && e is null) continue;
            // Both branches assigning IDENTICAL rhs → fold unconditionally (existing reverse-
            // iter logic handles it). Only branch-distinct rhs OR single-branch-assign qualifies.
            if (t is not null && e is not null && SyntaxFactory.AreEquivalent(t, e)) continue;
            return (name, t, e);
        }
        return (null, null, null);
    }

    /// <summary>
    /// Build a name → source-latest-rhs map for SimpleAssignments inside one branch's
    /// statement subtree. When the same local is assigned multiple times within the branch
    /// (e.g. `value = "first"; value = "second";`), the LATER assignment wins, matching the
    /// runtime semantic of "the last write before the branch ends is the observed value".
    /// </summary>
    private static Dictionary<string, ExpressionSyntax> CollectBranchAssignments(StatementSyntax branch)
    {
        var map = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        foreach (var assign in branch.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>())
        {
            if (!assign.IsKind(SyntaxKind.SimpleAssignmentExpression)) continue;
            if (assign.Left is not IdentifierNameSyntax lhs) continue;
            // DescendantNodesAndSelf yields source-document order, so a later assignment
            // overwrites an earlier one — exactly the source-latest-wins semantic we want.
            map[lhs.Identifier.ValueText] = assign.Right;
        }
        return map;
    }

    /// <summary>
    /// Look up the declared initializer for <paramref name="name"/> when the local has exactly
    /// one declarator with a non-null initializer in <paramref name="method"/>. Used to recover
    /// the "no-assign branch keeps prior state" value during branch fanout when only one branch
    /// assigns the local. Returns null when no usable initializer is found.
    /// </summary>
    private static ExpressionSyntax? LookupDeclaredInitializer(string name, MethodDeclarationSyntax method)
    {
        VariableDeclaratorSyntax? sole = null;
        foreach (var declarator in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Identifier.ValueText != name) continue;
            if (sole is not null) return null;  // multi-declarator — ambiguous
            sole = declarator;
        }
        return sole?.Initializer?.Value;
    }

    /// <summary>
    /// One `local += rhs` statement, tagged with whether its execution is gated by an enclosing
    /// `if`. Required (Optional=false) appends always run on the runtime path that reaches the
    /// setter, so they fold into the local's resolved value. Optional (Optional=true) appends
    /// drive `#opt:withN` fanout — one variant per optional, in source order, with all required
    /// appends still folded in.
    /// </summary>
    private readonly struct AppendEntry
    {
        public AppendEntry(ExpressionSyntax right, bool optional)
        {
            Right = right;
            Optional = optional;
        }

        public ExpressionSyntax Right { get; }
        public bool Optional { get; }
    }

    /// <summary>
    /// Drop compound-append entries whose owning append statement starts at or after the setter's
    /// span. Appends that execute after the setter never influence the value the runtime stores,
    /// so they must neither fold into the resolved value nor fan out as `#opt:withN` candidates.
    /// Also drop appends that are wiped by an intervening simple assignment to the same local —
    /// `text += " mid"; text = "override"; setter` runs `text="override"` at the setter, not
    /// `"override mid"`, so the stale `+=` must not chain onto the resolved value.
    /// </summary>
    private static IReadOnlyDictionary<string, List<AppendEntry>> FilterAppendsBeforeSetter(
        IReadOnlyDictionary<string, List<AppendEntry>> appendsByLocal,
        InvocationExpressionSyntax setter)
    {
        var dict = new Dictionary<string, List<AppendEntry>>(StringComparer.Ordinal);
        var method = setter.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        foreach (var (name, appends) in appendsByLocal)
        {
            var resetSpans = method?.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(a => a.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && a.Left is IdentifierNameSyntax lhs
                    && lhs.Identifier.ValueText == name)
                .Select(a => a.SpanStart)
                .ToList() ?? new List<int>();
            var kept = new List<AppendEntry>();
            foreach (var entry in appends)
            {
                // The rhs is the right-hand side of `local += rhs`; walk up to the owning
                // assignment expression (its parent) and use its span as the append's span.
                var assignSpanStart = entry.Right.Parent?.SpanStart ?? entry.Right.SpanStart;
                if (assignSpanStart >= setter.SpanStart) continue;
                if (resetSpans.Any(r => r > assignSpanStart && r < setter.SpanStart)) continue;
                kept.Add(entry);
            }
            if (kept.Count > 0) dict[name] = kept;
        }
        return dict;
    }

    private static IReadOnlyDictionary<string, List<AppendEntry>> FilterAppendsToLocals(
        IReadOnlyDictionary<string, ExpressionSyntax> activeLocals,
        IReadOnlyDictionary<string, List<AppendEntry>> appendsByLocal)
    {
        var dict = new Dictionary<string, List<AppendEntry>>(StringComparer.Ordinal);
        foreach (var (name, appends) in appendsByLocal)
        {
            if (activeLocals.ContainsKey(name)) dict[name] = appends;
        }
        return dict;
    }

    /// <summary>
    /// Collect compound `+=` reassignments (`local += expr`) per local, tagging each entry with
    /// whether it is conditionally executed (Optional = wrapped in an `IfStatementSyntax`).
    /// </summary>
    /// <remarks>
    /// Required (`Optional=false`) appends are folded into the resolved local value so the
    /// extracted candidate matches the runtime string. Optional (`Optional=true`) appends drive
    /// `#opt:base` / `#opt:withN` fanout. Switch-case ancestry is intentionally not treated as
    /// optional — case selection is already modelled by the `#case:N` id suffix.
    /// </remarks>
    private static IReadOnlyDictionary<string, List<AppendEntry>> CollectCompoundAppends(MethodDeclarationSyntax method)
    {
        var dict = new Dictionary<string, List<AppendEntry>>(StringComparer.Ordinal);
        // `DescendantNodes` yields nodes in document order, so the resulting per-local list is
        // already in source order — needed by `ExpandOptionalAppends` to interleave optional and
        // required appends correctly when synthesising the per-variant value chain.
        // CR R8: optional vs required is currently approximated by IfStatement-ancestor
        // presence. A reachability-based classification (does every path from method
        // entry to setter pass through this `+=`?) would be more precise but requires
        // control-flow analysis. None of the current PR1/PR2a targets exhibit the
        // approximation's failure mode (`+=` in same branch as setter producing
        // spurious `#opt:base`), so the precise fix is deferred to a follow-up.
        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assignment.IsKind(SyntaxKind.AddAssignmentExpression)) continue;
            if (assignment.Left is not IdentifierNameSyntax lhs) continue;
            var optional = assignment.Ancestors().OfType<IfStatementSyntax>().Any();
            var name = lhs.Identifier.ValueText;
            if (!dict.TryGetValue(name, out var list))
            {
                list = new List<AppendEntry>();
                dict[name] = list;
            }
            list.Add(new AppendEntry(assignment.Right, optional));
        }
        return dict;
    }

    /// <summary>
    /// Resolve a local that has compound `+=` appends into one or more variants:
    /// <list type="bullet">
    ///   <item>Required appends always fold into the local's resolved value.</item>
    ///   <item>If the local also has optional appends, emit a `#opt:base` variant (required-only)
    ///   plus one `#opt:withN` variant per optional. Within each `withN`, the optional is
    ///   inserted at its source position relative to the required appends so the synthesised
    ///   chain matches the runtime concatenation order.</item>
    ///   <item>If the local has only required appends (no optionals), emit a single (label=null,
    ///   locals) pair where the local's override value carries all required appends folded in —
    ///   no fanout, but the resolved value still matches the runtime string.</item>
    ///   <item>If the value expression has no append-bearing local, emit a single (label=null,
    ///   original-locals) pair.</item>
    /// </list>
    /// Only the first append-bearing local found is expanded; nested cross-products are not
    /// emitted (target files have at most one optionally-appended local in the value-expression
    /// tree).
    /// </summary>
    private static List<(string? optLabel, IReadOnlyDictionary<string, ExpressionSyntax> locals)>
        ExpandOptionalAppends(
            ExpressionSyntax valueExpr,
            IReadOnlyDictionary<string, ExpressionSyntax> locals,
            IReadOnlyDictionary<string, List<AppendEntry>> appendsByLocal)
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

        // Indices (within `appends`, in source order) of the optional entries.
        var optionalIndices = new List<int>();
        for (var i = 0; i < appends.Count; i++)
        {
            if (appends[i].Optional) optionalIndices.Add(i);
        }

        if (optionalIndices.Count == 0)
        {
            // Required-only: fold every append into the resolved value, no fanout.
            var folded = ChainAppends(baseInit, appends, includeIndex: -1);
            result.Add((null, BuildAppendOverride(locals, appendLocalName, folded)));
            return result;
        }

        // `#opt:base` = initializer + all required appends (in source order); optionals omitted.
        var baseChain = ChainAppends(baseInit, appends, includeIndex: -1);
        result.Add(("base", BuildAppendOverride(locals, appendLocalName, baseChain)));

        // `#opt:withK` = initializer + all required + the K-th optional, in source order.
        for (var k = 0; k < optionalIndices.Count; k++)
        {
            var withChain = ChainAppends(baseInit, appends, includeIndex: optionalIndices[k]);
            result.Add(($"with{k + 1}", BuildAppendOverride(locals, appendLocalName, withChain)));
        }
        return result;
    }

    /// <summary>
    /// Build a left-leaning `+`-concat chain `init + a_i1 + a_i2 + ...` from the source-ordered
    /// `appends`, including every required entry plus the entry at <paramref name="includeIndex"/>
    /// (when non-negative). The result is a synthetic <see cref="BinaryExpressionSyntax"/> tree
    /// that <see cref="ResolveValueExpression"/> / <see cref="FlattenConcat"/> can flatten as if
    /// the local were declared with that concat as its initializer.
    /// </summary>
    private static ExpressionSyntax ChainAppends(
        ExpressionSyntax init,
        List<AppendEntry> appends,
        int includeIndex)
    {
        var chain = init;
        for (var i = 0; i < appends.Count; i++)
        {
            var entry = appends[i];
            // Required entries always fold in. Optional entries fold in only when this is the
            // specific optional being expanded (`includeIndex == i`).
            if (entry.Optional && i != includeIndex) continue;
            chain = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, chain, entry.Right);
        }
        return chain;
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

    private static (string? name, List<AppendEntry>? appends) FindAppendableLocal(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        IReadOnlyDictionary<string, List<AppendEntry>> appendsByLocal,
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
                AddSlot(slots, RenderTokenRaw(invoc.ArgumentList.Arguments[0].Expression, locals), type: HistoryKitTokenType);
                pieces.Add($"{{{slots.Count - 1}}}");
                return true;
            }

            case InvocationExpressionSyntax invoc when IsCapitalizingFirstCharWrapper(invoc):
            {
                if (invoc.ArgumentList.Arguments.Count < 1)
                {
                    unsupportedReason = "InitCap/InitialCap with no arguments";
                    return false;
                }
                var savedPieceCount = pieces.Count;
                if (!FlattenConcat(invoc.ArgumentList.Arguments[0].Expression, locals, pieces, slots, visited, out unsupportedReason)) return false;
                // Capitalize the first character of the first piece emitted by the wrapped
                // expression — runtime `Grammar.InitCap` / `InitialCap` only touches the first
                // character. Skip if the inner emitted nothing, an empty piece, or a slot
                // placeholder (`{N}`): in those cases the runtime-capitalized character is
                // captured by the slot's `(.+?)` group and the regex still matches.
                // Walk forward from `savedPieceCount` skipping empty pieces (FlattenStringFormat
                // can emit `""` between back-to-back slots) until we find the first non-empty
                // piece. If that piece is a `{N}` slot placeholder, the runtime-capitalized
                // character is already captured by the slot's `(.+?)` group and there is
                // nothing to rewrite. Otherwise apply the runtime `Grammar.InitCap` rule:
                // only ASCII a-z is uppercased; other chars pass through unchanged.
                for (var i = savedPieceCount; i < pieces.Count; i++)
                {
                    var piece = pieces[i];
                    if (piece.Length == 0) continue;
                    if (piece[0] == '{') break;
                    if (piece[0] is >= 'a' and <= 'z')
                    {
                        pieces[i] = char.ToUpperInvariant(piece[0]) + piece[1..];
                    }
                    break;
                }
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
        if (!TryResolveStringLiteral(fmtExpr, locals, out var format))
        {
            unsupportedReason = $"string.Format format string is not a literal: {fmtExpr.Kind()}";
            return false;
        }

        // Walk the format string. Each `{N}` (with optional `,W` or `:fmt`) substitutes args[N+1].
        var i = 0;
        var literalSb = new StringBuilder();
        while (i < format.Length)
        {
            var c = format[i];
            if (c == '{' && i + 1 < format.Length && format[i + 1] == '{')
            {
                // Use a sentinel so downstream passes that interpret `{N}` as a slot reference
                // (ParsePieceIntoStream, BuildAnchoredRegex) cannot mistake an escaped literal
                // brace for one. Translated back to `{` for sample_source / `\{` for the regex.
                literalSb.Append(LiteralBraceOpenSentinel);
                i += 2;
                continue;
            }
            if (c == '}' && i + 1 < format.Length && format[i + 1] == '}')
            {
                literalSb.Append(LiteralBraceCloseSentinel);
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
                var sepIdx = holderBody.IndexOfAny(FormatAlignmentSeparators);
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
    /// <remarks>
    /// CR R8: when the expression is a local whose initializer is itself a literal-only
    /// concat that yields a token-shaped string (e.g. `var tok = "&lt;entity.name&gt;";
    /// ExpandString(tok)`), follow the local through `locals` so the slot raw matches what the
    /// runtime expands — not the C# identifier name. The follow-through is intentionally
    /// limited to literal-only initializers: locals whose RHS is a method call or another
    /// non-literal expression keep the prior `{name}` placeholder rendering, so PR2a outputs
    /// where helper-derived locals appear inside cross-piece tokens (e.g. `text3 = ExpandString
    /// (...)` in MeetFaction) remain byte-identical. Visited-set guard mirrors
    /// `LeftmostLiteral`/`RightmostLiteral` to prevent infinite recursion on cyclic locals.
    /// </remarks>
    private static string RenderTokenRaw(ExpressionSyntax expr, IReadOnlyDictionary<string, ExpressionSyntax> locals)
    {
        var sb = new StringBuilder();
        AppendTokenRaw(expr, locals, new HashSet<string>(StringComparer.Ordinal), sb);
        return sb.ToString();
    }

    private static void AppendTokenRaw(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        HashSet<string> visited,
        StringBuilder sb)
    {
        switch (expr)
        {
            case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                sb.Append(lit.Token.ValueText);
                break;
            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression):
                AppendTokenRaw(bin.Left, locals, visited, sb);
                AppendTokenRaw(bin.Right, locals, visited, sb);
                break;
            case IdentifierNameSyntax id
                when locals.TryGetValue(id.Identifier.ValueText, out var init)
                && IsLiteralOnlyConcat(init, locals, new HashSet<string>(StringComparer.Ordinal))
                && visited.Add(id.Identifier.ValueText):
                try
                {
                    AppendTokenRaw(init, locals, visited, sb);
                }
                finally
                {
                    visited.Remove(id.Identifier.ValueText);
                }
                break;
            default:
                sb.Append('{').Append(ClassifyHelperCallRaw(expr)).Append('}');
                break;
        }
    }

    /// <summary>
    /// True when <paramref name="expr"/> is a string literal, a `+`-concat of literal-only
    /// sub-expressions, or an identifier that resolves through `locals` to a literal-only
    /// expression. Used by `AppendTokenRaw` to decide whether following an identifier through
    /// the locals table is safe (i.e., yields a stable string with no embedded helper calls).
    /// </summary>
    private static bool IsLiteralOnlyConcat(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        HashSet<string> visited)
    {
        // `visited` must be cleaned up via try/finally; an earlier expression-switch added the
        // name as a side-effect of the `when` clause and never removed it, so `a + a` (same
        // local on both sides) flipped the right-hand recursion to `false` even when the
        // initializer was literal-only.
        switch (expr)
        {
            case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                return true;
            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression):
                return IsLiteralOnlyConcat(bin.Left, locals, visited)
                    && IsLiteralOnlyConcat(bin.Right, locals, visited);
            case IdentifierNameSyntax id when locals.TryGetValue(id.Identifier.ValueText, out var init):
                if (!visited.Add(id.Identifier.ValueText)) return false;
                try { return IsLiteralOnlyConcat(init, locals, visited); }
                finally { visited.Remove(id.Identifier.ValueText); }
            default:
                return false;
        }
    }

    private static string ClassifyHelperCallSlotType(ExpressionSyntax expr)
    {
        // Helper-call slot type for invocations from the known game-helper namespaces, format-arg
        // for everything else (literals already resolved by FlattenConcat, so this hits unknown
        // identifiers or expressions).
        if (expr is InvocationExpressionSyntax invoc
            && invoc.Expression is MemberAccessExpressionSyntax m
            && KnownHelperReceivers.Contains(m.Expression.ToString()))
        {
            return "helper-call";
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

    // Follow IdentifierNameSyntax through the locals table until a string literal is found
    // (or the chain breaks). Bounded by the locals dictionary size via a visited set so a
    // malformed `a = b; b = a;` cycle terminates instead of recursing forever.
    private static bool TryResolveStringLiteral(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, ExpressionSyntax> locals,
        out string value)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = expr;
        while (true)
        {
            switch (current)
            {
                case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                    value = lit.Token.ValueText;
                    return true;
                case IdentifierNameSyntax id when visited.Add(id.Identifier.ValueText)
                    && locals.TryGetValue(id.Identifier.ValueText, out var init):
                    current = init;
                    continue;
                default:
                    value = "";
                    return false;
            }
        }
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

    // Wrappers whose return value matches their first argument modulo HSE expansion (and
    // word-level title-casing for `MakeTitleCase` / `MakeTitleCaseWithArticle`, which currently
    // wrap slot-bound identifiers only — the slot's `(.+?)` group captures whatever case the
    // runtime emits). `Grammar.InitCap` / `InitialCap` are NOT included here: they capitalize
    // the first character of the result, which IS visible to a case-sensitive regex when the
    // pattern starts with a literal. Those go through a dedicated branch in FlattenConcat that
    // uppercases the first emitted literal piece.
    //
    // Receiver matching is intentionally syntax-only (`m.Expression.ToString() == "Grammar"`),
    // mirroring `KnownHelperReceivers` and the rest of the extractor — no Compilation /
    // SemanticModel is built. Fully-qualified usages (`XRL.Core.Grammar.InitCap`) would slip
    // through this check, but the decompiled `XRL.Annals/*.cs` corpus only emits the bare-
    // identifier form. Migrating to SemanticModel-based symbol resolution would require
    // building a Compilation with the full game's MetadataReferences and converting every
    // string-match site for consistency, which is out of scope. CR R9 noted; deferred.
    private static bool IsPatternPreservingWrapper(InvocationExpressionSyntax invoc)
    {
        if (IsExpandStringWrapper(invoc)) return true;
        if (invoc.ArgumentList.Arguments.Count < 1) return false;
        if (invoc.Expression is MemberAccessExpressionSyntax m
            && m.Expression.ToString() == "Grammar")
        {
            return m.Name.Identifier.ValueText is "MakeTitleCase" or "MakeTitleCaseWithArticle";
        }
        return false;
    }

    // `Grammar.InitCap(s)` / `Grammar.InitialCap(s)` uppercase only the first character of `s`
    // at runtime. When the wrapped expression flattens to a pattern starting with a literal
    // (e.g. MeetFaction's `"deep in ..."`), the case-sensitive runtime regex needs the literal's
    // first char uppercased — `^deep` would not match the runtime-emitted `Deep`.
    private static bool IsCapitalizingFirstCharWrapper(InvocationExpressionSyntax invoc)
    {
        if (invoc.ArgumentList.Arguments.Count < 1) return false;
        if (invoc.Expression is not MemberAccessExpressionSyntax m) return false;
        if (m.Expression.ToString() != "Grammar") return false;
        return m.Name.Identifier.ValueText is "InitCap" or "InitialCap";
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
        // BuildAnchoredRegex MUST run on the sentinel-bearing sample so it can distinguish
        // literal-brace sentinels (→ `\{`/`\}`) from real slot-ref braces (→ `(.+?)`). Only
        // after the regex is built do we restore the sentinels to literal `{`/`}` for the
        // human-facing sample_source field.
        var sample = resolved.SampleSource;
        var pattern = BuildAnchoredRegex(sample, resolved.Slots);
        var humanSample = sample
            .Replace(LiteralBraceOpenSentinel, '{')
            .Replace(LiteralBraceCloseSentinel, '}');
        var c = new CandidateEntry
        {
            Id = id,
            SourceFile = sourceFile,
            AnnalClass = annalClass,
            SwitchCase = switchCase,
            EventProperty = eventProperty,
            SampleSource = humanSample,
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
            // Translate literal-brace sentinels (planted by FlattenStringFormat for `{{`/`}}`)
            // back to regex-escaped literal braces. Regex.Escape on the sentinel codepoint would
            // emit the bare PUA char, which would silently fail to match the runtime `{`/`}`.
            if (sample[i] == LiteralBraceOpenSentinel)
            {
                sb.Append(Regex.Escape("{"));
                i++;
                continue;
            }
            if (sample[i] == LiteralBraceCloseSentinel)
            {
                sb.Append(Regex.Escape("}"));
                i++;
                continue;
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
