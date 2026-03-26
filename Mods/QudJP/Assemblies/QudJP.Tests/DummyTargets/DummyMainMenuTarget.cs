using System.Collections.Generic;
using System.Linq;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyMainMenuTarget
{
    public static List<DummyMainMenuOption> LeftOptions = new List<DummyMainMenuOption>
    {
        new DummyMainMenuOption("Options", "Pick:Options"),
        new DummyMainMenuOption("Mods", "Pick:Installed Mod Configuration"),
    };

    public static List<DummyMainMenuOption> RightOptions = new List<DummyMainMenuOption>
    {
        new DummyMainMenuOption("Help", "Pick:Help"),
        new DummyMainMenuOption("Credits", "Pick:Credits"),
    };

    public static void ResetDefaults()
    {
        LeftOptions = new List<DummyMainMenuOption>
        {
            new DummyMainMenuOption("Options", "Pick:Options"),
            new DummyMainMenuOption("Mods", "Pick:Installed Mod Configuration"),
        };

        RightOptions = new List<DummyMainMenuOption>
        {
            new DummyMainMenuOption("Help", "Pick:Help"),
            new DummyMainMenuOption("Credits", "Pick:Credits"),
        };

        LastHotkeyChoices = new List<DummyNavMenuOption>();
    }

    public DummyFrameworkScroller hotkeyBar = new DummyFrameworkScroller();

    public void Show()
    {
        UpdateMenuBars();
    }

    public static List<DummyNavMenuOption> LastHotkeyChoices { get; private set; } = new List<DummyNavMenuOption>();

    public void UpdateMenuBars()
    {
        var list = new List<DummyNavMenuOption>
        {
            new DummyNavMenuOption("navigate") { InputCommand = "NavigationXYAxis" },
            new DummyNavMenuOption("select") { KeyDescription = "Enter" },
            new DummyNavMenuOption("quit") { KeyDescription = "Esc" },
        };

        hotkeyBar.BeforeShow(null, list);
        LastHotkeyChoices = hotkeyBar.choices;
    }
}

internal sealed class DummyMainMenuOption
{
    public DummyMainMenuOption(string text, string command)
    {
        Text = text;
        Command = command;
    }

    public string Text;

    public string Command;
}

internal sealed class DummyFrameworkScroller
{
    public List<DummyNavMenuOption> choices = new List<DummyNavMenuOption>();

    public void BeforeShow(object? descriptor, IEnumerable<DummyNavMenuOption>? selections = null)
    {
        _ = descriptor;
        choices = selections?.ToList() ?? new List<DummyNavMenuOption>();
    }
}

internal sealed class DummyNavMenuOption
{
    public DummyNavMenuOption(string description)
    {
        Description = description;
    }

    public string Description;

    public string? KeyDescription;

    public string? InputCommand;
}
