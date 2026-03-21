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
