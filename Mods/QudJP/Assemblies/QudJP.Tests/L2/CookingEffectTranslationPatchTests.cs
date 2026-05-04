using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CookingEffectTranslationPatchTests
{
    private string localizationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        localizationRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.ResetForTests();
        CookingEffectFragmentTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        CookingEffectFragmentTranslator.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [Test]
    public void Postfix_TranslatesElectricDischargeDescription_WhenPatched()
    {
        var target = new DummyCookingEffectTextTarget
        {
            ReturnValue = "@they release an electrical discharge per Electrical Generation at level 5.",
        };

        var translated = InvokePatched(target, nameof(DummyCookingEffectTextTarget.GetDescription));
        Assert.That(translated, Is.EqualTo("@they は電気生成レベル5の放電を行う。"));
    }

    [Test]
    public void Postfix_TranslatesElectricDamageTrigger_WhenPatched()
    {
        var target = new DummyCookingEffectTextTarget
        {
            ReturnValue = "whenever @thisCreature take@s electric damage, there's a 50% chance",
        };

        var translated = InvokePatched(target, nameof(DummyCookingEffectTextTarget.GetTriggerDescription));
        Assert.That(translated, Is.EqualTo("@thisCreature が電撃ダメージを受けるたび、50%の確率で"));
    }

    [Test]
    public void Postfix_TranslatesReflectDetails_WhenPatched()
    {
        var target = new DummyCookingEffectTextTarget
        {
            ReturnValue = "Reflect 100% damage the next 3 times @they take damage.",
        };

        var translated = InvokePatched(target, nameof(DummyCookingEffectTextTarget.GetDetails));
        Assert.That(translated, Is.EqualTo("@they が次の3回ダメージを受けたとき、そのダメージを100%反射する。"));
    }

    [Test]
    public void Postfix_TranslatesHpIncreaseDescription_WhenPatched()
    {
        var target = new DummyCookingEffectTextTarget
        {
            ReturnValue = "@they get +31% max HP for 1 hour.",
        };

        var translated = InvokePatched(target, nameof(DummyCookingEffectTextTarget.GetDescription));
        Assert.That(translated, Is.EqualTo("@they は1時間のあいだ最大HP+31%を得る。"));
    }

    [Test]
    public void Postfix_TranslatesHpIncreaseDetails_WhenPatched()
    {
        var target = new DummyCookingEffectTextTarget
        {
            ReturnValue = "+31% max HP",
        };

        var translated = InvokePatched(target, nameof(DummyCookingEffectTextTarget.GetDetails));
        Assert.That(translated, Is.EqualTo("最大HP+31%"));
    }

    [TestCase("+12 Acid Resistance", "酸耐性+12")]
    [TestCase("+50-75 Electric Resist", "電気耐性+50-75")]
    [TestCase("+4 Strength", "筋力+4")]
    [TestCase("+10% hit points", "HP+10%")]
    [TestCase("+12 hit points", "HP+12")]
    [TestCase("+6% Move Speed", "移動速度+6%")]
    [TestCase("+4 Move Speed", "移動速度+4")]
    [TestCase("+1 to hit", "命中+1")]
    [TestCase("+5% XP gained", "獲得XP+5%")]
    [TestCase("+1 Intelligence", "知力+1")]
    [TestCase("+10% to natural healing rate", "自然治癒速度+10%")]
    [TestCase("+8-12 to saves vs. bleeding", "出血に対するセーヴ+8-12")]
    [TestCase("Reflect 15-18% damage back at @their attackers, rounded up.", "@their 攻撃者にダメージの15-18%を切り上げて反射する。")]
    [TestCase("whenever @thisCreature take@s damage, there's a 9% chance", "@thisCreature がダメージを受けるたび、9%の確率で")]
    [TestCase("whenever @thisCreature take@s damage, there's a 30-40% chance", "@thisCreature がダメージを受けるたび、30-40%の確率で")]
    [TestCase("whenever @thisCreature take@s damage, there's a 15-20% chance @they start phasing for 8-10 turns.", "@thisCreature がダメージを受けるたび、15-20%の確率で@they は8-10ターンのあいだフェイズアウトする。")]
    [TestCase("Whenever @thisCreature take@s avoidable damage, there's a 20-25% chance @they teleport to a random space on the map instead.", "@thisCreature が回避可能なダメージを受けると20-25%の確率で代わりにマップ内のランダムな地点へテレポートする。")]
    [TestCase("@they gain@s +8 Agility for 50 turns.", "@they は50ターンのあいだ敏捷+8を得る。")]
    [TestCase("@they gain 40-50 Cold Resist for 6 hours.", "@they は6時間のあいだ冷気耐性+40-50を得る。")]
    [TestCase("@they gain 125-175 Electric Resist for 50 turns.", "@they は50ターンのあいだ電気耐性+125-175を得る。")]
    [TestCase("@they gain 40-50 Heat Resist for 6 hours.", "@they は6時間のあいだ熱耐性+40-50を得る。")]
    [TestCase("Can use Electromagnetic Pulse at level 2-3. If @they already have Electromagnetic Pulse, it's enhanced by 3-4 levels.", "〈電磁パルス〉をレベル2～3で使用できる。既に持つ場合、さらにレベル3～4強化される。")]
    [TestCase("Can use Will Force at level 4-5. If @they already have Will Force, it's enhanced by 4-5 levels.", "〈意志の力〉をレベル4～5で使用できる。既に持つ場合、さらにレベル4～5強化される。")]
    [TestCase("Can use Burrowing Claws at level 5-6. If @they already have Burrowing Claws, it's enhanced by 3-4 levels.", "〈掘爪〉をレベル5～6で使用できる。既に持つ場合、さらにレベル3～4強化される。")]
    [TestCase("Can use Psychometry at level 1-2. If @they already have Psychometry, it's enhanced by 3-4 levels.", "〈サイコメトリー〉をレベル1～2で使用できる。既に持つ場合、さらにレベル3～4強化される。")]
    [TestCase("Can use Burgeoning at level 7-8. If @they already have Burgeoning, it's enhanced by 5-6 levels.", "〈バージョニング〉をレベル7～8で使用できる。既に持つ場合、さらにレベル5～6強化される。")]
    [TestCase("Can use Quills at level 5-6. If @they already have Quills, it's enhanced by 3-4 levels.", "〈棘〉をレベル5～6で使用できる。既に持つ場合、さらにレベル3～4強化される。")]
    [TestCase("Can use Sticky Tongue at level 4-5. If @they already have Sticky Tongue, it's enhanced by 4-5 levels.", "〈粘着舌〉をレベル4～5で使用できる。既に持つ場合、さらにレベル4～5強化される。")]
    [TestCase("+2 levels to Quills.", "〈棘〉が+2レベル上昇する。")]
    [TestCase("Can use Intimidate.", "〈威圧〉を使用できる。")]
    [TestCase("+2 bonus on Ego roll when using Intimidate.", "〈威圧〉使用時の意志判定に+2のボーナス。")]
    [TestCase("Can use Intimidate. If @they already have Intimidate, gain a +2 bonus on the Ego roll when using Intimidate.", "〈威圧〉を使用できる。既に習得している場合は〈威圧〉使用時の意志判定に+2のボーナス。")]
    [TestCase("No effect.", "効果なし。")]
    public void Postfix_TranslatesDynamicFragments_WhenPatched(string source, string expected)
    {
        var target = new DummyCookingEffectTextTarget
        {
            ReturnValue = source,
        };

        var translated = InvokePatched(target, nameof(DummyCookingEffectTextTarget.GetDescription));
        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("@they get +31% max HP for 1 hour.", "@they は1時間のあいだ最大HP+31%を得る。")]
    [TestCase("@they get +<color=yellow>31</color>% max HP for 1 hour.", "@they は1時間のあいだ最大HP+<color=yellow>31</color>%を得る。")]
    [TestCase("", "")]
    [TestCase("@they get +31% max DV for 1 hour.", "@they get +31% max DV for 1 hour.")]
    [TestCase("\u0001@they get +31% max HP for 1 hour.", "\u0001@they get +31% max HP for 1 hour.")]
    public void Postfix_HandlesHpIncreaseTemplatedDescriptionCases_WhenPatched(string source, string expected)
    {
        var target = new DummyCookingEffectTextTarget
        {
            ReturnValue = source,
        };

        var translated = InvokePatched(target, nameof(DummyCookingEffectTextTarget.GetTemplatedDescription));
        Assert.That(translated, Is.EqualTo(expected));
    }

    private static string InvokePatched(DummyCookingEffectTextTarget target, string methodName)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCookingEffectTextTarget), methodName),
                postfix: new HarmonyMethod(RequireMethod(typeof(CookingEffectTranslationPatch), nameof(CookingEffectTranslationPatch.Postfix))));

            return (string)RequireMethod(typeof(DummyCookingEffectTextTarget), methodName).Invoke(target, null)!;
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }
}
