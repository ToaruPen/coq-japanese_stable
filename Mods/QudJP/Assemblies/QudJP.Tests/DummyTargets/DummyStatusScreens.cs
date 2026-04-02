#pragma warning disable CS0649

using System.Linq;

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
    public static List<DummyStatusStatistic> stats = new List<DummyStatusStatistic>
    {
        new DummyStatusStatistic
        {
            Name = "Strength",
            ShortDisplayName = "STR",
            Value = 18,
            BaseValue = 18,
            Modifier = 2,
        },
    };

    public static List<DummyCharacterMutationRecord> mutations = new List<DummyCharacterMutationRecord>();

    public static List<DummyStatusEffect> effects = new List<DummyStatusEffect>();

    public static string[] PrimaryAttributes = new[] { "Strength" };

    public static string[] SecondaryAttributes = Array.Empty<string>();

    public static string[] SecondaryAttributesWithCP = new[] { "CP" };

    public static string[] ResistanceAttributes = Array.Empty<string>();

    public static int CP = int.MinValue;

    public DummyBindingScroller primaryAttributesController = new DummyBindingScroller();

    public DummyBindingScroller secondaryAttributesController = new DummyBindingScroller();

    public DummyBindingScroller resistanceAttributesController = new DummyBindingScroller();

    public DummyBindingScroller mutationsController = new DummyBindingScroller();

    public DummyBindingScroller effectsController = new DummyBindingScroller();

    public DummyUiThreeColorProperties playerIcon = new DummyUiThreeColorProperties();

    public DummyStatusGameObject GO = new DummyStatusGameObject();

    public string mutationsTerm = "Mutations";

    public string mutationTerm = "Mutation";

    public string mutationTermCapital = "Mutation";

    public string mutationColor = "C";

    public DummyUITextSkin mutationTermText = new DummyUITextSkin();

    public DummyUITextSkin nameText = new DummyUITextSkin();

    public DummyUITextSkin classText = new DummyUITextSkin();

    public DummyUITextSkin levelText = new DummyUITextSkin();

    public DummyUITextSkin attributePointsText = new DummyUITextSkin();

    public DummyUITextSkin mutationPointsText = new DummyUITextSkin();

    public void UpdateViewFromData()
    {
        mutationTermText.SetText("MUTATIONS");
        primaryAttributesController.BeforeShow(Array.Empty<object>());
        secondaryAttributesController.BeforeShow(Array.Empty<object>());
        resistanceAttributesController.BeforeShow(Array.Empty<object>());
        mutationsController.BeforeShow(Array.Empty<object>());
        effectsController.BeforeShow(Array.Empty<object>());
        playerIcon.FromRenderable(new DummyRenderable("status-icon"));
        nameText.SetText(GO.DisplayName);
        classText.SetText(GO.GetGenotype() + " " + GO.GetSubtype());
        levelText.SetText("Level: 1 ¯ HP: 10/10 ¯ XP: 100/200 ¯ Weight: 123#");
        attributePointsText.SetText("Attribute Points: 0");
        mutationPointsText.SetText("Mutation Points: 0");
    }
}

internal sealed class DummyCharacterMutation
{
    public string Name { get; set; } = "ForceWall";

    public string EntryName { get; set; } = "Force Wall";

    public string DisplayName { get; set; } = "Force Wall";

    public int Level { get; set; } = 1;

    public string GetDisplayName()
    {
        return DisplayName;
    }

    public DummyMutationEntry GetMutationEntry()
    {
        return new DummyMutationEntry { Name = EntryName };
    }
}

internal sealed class DummyMutationEntry
{
    public string Name { get; set; } = "Force Wall";
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

    public List<DummyMenuOption> renderedDefaultMenuOptions = new List<DummyMenuOption>();

    public List<DummyMenuOption> renderedGetItemMenuOptions = new List<DummyMenuOption>();

    public DummyMenuOption? renderedTakeAll;

    public DummyMenuOption? renderedStoreItem;

    public void UpdateViewFromData(bool reentry)
    {
        _ = reentry;
        renderedDefaultMenuOptions = defaultMenuOptions
            .Select(option => new DummyMenuOption(option.Description, option.InputCommand, option.KeyDescription))
            .ToList();
        renderedGetItemMenuOptions = getItemMenuOptions
            .Select(option => new DummyMenuOption(option.Description, option.InputCommand, option.KeyDescription))
            .ToList();
        renderedTakeAll = new DummyMenuOption(TAKE_ALL.Description, TAKE_ALL.InputCommand, TAKE_ALL.KeyDescription);
        renderedStoreItem = new DummyMenuOption(STORE_ITEM.Description, STORE_ITEM.InputCommand, STORE_ITEM.KeyDescription);
    }
}

internal sealed class DummyInventoryAndEquipmentStatusScreen
{
    public DummyMenuOption CMD_SHOWCYBERNETICS = new DummyMenuOption("Toggle Cybernetics");

    public DummyMenuOption CMD_OPTIONS = new DummyMenuOption("Display Options");

    public DummyMenuOption SET_PRIMARY_LIMB = new DummyMenuOption("Set Primary Limb");

    public DummyMenuOption SHOW_TOOLTIP = new DummyMenuOption("[{{W|Alt}}] Show Tooltip", "CmdShowTooltip", "Alt");

    public DummyMenuOption QUICK_DROP = new DummyMenuOption("Quick Drop");

    public DummyMenuOption QUICK_EAT = new DummyMenuOption("Quick Eat");

    public DummyMenuOption QUICK_DRINK = new DummyMenuOption("Quick Drink");

    public DummyMenuOption QUICK_APPLY = new DummyMenuOption("Quick Apply");

    public DummyUITextSkin priceText = new DummyUITextSkin();

    public DummyUITextSkin weightText = new DummyUITextSkin();

    public DummyUITextSkin cyberneticsHotkeySkin = new DummyUITextSkin();

    public DummyUITextSkin cyberneticsHotkeySkinForList = new DummyUITextSkin();

    public bool showCybernetics;

    public void UpdateViewFromData()
    {
        priceText.SetText("{{B|$42}}");
        weightText.SetText("{{C|12{{K|/34}} lbs. }}");
        cyberneticsHotkeySkin.text = showCybernetics ? "{{hotkey|[~Toggle]}} show equipment" : "{{hotkey|[~Toggle]}} show cybernetics";
        cyberneticsHotkeySkin.Apply();
        cyberneticsHotkeySkinForList.text = showCybernetics ? "{{hotkey|[~Toggle]}} show equipment" : "{{hotkey|[~Toggle]}} show cybernetics";
        cyberneticsHotkeySkinForList.Apply();
        _ = CMD_OPTIONS;
    }
}

internal sealed class DummyCharacterStatusMutationScreen
{
    public static DummyMenuOption BUY_MUTATION = new DummyMenuOption("Buy Mutation", "CmdStatusBuyMutation", "Buy Mutation");

    public static DummyMenuOption SHOW_EFFECTS = new DummyMenuOption("Show Effects", "CmdStatusShowEffects", "Show Effects");

    public DummyUITextSkin mutationNameText = new DummyUITextSkin();

    public DummyUITextSkin mutationRankText = new DummyUITextSkin();

    public DummyUITextSkin mutationTypeText = new DummyUITextSkin();

    public DummyUITextSkin mutationsDetails = new DummyUITextSkin();

    public static void ResetDefaults()
    {
        BUY_MUTATION = new DummyMenuOption("Buy Mutation", "CmdStatusBuyMutation", "Buy Mutation");
        SHOW_EFFECTS = new DummyMenuOption("Show Effects", "CmdStatusShowEffects", "Show Effects");
    }

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
