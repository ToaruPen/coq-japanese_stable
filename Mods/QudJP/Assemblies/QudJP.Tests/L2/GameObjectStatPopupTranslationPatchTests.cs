using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GameObjectStatPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(nameof(DummyGameObjectStatPopupProducerTarget.GainSP), "You gain {{C|4}} skill points!", "スキルポイントを{{C|4}}獲得した！", "SkillPointGain")]
    [TestCase(nameof(DummyGameObjectStatPopupProducerTarget.GainEgo), "Your Ego is increased by {{G|2}}!", "自我が{{G|2}}増加した！", "StatIncrease")]
    [TestCase(nameof(DummyGameObjectStatPopupProducerTarget.LoseEgo), "Your Ego is decreased by {{R|1}}!", "自我が{{R|1}}減少した！", "StatDecrease")]
    [TestCase(nameof(DummyGameObjectStatPopupProducerTarget.GainIntelligence), "Your Intelligence is increased by {{G|3}}!", "知力が{{G|3}}増加した！", "StatIncrease")]
    [TestCase(nameof(DummyGameObjectStatPopupProducerTarget.GainWillpower), "Your Willpower is increased by {{G|5}}!", "意志力が{{G|5}}増加した！", "StatIncrease")]
    public void Patch_TranslatesStatPopup_WhenOwnerPatched(string methodName, string source, string expected, string detail)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireStatMethod(methodName));

            var target = new DummyGameObjectStatPopupProducerTarget
            {
                PopupMessageToShow = source,
            };

            InvokeStatMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(StatHitCount(detail), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotTranslateStatPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You gain {{C|4}} skill points!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You gain {{C|4}} skill points!"));
                Assert.That(StatHitCount("SkillPointGain"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireStatMethod(nameof(DummyGameObjectStatPopupProducerTarget.GainSP)));

            var target = new DummyGameObjectStatPopupProducerTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation("You gain {{C|4}} skill points!"),
            };

            target.GainSP(4);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You gain {{C|4}} skill points!"));
                Assert.That(StatHitCount("SkillPointGain"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireStatMethod(nameof(DummyGameObjectStatPopupProducerTarget.GainSP)));

            var target = new DummyGameObjectStatPopupProducerTarget
            {
                PopupMessageToShow = string.Empty,
            };

            target.GainSP(4);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(StatHitCount("SkillPointGain"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireStatMethod(nameof(DummyGameObjectStatPopupProducerTarget.GainEgo)));

            var target = new DummyGameObjectStatPopupProducerTarget
            {
                PopupMessageToShow = "{{W|Your Ego is increased by {{G|2}}!}}",
            };

            target.GainEgo(2);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{W|自我が{{G|2}}増加した！}}"));
                Assert.That(StatHitCount("StatIncrease"), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(GameObjectStatPopupTranslationPatch), nameof(GameObjectStatPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(GameObjectStatPopupTranslationPatch), nameof(GameObjectStatPopupTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireStatMethod(string methodName)
    {
        return RequireMethod(typeof(DummyGameObjectStatPopupProducerTarget), methodName, typeof(int), typeof(bool));
    }

    private static void InvokeStatMethod(DummyGameObjectStatPopupProducerTarget target, string methodName)
    {
        _ = RequireStatMethod(methodName).Invoke(target, new object[] { 1, true });
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                   ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static int StatHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(GameObjectStatPopupTranslationPatch) + "." + detail);
    }
}
