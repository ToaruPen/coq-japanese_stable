using System;
using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyHighScoresScreenTarget
{
    internal enum Modes
    {
        Achievements,
        Local,
        Daily,
        DailyFriends,
    }

    public static DummyMenuOption ACHIEVEMENTS = new DummyMenuOption("Achievements");

    public static DummyMenuOption LOCAL_SCORES = new DummyMenuOption("Ended Runs");

    public static DummyMenuOption GLOBAL_DAILY = new DummyMenuOption("Daily (steam)");

    public static DummyMenuOption FRIENDS_DAILY = new DummyMenuOption("Daily (friends)");

    public static DummyMenuOption PREVIOUS_DAY = new DummyMenuOption("Previous Day", "Page Left");

    public static DummyMenuOption NEXT_DAY = new DummyMenuOption("Next Day", "Page Right");

    public static List<DummyMenuOption> leftSideMenuOptions = new List<DummyMenuOption>
    {
        LOCAL_SCORES,
        GLOBAL_DAILY,
        FRIENDS_DAILY,
        ACHIEVEMENTS,
    };

    public static void ResetStaticMenuOptions()
    {
        ACHIEVEMENTS = new DummyMenuOption("Achievements");
        LOCAL_SCORES = new DummyMenuOption("Ended Runs");
        GLOBAL_DAILY = new DummyMenuOption("Daily (steam)");
        FRIENDS_DAILY = new DummyMenuOption("Daily (friends)");
        PREVIOUS_DAY = new DummyMenuOption("Previous Day", "Page Left");
        NEXT_DAY = new DummyMenuOption("Next Day", "Page Right");
        leftSideMenuOptions = new List<DummyMenuOption>
        {
            LOCAL_SCORES,
            GLOBAL_DAILY,
            FRIENDS_DAILY,
            ACHIEVEMENTS,
        };
    }

    public DummyUITextSkin titleText = new DummyUITextSkin();

    public DummyFrameworkScroller hotkeyBar = new DummyFrameworkScroller();

    public Modes currentMode = Modes.Local;

    public bool friendsOnly;

    public bool OriginalExecuted { get; private set; }

    public void Show()
    {
        OriginalExecuted = true;
        leftSideMenuOptions.Clear();
        leftSideMenuOptions.Add(LOCAL_SCORES);
        leftSideMenuOptions.Add(GLOBAL_DAILY);
        leftSideMenuOptions.Add(FRIENDS_DAILY);
        leftSideMenuOptions.Add(ACHIEVEMENTS);

        if (currentMode == Modes.Achievements)
        {
            titleText.SetText("{{W|ACHIEVEMENTS}}");
            return;
        }

        if (currentMode == Modes.Local)
        {
            titleText.SetText("{{W|ENDED RUNS}}");
            return;
        }

        titleText.SetText("{{W|DAILY:2026:091" + (friendsOnly ? " (friends only)" : string.Empty) + "}}");
        hotkeyBar.BeforeShow(
            null,
            new List<DummyNavMenuOption>
            {
                new DummyNavMenuOption("navigate") { InputCommand = "NavigationXYAxis" },
                new DummyNavMenuOption(PREVIOUS_DAY.Description) { InputCommand = PREVIOUS_DAY.InputCommand },
                new DummyNavMenuOption(NEXT_DAY.Description) { InputCommand = NEXT_DAY.InputCommand },
            });
    }
}

internal sealed class DummyAchievementViewTarget
{
    public DummyFrameworkScroller HotkeyBar = new DummyFrameworkScroller();

    public bool OriginalExecuted { get; private set; }

    public void UpdateMenuBars()
    {
        OriginalExecuted = true;
        HotkeyBar.BeforeShow(
            null,
            new List<DummyNavMenuOption>
            {
                new DummyNavMenuOption("navigate") { InputCommand = "NavigationXYAxis" },
            });
    }
}

internal sealed class DummyAchievementViewRowTarget
{
    public DummyUITextSkin Name = new DummyUITextSkin();

    public DummyUITextSkin Description = new DummyUITextSkin();

    public DummyUITextSkin Date = new DummyUITextSkin();

    public bool OriginalExecuted { get; private set; }

    public void setData(object data)
    {
        OriginalExecuted = true;

        if (data is DummyAchievementInfoData achievementData)
        {
            Name.SetText(achievementData.Achievement.Name);
            Description.SetText(achievementData.Achievement.Description);
            Date.SetText(achievementData.Achievement.Achieved
                ? "Unlocked " + achievementData.Achievement.FormattedTimestamp
                : string.Empty);
            return;
        }

        if (data is DummyHiddenAchievementData hiddenData)
        {
            Name.SetText(hiddenData.Amount + " hidden achievements remaining");
            Description.SetText("Details will be revealed once unlocked.");
            Date.SetText(string.Empty);
        }
    }
}

internal sealed class DummyAchievementInfoData
{
    public DummyAchievementInfo Achievement { get; set; } = new DummyAchievementInfo();
}

internal sealed class DummyAchievementInfo
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool Achieved { get; set; }

    public string FormattedTimestamp { get; set; } = "2026-03-31 12:34";
}

internal sealed class DummyHiddenAchievementData
{
    public int Amount { get; set; }
}
