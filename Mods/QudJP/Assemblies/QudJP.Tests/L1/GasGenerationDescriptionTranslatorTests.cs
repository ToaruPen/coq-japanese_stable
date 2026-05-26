using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class GasGenerationDescriptionTranslatorTests
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

    [Test]
    public void TryTranslateDescription_TranslatesGasBurstFrameAndDisplayNameCapture()
    {
        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(
            "You release a burst of {{G|corrosive gas}} around yourself.");

        Assert.That(translated, Is.EqualTo("周囲に{{G|腐食性ガス}}を噴出する。"));
    }

    [Test]
    public void TryTranslateDescription_TranslatesFallbackGaseousBurstFrame()
    {
        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(
            "You release a gaseous burst around yourself.");

        Assert.That(translated, Is.EqualTo("周囲にガスを噴出する。"));
    }

    [Test]
    public void TryTranslateDescription_PreservesUnknownGasNameCapture()
    {
        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(
            "You release a burst of {{M|mystery vapor}} around yourself.");

        Assert.That(translated, Is.EqualTo("周囲に{{M|mystery vapor}}を噴出する。"));
    }

    [Test]
    public void TryTranslateDescription_LeavesUnrelatedTextUnchanged()
    {
        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(
            "You breathe a cone of gas.");

        Assert.That(translated, Is.EqualTo("You breathe a cone of gas."));
    }

    [TestCase("")]
    [TestCase("\u0001You release a burst of {{G|corrosive gas}} around yourself.")]
    public void TryTranslateDescription_PreservesEmptyAndDirectMarkedInput(string source)
    {
        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(source);

        Assert.That(translated, Is.EqualTo(source));
    }

    [Test]
    public void TryTranslateDescription_TranslatesKnownPoisonGas()
    {
        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(
            "You release a burst of {{G|poison gas}} around yourself.");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("周囲に"));
            Assert.That(translated, Does.EndWith("を噴出する。"));
        });
    }

    [Test]
    public void TryTranslateDescription_TranslatedResultContainsColorTagWhenPresent()
    {
        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(
            "You release a burst of {{G|corrosive gas}} around yourself.");

        Assert.That(translated, Does.Contain("{{G|"));
    }

    [Test]
    public void TryTranslateDescription_FallbackGaseousResultDoesNotContainColorTags()
    {
        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(
            "You release a gaseous burst around yourself.");

        Assert.That(translated, Does.Not.Contain("{{"));
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(

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
