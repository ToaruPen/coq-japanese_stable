using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class CookingEffectFragmentTranslatorTests
{
    [TestCase("@they release an electrical discharge per Electrical Generation at level 5.", "@they は電気生成レベル5の放電を行う。")]
    [TestCase("@they release an electromagnetic pulse at level 8-9.", "@they はレベル8-9の電磁パルスを放つ。")]
    [TestCase("@they release an electromagnetic pulse at level <color=yellow>8-9</color>.", "@they はレベル<color=yellow>8-9</color>の電磁パルスを放つ。")]
    [TestCase("whenever @thisCreature take@s electric damage, there's a 50% chance", "@thisCreature が電撃ダメージを受けるたび、50%の確率で")]
    [TestCase("whenever @thisCreature take@s electric damage, there's an 80% chance", "@thisCreature が電撃ダメージを受けるたび、80%の確率で")]
    [TestCase("whenever @thisCreature suffer@s 2X or greater physical penetration,", "@thisCreature が2倍以上の物理貫通を受けるたび、")]
    [TestCase("Reflect 100% damage the next time @they take damage.", "@they が次にダメージを受けたとき、そのダメージを100%反射する。")]
    [TestCase("Reflect 100% damage the next 3 times @they take damage.", "@they が次の3回ダメージを受けたとき、そのダメージを100%反射する。")]
    [TestCase("@they get +31% max HP for 1 hour.", "@they は1時間のあいだ最大HP+31%を得る。")]
    [TestCase("@they get +30-40% max HP for 1 hour.", "@they は1時間のあいだ最大HP+30-40%を得る。")]
    [TestCase("@they get +<color=yellow>31</color>% max HP for 1 hour.", "@they は1時間のあいだ最大HP+<color=yellow>31</color>%を得る。")]
    [TestCase("+12% max HP", "最大HP+12%")]
    [TestCase("+10-15% max HP", "最大HP+10-15%")]
    [TestCase("+<color=yellow>31</color>% max HP", "最大HP+<color=yellow>31</color>%")]
    [TestCase("+31% max HP", "最大HP+31%")]
    public void TryTranslate_TranslatesConfiguredFragments(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    [TestCase("")]
    [TestCase("@they get +31% max DV for 1 hour.")]
    [TestCase("\u0001@they get +31% max HP for 1 hour.")]
    [TestCase("whenever @thisCreature take@s fire damage, there's a 50% chance")]
    [TestCase("\u0001Reflect 100% damage the next time @they take damage.")]
    public void TryTranslate_ReturnsFalse_ForPassthroughFragments(string input)
    {
        AssertPassthrough(input);
    }

    private static void AssertTranslated(string input, string expected)
    {
        var ok = CookingEffectFragmentTranslator.TryTranslate(
            input,
            nameof(CookingEffectFragmentTranslatorTests),
            "Cooking",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    private static void AssertPassthrough(string input)
    {
        var ok = CookingEffectFragmentTranslator.TryTranslate(
            input,
            nameof(CookingEffectFragmentTranslatorTests),
            "Cooking",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(input));
        });
    }
}
