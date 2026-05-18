using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class PsychicHunterGeneratedTitleTranslatorTests
{
    [TestCase("crimson *rank*", "真紅の*rank*")]
    [TestCase("*rank*-in-cobalt", "コバルトに属する*rank*")]
    [TestCase("*rank* in the Psychic circle", "サイキック円環に属する*rank*")]
    [TestCase("*rank* of the sea", "海の*rank*")]
    [TestCase("*rank*-ghost", "幽鬼の*rank*")]
    [TestCase("*rank*, stamped in silver", "銀に刻印された*rank*")]
    [TestCase("*rank* and spouse", "*rank*と伴侶")]
    [TestCase("*rank* and bringer of gout", "*rank*、痛風をもたらす者")]
    [TestCase("stalker", "追跡者")]
    [TestCase("assassin", "暗殺者")]
    [TestCase("entropist", "エントロピスト")]
    public void TryTranslateExpandedText_TranslatesFinitePsychicHunterFragments(string source, string expected)
    {
        var translated = PsychicHunterGeneratedTitleTranslator.TryTranslateExpandedText(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [TestCase("Ptoh's crimson Osprey", "プトフの真紅のオスプレイ")]
    [TestCase("extradimensional snapjaw", "異次元のスナップジョー")]
    [TestCase("esper stalker", "エスパーの追跡者")]
    [TestCase("esper assassin", "エスパーの暗殺者")]
    [TestCase("transdimensional entropist", "超次元のエントロピスト")]
    [TestCase("esper from the Formless ∴", "Formless ∴出身のエスパー")]
    public void TryTranslateTitle_TranslatesPsychicHunterTitles(string source, string expected)
    {
        var translated = PsychicHunterGeneratedTitleTranslator.TryTranslateTitle(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateTitle_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = PsychicHunterGeneratedTitleTranslator.TryTranslateTitle(
            MessageFrameTranslator.DirectTranslationMarker + "esper stalker",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("esper stalker"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("unrelated title")]
    public void TryTranslateTitle_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = PsychicHunterGeneratedTitleTranslator.TryTranslateTitle(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }
}
