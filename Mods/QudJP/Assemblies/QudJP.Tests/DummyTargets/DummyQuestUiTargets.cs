using System.Collections.Generic;
using System.Linq;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyQuestTarget
{
    public string DisplayName { get; set; } = string.Empty;

    public string? QuestGiverName { get; set; }

    public string? QuestGiverLocationName { get; set; }
}

internal sealed class DummyQuestsLineDataTarget
{
    public DummyQuestTarget? quest { get; set; }

    public bool expanded { get; set; }
}

internal sealed class DummyQuestsLineTarget
{
    public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>
    {
        new DummyMenuOption("Expand", "Accept"),
    };

    public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>
    {
        new DummyMenuOption("Collapse", "Accept"),
    };

    public DummyUITextSkin titleText = new DummyUITextSkin();

    public DummyUITextSkin giverText = new DummyUITextSkin();

    public DummyUITextSkin bodyText = new DummyUITextSkin();

    public DummyActiveObject giverLabel = new DummyActiveObject();

    public static void ResetStaticMenuOptions()
    {
        categoryExpandOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Expand", "Accept"),
        };
        categoryCollapseOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Collapse", "Accept"),
        };
    }

    public void setData(object data)
    {
        if (data is not DummyQuestsLineDataTarget questsLineData)
        {
            titleText.SetText("quests fallback");
            return;
        }

        if (questsLineData.quest is null)
        {
            giverLabel.SetActive(active: false);
            titleText.SetText("You have no active quests.");
            giverText.SetText(string.Empty);
            bodyText.SetText(string.Empty);
            return;
        }

        giverLabel.SetActive(active: true);
        titleText.SetText((questsLineData.expanded ? "[-]" : "[+]") + " " + questsLineData.quest.DisplayName);
        giverText.SetText((questsLineData.quest.QuestGiverName ?? "<unknown>") + " / " + (questsLineData.quest.QuestGiverLocationName ?? "<unknown>"));
        bodyText.SetText("details");
    }
}

internal sealed class DummyMapPinData
{
    public string title = string.Empty;

    public string details = string.Empty;
}

internal sealed class DummyQuestMapScrollerPinItem
{
    public DummyUITextSkin titleText = new DummyUITextSkin();

    public DummyUITextSkin detailsText = new DummyUITextSkin();

    public void SetData(DummyMapPinData data)
    {
        titleText.SetText(data.title);
        detailsText.SetText(data.details);
    }
}

internal sealed class DummyMapPinGameObject
{
    public DummyQuestMapScrollerPinItem pinItem = new DummyQuestMapScrollerPinItem();

    public object? GetComponent(System.Type type)
    {
        return type == typeof(DummyQuestMapScrollerPinItem) ? pinItem : null;
    }
}

internal sealed class DummyMapScrollerControllerTarget
{
    public List<DummyMapPinGameObject> pins = new List<DummyMapPinGameObject>();

    public void SetPins(IEnumerable<DummyMapPinData> data)
    {
        pins = data
            .Select(datum =>
            {
                var pin = new DummyMapPinGameObject();
                pin.pinItem.SetData(datum);
                return pin;
            })
            .ToList();
    }
}

internal sealed class DummyQuestsStatusScreenTarget
{
    public DummyMapScrollerControllerTarget mapController = new DummyMapScrollerControllerTarget();

    public void UpdateViewFromData()
    {
        mapController.SetPins(
            new[]
            {
                new DummyMapPinData
                {
                    title = "{{W|Joppa}}",
                    details = "{{B|quest:}} Find Mehmet\n{{B|quest:}} Return to Argyve",
                },
            });
    }
}

internal static class DummyQuestLogTarget
{
    public static List<string> GetLinesForQuest(object? quest, bool includeTitle = true, bool clip = true, int clipWidth = 74)
    {
        _ = quest;
        _ = includeTitle;
        _ = clip;
        _ = clipWidth;

        return new List<string>
        {
            "{{white|{{white|ù Optional: Find Mehmet}}}",
            "  Bonus reward for completing this quest by level &C12&y.",
            "   {{y|Unchanged line}}",
        };
    }
}
