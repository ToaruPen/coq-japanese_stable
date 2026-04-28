using System.IO;
using QudJP.Patches;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SultanShrineWrapperTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        var dictionaryDir = Path.Combine(localizationRoot, "Dictionaries");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDir);
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFilesForTests(null);
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Translate_TranslatesPrefixGospelAndQuality_ForReshephAnnalsComposite()
    {
        const string source =
            "The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nIn 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and taught Abram to sow watervine along its fertile tracks."
            + "\n\n{{Y|Perfect}}";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("この祠は古のスルタン"), "wrapper prefix should be Japanese");
            Assert.That(translated, Does.Contain("レシェフ"), "sultan name should be translated");
            Assert.That(translated, Does.Contain("3年、レシェフは"), "annals gospel should be translated via JournalPatternTranslator");
            Assert.That(translated, Does.EndWith("{{Y|完璧}}"), "quality rating should be Japanese inside its color wrapper");
            Assert.That(translated, Does.Not.Contain("Resheph"), "no English sultan name should remain");
            Assert.That(translated, Does.Not.Contain("Perfect"), "no English quality word should remain");
            Assert.That(translated, Does.Not.Contain("The shrine depicts"), "no English wrapper should remain");
        });
    }

    [Test]
    public void Translate_TranslatesPrefixGospelAndQuality_ForPopupRoute()
    {
        // Tooltip/popup route: SultanShrine surfaces the full composite (with quality suffix)
        // through the popup path, so MessagePatternTranslator must translate it identically
        // regardless of which Patch route called it. Locking this here pins popup regressions.
        const string source =
            "The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nIn 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and taught Abram to sow watervine along its fertile tracks."
            + "\n\n{{Y|Perfect}}";

        var translated = MessagePatternTranslator.Translate(source, nameof(PopupMessageTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("この祠は古のスルタン"), "wrapper prefix should be Japanese on popup route");
            Assert.That(translated, Does.Contain("3年、レシェフは"), "annals gospel should be translated on popup route");
            Assert.That(translated, Does.EndWith("{{Y|完璧}}"), "quality rating should be Japanese inside its color wrapper on popup route");
            Assert.That(translated, Does.Not.Contain("The shrine depicts"), "no English wrapper should remain on popup route");
        });
    }

    [TestCase("Perfect", "完璧")]
    [TestCase("Fine", "良好")]
    [TestCase("Lightly Damaged", "軽微な損傷")]
    [TestCase("Damaged", "損傷")]
    [TestCase("Badly Damaged", "重度の損傷")]
    [TestCase("Undamaged", "無傷")]
    [TestCase("Badly Wounded", "重傷")]
    [TestCase("Wounded", "負傷")]
    [TestCase("Injured", "軽傷")]
    public void Translate_TranslatesAllShippedQualityRatings(string qualityEn, string qualityJa)
    {
        var source =
            "The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nIn 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and taught Abram to sow watervine along its fertile tracks."
            + "\n\n{{Y|" + qualityEn + "}}";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.That(translated, Does.EndWith("{{Y|" + qualityJa + "}}"));
        Assert.That(translated, Does.Not.Contain(qualityEn));
    }

    [Test]
    public void Translate_DoesNotMatchUnrelatedComposite_LeavesPassthroughBehavior()
    {
        const string source = "The shrine depicts something completely different.";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.That(translated, Is.EqualTo(source), "non-shrine-wrapper inputs must not be touched by this translator");
    }

    [Test]
    public void Translate_TranslatesReshephNameInsideShrineWrapper()
    {
        const string source =
            "The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nIn 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and taught Abram to sow watervine along its fertile tracks."
            + "\n\n{{Y|Perfect}}";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.That(translated, Does.Contain("レシェフ"));
    }

    [Test]
    public void Translate_PreservesUnknownGospel_WhenAnnalsPatternMissing()
    {
        const string source =
            "The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nUnknown gospel that does not match any annals pattern."
            + "\n\n{{Y|Perfect}}";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("この祠は古のスルタン"));
            Assert.That(translated, Does.Contain("Unknown gospel that does not match any annals pattern."));
            Assert.That(translated, Does.EndWith("{{Y|完璧}}"));
        });
    }

    [Test]
    public void Translate_TranslatesDiscoveredLocationAnnalsGospel()
    {
        const string source =
            "The shrine depicts a significant event from the life of the ancient sultan Yla Haj:"
            + "\n\ndiscovered Rust Wells"
            + "\n\n{{Y|Perfect}}";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("Rust Wellsを発見した"));
            Assert.That(translated, Does.Not.Contain("discovered Rust Wells"));
            Assert.That(translated, Does.EndWith("{{Y|完璧}}"));
        });
    }

    [Test]
    public void Translate_TranslatesTwoParagraphShape_WhenQualitySuffixAbsent()
    {
        // Shape produced directly by SultanShrine.ShrineInitialize (Description.Short).
        // Quality rating is appended only on the tooltip / popup path, so routes that show
        // the long description without the wound suffix must still translate the wrapper.
        const string source =
            "The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nIn 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and taught Abram to sow watervine along its fertile tracks.";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("この祠は古のスルタン"), "wrapper prefix should be Japanese");
            Assert.That(translated, Does.Contain("レシェフ"), "sultan name should be translated");
            Assert.That(translated, Does.Contain("3年、レシェフは"), "annals gospel should be translated");
            Assert.That(translated, Does.Not.Contain("The shrine depicts"), "no English wrapper should remain");
            Assert.That(translated, Does.Not.Contain("Resheph"), "no English sultan name should remain");
            Assert.That(translated, Does.Not.Contain("{quality}"), "quality placeholder should not leak");
            Assert.That(translated, Does.Not.EndWith("\n\n"), "no dangling separator should remain when quality is absent");
        });
    }

    [Test]
    public void Translate_DoesNotOverMatchUnrelatedTwoParagraphTextStartingWithTheShrine()
    {
        // Plausible-but-unrelated two-paragraph text that begins with "The shrine" must not
        // be claimed by the wrapper translator. Without the canonical "depicts a significant
        // event from the life of the ancient sultan ...:" prefix the regex must miss.
        const string source = "The shrine looks weathered.\n\nMoss has overgrown its base.";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.That(translated, Is.EqualTo(source), "unrelated two-paragraph inputs must fall through unchanged");
    }

    [Test]
    public void TranslateLongDescription_KeepsQualityWrapperAroundJapaneseQuality_ForProductionColorFlow()
    {
        // Production color flow: Description.GetLongDescription -> DescriptionLongDescription-
        // Patch.Postfix -> DescriptionTextTranslator.TranslateLongDescription -> Color-
        // AwareTranslationComposer.TranslatePreservingColors strips colors BEFORE the inner
        // visible-segment lambda calls MessagePatternTranslator. If shrine wrapper composes
        // its color restoration only against the inner spans (which are empty on this path),
        // the outer Restore re-applies absolute span positions from the long English source
        // onto the much shorter Japanese output, producing "...完璧{{Y|}}" instead of
        // "...{{Y|完璧}}". This test guards that path.
        const string source =
            "The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nIn 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and taught Abram to sow watervine along its fertile tracks."
            + "\n\n{{Y|Perfect}}";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            nameof(DescriptionLongDescriptionPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.EndWith("{{Y|完璧}}"),
                "color wrapper must enclose the translated quality word, not appear as an empty trailing pair");
            Assert.That(translated, Does.Not.Contain("{{Y|}}"),
                "empty color wrapper indicates spans were re-applied with stale source-length positions");
            Assert.That(translated, Does.Contain("3年、レシェフは"),
                "annals gospel must be translated");
            Assert.That(translated, Does.Not.Contain("The shrine depicts"),
                "wrapper prefix must be Japanese");
        });
    }

    [Test]
    public void Translate_ReturnsEmpty_WhenInputIsEmpty()
    {
        var translated = MessagePatternTranslator.Translate(string.Empty, nameof(DescriptionLongDescriptionPatch));

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_DoesNotMatch_WhenDirectTranslationMarkerIsPresent()
    {
        // \x01-prefixed text is the direct-translation marker: producers signal "already
        // translated, do not retranslate" by prepending it. The shrine wrapper must not
        // claim such input, so it falls through MessagePatternTranslator unchanged (the
        // outer patch is responsible for stripping the marker on its way to the sink).
        var source = "\x01The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nIn 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and taught Abram to sow watervine along its fertile tracks."
            + "\n\n{{Y|Perfect}}";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        Assert.That(translated, Is.EqualTo(source),
            "direct-marker-prefixed input must not be claimed by the shrine wrapper translator");
    }

    [Test]
    public void Translate_PassesThroughUnknownQuality_WhenDirectTranslationMarkerSitsInsideColoredQuality()
    {
        // The quality token \x01Perfect is not in QualityKeys, so the wrapper translates
        // sultan + gospel + prefix as usual but the quality segment falls through unchanged.
        // This pins the asymmetric behavior: outer wrapper still matches, only quality is
        // passthrough, no empty {{Y|}} pair leaks.
        var source = "The shrine depicts a significant event from the life of the ancient sultan Resheph:"
            + "\n\nIn 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and taught Abram to sow watervine along its fertile tracks."
            + "\n\n{{Y|\x01Perfect}}";

        var translated = MessagePatternTranslator.Translate(source, nameof(DescriptionLongDescriptionPatch));

        // The quality token "\x01Perfect" is not in the QualityKeys map, so quality
        // translation falls through to the original. The wrapper still translates because
        // sultan name + gospel + prefix all match — but the quality stays as the source
        // marker-prefixed token, which is correct passthrough behavior for unknown qualities.
        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("この祠は古のスルタン"),
                "wrapper prefix should still translate around the unknown marker-prefixed quality");
            Assert.That(translated, Does.Contain("{{Y|\x01Perfect}}"),
                "marker-bearing quality token must remain wrapped in its original color tag");
            Assert.That(translated, Does.Contain("\x01Perfect"),
                "marker-bearing quality token must pass through unchanged when not in QualityKeys");
            Assert.That(translated, Does.Not.Contain("{{Y|}}"),
                "no empty color wrapper should be left behind");
        });
    }
}
