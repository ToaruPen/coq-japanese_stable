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

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ColorTagStaticAnalysisTests
{
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
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "{{Y|You hit {{r|bloody Tam}} for {{R|10}} damage.}}");

        // Scaffold: route through MessagePatternTranslator once a pattern is loaded.
        // The expected behavior under issue-376 is "no nested {{r|{{r|...}}}}".
        // Production wiring lands later; this assertion locks the contract.
        var (firstPass, _) = ColorAwareTranslationComposer.Strip(stripped);
        Assert.That(spans, Is.Not.Null);
        Assert.That(firstPass, Does.Not.Contain("{{r|{{r|"));
    }

    // Scenario 6: sentence-specific owner route (DescriptionTextTranslator) must
    // not splice color tags across sentence boundaries during balanced-capture.
    [Test]
    [Ignore(SkipReason)]
    public void DescriptionText_BalancedCapture_DoesNotSpliceColorAcrossSentences()
    {
        // Two-sentence English description with the color span fully enclosing the
        // first sentence only.
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(
            "{{W|This is a Tam.}} It is bloody.");

        Assert.Multiple(() =>
        {
            Assert.That(spans, Is.Not.Null);
            Assert.That(stripped, Does.Not.Contain("{{W|"));
            // Production hook: post-DescriptionTextTranslator output must keep the
            // {{W|...}} span confined to sentence 1 with no leak into sentence 2.
        });
    }
}
