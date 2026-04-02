#pragma warning disable CS0649

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyHelpScreenTarget
{
    public List<DummyMenuOption> keyMenuOptions = new List<DummyMenuOption>();

    public DummyIssue289FrameworkScroller hotkeyBar = new DummyIssue289FrameworkScroller();

    public void UpdateMenuBars()
    {
        keyMenuOptions.Clear();
        keyMenuOptions.Add(new DummyMenuOption("navigate", "NavigationXYAxis"));
        keyMenuOptions.Add(new DummyMenuOption("Toggle Visibility", "Accept"));
        hotkeyBar.BeforeShow(null, keyMenuOptions);
    }
}

internal sealed class DummyIssue289FrameworkScroller
{
    public List<DummyMenuOption> choices = new List<DummyMenuOption>();

    public void BeforeShow(object? descriptor, IEnumerable<DummyMenuOption>? selections = null)
    {
        _ = descriptor;
        choices = selections?.Select(option => new DummyMenuOption(option.Description, option.InputCommand, option.KeyDescription)).ToList()
            ?? new List<DummyMenuOption>();
    }
}

internal sealed class DummyMessageLogStatusScreenTarget
{
    public bool CompactMode { get; set; }

    public string GetTabString()
    {
        return CompactMode ? "Log" : "Message Log";
    }
}

internal sealed class DummyMessageLogLineDataTarget
{
    public string text { get; set; } = string.Empty;
}

internal sealed class DummyMessageLogLineTarget
{
    public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>
    {
        new DummyMenuOption("Expand", "Accept"),
    };

    public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>
    {
        new DummyMenuOption("Collapse", "Accept"),
    };

    public DummyUITextSkin text = new DummyUITextSkin();

    public static void ResetMenuOptions()
    {
        categoryExpandOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Expand", "Accept"),
        };
        categoryCollapseOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Collapse", "Accept"),
        };
    }

    public void setData(object data)
    {
        if (data is DummyMessageLogLineDataTarget line)
        {
            text.SetText(line.text);
        }
    }
}

internal static class DummyTutorialManagerTarget
{
    public static string LastPopupText { get; private set; } = string.Empty;

    public static string LastButtonText { get; private set; } = string.Empty;

    public static void Reset()
    {
        LastPopupText = string.Empty;
        LastButtonText = string.Empty;
    }

#pragma warning disable CA1068
    public static async Task ShowCIDPopupAsync(
        string cid,
        string text,
        string directionHint = "ne",
        string buttonText = "[~Accept] Continue",
        int paddingX = 16,
        int paddingY = 16,
        float bottomMargin = 0f,
        Action? after = null)
    {
        _ = cid;
        _ = directionHint;
        _ = paddingX;
        _ = paddingY;
        _ = bottomMargin;

        await Task.Yield();

        LastPopupText = text;
        LastButtonText = buttonText;
        after?.Invoke();
    }
#pragma warning restore CA1068
}
