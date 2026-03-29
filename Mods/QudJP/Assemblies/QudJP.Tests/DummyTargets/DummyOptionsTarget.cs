using System.Linq;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyOptionsTarget
{
    public readonly List<DummyOptionsRow> menuItems = new List<DummyOptionsRow>();

    public readonly List<DummyOptionsRow> filteredMenuItems = new List<DummyOptionsRow>();

    public List<DummyOptionsRow> renderedMenuItems = new List<DummyOptionsRow>();

    public List<DummyOptionsRow> renderedFilteredMenuItems = new List<DummyOptionsRow>();

    public static List<DummyMenuOption> renderedDefaultMenuOptions = new List<DummyMenuOption>();

    public static List<DummyMenuOption> defaultMenuOptions = new List<DummyMenuOption>
    {
        new DummyMenuOption("Collapse All"),
        new DummyMenuOption("Help"),
    };

    public static void ResetDefaults()
    {
        defaultMenuOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Collapse All"),
            new DummyMenuOption("Help"),
        };
        renderedDefaultMenuOptions = new List<DummyMenuOption>();
    }

    public void Show()
    {
        renderedMenuItems = menuItems.Select(row => new DummyOptionsRow(row.Title, row.HelpText)).ToList();
        renderedFilteredMenuItems = filteredMenuItems.Select(row => new DummyOptionsRow(row.Title, row.HelpText)).ToList();
        renderedDefaultMenuOptions = defaultMenuOptions
            .Select(option => new DummyMenuOption(option.Description, option.InputCommand, option.KeyDescription))
            .ToList();
    }
}

internal sealed class DummyOptionsRow
{
    public DummyOptionsRow(string title, string helpText)
    {
        Title = title;
        HelpText = helpText;
    }

    public string Title;

    public string HelpText;
}

internal sealed class DummyMenuOption
{
    public DummyMenuOption(string description, string? inputCommand = null, string? keyDescription = null)
    {
        Description = description;
        InputCommand = inputCommand;
        KeyDescription = keyDescription;
    }

    public string Description;

    public string? InputCommand;

    public string? KeyDescription;

    public string getMenuText()
    {
        if (!string.IsNullOrEmpty(KeyDescription))
        {
            return "[{{W|" + KeyDescription + "}}] " + Description;
        }

        return Description;
    }
}
