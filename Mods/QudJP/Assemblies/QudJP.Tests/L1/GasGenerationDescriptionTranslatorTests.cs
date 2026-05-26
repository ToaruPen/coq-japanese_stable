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

    [TestCase("sleep gas", "睡眠ガス")]
    [TestCase("poison gas", "毒ガス")]
    [TestCase("normality gas", "正常化ガス")]
    [TestCase("confusion gas", "混乱ガス")]
    public void TryTranslateDescription_TranslatesPlainTextGasNameInBurstFrame(string gasName, string expectedGasTranslation)
    {
        var source = "You release a burst of " + gasName + " around yourself.";
        var expected = "周囲に" + expectedGasTranslation + "を噴出する。";

        var translated = GasGenerationDescriptionTranslationPatch.TranslateDescriptionForTests(source);

        Assert.That(translated, Is.EqualTo(expected));
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
