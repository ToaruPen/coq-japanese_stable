using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void AchievementViewPostfix_TranslatesHotkeyBar_WhenPatched()
    {
        WriteDictionary(("navigate", "ナビゲート"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAchievementViewTarget), nameof(DummyAchievementViewTarget.UpdateMenuBars)),
                postfix: new HarmonyMethod(RequireMethod(typeof(AchievementViewTranslationPatch), nameof(AchievementViewTranslationPatch.Postfix))));

            var target = new DummyAchievementViewTarget();
            target.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.HotkeyBar.choices[0].Description, Is.EqualTo("ナビゲート"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AchievementViewTranslationPatch), "AchievementView.HotkeyBar"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AchievementViewPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAchievementViewTarget), nameof(DummyAchievementViewTarget.UpdateMenuBars)),
                postfix: new HarmonyMethod(RequireMethod(typeof(AchievementViewTranslationPatch), nameof(AchievementViewTranslationPatch.Postfix))));

            var target = new DummyAchievementViewTarget();
            target.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.HotkeyBar.choices[0].Description, Is.EqualTo("navigate"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
