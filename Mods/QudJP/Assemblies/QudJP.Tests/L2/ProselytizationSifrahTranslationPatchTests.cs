using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ProselytizationSifrahTranslationPatchTests
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
        nameof(DummyProselytizationSifrahProducerTarget.ResultCriticalFailure),
        "{{R|怒れる遊牧民}} is offended by your impertinence.",
        "{{R|怒れる遊牧民}}はあなたの無礼に気分を害した。",
        "CriticalFailure")]
    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultFailure),
        "{{Y|砂漠の隠者}} is unconvinced by your pleas.",
        "{{Y|砂漠の隠者}}はあなたの懇願に納得しなかった。",
        "Failure")]
    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultPartialSuccess),
        "{{C|眠たげな商人}} is unconvinced by your pleas, but interested in hearing more.",
        "{{C|眠たげな商人}}はあなたの懇願に納得しなかったが、さらに聞きたがっている。",
        "PartialSuccess")]
    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultSuccess),
        "{{G|輝く巡礼者 is sympathetic, but unable to join you.}}",
        "{{G|輝く巡礼者は同情的だが、あなたに加われない。}}",
        "SympatheticButUnable")]
    [TestCase(
        nameof(DummyProselytizationSifrahProducerTarget.ResultExceptionalSuccess),
        "古代の番人 are sympathetic, but unable to join you.",
        "古代の番人は同情的だが、あなたに加われない。",
        "SympatheticButUnable")]
    public void Patch_TranslatesProselytizationResultPopups_WhenOwnerPatched(
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

            var target = new DummyProselytizationSifrahProducerTarget
            {
                PopupMessageToShow = source,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(ProselytizationHitCount(detail), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotTranslateProselytizationPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            const string source = "{{Y|砂漠の隠者}} is unconvinced by your pleas.";
            DummyPopupShow.Show(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(ProselytizationHitCount("Failure"), Is.Zero);
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
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyProselytizationSifrahProducerTarget.ResultFailure)));

            var source = MessageFrameTranslator.MarkDirectTranslation("{{Y|砂漠の隠者}} is unconvinced by your pleas.");
            var target = new DummyProselytizationSifrahProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.ResultFailure(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|砂漠の隠者}} is unconvinced by your pleas."));
                Assert.That(ProselytizationHitCount("Failure"), Is.Zero);
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
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyProselytizationSifrahProducerTarget.ResultFailure)));

            var target = new DummyProselytizationSifrahProducerTarget();

            target.ResultFailure(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(ProselytizationHitCount("Failure"), Is.Zero);
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
            prefix: new HarmonyMethod(RequireMethod(typeof(ProselytizationSifrahTranslationPatch), nameof(ProselytizationSifrahTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(ProselytizationSifrahTranslationPatch), nameof(ProselytizationSifrahTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyProselytizationSifrahProducerTarget), methodName, typeof(DummyGameObject));
    }

    private static void InvokeOwnerMethod(DummyProselytizationSifrahProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, new object[] { new DummyGameObject() });
    }

    private static int ProselytizationHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(ProselytizationSifrahTranslationPatch) + "." + detail);
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
