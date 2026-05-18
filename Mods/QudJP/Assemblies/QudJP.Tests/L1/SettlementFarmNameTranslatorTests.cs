using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class SettlementFarmNameTranslatorTests
{
    [TestCase("a secluded pig farm", "人里離れた豚農場")]
    [TestCase("a remote starapple farm", "辺境のスターアップル農場")]
    [TestCase("Urist Farm", "Uristの農場")]
    [TestCase("Urist's Ranch", "Uristの牧場")]
    [TestCase("Farmers' Farm", "Farmersの農場")]
    [TestCase("the Urist Shire", "Uristの村郡")]
    [TestCase("the Shire of Urist", "Uristの村郡")]
    [TestCase("Mudshire", "泥村郡")]
    [TestCase("Applehearth", "リンゴ炉辺")]
    public void TryTranslate_TranslatesGeneratedFarmNames(string source, string expected)
    {
        var translated = SettlementFarmNameTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = SettlementFarmNameTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "Urist Farm",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Urist Farm"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("Joppa")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = SettlementFarmNameTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }
}
