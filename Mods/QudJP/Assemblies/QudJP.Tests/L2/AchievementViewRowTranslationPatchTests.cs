using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void AchievementViewRowPostfix_TranslatesHiddenTemplateAndUnlockedPrefix_WhenPatched()
    {
        WriteDictionary(
            ("{N} hidden achievements remaining", "残り{N}件の隠し実績"),
            ("Details will be revealed once unlocked.", "解除すると詳細が表示されます。"),
            ("Unlocked ", "解除日: "));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAchievementViewRowTarget), nameof(DummyAchievementViewRowTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(AchievementViewRowTranslationPatch), nameof(AchievementViewRowTranslationPatch.Postfix))));

            var hiddenTarget = new DummyAchievementViewRowTarget();
            hiddenTarget.setData(new DummyHiddenAchievementData { Amount = 3 });

            var unlockedTarget = new DummyAchievementViewRowTarget();
            unlockedTarget.setData(new DummyAchievementInfoData
            {
                Achievement = new DummyAchievementInfo
                {
                    Name = "ACH_NAME",
                    Description = "ACH_DESC",
                    Achieved = true,
                    FormattedTimestamp = "2026-03-31 12:34",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(hiddenTarget.Name.Text, Is.EqualTo("残り3件の隠し実績"));
                Assert.That(hiddenTarget.Description.Text, Is.EqualTo("解除すると詳細が表示されます。"));
                Assert.That(hiddenTarget.Date.Text, Is.EqualTo(string.Empty));
                Assert.That(unlockedTarget.Name.Text, Is.EqualTo("ACH_NAME"));
                Assert.That(unlockedTarget.Description.Text, Is.EqualTo("ACH_DESC"));
                Assert.That(unlockedTarget.Date.Text, Is.EqualTo("解除日: 2026-03-31 12:34"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AchievementViewRowTranslationPatch), "AchievementViewRow.HiddenCount"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AchievementViewRowTranslationPatch), "AchievementViewRow.Description"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AchievementViewRowTranslationPatch), "AchievementViewRow.DatePrefix"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AchievementViewRowPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAchievementViewRowTarget), nameof(DummyAchievementViewRowTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(AchievementViewRowTranslationPatch), nameof(AchievementViewRowTranslationPatch.Postfix))));

            var hiddenTarget = new DummyAchievementViewRowTarget();
            hiddenTarget.setData(new DummyHiddenAchievementData { Amount = 2 });

            var unlockedTarget = new DummyAchievementViewRowTarget();
            unlockedTarget.setData(new DummyAchievementInfoData
            {
                Achievement = new DummyAchievementInfo
                {
                    Name = "ACH_NAME",
                    Description = "ACH_DESC",
                    Achieved = true,
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(hiddenTarget.Name.Text, Is.EqualTo("2 hidden achievements remaining"));
                Assert.That(hiddenTarget.Description.Text, Is.EqualTo("Details will be revealed once unlocked."));
                Assert.That(unlockedTarget.Date.Text, Is.EqualTo("Unlocked 2026-03-31 12:34"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
