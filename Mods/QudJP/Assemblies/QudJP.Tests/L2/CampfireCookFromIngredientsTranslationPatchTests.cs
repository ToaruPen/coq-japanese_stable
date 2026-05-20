using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfireCookFromIngredientsTranslationPatchTests
{
    private string tempDictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDictionaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-campfire-cook-from-ingredients-l2",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDictionaryDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDictionaryDirectory);
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();

        if (Directory.Exists(tempDictionaryDirectory))
        {
            Directory.Delete(tempDictionaryDirectory, recursive: true);
        }
    }

    [TestCase(
        "You create a new recipe for {{|Glowfish Stew}}!",
        "{{|Glowfish Stew}}の新しいレシピを作った！",
        "RecipeCreated")]
    [TestCase(
        "You eat the meal.",
        "食事をとった。",
        "AteMeal")]
    [TestCase(
        "You toss {{Y|snapjaw haunch}} into a pot and stir.",
        "{{Y|snapjaw haunch}}を鍋に放り込み、かき混ぜた。",
        "MealDescription")]
    [TestCase(
        "You start to metabolize the meal, gaining the following effect for the rest of the day:\n\n{{W|+1 to hit}}",
        "食事の代謝が始まり、一日中次の効果を得る:\n\n{{W|命中+1}}",
        "MetabolizeMeal")]
    public void CookFromIngredients_TranslatesOwnerPopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookFromIngredientsTarget
            {
                PopupMessageToShow = source,
            }.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void CookFromIngredients_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.Show("You create a new recipe for {{|Glowfish Stew}}!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.Not.Null);
                Assert.That(HitCount("RecipeCreated"), Is.Zero);
            });
        });
    }

    [Test]
    public void CookFromIngredients_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You create a new recipe for {{|Glowfish Stew}}!";

        WithPatchedOwner(() =>
        {
            new DummyCampfireCookFromIngredientsTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            }.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("RecipeCreated"), Is.Zero);
            });
        });
    }

    [Test]
    public void CookFromIngredients_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookFromIngredientsTarget
            {
                PopupMessageToShow = string.Empty,
            }.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("RecipeCreated"), Is.Zero);
                Assert.That(HitCount("MetabolizeMeal"), Is.Zero);
            });
        });
    }

    [TestCase("You aren't hungry. Instead, you relax by the warmth of the fire.")]
    [TestCase("You don't have the Cooking and Gathering skill.")]
    [TestCase("Are you sure you want to forget this recipe?")]
    [TestCase("A savory meal made from {{Y|snapjaw haunch}}.")]
    [TestCase("You eat the meal. It's tastier than usual.")]
    public void CookFromIngredients_DoesNotClaimDeferredFixedOrRuntimePopups_WhenOwnerPatched(string source)
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookFromIngredientsTarget
            {
                PopupMessageToShow = source,
            }.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.Not.Null);
                Assert.That(HitCount("AteMeal"), Is.Zero);
                Assert.That(HitCount("MealDescription"), Is.Zero);
                Assert.That(HitCount("RecipeCreated"), Is.Zero);
                Assert.That(HitCount("MetabolizeMeal"), Is.Zero);
            });
        });
    }

    [TestCase(
        "{{W|Cook with the {{C|0}} selected ingredients.}}\n{{y|[up to 2 remaining]}}",
        "{{W|選択した材料{{C|0}}個で料理する。}}\n{{y|[あと2個まで]}}")]
    [TestCase(
        "{{W|Cook with the {{R|3}} selected ingredients.}}\n{{y|[0 remaining]}}",
        "{{W|選択した材料{{R|3}}個で料理する。}}\n{{y|[残り0個]}}")]
    public void CookFromIngredients_TranslatesSelectedIngredientMenuRows_WhenOwnerActive(
        string source,
        string expected)
    {
        CampfireCookFromIngredientsTranslationPatch.Prefix();
        try
        {
            var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
                source,
                nameof(PopupPickOptionTranslationPatch));

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo(expected));
                Assert.That(PickOptionProducerHitCount("SelectedIngredientsMenuRow"), Is.EqualTo(1));
            });
        }
        finally
        {
            CampfireCookFromIngredientsTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void CookFromIngredients_TranslatesSelectedIngredientMenuRows_WithCrLfLineEndings()
    {
        const string source = "{{W|Cook with the {{C|0}} selected ingredients.}}\r\n{{y|[up to 2 remaining]}}";

        CampfireCookFromIngredientsTranslationPatch.Prefix();
        try
        {
            var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
                source,
                nameof(PopupPickOptionTranslationPatch));

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo("{{W|選択した材料{{C|0}}個で料理する。}}\n{{y|[あと2個まで]}}"));
                Assert.That(PickOptionProducerHitCount("SelectedIngredientsMenuRow"), Is.EqualTo(1));
            });
        }
        finally
        {
            CampfireCookFromIngredientsTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void CookFromIngredients_DoesNotTranslateSelectedIngredientMenuRows_WhenOwnerAbsent()
    {
        const string source = "{{W|Cook with the {{C|0}} selected ingredients.}}\n{{y|[up to 2 remaining]}}";

        var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
            source,
            nameof(PopupPickOptionTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(PickOptionProducerHitCount("SelectedIngredientsMenuRow"), Is.Zero);
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

    private static void WithPatchedPopupOnly(Action action)
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
        var prefix = new HarmonyMethod(RequireMethod(
            typeof(CampfireCookFromIngredientsTranslationPatch),
            nameof(CampfireCookFromIngredientsTranslationPatch.Prefix)));
        var finalizer = new HarmonyMethod(RequireMethod(
            typeof(CampfireCookFromIngredientsTranslationPatch),
            nameof(CampfireCookFromIngredientsTranslationPatch.Finalizer),
            typeof(Exception)));

        harmony.Patch(
            original: RequireMethod(
                typeof(DummyCampfireCookFromIngredientsTarget),
                nameof(DummyCampfireCookFromIngredientsTarget.CookFromIngredients),
                typeof(bool)),
            prefix: prefix,
            finalizer: finalizer);
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(CampfireCookFromIngredientsTranslationPatch) + "." + detail);
    }

    private static int PickOptionProducerHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupPickOptionTranslationPatch),
            "Popup.ProducerText." + nameof(CampfireCookFromIngredientsTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.campfire-cook-from-ingredients." + Guid.NewGuid().ToString("N");
    }
}

internal sealed class DummyCampfireCookFromIngredientsTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public bool CookFromIngredients(bool random)
    {
        _ = random;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}
