using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TelekinesisTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TestCase(
        "The {{Y|bronze dagger}} does not budge.",
        "{{Y|bronze dagger}}はびくともしない。")]
    [TestCase(
        "{{Y|lead slugs}} do not budge.",
        "{{Y|lead slugs}}はびくともしない。")]
    public void Patch_TranslatesObjectNotBudgePopup_WhenOwnerPatched(string source, string expected)
    {
        AssertTelekinesisPopup(source, expected, expectedHits: 1);
    }

    [Test]
    public void Patch_TranslatesPlayerNotBudgePopup_WhenOwnerPatched()
    {
        AssertTelekinesisPopup("You do not budge.", "あなたはびくともしない。", expectedHits: 1);
    }

    [Test]
    public void Patch_TranslatesExhaustedPsychePopup_WhenActivateOwnerPatched()
    {
        AssertTelekinesisPopup(
            "Your psyche is too exhausted.",
            "精神が疲弊しすぎている。",
            nameof(DummyTelekinesisTarget.Activate),
            expectedHits: 1);
    }

    [Test]
    public void Patch_TranslatesNoTelekineticManipulationPopup_WhenAttemptOwnerPatched()
    {
        AssertTelekinesisPopup(
            "There is nothing you can telekinetically manipulate there.",
            "そこには念動力で操作できるものがない。",
            nameof(DummyTelekinesisTarget.AttemptTelekinesis),
            expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "The {{Y|bronze dagger}} does not budge.";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony);

            DummyPopupShow.ShowFail(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount(), Is.Zero);
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
        const string unmarked = "The bronze dagger does not budge.";

        AssertTelekinesisPopup(
            MessageFrameTranslator.MarkDirectTranslation(unmarked),
            unmarked,
            expectedHits: 0);
    }

    [Test]
    public void Patch_LeavesDirectMarkedUnsupportedPopupUnchanged_WhenOwnerPatched()
    {
        const string unmarked = "You do not budge.";

        AssertTelekinesisPopup(
            MessageFrameTranslator.MarkDirectTranslation(unmarked),
            unmarked,
            expectedHits: 0);
    }

    [TestCase("")]
    public void Patch_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched(string source)
    {
        AssertTelekinesisPopup(source, source, expectedHits: 0);
    }

    private static void AssertTelekinesisPopup(string source, string expected, int expectedHits)
    {
        AssertTelekinesisPopup(source, expected, nameof(DummyTelekinesisTarget.HandleEvent), expectedHits);
    }

    private static void AssertTelekinesisPopup(string source, string expected, string ownerMethodName, int expectedHits)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony);
            PatchOwner(harmony, ownerMethodName);

            var target = new DummyTelekinesisTarget
            {
                PopupMessageToShow = source,
            };

            InvokeOwner(target, ownerMethodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(), Is.EqualTo(expectedHits));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopup(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, string ownerMethodName)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyTelekinesisTarget), ownerMethodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(TelekinesisTranslationPatch), nameof(TelekinesisTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(TelekinesisTranslationPatch), nameof(TelekinesisTranslationPatch.Finalizer))));
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(TelekinesisTranslationPatch));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void InvokeOwner(DummyTelekinesisTarget target, string ownerMethodName)
    {
        _ = RequireMethod(typeof(DummyTelekinesisTarget), ownerMethodName).Invoke(target, null);
    }

    private sealed class DummyTelekinesisTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool HandleEvent()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Activate()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow, CopyScrap: false);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool AttemptTelekinesis()
        {
            DummyPopupShow.ShowFail(PopupMessageToShow, DimBackground: false);
            return true;
        }
    }
}
