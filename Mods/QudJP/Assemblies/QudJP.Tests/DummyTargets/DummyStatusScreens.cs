namespace QudJP.Tests.DummyTargets;

internal sealed class DummySkillsAndPowersStatusScreen
{
    public DummyUITextSkin spText = new DummyUITextSkin();

    public void UpdateViewFromData()
    {
        spText.SetText("Skill Points (SP): 0");
    }
}

internal sealed class DummyCharacterStatusScreen
{
    public DummyUITextSkin attributePointsText = new DummyUITextSkin();

    public DummyUITextSkin mutationPointsText = new DummyUITextSkin();

    public void UpdateViewFromData()
    {
        attributePointsText.SetText("Attribute Points: 0");
        mutationPointsText.SetText("Mutation Points: 0");
    }
}

internal sealed class DummyCharacterMutation
{
    public string Name { get; set; } = "Force Wall";

    public int Level { get; set; } = 1;
}

internal sealed class DummyCharacterMutationLineData
{
    public DummyCharacterMutation? mutation { get; set; }
}

internal sealed class DummyPickGameObjectScreen
{
    public List<DummyMenuOption> defaultMenuOptions = new List<DummyMenuOption>
    {
        new DummyMenuOption("Close Menu"),
        new DummyMenuOption("navigate"),
    };

    public List<DummyMenuOption> getItemMenuOptions = new List<DummyMenuOption>
    {
        new DummyMenuOption("Close Menu"),
        new DummyMenuOption("navigate"),
    };

    public DummyMenuOption TAKE_ALL = new DummyMenuOption("take all");

    public DummyMenuOption STORE_ITEM = new DummyMenuOption("store an item");

    public void UpdateViewFromData(bool reentry)
    {
        _ = reentry;
        _ = TAKE_ALL;
    }
}

internal sealed class DummyInventoryAndEquipmentStatusScreen
{
    public DummyMenuOption CMD_OPTIONS = new DummyMenuOption("Display Options");

    public DummyMenuOption SET_PRIMARY_LIMB = new DummyMenuOption("Set Primary Limb");

    public DummyMenuOption SHOW_TOOLTIP = new DummyMenuOption("[{{W|Alt}}] Show Tooltip", "CmdShowTooltip", "Alt");

    public DummyMenuOption QUICK_DROP = new DummyMenuOption("Quick Drop");

    public DummyMenuOption QUICK_EAT = new DummyMenuOption("Quick Eat");

    public DummyMenuOption QUICK_DRINK = new DummyMenuOption("Quick Drink");

    public DummyMenuOption QUICK_APPLY = new DummyMenuOption("Quick Apply");

    public void UpdateViewFromData()
    {
        _ = CMD_OPTIONS;
    }
}

internal sealed class DummyCharacterStatusMutationScreen
{
    public DummyUITextSkin mutationNameText = new DummyUITextSkin();

    public DummyUITextSkin mutationRankText = new DummyUITextSkin();

    public DummyUITextSkin mutationTypeText = new DummyUITextSkin();

    public DummyUITextSkin mutationsDetails = new DummyUITextSkin();

    public void HandleHighlightMutation(object? element)
    {
        if (element is not DummyCharacterMutationLineData { mutation: not null })
        {
            mutationsDetails.SetText(string.Empty);
            return;
        }

        mutationNameText.SetText("{{B|Force Wall}}");
        mutationRankText.SetText("{{G|RANK 1/10}}");
        mutationTypeText.SetText("{{c|[Mental Mutation]}}");
        mutationsDetails.SetText("You generate a wall of force...\n\n9 contiguous stationary force fields.");
    }
}
