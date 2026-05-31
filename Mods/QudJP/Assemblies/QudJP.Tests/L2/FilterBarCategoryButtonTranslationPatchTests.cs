using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void FilterBarCategoryButtonPostfix_TranslatesAllMappingAndTooltip_WhenPatched()
    {
        WriteDictionary(("ALL", "すべて"), ("*All", "すべて"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFilterBarCategoryButtonTarget), nameof(DummyFilterBarCategoryButtonTarget.SetCategory), new[] { typeof(string), typeof(string) }),
                postfix: new HarmonyMethod(RequireMethod(typeof(FilterBarCategoryButtonTranslationPatch), nameof(FilterBarCategoryButtonTranslationPatch.Postfix))));

            var target = new DummyFilterBarCategoryButtonTarget();
            target.SetCategory("*All", null);

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("すべて"));
                Assert.That(target.tooltipText.Text, Is.EqualTo("すべて"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(FilterBarCategoryButtonTranslationPatch), "FilterBarCategoryButton.Text"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void FilterBarCategoryButtonPostfix_TranslatesScrapCategory_WhenPatched()
    {
        WriteDictionary(("Scrap", "スクラップ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFilterBarCategoryButtonTarget), nameof(DummyFilterBarCategoryButtonTarget.SetCategory), new[] { typeof(string), typeof(string) }),
                postfix: new HarmonyMethod(RequireMethod(typeof(FilterBarCategoryButtonTranslationPatch), nameof(FilterBarCategoryButtonTranslationPatch.Postfix))));

            var target = new DummyFilterBarCategoryButtonTarget();
            target.SetCategory("Scrap", "Scrap");

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("スクラップ"));
                Assert.That(target.tooltipText.Text, Is.EqualTo("スクラップ"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void FilterBarCategoryButtonPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFilterBarCategoryButtonTarget), nameof(DummyFilterBarCategoryButtonTarget.SetCategory), new[] { typeof(string), typeof(string) }),
                postfix: new HarmonyMethod(RequireMethod(typeof(FilterBarCategoryButtonTranslationPatch), nameof(FilterBarCategoryButtonTranslationPatch.Postfix))));

            var target = new DummyFilterBarCategoryButtonTarget();
            target.SetCategory("Artifacts", "Artifacts");

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("Artifacts"));
                Assert.That(target.tooltipText.Text, Is.EqualTo("Artifacts"));
                Assert.That(target.OriginalExecuted, Is.True);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
