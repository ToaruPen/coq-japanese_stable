using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class SultanRegionRevealDescriptionTranslatorTests
{
    [Test]
    public void TryTranslate_TranslatesAncientGovernmentFrame()
    {
        const string source =
            "Over the flats and under the chrome carcasses of giants, here stretches the ancient republic where the Eaters dwelled and dreamed.";

        var translated = SultanRegionRevealDescriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                result,
                Is.EqualTo("平原の上と巨人たちのクロムの残骸の下、ここには古代の共和国が広がり、イーターたちが住まい、夢見ていた。"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesLostGovernmentFrame()
    {
        const string source =
            "The Eaters admired their strange flora in the vanished city-state whose ruins lie over the flats.";

        var translated = SultanRegionRevealDescriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                result,
                Is.EqualTo("消え失せた都市国家ではイーターたちが奇妙な植物群を愛でていた。その遺跡は平原の上に横たわっている。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        const string source =
            "{{Y|The Eaters admired their strange flora in the lost province whose ruins lie over the flats.}}";

        var translated = SultanRegionRevealDescriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                result,
                Is.EqualTo("{{Y|失われた州ではイーターたちが奇妙な植物群を愛でていた。その遺跡は平原の上に横たわっている。}}"));
        });
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForUnknownFrame()
    {
        var translated = SultanRegionRevealDescriptionTranslator.TryTranslate("A different region.", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("A different region."));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    public void TryTranslate_ReturnsFalse_ForEmptyInput(string? source)
    {
        var translated = SultanRegionRevealDescriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutTranslation()
    {
        var translated = SultanRegionRevealDescriptionTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "The Eaters admired their strange flora in the lost province whose ruins lie over the flats.",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(
                result,
                Is.EqualTo("The Eaters admired their strange flora in the lost province whose ruins lie over the flats."));
        });
    }
}
