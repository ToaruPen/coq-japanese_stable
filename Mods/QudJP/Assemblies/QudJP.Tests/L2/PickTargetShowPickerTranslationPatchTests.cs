using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PickTargetShowPickerTranslationPatchTests
{
    private const string PickTargetShowPickerOwner = "XRL.UI.PickTarget|ShowPicker";

    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
    }

    [Test]
    public void ShowPicker_TranslatesRangeFailurePopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "You must select a location within 7 tiles!",
            "7マス以内の場所を選択しなければならない！",
            expectedHits: 1);
    }

    [Test]
    public void ShowPicker_PreservesWholePopupColor_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "{{R|You must select a location within 12 tiles!}}",
            "{{R|12マス以内の場所を選択しなければならない！}}",
            expectedHits: 1);
    }

    [Test]
    public void ShowPicker_DoesNotClaimPopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);

            DummyPopupShow.ShowFail("You must select a location within 7 tiles!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You must select a location within 7 tiles!"));
                Assert.That(HitCount(), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void ShowPicker_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation("You must select a location within 7 tiles!"),
            "You must select a location within 7 tiles!",
            expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("You may only select a visible square!")]
    [TestCase("You may only select an explored square!")]
    [TestCase("You must select a location within nearby tiles!")]
    public void ShowPicker_LeavesUnsupportedPopupsUnchanged_WhenOwnerPatched(string source)
    {
        AssertPopupMessage(source, source, expectedHits: 0);
    }

    private static void AssertPopupMessage(string source, string expected, int expectedHits)
    {
        var ownerRoute = CreateOwnerRouteFromKey(PickTargetShowPickerOwner);
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowFail(harmony);
            PatchOwner(harmony, ownerRoute.Method);

            ownerRoute.Invoke(() => DummyPopupShow.ShowFail(source));

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

    private static void PatchPopupShowFail(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo ownerMethod)
    {
        harmony.Patch(
            original: ownerMethod,
            prefix: new HarmonyMethod(RequireMethod(typeof(PickTargetShowPickerTranslationPatch), nameof(PickTargetShowPickerTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(PickTargetShowPickerTranslationPatch),
                nameof(PickTargetShowPickerTranslationPatch.Finalizer),
                typeof(Exception))));
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

    private static DynamicOwnerRouteMethod CreateOwnerRouteFromKey(string ownerKey)
    {
        var separator = ownerKey.LastIndexOf('|');
        return DynamicOwnerRouteMethod.Create(ownerKey[..separator], ownerKey[(separator + 1)..]);
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.PickTargetShowPickerTranslationPatch.RangeFailure");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
