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
