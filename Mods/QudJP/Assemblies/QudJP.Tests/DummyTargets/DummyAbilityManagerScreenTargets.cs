namespace QudJP.Tests.DummyTargets;

internal sealed class DummyAbilityManagerEntryTarget
{
    public string DisplayName { get; set; } = string.Empty;

    public string Class { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

internal sealed class DummyAbilityManagerScreenLineData
{
    public string Id { get; set; } = string.Empty;

    public string? category { get; set; }

    public DummyAbilityManagerEntryTarget? ability { get; set; }
}

internal sealed class DummyAbilityManagerMenuOption
{
    public string? Description { get; set; }

    public string? KeyDescription { get; set; }
}

internal sealed class DummyAbilityManagerHotkeyBar
{
    public List<DummyAbilityManagerMenuOption> choices { get; private set; } = [];
    public List<string?> renderedDescriptions { get; private set; } = [];
    public List<string?> renderedKeyDescriptions { get; private set; } = [];

    public void BeforeShow(object? descriptor, IEnumerable<DummyAbilityManagerMenuOption> menuOptions)
    {
        choices = menuOptions.ToList();
        renderedDescriptions = choices.Select(static option => option.Description).ToList();
        renderedKeyDescriptions = choices.Select(static option => option.KeyDescription).ToList();
    }
}

internal sealed class DummyAbilityManagerScreenTarget
{
    public static DummyAbilityManagerMenuOption TOGGLE_SORT = new()
    {
        KeyDescription = "Toggle Sort",
        Description = "sort: {{w|custom}}/{{y|by class}}",
    };

    public static DummyAbilityManagerMenuOption FILTER_ITEMS = new()
    {
        Description = "search",
    };

    public static List<DummyAbilityManagerMenuOption> defaultMenuOptions =
    [
        new DummyAbilityManagerMenuOption { Description = "Close Menu" },
        new DummyAbilityManagerMenuOption { Description = "navigate" },
        TOGGLE_SORT,
        new DummyAbilityManagerMenuOption { Description = "Activate Selected Ability" },
        FILTER_ITEMS,
    ];

    public DummyAbilityManagerHotkeyBar hotkeyBar = new();
    public List<DummyAbilityManagerScreenLineData> leftSideItems = [];
    public List<DummyAbilityManagerScreenLineData> filteredItems = [];
    public string searchText = string.Empty;
    public DummyUITextSkinField rightSideHeaderText = new();
    public DummyUITextSkinField rightSideDescriptionArea = new();

    public void FilterItems()
    {
        filteredItems.Clear();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            FILTER_ITEMS.Description = "search";
            filteredItems.AddRange(leftSideItems);
            return;
        }

        FILTER_ITEMS.Description = "search: {{w|" + searchText + "}}";
        filteredItems.AddRange(leftSideItems);
    }

    public void UpdateMenuBars()
    {
        hotkeyBar.BeforeShow(null, defaultMenuOptions);
    }

    public void HandleHighlightLeft(DummyAbilityManagerScreenLineData element)
    {
        if (element is null)
        {
            return;
        }

        if (element.ability is not null)
        {
            rightSideHeaderText.SetText(element.ability.DisplayName);
            rightSideDescriptionArea.SetText("{{y|Type: }}" + element.ability.Class + "\n\n" + element.ability.Description);
            return;
        }

        rightSideHeaderText.SetText(element.category ?? string.Empty);
        rightSideDescriptionArea.SetText(string.Empty);
    }

    public static void ResetMenuOptions()
    {
        TOGGLE_SORT = new DummyAbilityManagerMenuOption
        {
            KeyDescription = "Toggle Sort",
            Description = "sort: {{w|custom}}/{{y|by class}}",
        };
        FILTER_ITEMS = new DummyAbilityManagerMenuOption { Description = "search" };
        defaultMenuOptions =
        [
            new DummyAbilityManagerMenuOption { Description = "Close Menu" },
            new DummyAbilityManagerMenuOption { Description = "navigate" },
            TOGGLE_SORT,
            new DummyAbilityManagerMenuOption { Description = "Activate Selected Ability" },
            FILTER_ITEMS,
        ];
    }
}
