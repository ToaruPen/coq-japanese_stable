using QudJP;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class RelicGeneratedNameTranslatorTests
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

    [TestCase("Edge of the Dominant Sword", "支配的な剣の刃")]
    [TestCase("Adventurer's Dominant Edge", "冒険者の支配的な刃")]
    [TestCase("Dominant-Edge", "支配的な・刃")]
    [TestCase("Dominant Edge", "支配的な刃")]
    [TestCase("Dominant Sword of Bethesda Susa", "Bethesda Susaの支配的な剣")]
    [TestCase("The Dominant Sword of Bethesda Susa", "Bethesda Susaの支配的な剣")]
    public void TryTranslate_TranslatesFiniteRelicNameShapes(string source, string expected)
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate("{{Y|Edge of the Dominant Sword}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y|支配的な剣の刃}}"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "Edge of the Dominant Sword",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Edge of the Dominant Sword"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("Qwern of the Dominant Sword")]
    [TestCase("Edge of the Qwern Sword")]
    public void TryTranslate_LeavesUnsupportedNamesUnchanged(string? source)
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(source, out var result);

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
