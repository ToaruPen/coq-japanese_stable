using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class GameTextDeathReasonTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void TranslateThirdPersonDeathReason_LeavesUnknownReasonUnchanged()
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests("snapjaw was splatted."),
            Is.EqualTo("snapjaw was splatted."));
    }

    [TestCase("snapjaw was vaporized.", "スナップジョーは蒸発した。")]
    [TestCase("snapjaws were vaporized.", "snapjawsは蒸発した。")]
    [TestCase("snapjaw was vaporized", "snapjaw was vaporized")]
    [TestCase("snapjaw was vaporized!", "snapjaw was vaporized!")]
    [TestCase("", "")]
    public void TranslateThirdPersonDeathReason_HandlesWasWerePunctuationAndEmptyCases(
        string source,
        string expected)
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests(source),
            Is.EqualTo(expected));
    }

    [Test]
    public void TranslateThirdPersonDeathReason_PreservesColorTaggedSubject()
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests("{{R|snapjaw}} was vaporized."),
            Is.EqualTo("{{R|スナップジョー}}は蒸発した。"));
    }

    [Test]
    public void TranslateThirdPersonDeathReason_StripsDirectMarkerInsideColorTaggedSubject()
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests("{{R|\u0001snapjaw}} was vaporized."),
            Is.EqualTo("{{R|スナップジョー}}は蒸発した。"));
    }

    [Test]
    public void TranslateThirdPersonDeathReason_StripsDirectMarkerWithoutRetranslating()
    {
        Assert.That(
            GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests(
                MessageFrameTranslator.MarkDirectTranslation("snapjaw was vaporized.")),
            Is.EqualTo("snapjaw was vaporized."));
    }

    [Test]
    public void TranslateThirdPersonDeathReason_UsesSingleJapaneseSentenceEnd()
    {
        var result = GameTextDeathReasonTranslationPatch.TranslateThirdPersonDeathReasonForTests("snapjaw was vaporized.");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("スナップジョーは蒸発した。"));
            Assert.That(result.Count(character => character == '。'), Is.EqualTo(1));
        });
    }
}
