using QudJP.Patches;

namespace QudJP.Tests.L2;

// issue-376: color tag application/restoration static analysis.
//
// These DummyTarget-shape L2 tests pin the post-translation behavior we expect once
// the static-analysis-driven detection layer lands. They are intentionally skipped
// (NUnit Ignore) so CI stays green; they become discoverable failures the moment a
// production fix is wired up that flips them to passing.
//
// Skip pattern note:
//   The umbrella prompt asked for `[Fact(Skip = "...")]` (xUnit). The QudJP test
//   project is NUnit (see QudJP.Tests.csproj `<Using Include="NUnit.Framework" />`),
//   so we use the NUnit-equivalent `[Test, Ignore("issue-376 — production code pending")]`.
//   Behavior contract is identical: the test is discoverable but does not run.
//
// Each scenario below corresponds to a concrete drop pattern documented in the issue
// body and in `docs/superpowers/plans/2026-04-27-issue-376-color-static-analysis.md`.
// The tests are written in arrange/act/assert form with comments describing the
// expected post-fix behavior. Implementation hooks are deliberately sketched
// (commented out) — wiring them up is the responsibility of the future production PR.

// `[NonParallelizable]` because the un-ignored production tests will mutate the
// shared MessagePatternTranslator / Translator / DynamicTextObservability /
// SinkObservation static state (see PopupPickOptionTranslationPatchTests for the
// full reset surface). The scaffold itself is read-only, but flipping any
// `[Ignore]` should not require remembering to add the attribute.
[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ColorTagStaticAnalysisTests
{
    // Common prefix `"issue-376 — production code pending"` is shared with the L1
    // counterpart (ColorTagAllowlistCoverageTests.SkipReason) so a single grep
    // surfaces every scaffold the production PR must address.
    private const string SkipReason = "issue-376 — production code pending; static analysis layer not yet implemented";

    // Scenario 1: triple-nested {{r|...}} accumulation reproduced in Player.log
    //   "{{r|{{r|{{r|血ま}}みれの}}タム、ドロマド商団 [座ってい}}る]}}"
    // Expectation post-fix: outer wrapper restoration must NOT reapply on already-
    // localized killer fragments that already carry markup.
    [Test]
    [Ignore(SkipReason)]
    public void DeathPopup_DoesNotAccumulateNestedRedWrappers_OnAlreadyLocalizedKiller()
    {
        // Arrange: source carries one outer {{r|...}} and the localized killer
        // already injects its own {{r|血まみれの}} via dictionary lookup.
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You died.\n\nYou were killed by {{r|bloody Tam, dromad merchant [sitting]}}.");

        // Act
        var translated = DeathWrapperFamilyTranslator.TryTranslatePopup(
            stripped,
            spans,
            nameof(PopupTranslationPatch),
            out var popupTranslated);

        // Assert: the killer's own injected markup must remain singly-wrapped and
        // the outer span must not bleed into the trailing "[座っている]" bracket.
        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                popupTranslated,
                Does.Not.Contain("{{r|{{r|"),
                "Detected {{r|{{r|... — wrapper double-restoration regressed.");
            Assert.That(
                popupTranslated,
                Does.Not.Match("\\[座ってい}}る\\]"),
                "Closing }} must not land mid-bracket.");
        });
    }

    // Scenario 2: ampersand color code {&y} eats into a Japanese bracketed token.
    //   "&r血まみれのタム、ドロマド商団 [座ってい&yる]に殺された。"
    [Test]
    [Ignore(SkipReason)]
    public void DeathReason_AmpersandCode_DoesNotSplitBracketedJapaneseToken()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "&rbloody Tam, dromad merchant [sitting]&y was killed by you.");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(
            stripped,
            spans,
            out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                messageTranslated,
                Does.Not.Match("\\[座ってい&[a-zA-Z]る\\]"),
                "Ampersand color code must not split bracketed Japanese token.");
        });
    }

    // Scenario 3: capture-group RestoreCapture must not insert close tags into
    // already-translated display name internals (issue body "double restoration").
    [Test]
    [Ignore(SkipReason)]
    public void RestoreCapture_DoesNotInjectCloseTagInsideTranslatedDisplayName()
    {
        // Source has a {{C|...whole...}} wrapper around an English display name.
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "You were killed by {{C|bloody Tam, dromad merchant [sitting]}}.");

        var translated = DeathWrapperFamilyTranslator.TryTranslateMessage(
            stripped,
            spans,
            out var messageTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            // No stray "}}" inside the Japanese display name body.
            // NOTE: this regex assumes `Tam` survives un-translated. The production PR
            // must update the right-hand anchor to whatever the actual dictionary entry
            // emits (e.g. `タム`) — otherwise this assertion becomes vacuously true.
            Assert.That(
                messageTranslated,
                Does.Not.Match("血まみれの.*}}.*Tam"),
                "RestoreCapture inserted close tag into translated display-name body.");
        });
    }

    // Scenario 4: pre-rendered string passed through the translator twice.
    // When a sink receives an already-translated and already-color-restored string,
    // the second pass must be a no-op (idempotency contract).
    [Test]
    [Ignore(SkipReason)]
    public void Translator_IsIdempotent_OnAlreadyLocalizedColoredDisplayName()
    {
        const string preLocalized = "{{r|血まみれの}}タム、ドロマド商人 [座っている]";

        var first = ColorAwareTranslationComposer.TranslatePreservingColors(preLocalized);
        var second = ColorAwareTranslationComposer.TranslatePreservingColors(first);

        Assert.That(
            second,
            Is.EqualTo(first),
            "Second pass through TranslatePreservingColors must be identity on already-localized text.");
    }

    // Scenario 5: MessagePatternTranslator capture restoration must not duplicate
    // wrapper spans when the captured group already carries its own markup.
    // Mirrors the DeathWrapperFamilyTranslator HasColorMarkup branch but at the
    // generic message-pattern layer (decompiled-source families using {0}/{1}).
    [Test]
    [Ignore(SkipReason)]
    public void MessagePattern_CaptureRestoration_DoesNotDoubleWrapMarkupCarryingCapture()
    {
        // Production PR prerequisites (replace this body — model on
        // `PopupPickOptionTranslationPatchTests` SetUp/TearDown):
        //   1. Use `MessagePatternTranslator.SetPatternFileForTests(path)` to load a fixture
        //      pattern whose template wraps a capture (e.g. `{{r|{0}}}`).
        //   2. Pick a `source` whose Strip+pattern-match path actually fires that template,
        //      and whose capture value already carries its own `{{r|...}}` markup.
        //   3. Translate; assert exact output equals the markup-aware expectation, AND
        //      `Does.Not.Contain("{{r|{{r|")`. The exact-output assertion catches the case
        //      where the translator silently passes through and the negative assertion
        //      becomes vacuously true.
        // Reset state in `[TearDown]` via `MessagePatternTranslator.ResetForTests()`,
        // `Translator.ResetForTests()`, `DynamicTextObservability.ResetForTests()`,
        // `SinkObservation.ResetForTests()`.
        Assert.Pass("Scaffold; production PR replaces with pattern-fixture-driven exact-output test.");
    }

    // Scenario 6: sentence-specific owner route (DescriptionTextTranslator) must
    // not splice color tags across sentence boundaries during balanced-capture.
    [Test]
    [Ignore(SkipReason)]
    public void DescriptionText_BalancedCapture_DoesNotSpliceColorAcrossSentences()
    {
        // Production PR prerequisites (replace this body):
        //   1. Pick an input that actually engages a `DescriptionTextTranslator`
        //      regex route (FactionDispositionPattern / LabeledListPattern /
        //      VillageDispositionTargetPattern), e.g. multiline
        //      `"{{W|Hated by the bandits for stealing.}}\nIt is dangerous."`
        //      and inject a matching dictionary entry via the test scope.
        //   2. Call `TranslateLongDescription`; assert exact output equals the
        //      translated-with-confined-wrapper expectation (e.g. line 1 fully
        //      Japanese-translated and re-wrapped, line 2 left alone) AND that
        //      no `{{W|` survives onto line 2.
        //   3. Reset dictionary scope in `[TearDown]`.
        Assert.Pass("Scaffold; production PR replaces with regex-fixture-driven exact-output test.");
    }
}
