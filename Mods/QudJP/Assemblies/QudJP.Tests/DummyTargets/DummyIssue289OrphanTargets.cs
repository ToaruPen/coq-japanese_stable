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

internal sealed class MessageLogStatusScreen
{
    public string Label { get; set; } = "Message Log";

    public string GetTabString()
    {
        return Label;
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

    public static string LastCellPopupText { get; private set; } = string.Empty;

    public static void Reset()
    {
        LastPopupText = string.Empty;
        LastButtonText = string.Empty;
        LastCellPopupText = string.Empty;
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

    public static async Task ShowCellPopup(
        Genkit.Location2D cell,
        string text,
        string directionHint = "ne",
        int paddingX = 6,
        int paddingY = 6,
        Action? after = null)
    {
        _ = cell;
        _ = directionHint;
        _ = paddingX;
        _ = paddingY;

        await Task.Yield();

        LastCellPopupText = text;
        after?.Invoke();
    }
}

internal interface IDummyRectTransform
{
}

internal sealed class DummyTutorialManagerInstanceTarget
{
    public string LastHighlightText { get; private set; } = string.Empty;

    public string LastCellHighlightText { get; private set; } = string.Empty;

    public string LastDirectHighlightText { get; private set; } = string.Empty;

    public bool HighlightByCID(
        string cid,
        string text,
        string directionHint,
        int paddingX = 64,
        int paddingY = 64,
        float bottomMargin = 0f,
        string style = "horiz")
    {
        _ = cid;
        _ = directionHint;
        _ = paddingX;
        _ = paddingY;
        _ = bottomMargin;
        _ = style;

        LastHighlightText = text is null ? string.Empty : "{{y|" + text + "}}";
        return true;
    }

    public void HighlightCell(
        int x,
        int y,
        string text,
        string directionHint,
        float paddingX = 3f,
        float paddingY = 3f,
        float bottomMargin = 0f)
    {
        _ = x;
        _ = y;
        _ = directionHint;
        _ = paddingX;
        _ = paddingY;
        _ = bottomMargin;

        LastCellHighlightText = text is null ? string.Empty : "{{y|" + text + "}}";
    }

    public void Highlight(
        IDummyRectTransform? target,
        string text,
        string directionHint,
        float paddingX = 64f,
        float paddingY = 64f,
        float bottomMargin = 0f,
        string style = "big")
    {
        _ = target;
        _ = directionHint;
        _ = paddingX;
        _ = paddingY;
        _ = bottomMargin;
        _ = style;

        LastDirectHighlightText = text is null ? string.Empty : "{{y|" + text + "}}";
    }
}
