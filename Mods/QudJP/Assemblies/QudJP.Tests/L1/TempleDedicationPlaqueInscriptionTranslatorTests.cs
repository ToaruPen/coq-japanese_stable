using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class TempleDedicationPlaqueInscriptionTranslatorTests
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
    public void TryTranslate_TranslatesDedicationFrame_AndReconstructsGeneratedCaptures()
    {
        const string source =
            "This temple was built in 638,01qy by the Exhaustiers' Guild, who detached from their egregore " +
            "Square Wheel in the Chrome Era.";

        var translated = TempleDedicationPlaqueInscriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                result,
                Is.EqualTo("この寺院は638,01qyにthe Exhaustiers' Guildによって建てられた。彼らはクロムの時代に、エグレゴア「四角車輪」から分離した。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        const string source =
            "{{Y|This temple was built in 638,01qy by the Exhaustiers' Guild, who detached from their egregore " +
            "Square Wheel in the Chrome Era.}}";

        var translated = TempleDedicationPlaqueInscriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                result,
                Is.EqualTo("{{Y|この寺院は638,01qyにthe Exhaustiers' Guildによって建てられた。彼らはクロムの時代に、エグレゴア「四角車輪」から分離した。}}"));
        });
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForUnknownFrame()
    {
        var translated = TempleDedicationPlaqueInscriptionTranslator.TryTranslate(
            "A different inscription.",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("A different inscription."));
        });
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForEmptyInput()
    {
        var translated = TempleDedicationPlaqueInscriptionTranslator.TryTranslate(null, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.Empty);
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutTranslation()
    {
        var translated = TempleDedicationPlaqueInscriptionTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "This temple was built in 638,01qy by the Exhaustiers' Guild, who detached from their egregore Square Wheel in the Chrome Era.",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(
                result,
                Is.EqualTo("This temple was built in 638,01qy by the Exhaustiers' Guild, who detached from their egregore Square Wheel in the Chrome Era."));
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
