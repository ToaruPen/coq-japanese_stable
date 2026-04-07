using System.Threading.Tasks;

namespace QudJP.Tests.DummyTargets;

internal static class DummyPopupGenericTarget
{
    public static string LastPickOptionTitle { get; private set; } = string.Empty;

    public static string LastPickOptionIntro { get; private set; } = string.Empty;

    public static string LastPickOptionSpacingText { get; private set; } = string.Empty;

    public static IReadOnlyList<string>? LastPickOptionOptions { get; private set; }

    public static IReadOnlyList<DummyPopupMenuItem>? LastPickOptionButtons { get; private set; }

    public static string LastAskStringMessage { get; private set; } = string.Empty;

    public static string LastAskNumberMessage { get; private set; } = string.Empty;

    public static string LastShowSpaceMessage { get; private set; } = string.Empty;

    public static string LastShowSpaceTitle { get; private set; } = string.Empty;

    public static void Reset()
    {
        LastPickOptionTitle = string.Empty;
        LastPickOptionIntro = string.Empty;
        LastPickOptionSpacingText = string.Empty;
        LastPickOptionOptions = null;
        LastPickOptionButtons = null;
        LastAskStringMessage = string.Empty;
        LastAskNumberMessage = string.Empty;
        LastShowSpaceMessage = string.Empty;
        LastShowSpaceTitle = string.Empty;
    }

    public static int PickOption(
        string Title = "",
        string? Intro = null,
        string SpacingText = "",
        string Sound = "Sounds/UI/ui_notification",
        IReadOnlyList<string>? Options = null,
        IReadOnlyList<char>? Hotkeys = null,
        IReadOnlyList<object>? Icons = null,
        IReadOnlyList<DummyPopupMenuItem>? Buttons = null,
        object? Context = null,
        object? IntroIcon = null,
        Action<int>? OnResult = null,
        int Spacing = 0,
        int MaxWidth = 60,
        int DefaultSelected = 0,
        int IconPosition = -1,
        bool AllowEscape = false,
        bool RespectOptionNewlines = false,
        bool CenterIntro = false,
        bool CenterIntroIcon = true,
        bool ForceNewPopup = false,
        object? PopupLocation = null,
        string? PopupID = null)
    {
        _ = SpacingText;
        _ = Sound;
        _ = Hotkeys;
        _ = Icons;
        _ = Buttons;
        _ = Context;
        _ = IntroIcon;
        _ = OnResult;
        _ = Spacing;
        _ = MaxWidth;
        _ = DefaultSelected;
        _ = IconPosition;
        _ = AllowEscape;
        _ = RespectOptionNewlines;
        _ = CenterIntro;
        _ = CenterIntroIcon;
        _ = ForceNewPopup;
        _ = PopupLocation;
        _ = PopupID;

        LastPickOptionTitle = Title;
        LastPickOptionIntro = Intro ?? string.Empty;
        LastPickOptionSpacingText = SpacingText;
        LastPickOptionOptions = Options is null ? null : new List<string>(Options);
        LastPickOptionButtons = Buttons is null ? null : new List<DummyPopupMenuItem>(Buttons);
        return 0;
    }

    public static string AskString(
        string Message,
        string Default = "",
        string Sound = "Sounds/UI/ui_notification",
        string? RestrictChars = null,
        string? WantsSpecificPrompt = null,
        int MaxLength = 80,
        int MinLength = 0,
        bool ReturnNullForEscape = false,
        bool EscapeNonMarkupFormatting = true,
        bool? AllowColorize = null)
    {
        _ = Default;
        _ = Sound;
        _ = RestrictChars;
        _ = WantsSpecificPrompt;
        _ = MaxLength;
        _ = MinLength;
        _ = ReturnNullForEscape;
        _ = EscapeNonMarkupFormatting;
        _ = AllowColorize;

        LastAskStringMessage = Message;
        return Default;
    }

    public static Task<string> AskStringAsync(
        string Message,
        string Default = "",
        int MaxLength = 80,
        int MinLength = 0,
        string? RestrictChars = null,
        bool ReturnNullForEscape = false,
        bool EscapeNonMarkupFormatting = true,
        bool? AllowColorize = null,
        bool pushView = false,
        string? WantsSpecificPrompt = null)
    {
        _ = Default;
        _ = MaxLength;
        _ = MinLength;
        _ = RestrictChars;
        _ = ReturnNullForEscape;
        _ = EscapeNonMarkupFormatting;
        _ = AllowColorize;
        _ = pushView;
        _ = WantsSpecificPrompt;

        LastAskStringMessage = Message;
        return Task.FromResult(Default);
    }

    public static DummyPopupMenuItem GetPopupOption(
        int Index,
        IReadOnlyList<string> Options,
        IReadOnlyList<char>? Hotkeys = null,
        IReadOnlyList<object>? Icons = null)
    {
        _ = Icons;

        var hasHotkeys = Hotkeys is { Count: > 0 };
        var hotkey = hasHotkeys && Index < Hotkeys!.Count ? Hotkeys[Index] : '\0';
        if (hotkey == ' ')
        {
            hotkey = '\0';
        }

        string text;
        if (!hasHotkeys)
        {
            text = "{{y|" + Options[Index] + "}}";
        }
        else if (hotkey == '\0')
        {
            text = "    {{y|" + Options[Index] + "}}";
        }
        else
        {
            text = "{{W|[" + hotkey + "]}} {{y|" + Options[Index] + "}}";
        }

        return new DummyPopupMenuItem(text);
    }

    public static int? AskNumber(
        string Message,
        string Sound = "Sounds/UI/ui_notification",
        string RestrictChars = "",
        int Start = 0,
        int Min = 0,
        int Max = int.MaxValue)
    {
        _ = Sound;
        _ = RestrictChars;
        _ = Start;
        _ = Min;
        _ = Max;

        LastAskNumberMessage = Message;
        return Start;
    }

    public static Task<int?> AskNumberAsync(
        string Message,
        int Start = 0,
        int Min = 0,
        int Max = int.MaxValue,
        string RestrictChars = "",
        bool pushView = false)
    {
        _ = Start;
        _ = Min;
        _ = Max;
        _ = RestrictChars;
        _ = pushView;

        LastAskNumberMessage = Message;
        return Task.FromResult<int?>(Start);
    }

    public static Task<int?> AskNumberAsyncGamepad(
        string Message,
        int Start = 0,
        int Min = 0,
        int Max = int.MaxValue,
        string RestrictChars = "",
        bool pushView = false)
    {
        _ = RestrictChars;
        _ = pushView;

        LastAskNumberMessage = Message;
        _ = DummyAskNumberScreenTarget.Show(Message, Start, Min, Max);
        return Task.FromResult<int?>(Start);
    }

    public static void ShowSpace(
        string Message,
        string? Title = null,
        string Sound = "Sounds/UI/ui_notification",
        object? AfterRender = null,
        bool LogMessage = true,
        bool ShowContextFrame = true,
        string? PopupID = null)
    {
        _ = Sound;
        _ = AfterRender;
        _ = LogMessage;
        _ = ShowContextFrame;
        _ = PopupID;

        LastShowSpaceMessage = Message;
        LastShowSpaceTitle = Title ?? string.Empty;
    }
}

internal static class DummyAskNumberScreenTarget
{
    public static string LastMessage { get; private set; } = string.Empty;

    public static void Reset()
    {
        LastMessage = string.Empty;
    }

    public static string Show(string Message, int Start = 0, int Min = 0, int Max = int.MaxValue)
    {
        _ = Start;
        _ = Min;
        _ = Max;

        LastMessage = Message;
        return Message;
    }
}
