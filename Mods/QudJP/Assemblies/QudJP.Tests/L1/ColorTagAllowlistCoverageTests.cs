namespace QudJP.Tests.L1;

// issue-376: color tag application/restoration static analysis — L1 supplement.
//
// Catalog-level allowlist tests that would have caught past color-tag drops once
// the static-analysis layer is wired up. They are NUnit `[Test, Ignore(...)]` (the
// project does not use xUnit; see ColorTagStaticAnalysisTests.cs for rationale).
//
// Layer rationale per docs/test-architecture.md:
//   Both this file and ColorTagStaticAnalysisTests.cs sit at L1 (no HarmonyLib,
//   no Assembly-CSharp.dll, no Unity). This file is the static catalog scan
//   layer; ColorTagStaticAnalysisTests.cs is the translator-surface drop-scenario
//   layer. Catalog-1/-2/-3 here scan `Mods/QudJP/Assemblies/src/`; Catalog-4
//   scans `Mods/QudJP/Localization/Dictionaries/*.json`.

[TestFixture]
[Category("L1")]
public sealed class ColorTagAllowlistCoverageTests
{
    // Common prefix `"issue-376 — production code pending"` is shared with
    // ColorTagStaticAnalysisTests.SkipReason so a single grep surfaces every
    // scaffold the production PR must address.
    private const string SkipReason = "issue-376 — production code pending; catalog allowlist not yet defined";

    // Catalog-1: every Patches/*.cs that calls ColorAwareTranslationComposer.Strip
    // must also reach a paired Restore site (RestoreCapture / RestoreSpans /
    // TranslatePreservingColors). The check is per-call-site, NOT a count match:
    // one Strip can fan out to multiple RestoreCapture calls (one per capture
    // group), and TranslatePreservingColors performs Strip+Restore internally
    // and shouldn't be paired with a sibling Strip at all.
    [Test]
    [Ignore(SkipReason)]
    public void EveryStripCallSite_HasMatchingRestoreCallSite()
    {
        // Production rule (to encode here):
        //   for each *.cs under Mods/QudJP/Assemblies/src/ that actually calls
        //   ColorAwareTranslationComposer.Strip / RestoreCapture (i.e. NOT limited to
        //   src/Patches/ — translator-layer files like
        //   Translation/MessagePatternTranslator.cs are equally subject to the rule):
        //     each `Strip(` call site must have at least one Restore* / TranslatePreservingColors
        //     reachable in the same control-flow path (function or method). Files where
        //     `TranslatePreservingColors(` already encapsulates the round-trip do NOT need
        //     an explicit sibling Restore. Exemptions live in an explicit allowlist
        //     (path -> reason) that this test reads.
        // Implementation hint: extend `ColorRouteCatalog.RouteSymbols` with
        // `"ColorAwareTranslationComposer.Strip("` and `"ColorAwareTranslationComposer.RestoreCapture("`,
        // then reuse `ColorRouteCatalogTests.ScanSymbolOccurrences` for the per-file
        // symbol map; the pairing logic is what's new (counts alone are not the contract).
        Assert.Pass("Scaffold; replace with per-call-site reachability scan when production lands.");
    }

    // Catalog-2: routes that call GetDisplayNameRouteTranslator.TranslatePreservingColors
    // from inside a wrapper translator must be in the wrapper-owner allowlist —
    // observation-only / sink routes must NOT call it.
    // This guards the "ownership at producer / mid-pipeline, not sink" rule from
    // docs/RULES.md.
    [Test]
    [Ignore(SkipReason)]
    public void DisplayNameTranslate_OnlyCalled_FromOwnerRouteAllowlist()
    {
        // Production rule:
        //   the set of files calling GetDisplayNameRouteTranslator.TranslatePreservingColors
        //   must be a subset of OwnerRouteAllowlist (defined in this test file or a
        //   shared catalog).
        // Implementation hint: `ColorRouteCatalog.ExpectedSymbolOccurrences` already
        // tracks per-file counts of `GetDisplayNameRouteTranslator.TranslatePreservingColors(`
        // — the allowlist is just the keys of that map filtered by owner-vs-observation
        // classification.
        Assert.Pass("Scaffold; pair with ColorRouteCatalog OwnerRouteAllowlist when production lands.");
    }

    // Catalog-3: every owner route that performs RestoreCapture on a capture group
    // whose value can carry pre-existing markup must branch on a markup-aware guard
    // (mirroring DeathWrapperFamilyTranslator.cs:326-328 which uses HasColorMarkup).
    // Static check: any file that contains both "RestoreCapture(" and a name-like
    // capture group ("killer" / "name" / "subject" / "target") must also reference
    // an equivalent guard or markup-aware abstraction.
    [Test]
    [Ignore(SkipReason)]
    public void RestoreCapture_OnNameLikeCapture_HasMarkupGuard()
    {
        // Production rule (the GUARD side is intentionally an OPEN set, not a literal):
        //   trigger: file contains `RestoreCapture(` against any name-like capture group
        //            (current corpus uses killer / name / subject / target — extend as
        //            new owner routes appear).
        //   guard:   file must also contain a call into ANY symbol from
        //            `static readonly string[] AllowedGuardSymbols` defined in this test.
        //            Initial set: { "HasColorMarkup(", "MarkupAwareRestoreCapture(",
        //            "IsAlreadyLocalized" }. Renames or new abstractions land as one-line
        //            additions to that constant; the test must NOT pin to a single
        //            literal like `HasColorMarkup\(`.
        //   exempt:  pre-rendered sinks listed in an explicit (path -> reason) allowlist
        //            this test reads.
        Assert.Pass("Scaffold; replace with regex-based catalog scan when production lands.");
    }

    // Catalog-4: the dictionary corpus must not contain unbalanced markup tokens.
    // (Closely related to issue #404 but distinct: #404 was about a single XML
    // entry; this catalog enforces the invariant repository-wide.)
    [Test]
    [Ignore(SkipReason)]
    public void DictionaryCorpus_HasBalancedMarkupTokens()
    {
        // Production rule:
        //   for every translated value in Mods/QudJP/Localization/Dictionaries/*.json
        //   the multiset of `{{`, `}}`, `&<letter>` opening/closing markers must be
        //   token-balanced. Mirrors scripts/validate_xml.py's invariant for JSON.
        Assert.Pass("Scaffold; defer to issue #409 if a CI gate already covers this.");
    }
}
