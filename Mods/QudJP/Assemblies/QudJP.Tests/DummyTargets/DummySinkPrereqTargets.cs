namespace QudJP.Tests.DummyTargets;

internal sealed class DummyUITextSkinField
{
    public string? text;

    public void SetText(string value)
    {
        text = value;
    }
}

internal sealed class DummyFrameworkDataElement
{
    public string? Description { get; set; }
    public string? Title { get; set; }
    public string? LongDescription { get; set; }
}

internal sealed class DummyLeftSideCategory
{
    public DummyUITextSkinField text = new DummyUITextSkinField();

    public void setData(DummyFrameworkDataElement data)
    {
        text.SetText("{{C|" + (data.Description ?? "") + "}}");
    }
}

internal sealed class DummyFrameworkHeader
{
    public DummyUITextSkinField textSkin = new DummyUITextSkinField();

    public void setData(DummyFrameworkDataElement data)
    {
        textSkin.SetText(data.Description ?? "");
    }
}

internal sealed class DummySummaryBlockControl
{
    public DummyUITextSkinField text = new DummyUITextSkinField();
    public DummyUITextSkinField title = new DummyUITextSkinField();

    public void setData(DummyFrameworkDataElement data)
    {
        text.SetText(data.Description ?? "");
        title.SetText("{{W|" + (data.Title ?? "") + "}}");
    }
}

internal sealed class DummyObjectFinderLine
{
    public DummyUITextSkinField RightText = new DummyUITextSkinField();
    public DummyUITextSkinField ObjectDescription = new DummyUITextSkinField();

    public void setData(DummyFrameworkDataElement data)
    {
        RightText.SetText(data.Title ?? "");
        ObjectDescription.SetText(data.Description ?? "");
    }
}

internal sealed class DummyCharacterEffectLine
{
    public DummyUITextSkinField text = new DummyUITextSkinField();

    public void setData(DummyFrameworkDataElement data)
    {
        text.SetText(data.Description ?? "");
    }
}

internal sealed class DummyCharacterAttributeLine
{
    public DummyUITextSkinField attributeText = new DummyUITextSkinField();

    public void setData(DummyFrameworkDataElement data)
    {
        attributeText.SetText(data.Description ?? "");
    }
}

internal sealed class DummyTinkeringDetailsLine
{
    public DummyUITextSkinField text = new DummyUITextSkinField();
    public DummyUITextSkinField descriptionText = new DummyUITextSkinField();
    public DummyUITextSkinField modDescriptionText = new DummyUITextSkinField();
    public DummyUITextSkinField modBitCostText = new DummyUITextSkinField();
    public DummyUITextSkinField requirementsHeaderText = new DummyUITextSkinField();

    public void setData(DummyFrameworkDataElement data)
    {
        text.SetText(data.Title ?? "");
        descriptionText.SetText(data.Description ?? "");
        modDescriptionText.SetText(data.LongDescription ?? "");
    }
}

internal sealed class DummyCategoryMenusScroller
{
    public DummyUITextSkinField selectedTitleText = new DummyUITextSkinField();
    public DummyUITextSkinField selectedDescriptionText = new DummyUITextSkinField();

    public void UpdateDescriptions(DummyFrameworkDataElement data)
    {
        selectedTitleText.SetText(data.Description ?? "");
        selectedDescriptionText.SetText(data.LongDescription ?? "");
    }
}

internal sealed class DummyTitledIconButton
{
    public DummyUITextSkinField TitleText = new DummyUITextSkinField();
    public string? Title;

    public void Update()
    {
        if (Title is not null)
        {
            TitleText.SetText(Title);
        }
    }
}

internal sealed class DummyCyberneticsTerminalRow
{
    public DummyUITextSkinField description = new DummyUITextSkinField();
    public string? DataText;

    public void Update()
    {
        if (DataText is not null)
        {
            description.SetText(DataText);
        }
    }
}

internal sealed class DummyAbilityManagerScreen
{
    public DummyUITextSkinField rightSideHeaderText = new DummyUITextSkinField();
    public DummyUITextSkinField rightSideDescriptionArea = new DummyUITextSkinField();

    public void HandleHighlightLeft(DummyFrameworkDataElement element)
    {
        rightSideHeaderText.SetText(element.Description ?? "");
        rightSideDescriptionArea.SetText(element.LongDescription ?? "");
    }
}

internal sealed class DummyMapScrollerPinItem
{
    public DummyUITextSkinField titleText = new DummyUITextSkinField();
    public DummyUITextSkinField detailsText = new DummyUITextSkinField();

    public void SetData(DummyFrameworkDataElement data)
    {
        titleText.SetText(data.Title ?? "");
        detailsText.SetText(data.Description ?? "");
    }
}

internal sealed class DummyPlayerStatusBar
{
    public DummyUITextSkinField ZoneText = new DummyUITextSkinField();
    public string? ZoneString;

    public void Update()
    {
        if (ZoneString is not null)
        {
            ZoneText.SetText(ZoneString);
        }
    }
}

internal sealed class DummyTradeScreen
{
    public DummyUITextSkinField detailsRightText = new DummyUITextSkinField();
    public DummyUITextSkinField detailsLeftText = new DummyUITextSkinField();

    public void HandleHighlightObject(DummyFrameworkDataElement element)
    {
        detailsRightText.SetText(element.Description ?? "");
        detailsLeftText.SetText(element.Title ?? "");
    }
}
