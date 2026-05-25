namespace QudJP.Tests.DummyTargets;

internal sealed class DummyQudMutationsModuleWindow
{
    public DummyMutationCategoryMenusScroller prefabComponent = new();

    public List<DummyMutationCategoryMenuData> categoryMenus =
    [
        new DummyMutationCategoryMenuData(
            new DummyMutationMenuOption("Esper", "Esper", "You only manifest mental mutations, and all of your mutation choices when manifesting a new mutation are mental."),
            new DummyMutationMenuOption("Adrenal Control", "Adrenal Control", "You can regulate your adrenal glands at will.\n\nCooldown: 200 rounds."),
            new DummyMutationMenuOption("Stinger (Confusing Venom)", "Stinger (Confusing Venom) [{{W|V}}]", "Sting things.", hasVariantSelector: true)),
    ];

    public List<DummyMutationNode> mutationNodes =
    [
        new DummyMutationNode("Esper"),
        new DummyMutationNode("Adrenal Control"),
        new DummyMutationNode("Stinger (Confusing Venom)"),
    ];

    public void UpdateControls()
    {
        foreach (var categoryMenu in categoryMenus)
        {
            foreach (var menuOption in categoryMenu.menuOptions)
            {
                menuOption.Description = FormatNodeDescription(menuOption, menuOption.Id);
            }
        }

        prefabComponent.BeforeShow(null, categoryMenus);
    }

    private static string FormatNodeDescription(DummyMutationMenuOption menuOption, string entryId)
    {
        return menuOption.HasVariantSelector
            ? string.Concat(entryId, " [{{W|V}}]")
            : entryId;
    }
}

internal sealed class DummyMutationNode
{
    public DummyMutationNode(string entryName, string variant = "")
    {
        Entry = new DummyMutationWindowEntry(entryName, variant);
        Variant = variant;
        Exemplar = new DummyMutationWindowExemplar(variant);
    }

    public DummyMutationWindowEntry Entry { get; }

    public string Variant { get; set; }

    public DummyMutationWindowExemplar Exemplar { get; }
}

internal sealed class DummyMutationWindowEntry
{
    public DummyMutationWindowEntry(string name, string variant)
    {
        Name = name;
        Variant = variant;
    }

    public string Name { get; }

    public string Variant { get; }
}

internal sealed class DummyMutationWindowExemplar
{
    public DummyMutationWindowExemplar(string variant)
    {
        Variant = variant;
    }

    public string Variant { get; }
}

internal sealed class DummyMutationCategoryMenuData
{
    public DummyMutationCategoryMenuData(params DummyMutationMenuOption[] menuOptions)
    {
        this.menuOptions = new List<DummyMutationMenuOption>(menuOptions);
    }

    public List<DummyMutationMenuOption> menuOptions;
}

internal sealed class DummyMutationMenuOption
{
    public DummyMutationMenuOption(string id, string description, string longDescription, bool hasVariantSelector = false)
    {
        Id = id;
        Description = description;
        LongDescription = longDescription;
        HasVariantSelector = hasVariantSelector;
    }

    public string Id { get; set; }

    public string Description { get; set; }

    public string LongDescription { get; set; }

    public bool HasVariantSelector { get; }
}

internal sealed class DummyMutationCategoryMenusScroller
{
    public List<string> LastRenderedDescriptions { get; } = [];
    public List<string> LastRenderedLongDescriptions { get; } = [];

    public void BeforeShow(object? descriptor, IEnumerable<DummyMutationCategoryMenuData> selections)
    {
        LastRenderedDescriptions.Clear();
        LastRenderedDescriptions.AddRange(
            selections.SelectMany(static categoryMenu => categoryMenu.menuOptions)
                .Select(static menuOption => menuOption.Description));
        LastRenderedLongDescriptions.Clear();
        LastRenderedLongDescriptions.AddRange(
            selections.SelectMany(static categoryMenu => categoryMenu.menuOptions)
                .Select(static menuOption => menuOption.LongDescription));
    }
}
