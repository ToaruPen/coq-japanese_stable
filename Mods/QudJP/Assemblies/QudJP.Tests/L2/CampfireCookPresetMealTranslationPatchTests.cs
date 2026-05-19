using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfireCookPresetMealTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
    }

    [Test]
    public void CookPresetMeal_TranslatesAteMealPopup_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookPresetMealTarget
            {
                PopupMessageToShow = "You eat the meal.",
            }.CookPresetMeal(0);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("食事をとった。"));
                Assert.That(HitCount("AteMeal"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void CookPresetMeal_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You eat the meal.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You eat the meal."));
                Assert.That(HitCount("AteMeal"), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CookPresetMeal_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You eat the meal.";

        WithPatchedOwner(() =>
        {
            new DummyCampfireCookPresetMealTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            }.CookPresetMeal(0);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("AteMeal"), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony);
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
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupShowTranslationPatch),
                nameof(PopupShowTranslationPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(MethodBase))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyCampfireCookPresetMealTarget),
                nameof(DummyCampfireCookPresetMealTarget.CookPresetMeal),
                typeof(int)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CampfireCookPresetMealTranslationPatch),
                nameof(CampfireCookPresetMealTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(CampfireCookPresetMealTranslationPatch),
                nameof(CampfireCookPresetMealTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(CampfireCookPresetMealTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.campfire-cook-preset-meal." + Guid.NewGuid().ToString("N");
    }
}

internal sealed class DummyCampfireCookPresetMealTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public bool CookPresetMeal(int index)
    {
        _ = index;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}
