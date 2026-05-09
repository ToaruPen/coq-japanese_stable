using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class BeguilingSifrahTranslationPatchTests
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
        nameof(DummyBeguilingSifrahProducerTarget.ResultCriticalFailure),
        "Your coquetry infuriates {{R|機械仕掛けの修道士}}.",
        "{{R|機械仕掛けの修道士}}を口説こうとして怒らせた。",
        "CriticalFailure")]
    [TestCase(
        nameof(DummyBeguilingSifrahProducerTarget.ResultFailure),
        "Your coquetry does not impress {{Y|砂漠の隠者}}.",
        "{{Y|砂漠の隠者}}に口説き文句は響かなかった。",
        "Failure")]
    [TestCase(
        nameof(DummyBeguilingSifrahProducerTarget.ResultPartialSuccess),
        "Your coquetry does not overcome {{C|眠たげな商人}}, but they're interested in hearing more.",
        "{{C|眠たげな商人}}を口説き落とせなかったが、さらに聞きたがっている。",
        "PartialSuccess")]
    [TestCase(
        nameof(DummyBeguilingSifrahProducerTarget.ResultSuccess),
        "{{G|輝く巡礼者 is interested, but unable to join you.}}",
        "{{G|輝く巡礼者は興味を示しているが、あなたに加われない。}}",
        "InterestedButUnable")]
    [TestCase(
        nameof(DummyBeguilingSifrahProducerTarget.ResultExceptionalSuccess),
        "古代の番人 is interested, but unable to join you.",
        "古代の番人は興味を示しているが、あなたに加われない。",
        "InterestedButUnable")]
    public void Patch_TranslatesBeguilingResultPopups_WhenOwnerPatched(
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

            var target = new DummyBeguilingSifrahProducerTarget
            {
                PopupMessageToShow = source,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(BeguilingHitCount(detail), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotTranslateBeguilingPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);

            const string source = "Your coquetry does not impress {{Y|砂漠の隠者}}.";
            DummyPopupShow.Show(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(BeguilingHitCount("Failure"), Is.Zero);
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
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyBeguilingSifrahProducerTarget.ResultFailure)));

            var source = MessageFrameTranslator.MarkDirectTranslation("Your coquetry does not impress {{Y|砂漠の隠者}}.");
            var target = new DummyBeguilingSifrahProducerTarget
            {
                PopupMessageToShow = source,
            };

            target.ResultFailure(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("Your coquetry does not impress {{Y|砂漠の隠者}}."));
                Assert.That(BeguilingHitCount("Failure"), Is.Zero);
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
            PatchOwner(harmony, RequireOwnerMethod(nameof(DummyBeguilingSifrahProducerTarget.ResultFailure)));

            var target = new DummyBeguilingSifrahProducerTarget();

            target.ResultFailure(new DummyGameObject());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(BeguilingHitCount("Failure"), Is.Zero);
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
            prefix: new HarmonyMethod(RequireMethod(typeof(BeguilingSifrahTranslationPatch), nameof(BeguilingSifrahTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(BeguilingSifrahTranslationPatch), nameof(BeguilingSifrahTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyBeguilingSifrahProducerTarget), methodName, typeof(DummyGameObject));
    }

    private static void InvokeOwnerMethod(DummyBeguilingSifrahProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, new object[] { new DummyGameObject() });
    }

    private static int BeguilingHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(BeguilingSifrahTranslationPatch) + "." + detail);
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
