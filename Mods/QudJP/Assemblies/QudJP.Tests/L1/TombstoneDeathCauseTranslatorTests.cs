using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class TombstoneDeathCauseTranslatorTests
{
    [TestCase("Died of old age", "老衰で死亡した")]
    [TestCase("Died of magmatic causes", "マグマ性の原因で死亡した")]
    [TestCase("Tricked into jumping into a pool of lava", "溶岩の池へ飛び込むようだまされて死亡した")]
    [TestCase("Succumbed to glotrot.", "グロットロットに倒れた。")]
    [TestCase("Stabbed to death by a snapjaw", "snapjawに刺殺された")]
    [TestCase("Killed in a duel over a jeweled dagger", "jeweled daggerをめぐる決闘で死亡した")]
    [TestCase("Injected one salve injector too many", "salve injectorを一本多く注射し過ぎた")]
    [TestCase("Released a canister of sleep gas in a locked room", "鍵のかかった部屋でsleep gasのキャニスターを放出した")]
    [TestCase("Became obsessed with The Accounting of Qud and forgot to eat", "Accounting of Qudに取りつかれて食事を忘れた")]
    public void TryTranslate_TranslatesFiniteDeathCauseFrames(string source, string expected)
    {
        var translated = TombstoneDeathCauseTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        var translated = TombstoneDeathCauseTranslator.TryTranslate("{{R|Died of old age}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{R|老衰で死亡した}}"));
        });
    }

    [Test]
    public void TryTranslate_PreservesLocalizedCapture()
    {
        var translated = TombstoneDeathCauseTranslator.TryTranslate("Poisoned by 監視官イラメ", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("監視官イラメに毒殺された"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var source = MessageFrameTranslator.DirectTranslationMarker + "Died of old age";

        var translated = TombstoneDeathCauseTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Died of old age"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("Died while doing something untracked")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = TombstoneDeathCauseTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }
}
