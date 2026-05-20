using System.Reflection;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfireCookFromRecipeTranslationPatchTests
{
    private string tempDictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDictionaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-campfire-cook-from-recipe-l2",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDictionaryDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDictionaryDirectory);
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDictionaryDirectory))
        {
            Directory.Delete(tempDictionaryDirectory, recursive: true);
        }
    }

    [TestCase("Cook", "料理する", "MenuLabel")]
    [TestCase("Add to favorite recipes", "お気に入りレシピに追加", "MenuLabel")]
    [TestCase("Remove from favorite recipes", "お気に入りレシピから外す", "MenuLabel")]
    [TestCase("Forget", "忘れる", "MenuLabel")]
    [TestCase("Back", "戻る", "MenuLabel")]
    [TestCase("Show 3 hidden recipes missing ingredients", "材料不足の非表示レシピを3件表示", "HiddenRecipesRow")]
    [TestCase("{{K|Show {{C|12}} hidden recipes missing ingredients}}", "{{K|材料不足の非表示レシピを{{C|12}}件表示}}", "HiddenRecipesRow")]
    [TestCase("&K< 3 hidden for missing ingredients >", "&K< 材料不足のため非表示: 3件 >", "HiddenRecipesIntro")]
    public void CookFromRecipe_TranslatesRecipeMenuRows_WhenOwnerActive(
        string source,
        string expected,
        string detail)
    {
        CampfireCookFromRecipeTranslationPatch.Prefix(out var state);
        try
        {
            var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
                source,
                nameof(PopupPickOptionTranslationPatch));

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        }
        finally
        {
            CampfireCookFromRecipeTranslationPatch.Finalizer(null, state);
        }
    }

    [Test]
    public void CookFromRecipe_DoesNotTranslateRecipeMenuRows_WhenOwnerAbsent()
    {
        const string source = "Show 3 hidden recipes missing ingredients";

        var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
            source,
            nameof(PopupPickOptionTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(HitCount("HiddenRecipesRow"), Is.Zero);
        });
    }

    [Test]
    public void CookFromRecipe_LeavesEmptyProducerTextUnchanged_WhenOwnerActive()
    {
        CampfireCookFromRecipeTranslationPatch.Prefix(out var state);
        try
        {
            var menuTranslated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
                string.Empty,
                nameof(PopupPickOptionTranslationPatch));
            var handled = CampfireCookFromRecipeTranslationPatch.TryTranslatePopupMessage(
                string.Empty,
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out var popupTranslated);

            Assert.Multiple(() =>
            {
                Assert.That(menuTranslated, Is.EqualTo(string.Empty));
                Assert.That(handled, Is.False);
                Assert.That(popupTranslated, Is.EqualTo(string.Empty));
                Assert.That(HitCount("HiddenRecipesRow"), Is.Zero);
                Assert.That(PopupHitCount("MissingIngredientServings"), Is.Zero);
                Assert.That(PopupHitCount("AteMeal"), Is.Zero);
            });
        }
        finally
        {
            CampfireCookFromRecipeTranslationPatch.Finalizer(null, state);
        }
    }

    [Test]
    public void CookFromRecipe_LeavesEmptyProducerTextUnchanged_WhenOwnerAbsent()
    {
        var menuTranslated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
            string.Empty,
            nameof(PopupPickOptionTranslationPatch));
        var handled = CampfireCookFromRecipeTranslationPatch.TryTranslatePopupMessage(
            string.Empty,
            nameof(PopupShowTranslationPatch),
            "Popup.Show",
            out var popupTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(menuTranslated, Is.EqualTo(string.Empty));
            Assert.That(handled, Is.False);
            Assert.That(popupTranslated, Is.EqualTo(string.Empty));
            Assert.That(HitCount("HiddenRecipesRow"), Is.Zero);
            Assert.That(PopupHitCount("MissingIngredientServings"), Is.Zero);
            Assert.That(PopupHitCount("AteMeal"), Is.Zero);
        });
    }

    [TestCase(
        "You don't have enough servings of {{Y|witchwood bark}}.",
        "{{Y|witchwood bark}}の食分が足りない。",
        "MissingIngredientServings")]
    [TestCase(
        "You don't have enough mushroom.",
        "mushroomが足りない。",
        "MissingIngredient")]
    public void CookFromRecipe_TranslatesMissingIngredientPopup_WhenOwnerActive(
        string source,
        string expected,
        string detail)
    {
        CampfireCookFromRecipeTranslationPatch.Prefix(out var state);
        try
        {
            var handled = CampfireCookFromRecipeTranslationPatch.TryTranslatePopupMessage(
                source,
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out var translated);

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(translated, Is.EqualTo(expected));
                Assert.That(PopupHitCount(detail), Is.EqualTo(1));
            });
        }
        finally
        {
            CampfireCookFromRecipeTranslationPatch.Finalizer(null, state);
        }
    }

    [Test]
    public void CookFromRecipe_TranslatesAteMealPopup_WhenOwnerActive()
    {
        CampfireCookFromRecipeTranslationPatch.Prefix(out var state);
        try
        {
            var handled = CampfireCookFromRecipeTranslationPatch.TryTranslatePopupMessage(
                "You eat the meal.",
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out var translated);

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(translated, Is.EqualTo("食事をとった。"));
                Assert.That(PopupHitCount("AteMeal"), Is.EqualTo(1));
            });
        }
        finally
        {
            CampfireCookFromRecipeTranslationPatch.Finalizer(null, state);
        }
    }

    [Test]
    public void CookFromRecipe_DoesNotTranslateAteMealPopup_WhenOwnerAbsent()
    {
        var handled = CampfireCookFromRecipeTranslationPatch.TryTranslatePopupMessage(
            "You eat the meal.",
            nameof(PopupShowTranslationPatch),
            "Popup.Show",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(translated, Is.EqualTo("You eat the meal."));
            Assert.That(PopupHitCount("AteMeal"), Is.Zero);
        });
    }

    [Test]
    public void CookFromRecipe_RestoresDirectMarkerPassThroughText_ForNestedOwnerScopes()
    {
        CampfireCookFromRecipeTranslationPatch.Prefix(out var outerState);
        try
        {
            _ = CampfireCookFromRecipeTranslationPatch.TryTranslatePopupMessage(
                MessageFrameTranslator.MarkDirectTranslation("You eat the meal."),
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out _);

            Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("You eat the meal."));
            AssertDirectMarkedPopupPassesThrough("You eat the meal.");

            CampfireCookFromRecipeTranslationPatch.Prefix(out var innerState);
            try
            {
                _ = CampfireCookFromRecipeTranslationPatch.TryTranslatePopupMessage(
                    MessageFrameTranslator.MarkDirectTranslation("You don't have enough mushroom."),
                    nameof(PopupShowTranslationPatch),
                    "Popup.Show",
                    out _);

                Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("You don't have enough mushroom."));
                AssertDirectMarkedPopupPassesThrough("You don't have enough mushroom.");
            }
            finally
            {
                CampfireCookFromRecipeTranslationPatch.Finalizer(null, innerState);
            }

            Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("You eat the meal."));
        }
        finally
        {
            CampfireCookFromRecipeTranslationPatch.Finalizer(null, outerState);
        }

        Assert.That(DirectMarkerPassThroughText(), Is.Null);
    }

    private static void AssertDirectMarkedPopupPassesThrough(string source)
    {
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        var handled = CampfireCookFromRecipeTranslationPatch.TryTranslatePopupMessage(
            marked,
            nameof(PopupShowTranslationPatch),
            "Popup.Show",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(PopupHitCount("AteMeal"), Is.Zero);
            Assert.That(PopupHitCount("MissingIngredient"), Is.Zero);
            Assert.That(PopupHitCount("MissingIngredientServings"), Is.Zero);
        });
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupPickOptionTranslationPatch),
            "Popup.ProducerText." + nameof(CampfireCookFromRecipeTranslationPatch) + "." + detail);
    }

    private static int PopupHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(CampfireCookFromRecipeTranslationPatch) + "." + detail);
    }

    private static string? DirectMarkerPassThroughText()
    {
        var field = typeof(CampfireCookFromRecipeTranslationPatch).GetField(
            "directMarkerPassThroughText",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);
        return field!.GetValue(null) as string;
    }
}
