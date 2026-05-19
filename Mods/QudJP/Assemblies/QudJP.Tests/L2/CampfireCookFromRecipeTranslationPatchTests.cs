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
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
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
        CampfireCookFromRecipeTranslationPatch.Prefix();
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
            CampfireCookFromRecipeTranslationPatch.Finalizer(null);
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
        CampfireCookFromRecipeTranslationPatch.Prefix();
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
            CampfireCookFromRecipeTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void CookFromRecipe_TranslatesAteMealPopup_WhenOwnerActive()
    {
        CampfireCookFromRecipeTranslationPatch.Prefix();
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
            CampfireCookFromRecipeTranslationPatch.Finalizer(null);
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
}
