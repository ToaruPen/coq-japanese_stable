using System;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyPadding
{
    public int left { get; set; }
}

internal sealed class DummyLayoutGroup
{
    public DummyPadding padding = new DummyPadding();
}

internal sealed class DummyJournalStatusScreenTarget
{
    public static string NO_ENTRIES_TEXT = " No entries found.";

    public int CurrentCategory { get; set; } = 2;

    public DummyUITextSkin categoryText = new DummyUITextSkin();

    public DummyMenuOption CMD_INSERT = new DummyMenuOption("Add", "CmdInsert");

    public DummyMenuOption CMD_DELETE = new DummyMenuOption("Delete", "CmdDelete");

    public string NextCategoryText { get; set; } = "Locations";

    public void UpdateViewFromData()
    {
        categoryText.SetText(NextCategoryText);
    }

    public static string GetTabString()
    {
        return "Journal";
    }
}

internal sealed class DummyJournalLineDataTarget
{
    public bool category { get; set; }

    public bool categoryExpanded { get; set; }

    public string categoryName { get; set; } = string.Empty;

    public object? entry { get; set; }

    public object? renderable { get; set; }

    public DummyJournalStatusScreenTarget? screen { get; set; }
}

internal sealed class DummyFallbackJournalLineDataTarget
{
    public string FallbackText { get; set; } = "journal fallback";
}

internal sealed class DummyJournalRecipeTarget
{
    public string DisplayName { get; set; } = string.Empty;

    public string Ingredients { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string GetDisplayName() => DisplayName;

    public string GetIngredients() => Ingredients;

    public string GetDescription() => Description;
}

internal sealed class DummyJournalRecipeNoteEntry
{
    public DummyJournalRecipeTarget? Recipe { get; set; }
}

internal sealed class DummyJournalObservationEntry
{
    public string Text { get; set; } = string.Empty;

    public bool Tradable { get; set; }

    public bool TombPropaganda { get; set; }

    public string GetDisplayText() => Text;

    public bool Has(string key) => TombPropaganda && string.Equals(key, "sultanTombPropaganda", StringComparison.Ordinal);
}

internal sealed class DummyJournalMapNoteEntry
{
    public string Text { get; set; } = string.Empty;

    public bool Tradable { get; set; }

    public bool Tracked { get; set; }

    public string Category { get; set; } = "Merchants";

    public bool TombPropaganda { get; set; }

    public string GetDisplayText() => Text;

    public bool Has(string key) => TombPropaganda && string.Equals(key, "sultanTombPropaganda", StringComparison.Ordinal);
}

internal sealed class DummyJournalLineTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyStatusContext context = new DummyStatusContext();

    public DummyUITextSkin text = new DummyUITextSkin();

    public DummyActiveObject imageContainer = new DummyActiveObject();

    public DummyUiThreeColorProperties image = new DummyUiThreeColorProperties();

    public DummyActiveObject headerContainer = new DummyActiveObject();

    public DummyUITextSkin headerText = new DummyUITextSkin();

    public DummyLayoutGroup layoutGroup = new DummyLayoutGroup();

    public object? screen;

    public void setData(object data)
    {
        OriginalExecuted = true;
        context.data = data;
        if (data is DummyFallbackJournalLineDataTarget fallback)
        {
            text.SetText(fallback.FallbackText);
            return;
        }

        if (data is DummyJournalLineDataTarget lineData)
        {
            screen = lineData.screen;
            text.SetText(lineData.categoryName);
        }
    }
}
