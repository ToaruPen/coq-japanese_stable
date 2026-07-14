using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [TestCase(2, "Small", false)]
    [TestCase(1, "Small", true)]
    [TestCase(1, "Medium", false)]
    public void ShouldClipForSmallMedia_UsesCategoryAndMediaSize(
        int currentCategory,
        string sizeClass,
        bool expected)
    {
        Assert.That(
            JournalLineTranslationPatch.ShouldClipForSmallMedia(currentCategory, sizeClass),
            Is.EqualTo(expected));
    }

    [Test]
    public void JournalLinePrefix_TranslatesCategoryRecipeAndEntryRows_WhenPatched()
    {
        WriteDictionary(
            ("Legends", "伝承"),
            ("Blue Bananas", "青いバナナ"),
            ("Ingredients:", "材料:"),
            ("Salt, Vinegar", "塩, 酢"),
            ("Effects:", "効果:"),
            ("Restores thirst.", "喉の渇きを癒やす。"),
            ("Found a relic", "遺物を見つけた"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalLineTarget), nameof(DummyJournalLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalLineTranslationPatch), nameof(JournalLineTranslationPatch.Prefix))));

            var screen = new DummyJournalStatusScreenTarget();

            var categoryTarget = new DummyJournalLineTarget();
            categoryTarget.setData(new DummyJournalLineDataTarget
            {
                category = true,
                categoryExpanded = false,
                categoryName = "Legends",
                screen = screen,
            });

            var recipeTarget = new DummyJournalLineTarget();
            recipeTarget.setData(new DummyJournalLineDataTarget
            {
                screen = screen,
                entry = new DummyJournalRecipeNoteEntry
                {
                    Recipe = new DummyJournalRecipeTarget
                    {
                        DisplayName = "Blue Bananas",
                        Ingredients = "Salt, Vinegar",
                        Description = "Restores thirst.",
                    },
                },
            });

            var entryTarget = new DummyJournalLineTarget();
            entryTarget.setData(new DummyJournalLineDataTarget
            {
                screen = screen,
                entry = new DummyJournalObservationEntry
                {
                    Text = "Found a relic",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(categoryTarget.headerText.Text, Is.EqualTo("[+] 伝承"));
                Assert.That(recipeTarget.headerText.Text, Is.EqualTo("青いバナナ"));
                Assert.That(recipeTarget.text.Text, Does.Contain("材料:"));
                Assert.That(recipeTarget.text.Text, Does.Contain("塩, 酢"));
                Assert.That(recipeTarget.text.Text, Does.Contain("効果:"));
                Assert.That(recipeTarget.text.Text, Does.Contain("喉の渇きを癒やす。"));
                Assert.That(entryTarget.text.Text, Is.EqualTo("{{K|$}} 遺物を見つけた"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(JournalLineTranslationPatch), "JournalLine.CategoryHeader"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(JournalLineTranslationPatch), "JournalLine.RecipeBody"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(JournalLineTranslationPatch), "JournalLine.EntryText"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void JournalLinePrefix_TranslatesSultanHistoryCategoryHeader_WhenPatched()
    {
        WriteDictionary(("unused", "未使用"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalLineTarget), nameof(DummyJournalLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalLineTranslationPatch), nameof(JournalLineTranslationPatch.Prefix))));

            var target = new DummyJournalLineTarget();
            target.setData(new DummyJournalLineDataTarget
            {
                category = true,
                categoryExpanded = false,
                categoryName = "{{W|HISTORY OF ナレドゥクフト}}",
                screen = new DummyJournalStatusScreenTarget(),
            });

            Assert.That(target.headerText.Text, Is.EqualTo("[+] {{W|ナレドゥクフトの歴史}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void JournalLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalLineTarget), nameof(DummyJournalLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalLineTranslationPatch), nameof(JournalLineTranslationPatch.Prefix))));

            var target = new DummyJournalLineTarget();
            target.setData(new DummyFallbackJournalLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("journal fallback"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
