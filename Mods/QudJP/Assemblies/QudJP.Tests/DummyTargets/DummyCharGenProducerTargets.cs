using System.Collections.Generic;
using System.Threading.Tasks;

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

internal sealed class DummyCharGenCustomizePrefixMenuOption
{
    public string? Prefix { get; set; }

    public string? Description { get; set; }
}

internal sealed class DummyChoiceWithColorIcon
{
    public string? Title { get; set; }

    public string? Description { get; set; }
}

internal sealed class DummyCharGenSubtypeModuleTarget
{
    public string Description { get; set; } = """
        {{c|ù}} +2 Agility
        {{c|ù}} Short Blade
        {{c|ù}} Tinkering
          {{C|ù}} Scavenger
        {{c|ù}} Acrobatics
          {{C|ù}} Spry
        {{c|ù}} Starts with random junk and artifacts
        """;

    public IEnumerable<DummyChoiceWithColorIcon> GetSelections()
    {
        yield return new DummyChoiceWithColorIcon
        {
            Title = "Arconaut",
            Description = Description,
        };
    }
}

internal sealed class DummyCharGenCustomizeWindowTarget
{
    public IEnumerable<DummyCharGenCustomizePrefixMenuOption> GetSelections()
    {
        yield return new DummyCharGenCustomizePrefixMenuOption
        {
            Prefix = "Gender: ",
            Description = "Male",
        };
        yield return new DummyCharGenCustomizePrefixMenuOption
        {
            Prefix = "Pronoun Set: ",
            Description = "<from gender>",
        };
        yield return new DummyCharGenCustomizePrefixMenuOption
        {
            Prefix = "Pet: ",
            Description = "<none>",
        };
    }

    public static async Task ShowNamePromptAsync()
    {
        await Task.Yield();
        DummyPopupTarget.ShowBlock("Enter name:");
    }

    public static async Task ShowChooseGenderAsync()
    {
        await Task.Yield();
        DummyPopupTarget.ShowOptionList(
            Title: "Choose Gender",
            Options: new List<string> { "<create new>" });
    }

    public static async Task ShowChoosePronounSetAsync()
    {
        await Task.Yield();
        DummyPopupTarget.ShowOptionList(
            Title: "Choose Pronoun Set",
            Options: new List<string> { "<from gender>", "<create new>" });
    }

    public static async Task ShowChoosePetAsync()
    {
        await Task.Yield();
        DummyPopupTarget.ShowOptionList(
            Title: "Choose Pet",
            Options: new List<string> { "<none>" });
    }

    public static async Task ShowEmptyPromptAsync()
    {
        await Task.Yield();
        DummyPopupTarget.ShowBlock(string.Empty);
    }

    public static async Task ShowMarkedChooseGenderAsync()
    {
        await Task.Yield();
        DummyPopupTarget.ShowOptionList(
            Title: "\u0001Choose Gender",
            Options: new List<string> { "\u0001<create new>" });
    }

    public static async Task ShowColorTaggedChooseGenderAsync()
    {
        await Task.Yield();
        DummyPopupTarget.ShowOptionList(
            Title: string.Concat("{{y|", "Choose Gender", "}}"),
            Options: new List<string> { string.Concat("{{y|", "<create new>", "}}") });
    }
}
