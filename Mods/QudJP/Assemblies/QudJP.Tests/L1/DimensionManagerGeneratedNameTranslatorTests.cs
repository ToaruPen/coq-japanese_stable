using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class DimensionManagerGeneratedNameTranslatorTests
{
    [TestCase("realm of crimson", "crimsonの領域")]
    [TestCase("void of *DimensionSymbol*", "*DimensionSymbol*の虚空")]
    [TestCase("vacuous cross", "空虚なcross")]
    [TestCase("cult of *CultSymbol*", "*CultSymbol*のカルト")]
    [TestCase("Charming *cult*", "魅惑の*cult*")]
    [TestCase("*cult*, Aspect of Fire", "火の相としての*cult*")]
    [TestCase("fickle *cult*", "移り気な*cult*")]
    public void TryTranslateExpandedText_TranslatesDimensionAndCultFrames(string source, string expected)
    {
        var translated = DimensionManagerGeneratedNameTranslator.TryTranslateExpandedText(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [TestCase("the Crimsonの領域", "Crimsonの領域")]
    [TestCase("the Realm of Crimson", "Crimsonの領域")]
    public void TryTranslateStoredName_RemovesArticleAndTranslatesKnownFrames(string source, string expected)
    {
        var translated = DimensionManagerGeneratedNameTranslator.TryTranslateStoredName(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateExpandedText_LeavesNumericCultSymbolsUntouched()
    {
        var translated = DimensionManagerGeneratedNameTranslator.TryTranslateExpandedText("8756", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("8756"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("unknown dimension frame")]
    public void TryTranslateExpandedText_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = DimensionManagerGeneratedNameTranslator.TryTranslateExpandedText(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }

    [Test]
    public void TryTranslateExpandedText_PreservesWholeSourceColorWrapper()
    {
        var translated = DimensionManagerGeneratedNameTranslator.TryTranslateExpandedText("{{Y|realm of crimson}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y|crimsonの領域}}"));
        });
    }

    [Test]
    public void TryTranslateExpandedText_StripsDirectMarkerWithoutTranslation()
    {
        var translated = DimensionManagerGeneratedNameTranslator.TryTranslateExpandedText(
            MessageFrameTranslator.DirectTranslationMarker + "realm of crimson",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("realm of crimson"));
        });
    }

    [Test]
    public void TryTranslateExpandedText_StripsMarkerOnlyInputToEmptyText()
    {
        var translated = DimensionManagerGeneratedNameTranslator.TryTranslateExpandedText(
            MessageFrameTranslator.DirectTranslationMarker.ToString(),
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.Empty);
        });
    }
}
