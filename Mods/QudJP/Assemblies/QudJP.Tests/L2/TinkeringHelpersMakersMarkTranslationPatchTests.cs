using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TinkeringHelpersMakersMarkTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupGenericTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupGenericTarget.Reset();
        TinkeringHelpersMakersMarkTranslationPatch.ResetForTests();
    }

    [Test]
    public void Patch_TranslatesMakersMarkPickerAndColorPrompt_WhenOwnerPatched()
    {
        WithPatchedOwner(() => DummyTinkeringHelpersTarget.CheckMakersMark());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("作り手の印を選ぶ。"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "none", "{{C|star}}" }));
            Assert.That(DummyPopupGenericTarget.LastShowColorPickerTitle, Is.EqualTo("作り手の印の色を選ぶ。"));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "Select"), Is.EqualTo(1));
            Assert.That(HitCount(nameof(PopupShowColorPickerTranslationPatch), "Color"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_DoesNotClaimMakersMarkPrompts_WhenOwnerAbsent()
    {
        WithPatchedPopupRoutesOnly(() => DummyTinkeringHelpersTarget.CheckMakersMark());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("Select your maker's mark."));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "none", "{{C|star}}" }));
            Assert.That(DummyPopupGenericTarget.LastShowColorPickerTitle, Is.EqualTo("Choose a color for your maker's mark."));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "Select"), Is.Zero);
            Assert.That(HitCount(nameof(PopupShowColorPickerTranslationPatch), "Color"), Is.Zero);
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupRoutes(harmony);
            TinkeringHelpersMakersMarkTranslationPatch.Prefix();
            action();
        }
        finally
        {
            _ = TinkeringHelpersMakersMarkTranslationPatch.Finalizer(null);
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupRoutesOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupRoutes(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupRoutes(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Finalizer))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.ShowColorPicker)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowColorPickerTranslationPatch), nameof(PopupShowColorPickerTranslationPatch.Prefix))));
    }

    private static int HitCount(string route, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            route,
            "Popup.ProducerText." + nameof(TinkeringHelpersMakersMarkTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
