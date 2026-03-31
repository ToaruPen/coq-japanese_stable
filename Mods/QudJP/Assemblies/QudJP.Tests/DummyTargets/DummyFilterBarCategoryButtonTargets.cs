using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyFilterBarCategoryButtonTarget
{
    public static List<DummyMenuOption> categoryExpandOptions = new()
    {
        new DummyMenuOption("Expand", "Accept"),
    };

    public static List<DummyMenuOption> categoryCollapseOptions = new()
    {
        new DummyMenuOption("Collapse", "Accept"),
    };

    public static List<DummyMenuOption> itemOptions = new()
    {
        new DummyMenuOption("Select", "Accept"),
    };

    public bool OriginalExecuted { get; private set; }

    public string Tooltip { get; set; } = string.Empty;

    public DummyUITextSkin tooltipText = new DummyUITextSkin();

    public DummyUITextSkin text = new DummyUITextSkin();

    public string category = string.Empty;

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
        itemOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Select", "Accept"),
        };
    }

    public void SetCategory(string category, string? tooltip = null)
    {
        OriginalExecuted = true;
        this.category = category;
        Tooltip = tooltip ?? category;
        tooltipText.SetText(Tooltip);
        text.SetText(category == "*All" ? "ALL" : category);
    }
}
