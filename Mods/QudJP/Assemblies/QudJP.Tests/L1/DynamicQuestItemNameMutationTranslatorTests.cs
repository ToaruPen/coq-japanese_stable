using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DynamicQuestItemNameMutationTranslatorTests
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

    [TestCase("holy copper nugget", "聖なる銅塊")]
    [TestCase("venerable {{Y|folded carbide dagger}}", "尊い{{Y|積層カーバイドの短剣}}")]
    [TestCase("holy Lead Slug", "聖なる鉛スラッグ")]
    public void TryTranslate_TranslatesKnownPrefixMutation(string source, string expected)
    {
        var translated = DynamicQuestItemNameMutationTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_TranslatesOfTheMutation()
    {
        var translated = DynamicQuestItemNameMutationTranslator.TryTranslate(
            "Copper Nugget of the Holy Wheel",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("聖なる車輪の銅塊"));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        var translated = DynamicQuestItemNameMutationTranslator.TryTranslate(
            "{{Y|Copper Nugget of the Holy Wheel}}",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y|聖なる車輪の銅塊}}"));
        });
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForUnknownMutation()
    {
        var translated = DynamicQuestItemNameMutationTranslator.TryTranslate(
            "Reshephian copper nugget",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Reshephian copper nugget"));
        });
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForUnknownItemCapture()
    {
        var translated = DynamicQuestItemNameMutationTranslator.TryTranslate(
            "holy unknown trinket",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("holy unknown trinket"));
        });
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForEmptyInput()
    {
        var translated = DynamicQuestItemNameMutationTranslator.TryTranslate(null, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.Empty);
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutTranslation()
    {
        var translated = DynamicQuestItemNameMutationTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "holy copper nugget",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("holy copper nugget"));
        });
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.GetFullPath(
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
