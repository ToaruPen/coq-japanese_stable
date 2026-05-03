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
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
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
