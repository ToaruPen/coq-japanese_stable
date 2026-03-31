using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void HighScoresScreenPostfix_TranslatesTitlesMenuOptionsAndFriendsSuffix_WhenPatched()
    {
        WriteDictionary(
            ("Achievements", "実績"),
            ("Ended Runs", "終了した冒険"),
            ("Daily (steam)", "デイリー (Steam)"),
            ("Daily (friends)", "デイリー (フレンド)"),
            ("Previous Day", "前日"),
            ("Next Day", "翌日"),
            ("{{W|ACHIEVEMENTS}}", "{{W|実績}}"),
            ("{{W|ENDED RUNS}}", "{{W|終了した冒険}}"),
            (" (friends only)", " (フレンドのみ)"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHighScoresScreenTarget), nameof(DummyHighScoresScreenTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(HighScoresScreenTranslationPatch), nameof(HighScoresScreenTranslationPatch.Postfix))));

            var achievementsScreen = new DummyHighScoresScreenTarget
            {
                currentMode = DummyHighScoresScreenTarget.Modes.Achievements,
            };
            achievementsScreen.Show();

            var localScreen = new DummyHighScoresScreenTarget
            {
                currentMode = DummyHighScoresScreenTarget.Modes.Local,
            };
            localScreen.Show();

            var dailyScreen = new DummyHighScoresScreenTarget
            {
                currentMode = DummyHighScoresScreenTarget.Modes.DailyFriends,
                friendsOnly = true,
            };
            dailyScreen.Show();

            Assert.Multiple(() =>
            {
                Assert.That(achievementsScreen.titleText.Text, Is.EqualTo("{{W|実績}}"));
                Assert.That(localScreen.titleText.Text, Is.EqualTo("{{W|終了した冒険}}"));
                Assert.That(dailyScreen.titleText.Text, Is.EqualTo("{{W|DAILY:2026:091 (フレンドのみ)}}"));
                Assert.That(DummyHighScoresScreenTarget.ACHIEVEMENTS.Description, Is.EqualTo("実績"));
                Assert.That(DummyHighScoresScreenTarget.LOCAL_SCORES.Description, Is.EqualTo("終了した冒険"));
                Assert.That(DummyHighScoresScreenTarget.GLOBAL_DAILY.Description, Is.EqualTo("デイリー (Steam)"));
                Assert.That(DummyHighScoresScreenTarget.FRIENDS_DAILY.Description, Is.EqualTo("デイリー (フレンド)"));
                Assert.That(DummyHighScoresScreenTarget.PREVIOUS_DAY.Description, Is.EqualTo("前日"));
                Assert.That(DummyHighScoresScreenTarget.NEXT_DAY.Description, Is.EqualTo("翌日"));
                Assert.That(dailyScreen.hotkeyBar.choices[1].Description, Is.EqualTo("前日"));
                Assert.That(dailyScreen.hotkeyBar.choices[2].Description, Is.EqualTo("翌日"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(HighScoresScreenTranslationPatch), "HighScoresScreen.TitleText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(HighScoresScreenTranslationPatch), "HighScoresScreen.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            DummyHighScoresScreenTarget.ResetStaticMenuOptions();
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void HighScoresScreenPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHighScoresScreenTarget), nameof(DummyHighScoresScreenTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(HighScoresScreenTranslationPatch), nameof(HighScoresScreenTranslationPatch.Postfix))));

            var screen = new DummyHighScoresScreenTarget
            {
                currentMode = DummyHighScoresScreenTarget.Modes.DailyFriends,
                friendsOnly = true,
            };
            screen.Show();

            Assert.Multiple(() =>
            {
                Assert.That(screen.OriginalExecuted, Is.True);
                Assert.That(screen.titleText.Text, Is.EqualTo("{{W|DAILY:2026:091 (friends only)}}"));
                Assert.That(DummyHighScoresScreenTarget.ACHIEVEMENTS.Description, Is.EqualTo("Achievements"));
                Assert.That(DummyHighScoresScreenTarget.PREVIOUS_DAY.Description, Is.EqualTo("Previous Day"));
                Assert.That(screen.hotkeyBar.choices[1].Description, Is.EqualTo("Previous Day"));
            });
        }
        finally
        {
            DummyHighScoresScreenTarget.ResetStaticMenuOptions();
            harmony.UnpatchAll(harmonyId);
        }
    }
}
