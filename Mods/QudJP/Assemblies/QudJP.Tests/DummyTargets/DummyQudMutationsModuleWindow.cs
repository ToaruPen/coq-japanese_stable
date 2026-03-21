namespace QudJP.Tests.DummyTargets;

internal sealed class DummyQudMutationsModuleWindow
{
    public List<DummyMutationCategoryMenuData> categoryMenus =
    [
        new DummyMutationCategoryMenuData(
            new DummyMutationMenuOption("Esper", "Esper", "You only manifest mental mutations, and all of your mutation choices when manifesting a new mutation are mental."),
            new DummyMutationMenuOption("Adrenal Control", "Adrenal Control", "You can regulate your adrenal glands at will.\n\nCooldown: 200 rounds."),
            new DummyMutationMenuOption("Stinger (Confusing Venom)", "Stinger (Confusing Venom) [{{W|V}}]", "Sting things.")),
    ];

    public void UpdateControls()
    {
    }
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
    public DummyMutationMenuOption(string id, string description, string longDescription)
    {
        Id = id;
        Description = description;
        LongDescription = longDescription;
    }

    public string Id { get; set; }

    public string Description { get; set; }

    public string LongDescription { get; set; }
}
