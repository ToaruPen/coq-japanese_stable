using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FloatingEquipmentPopupTranslationPatchTests
{
    private const string CeaseSource = "The {{Y|hover boots}} cease floating near you.";
    private const string FallSource = "The {{Y|hover boots}} fall to the ground.";
    private const string PickSource = "The {{Y|magnetized dagger}} falls to the ground; you pick it up.";
    private const string ScoopSource = "The {{Y|floating orb}} falls to the ground; you scoop it up.";
    private const string CeaseTranslated = "{{Y|hover boots}}はあなたの近くで浮遊するのをやめた";
    private const string FallTranslated = "{{Y|hover boots}}は地面に倒れた。";
    private const string PickTranslated = "{{Y|magnetized dagger}}は地面に落ちた。あなたはそれを拾った。";
    private const string ScoopTranslated = "{{Y|floating orb}}は地面に落ちた。あなたはそれをすくい上げた。";

    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(RepositoryDictionaryDirectory());
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(RepositoryMessageFramePath());
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(nameof(DummyFloatingEquipmentProducer.PoweredCheckFloating), CeaseSource, CeaseTranslated, "FloatingEquipmentCease")]
    [TestCase(nameof(DummyFloatingEquipmentProducer.PoweredCheckFloating), FallSource, FallTranslated, "FloatingEquipmentFall")]
    [TestCase(nameof(DummyFloatingEquipmentProducer.PoweredCheckFloating), ScoopSource, ScoopTranslated, "FloatingEquipmentFall")]
    [TestCase(nameof(DummyFloatingEquipmentProducer.MagnetizedCheckFloating), PickSource, PickTranslated, "FloatingEquipmentFall")]
    [TestCase(nameof(DummyFloatingEquipmentProducer.MagnetizedCheckFloating), FallSource, FallTranslated, "FloatingEquipmentFall")]
    public void Patch_TranslatesFloatingEquipmentPopup_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string expectedFamilySuffix)
    {
        RunWithOwnerAndPopupPatches(methodName, () =>
        {
            var target = new DummyFloatingEquipmentProducer
            {
                PopupMessageToShow = source,
            };

            InvokeProducer(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.Show." + expectedFamilySuffix),
                    Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        RunWithPopupPatchOnly(() => DummyPopupShow.Show(CeaseSource));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(CeaseSource));
            Assert.That(GetCeaseHitCount(), Is.EqualTo(0));
        });
    }

    [Test]
    public void Patch_StripsDirectMarkedPopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(CeaseSource);

        RunWithOwnerAndPopupPatches(nameof(DummyFloatingEquipmentProducer.PoweredCheckFloating), () =>
        {
            var target = new DummyFloatingEquipmentProducer
            {
                PopupMessageToShow = source,
            };

            target.PoweredCheckFloating();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(CeaseSource));
                Assert.That(GetCeaseHitCount(), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        RunWithOwnerAndPopupPatches(nameof(DummyFloatingEquipmentProducer.PoweredCheckFloating), () =>
        {
            var target = new DummyFloatingEquipmentProducer
            {
                PopupMessageToShow = string.Empty,
            };

            target.PoweredCheckFloating();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
        });
    }

    private static string RepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }

    private static string RepositoryMessageFramePath()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "MessageFrames",
                "verbs.ja.json"));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return (parameters.Length == 0
                ? AccessTools.Method(type, methodName)
                : AccessTools.Method(type, methodName, parameters))
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void RunWithPopupPatchOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithOwnerAndPopupPatches(string methodName, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFloatingEquipmentProducer), methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(FloatingEquipmentPopupTranslationPatch), nameof(FloatingEquipmentPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(FloatingEquipmentPopupTranslationPatch), nameof(FloatingEquipmentPopupTranslationPatch.Finalizer), typeof(Exception))));
            PatchPopupShow(harmony);
            action();
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

    private static void InvokeProducer(DummyFloatingEquipmentProducer target, string methodName)
    {
        if (methodName == nameof(DummyFloatingEquipmentProducer.PoweredCheckFloating))
        {
            target.PoweredCheckFloating();
            return;
        }

        target.MagnetizedCheckFloating();
    }

    private static int GetCeaseHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.FloatingEquipmentCease");
    }

    private sealed class DummyFloatingEquipmentProducer
    {
        public string PopupMessageToShow = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PoweredCheckFloating()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void MagnetizedCheckFloating()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
