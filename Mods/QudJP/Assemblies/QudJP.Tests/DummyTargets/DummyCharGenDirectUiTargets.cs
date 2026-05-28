using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCharGenAttributeSelectionControlTarget
{
    public DummyCharGenDirectAttributeDataElement data = new();

    public DummyTooltipTrigger tooltip = new();

    public DummyCharGenTitledIconButton TitleButton { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Updated()
    {
        tooltip.SetText("BodyText", data.BonusSource);
        TitleButton.SetTitle("[" + data.APToRaise + "pts]");
    }
}

internal sealed class DummyCharGenDirectAttributeDataElement
{
    public string BonusSource { get; set; } = string.Empty;

    public int APToRaise { get; set; }
}

internal sealed class DummyTooltipTrigger
{
    public string? LastKey { get; private set; }

    public string? LastText { get; private set; }

    public void SetText(string key, string text)
    {
        LastKey = key;
        LastText = text;
    }
}

internal sealed class DummyCharGenTitledIconButton
{
    public string? Title { get; private set; }

    public void SetTitle(string title)
    {
        Title = title;
    }
}

internal sealed class DummyQudSubtypeModuleWindowTarget
{
    public DummyHorizontalScroller prefabComponent { get; } = new();

    public string SubtypeTitle { get; set; } = "choose subtype";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BeforeShow(DummyEmbarkBuilderModuleWindowDescriptor descriptor)
    {
        prefabComponent.titleText.SetText(":" + getSubtypeTitle() + ":");
    }

    public string getSubtypeTitle()
    {
        return SubtypeTitle;
    }
}

internal sealed class DummyHorizontalScroller
{
    public DummyTextMesh titleText { get; } = new();
}

internal sealed class DummyTextMesh
{
    public string? text { get; private set; }

    public void SetText(string value)
    {
        text = value;
    }
}
