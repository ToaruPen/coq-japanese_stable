using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyJournalStatusScreenUiTarget
{
    public DummyUITextSkin categoryText = new DummyUITextSkin();

    public DummyMenuOption CMD_INSERT = new DummyMenuOption("Add", "CmdInsert");

    public DummyMenuOption CMD_DELETE = new DummyMenuOption("Delete", "CmdDelete");

    public string NextCategoryText { get; set; } = "Locations";

    public void UpdateViewFromData()
    {
        categoryText.SetText(NextCategoryText);
    }
}

internal sealed class DummyStatusScreensScreenTarget
{
    public static DummyMenuOption SET_FILTER = new DummyMenuOption("Filter", "CmdFilter", "Filter");

    public List<DummyMenuOption> defaultMenuOptionOrder = new List<DummyMenuOption>
    {
        new DummyMenuOption("navigation", "NavigationXYAxis"),
        new DummyMenuOption("Accept", "Accept"),
    };

    public static void ResetStaticMenuOptions()
    {
        SET_FILTER = new DummyMenuOption("Filter", "CmdFilter", "Filter");
    }

    public void UpdateViewFromData()
    {
    }
}
