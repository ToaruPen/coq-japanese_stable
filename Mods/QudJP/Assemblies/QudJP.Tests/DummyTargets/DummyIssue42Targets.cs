namespace QudJP.Tests.DummyTargets;

internal sealed class DummyStatistic
{
    public string HelpText { get; set; } = string.Empty;

    public string GetHelpText()
    {
        return HelpText;
    }
}

internal sealed class DummyAttributeHighlightElement
{
    public string TargetField { get; set; } = "primary";

    public DummyStatistic? statistic { get; set; }
}

internal sealed class DummyCharacterAttributeHighlightScreen
{
    public DummyUITextSkin primaryAttributesDetails = new DummyUITextSkin();

    public DummyUITextSkin secondaryAttributesDetails = new DummyUITextSkin();

    public DummyUITextSkin resistanceAttributesDetails = new DummyUITextSkin();

    public void HandleHighlightAttribute(object? element)
    {
        primaryAttributesDetails.SetText(string.Empty);
        secondaryAttributesDetails.SetText(string.Empty);
        resistanceAttributesDetails.SetText(string.Empty);

        if (element is not DummyAttributeHighlightElement data)
        {
            return;
        }

        var helpText = data.statistic?.GetHelpText()
            ?? "Your {{W|Compute Power (CP)}} scales the bonuses of certain compute-enabled items and cybernetic implants. Your base compute power is 0.";
        if (data.TargetField == "secondary")
        {
            secondaryAttributesDetails.SetText(helpText);
            return;
        }

        if (data.TargetField == "resistance")
        {
            resistanceAttributesDetails.SetText(helpText);
            return;
        }

        primaryAttributesDetails.SetText(helpText);
    }
}

internal sealed class DummySPNode
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

internal sealed class DummySkillsAndPowersDetailScreen
{
    public DummyUITextSkin detailsText = new DummyUITextSkin();

    public DummyUITextSkin skillNameText = new DummyUITextSkin();

    public DummyUITextSkin learnedText = new DummyUITextSkin();

    public DummyUITextSkin requirementsText = new DummyUITextSkin();

    public DummyUITextSkin requiredSkillsText = new DummyUITextSkin();

    public DummyUITextSkin requiredSkillsHeader = new DummyUITextSkin();

    public void UpdateDetailsFromNode(object? node)
    {
        if (node is not DummySPNode skillNode)
        {
            detailsText.SetText(string.Empty);
            skillNameText.SetText(string.Empty);
            learnedText.SetText(string.Empty);
            requirementsText.SetText(string.Empty);
            requiredSkillsText.SetText(string.Empty);
            requiredSkillsHeader.SetText(string.Empty);
            return;
        }

        detailsText.SetText(skillNode.Description);
        skillNameText.SetText(skillNode.Name);
        learnedText.SetText("{{G|[Learned]}}");
        requirementsText.SetText(":: {{C|100}} SP ::\n:: 23 Intelligence ::");
        requiredSkillsHeader.SetText("Required Skills");
        requiredSkillsText.SetText("  :Tinker II [200sp] 23 Intelligence, Tinker I\n:Melee");
    }
}

internal sealed class DummyAbilityBar
{
    public DummyUITextSkin AbilityCommandText = new DummyUITextSkin();

    public DummyUITextSkin CycleCommandText = new DummyUITextSkin();

    public void UpdateAbilitiesText()
    {
        AbilityCommandText.SetText("ABILITIES\npage 1 of 3");
        CycleCommandText.SetText("Previous Page");
    }
}

internal sealed class DummyEffect
{
    public string DescriptionText { get; set; } = string.Empty;

    public string DetailsText { get; set; } = string.Empty;

    public string GetDescription()
    {
        return DescriptionText;
    }

    public string GetDetails()
    {
        return DetailsText;
    }
}

internal sealed class DummyCharacterEffectLineData
{
    public DummyEffect? effect { get; set; }
}

internal sealed class DummyCharacterEffectStatusScreen
{
    public DummyUITextSkin mutationsDetails = new DummyUITextSkin();

    public void HandleHighlightEffect(object? element)
    {
        if (element is not DummyCharacterEffectLineData { effect: not null } data)
        {
            mutationsDetails.SetText(string.Empty);
            return;
        }

        mutationsDetails.SetText(data.effect.GetDescription() + "\n\n" + data.effect.GetDetails());
    }
}

internal static class DummyBookUI
{
    public static string? LastTitle { get; private set; }

    public static string? LastText { get; private set; }

    public static void Reset()
    {
        LastTitle = null;
        LastText = null;
    }

    public static void ShowBook(string title, string text)
    {
        LastTitle = title;
        LastText = text;
    }
}

internal sealed class DummyGameObjectActiveEffectsTarget
{
    public string TitleSuffix { get; set; } = "Rusty";

    public DummyEffect? Effect { get; set; }

    public bool HasEffects => Effect is not null;

    public void ShowActiveEffects()
    {
        var title = "&WActive Effects&Y - " + TitleSuffix;
        if (Effect is null)
        {
            DummyBookUI.ShowBook(title, "No active effects.");
            return;
        }

        DummyBookUI.ShowBook(title, Effect.GetDescription() + "\n\n" + Effect.GetDetails());
    }
}

internal sealed class DummyDescriptionShortDescriptionTarget
{
    private readonly string result;

    public DummyDescriptionShortDescriptionTarget(string result)
    {
        this.result = result;
    }

    public string GetShortDescription(bool useShort, bool useLong, string prefix)
    {
        _ = useShort;
        _ = useLong;
        _ = prefix;
        return result;
    }
}
