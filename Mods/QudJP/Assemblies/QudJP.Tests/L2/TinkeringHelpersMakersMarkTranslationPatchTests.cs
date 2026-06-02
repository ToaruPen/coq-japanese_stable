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
        DummyTinkeringHelpersTarget.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupGenericTarget.Reset();
        DummyTinkeringHelpersTarget.ResetForTests();
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

    [Test]
    public void Patch_StripsDirectMarkedMakersMarkPrompts_WhenOwnerPatched()
    {
        DummyTinkeringHelpersTarget.PickOptionTitleToSend =
            MessageFrameTranslator.MarkDirectTranslation("作り手の印を選ぶ。");
        DummyTinkeringHelpersTarget.PickOptionOptionsToSend =
            new[] { MessageFrameTranslator.MarkDirectTranslation("なし") };
        DummyTinkeringHelpersTarget.ColorPickerTitleToSend =
            MessageFrameTranslator.MarkDirectTranslation("作り手の印の色を選ぶ。");

        WithPatchedOwner(() => DummyTinkeringHelpersTarget.CheckMakersMark());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("作り手の印を選ぶ。"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "なし" }));
            Assert.That(DummyPopupGenericTarget.LastShowColorPickerTitle, Is.EqualTo("作り手の印の色を選ぶ。"));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "Select"), Is.Zero);
            Assert.That(HitCount(nameof(PopupShowColorPickerTranslationPatch), "Color"), Is.Zero);
        });
    }

    [Test]
    public void Patch_TranslatesColorTaggedMakersMarkPrompts_WhenOwnerPatched()
    {
        DummyTinkeringHelpersTarget.PickOptionTitleToSend = "{{C|Select your maker's mark.}}";
        DummyTinkeringHelpersTarget.PickOptionOptionsToSend = new[] { "{{C|none}}" };
        DummyTinkeringHelpersTarget.ColorPickerTitleToSend = "{{C|Choose a color for your maker's mark.}}";

        WithPatchedOwner(() => DummyTinkeringHelpersTarget.CheckMakersMark());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("{{C|作り手の印を選ぶ。}}"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "{{C|なし}}" }));
            Assert.That(DummyPopupGenericTarget.LastShowColorPickerTitle, Is.EqualTo("{{C|作り手の印の色を選ぶ。}}"));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "Select"), Is.EqualTo(1));
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "None"), Is.Zero);
            Assert.That(HitCount(nameof(PopupShowColorPickerTranslationPatch), "Color"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_LeavesEmptyMakersMarkPromptsUnclaimed_WhenOwnerPatched()
    {
        DummyTinkeringHelpersTarget.PickOptionTitleToSend = string.Empty;
        DummyTinkeringHelpersTarget.PickOptionOptionsToSend = new[] { string.Empty };
        DummyTinkeringHelpersTarget.ColorPickerTitleToSend = string.Empty;

        WithPatchedOwner(() => DummyTinkeringHelpersTarget.CheckMakersMark());

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.Empty);
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { string.Empty }));
            Assert.That(DummyPopupGenericTarget.LastShowColorPickerTitle, Is.Empty);
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "Select"), Is.Zero);
            Assert.That(HitCount(nameof(PopupPickOptionTranslationPatch), "None"), Is.Zero);
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
            harmony.Patch(
                original: RequireMethod(typeof(DummyTinkeringHelpersTarget), nameof(DummyTinkeringHelpersTarget.CheckMakersMark)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(TinkeringHelpersMakersMarkTranslationPatch),
                    nameof(TinkeringHelpersMakersMarkTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(
                    typeof(TinkeringHelpersMakersMarkTranslationPatch),
                    nameof(TinkeringHelpersMakersMarkTranslationPatch.Finalizer))));
            action();
        }
        finally
        {
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
