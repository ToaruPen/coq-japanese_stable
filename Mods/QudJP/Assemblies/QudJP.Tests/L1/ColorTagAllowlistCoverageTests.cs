namespace QudJP.Tests.L1;

// issue-376: color tag application/restoration static analysis — L1 supplement.
//
// Catalog-level allowlist tests that would have caught past color-tag drops once
// the static-analysis layer is wired up. They are NUnit `[Test, Ignore(...)]` (the
// project does not use xUnit; see ColorTagStaticAnalysisTests.cs for rationale).
//
// Layer rationale per docs/test-architecture.md:
//   L1 here is a static catalog over `Mods/QudJP/Assemblies/src/`. No HarmonyLib,
//   no Assembly-CSharp.dll, no Unity. Pair this with the L2 DummyTarget tests in
//   ColorTagStaticAnalysisTests.cs which exercise the runtime code path.

[TestFixture]
[Category("L1")]
public sealed class ColorTagAllowlistCoverageTests
{
    private const string SkipReason = "issue-376 — production code pending; catalog allowlist not yet defined";

    // Catalog-1: every Patches/*.cs that calls ColorAwareTranslationComposer.Strip
    // OR ColorAwareTranslationComposer.RestoreCapture must also call exactly one
    // of TranslatePreservingColors / RestoreCapture / Restore — i.e. no orphan
    // strip-without-restore call sites that silently drop spans.
    [Test]
    [Ignore(SkipReason)]
    public void EveryStripCallSite_HasMatchingRestoreCallSite()
    {
        // Production rule (to encode here):
        //   for each *.cs under Mods/QudJP/Assemblies/src/Patches:
        //     count(Strip)   ==  count(Restore | RestoreCapture | TranslatePreservingColors)
        //   exemptions live in an explicit allowlist (path -> reason) that this test reads.
        Assert.Pass("Scaffold; replace with real catalog scan when production lands.");
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
        Assert.Pass("Scaffold; pair with ColorRouteCatalog OwnerRouteAllowlist when production lands.");
    }

    // Catalog-3: every owner route that performs RestoreCapture on a capture group
    // whose value can carry pre-existing markup must branch on HasColorMarkup
    // (mirroring DeathWrapperFamilyTranslator.cs:326-328). Static check: any file
    // that contains both "RestoreCapture(" and a non-trivial capture group named
    // "killer" / "name" / "subject" / "target" must also reference a HasColorMarkup
    // (or equivalent) guard.
    [Test]
    [Ignore(SkipReason)]
    public void RestoreCapture_OnNameLikeCapture_HasMarkupGuard()
    {
        // Production rule:
        //   files matching `RestoreCapture\(.*Groups\["(killer|name|subject|target)"\]\)`
        //   must also match `HasColorMarkup\(` in the same file. Otherwise the file
        //   is a candidate for the double-restoration regression seen in issue-376.
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
