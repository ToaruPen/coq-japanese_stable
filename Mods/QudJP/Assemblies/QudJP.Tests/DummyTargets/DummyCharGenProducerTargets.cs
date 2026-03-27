using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCharGenBreadcrumb
{
    public string? Title { get; set; }
}

internal sealed class DummyCharGenMenuOption
{
    public string? Description { get; set; }

    public string? KeyDescription { get; set; }

    public string? InputCommand { get; set; }
}

internal sealed class DummyEmbarkBuilderModuleWindowDescriptor
{
    public string? title;
}

internal sealed class DummyCharGenModuleWindowTarget
{
    public string BreadcrumbTitle { get; set; } = string.Empty;

    public List<DummyCharGenMenuOption> MenuOptions { get; } = new List<DummyCharGenMenuOption>();

    public DummyCharGenBreadcrumb GetBreadcrumb()
    {
        return new DummyCharGenBreadcrumb { Title = BreadcrumbTitle };
    }

    public IEnumerable<DummyCharGenMenuOption> GetKeyMenuBar()
    {
        foreach (var option in MenuOptions)
        {
            yield return option;
        }
    }
}

internal sealed class DummyCharGenFrameworkScrollerTarget
{
    public string? LastTitle { get; private set; }

    public void BeforeShow(DummyEmbarkBuilderModuleWindowDescriptor? descriptor, IEnumerable<DummyFrameworkDataElement>? selections = null)
    {
        _ = selections;
        LastTitle = descriptor?.title;
    }
}

internal sealed class DummyCharGenCategoryMenuControllerTarget
{
    public string? LastTitle { get; private set; }

    public void setData(DummyFrameworkDataElement dataElement)
    {
        LastTitle = dataElement.Title;
    }
}
