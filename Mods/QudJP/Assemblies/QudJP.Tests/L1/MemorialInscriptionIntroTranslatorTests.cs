using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class MemorialInscriptionIntroTranslatorTests
{
    [TestCase("Here Lies", "ここに眠る")]
    [TestCase("Rest in Peace", "安らかに眠れ")]
    [TestCase("Here Rests", "ここに憩う")]
    [TestCase("Here Lies the Body of", "ここにその身を横たえる")]
    [TestCase("Here Rests the Body of", "ここにその身を休める")]
    [TestCase("In Memory of", "追憶")]
    [TestCase("In Loving Memory of", "愛しき追憶")]
    [TestCase("Here Lie the Remains of", "ここに遺骸眠る")]
    [TestCase("Here Rests in the Light of Friends", "友らの光の中に眠る")]
    [TestCase("Rest with Friends", "友らとともに眠れ")]
    [TestCase("Under this Reef Lies", "この礁の下に眠る")]
    [TestCase("Dream by the Light of Our Freehold", "われらの自由保有地の光に夢見よ")]
    [TestCase("Here Rests in the Light of Gjaus", "ジャウスの光の中に眠る")]
    [TestCase("Here Sheltered under Gjaus is", "ジャウスの庇護の下、ここに眠る")]
    [TestCase("Rest in the Light of Gjaus", "ジャウスの光の中で眠れ")]
    [TestCase("Dream in Peace", "安らかに夢見よ")]
    [TestCase("Dream in the Light of Gjaus", "ジャウスの光の中に夢見よ")]
    public void TryTranslate_TranslatesKnownIntroFrame(string source, string expected)
    {
        var translated = MemorialInscriptionIntroTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeColorWrapper()
    {
        var translated = MemorialInscriptionIntroTranslator.TryTranslate("{{Y|Here Lies}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y|ここに眠る}}"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var source = MessageFrameTranslator.DirectTranslationMarker + "Here Lies";

        var translated = MemorialInscriptionIntroTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Here Lies"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("An unknown memorial line")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = MemorialInscriptionIntroTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }
}
