using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class RelicDescriptionAddendumTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase("A relic. It is stamped with tiny images of salt.", "A relic. それには塩の小さな図像が刻まれている。")]
    [TestCase("A relic. It is engraved with fanciful depictions of glass.", "A relic. それにはガラスの幻想的な描写が刻まれている。")]
    [TestCase("A relic. There's an engraving of {{C|the Farmers' Guild}} being venerated as idols.", "A relic. {{C|the Farmers' Guild}}が偶像として崇敬されている様子を描いた彫刻がある。")]
    [TestCase("A relic. There's an engraving of {{C|the Farmers' Guild}} being trapped in salt.", "A relic. {{C|the Farmers' Guild}}が塩に閉じ込められている様子を描いた彫刻がある。")]
    public void TryTranslate_TranslatesFiniteRelicDescriptionAddenda(string source, string expected)
    {
        var translated = RelicDescriptionAddendumTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = RelicDescriptionAddendumTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "A relic. It is stamped with tiny images of salt.",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("A relic. It is stamped with tiny images of salt."));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("A relic with no generated addendum.")]
    public void TryTranslate_LeavesUnsupportedTextUnchanged(string? source)
    {
        var translated = RelicDescriptionAddendumTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }
}
