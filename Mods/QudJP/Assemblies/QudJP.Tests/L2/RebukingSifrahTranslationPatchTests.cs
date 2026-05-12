using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class RebukingSifrahTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(
        nameof(DummyRebukingSifrahProducerTarget.ResultCriticalFailure),
        "{{R|機械仕掛けの修道士}} is enraged by your poor reasoning.",
        "{{R|機械仕掛けの修道士}}はあなたの拙い論理に激怒した。",
        "CriticalFailure")]
    [TestCase(
        nameof(DummyRebukingSifrahProducerTarget.ResultPartialSuccess),
        "{{C|眠たげな機械}} wanders away disinterestedly.",
        "{{C|眠たげな機械}}は興味なさげに立ち去った。",
        "PartialSuccess")]
    public void Patch_TranslatesRebukingResultPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireOwnerMethod(methodName));

            var target = new DummyRebukingSifrahProducerTarget
            {
                PopupMessageToShow = source,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(RebukingHitCount(detail), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotTranslateRebukingPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            const string source = "{{Y|砂漠の機械}} wanders away disinterestedly.";
            DummyPopupShow.Show(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(RebukingHitCount("PartialSuccess"), Is.Zero);
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
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRebukingSifrahProducerTarget.ResultPartialSuccess)));

            var source = MessageFrameTranslator.MarkDirectTranslation("{{Y|砂漠の機械}} wanders away disinterestedly.");
            var target = new DummyRebukingSifrahProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.ResultPartialSuccess(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|砂漠の機械}} wanders away disinterestedly."));
                Assert.That(RebukingHitCount("PartialSuccess"), Is.Zero);
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
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyRebukingSifrahProducerTarget.ResultPartialSuccess)));

            var target = new DummyRebukingSifrahProducerTarget();

            target.ResultPartialSuccess(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(RebukingHitCount("PartialSuccess"), Is.Zero);
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
            prefix: new HarmonyMethod(RequireMethod(typeof(RebukingSifrahTranslationPatch), nameof(RebukingSifrahTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(RebukingSifrahTranslationPatch), nameof(RebukingSifrahTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyRebukingSifrahProducerTarget), methodName, typeof(DummyGameObject));
    }

    private static void InvokeOwnerMethod(DummyRebukingSifrahProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, new object[] { new DummyGameObject() });
    }

    private static int RebukingHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(RebukingSifrahTranslationPatch) + "." + detail);
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
}
