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

    [Test]
    public void CookPresetMeal_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookPresetMealTarget
            {
                PopupMessageToShow = string.Empty,
            }.CookPresetMeal(0);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("AteMeal"), Is.Zero);
            });
        });
    }

    [Test]
    public void CookPresetMeal_TranslatesColorTaggedPopup_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookPresetMealTarget
            {
                PopupMessageToShow = "<color=#44ff88>You eat the meal.</color>",
            }.CookPresetMeal(0);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("<color=#44ff88>食事をとった。</color>"));
                Assert.That(HitCount("AteMeal"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void CookPresetMeal_RestoresDirectMarkerPassThroughText_ForNestedOwnerScopes()
    {
        CampfireCookPresetMealTranslationPatch.Prefix(out var outerState);
        try
        {
            _ = CampfireCookPresetMealTranslationPatch.TryTranslatePopupMessage(
                MessageFrameTranslator.MarkDirectTranslation("You eat the meal."),
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out _);

            Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("You eat the meal."));

            CampfireCookPresetMealTranslationPatch.Prefix(out var innerState);
            try
            {
                _ = CampfireCookPresetMealTranslationPatch.TryTranslatePopupMessage(
                    MessageFrameTranslator.MarkDirectTranslation("Nested direct popup."),
                    nameof(PopupShowTranslationPatch),
                    "Popup.Show",
                    out _);

                Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("Nested direct popup."));
            }
            finally
            {
                CampfireCookPresetMealTranslationPatch.Finalizer(null, innerState);
            }

            Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("You eat the meal."));
        }
        finally
        {
            CampfireCookPresetMealTranslationPatch.Finalizer(null, outerState);
        }

        Assert.That(DirectMarkerPassThroughText(), Is.Null);
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
                nameof(CampfireCookPresetMealTranslationPatch.Prefix),
                typeof(string).MakeByRefType())),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(CampfireCookPresetMealTranslationPatch),
                nameof(CampfireCookPresetMealTranslationPatch.Finalizer),
                typeof(Exception),
                typeof(string))));
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

    private static string? DirectMarkerPassThroughText()
    {
        var field = typeof(CampfireCookPresetMealTranslationPatch).GetField(
            "directMarkerPassThroughText",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);
        return field!.GetValue(null) as string;
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
