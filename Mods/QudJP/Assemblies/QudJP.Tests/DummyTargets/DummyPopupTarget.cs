namespace QudJP.Tests.DummyTargets;

internal static class DummyPopupTarget
{
    public static string LastShowBlockMessage { get; private set; } = string.Empty;

    public static string LastShowBlockTitle { get; private set; } = string.Empty;

    public static string LastOptionListTitle { get; private set; } = string.Empty;

    public static IReadOnlyList<string>? LastOptionListOptions { get; private set; }

    public static string LastOptionListIntro { get; private set; } = string.Empty;

    public static string LastOptionListSpacingText { get; private set; } = string.Empty;

    public static IReadOnlyList<DummyPopupMenuItem>? LastOptionListButtons { get; private set; }

    public static string LastShowConversationTitle { get; private set; } = string.Empty;

    public static string LastShowConversationIntro { get; private set; } = string.Empty;

    public static IReadOnlyList<string>? LastShowConversationOptions { get; private set; }

    public static void Reset()
    {
        LastShowBlockMessage = string.Empty;
        LastShowBlockTitle = string.Empty;
        LastOptionListTitle = string.Empty;
        LastOptionListOptions = null;
        LastOptionListIntro = string.Empty;
        LastOptionListSpacingText = string.Empty;
        LastOptionListButtons = null;
        LastShowConversationTitle = string.Empty;
        LastShowConversationIntro = string.Empty;
        LastShowConversationOptions = null;
    }

    public static int ShowBlock(
        string Message,
        string? Title = null,
        string Sound = "Sounds/UI/ui_notification",
        bool CopyScrap = true,
        bool Capitalize = true,
        bool DimBackground = true,
        bool LogMessage = true,
        object? PopupLocation = null)
    {
        _ = Sound;
        _ = CopyScrap;
        _ = Capitalize;
        _ = DimBackground;
        _ = LogMessage;
        _ = PopupLocation;

        LastShowBlockMessage = Message;
        LastShowBlockTitle = Title ?? string.Empty;
        return 0;
    }

    public static int ShowOptionList(
        string Title = "",
        IReadOnlyList<string>? Options = null,
        IReadOnlyList<char>? Hotkeys = null,
        int Spacing = 0,
        string? Intro = null,
        int MaxWidth = 60,
        bool RespectOptionNewlines = false,
        bool AllowEscape = false,
        int DefaultSelected = 0,
        string SpacingText = "",
        Action<int>? onResult = null,
        object? context = null,
        IReadOnlyList<object>? Icons = null,
        object? IntroIcon = null,
        IReadOnlyList<DummyPopupMenuItem>? Buttons = null,
        bool centerIntro = false,
        bool centerIntroIcon = true,
        int iconPosition = -1,
        bool forceNewPopup = false)
    {
        _ = Hotkeys;
        _ = Spacing;
        _ = MaxWidth;
        _ = RespectOptionNewlines;
        _ = AllowEscape;
        _ = DefaultSelected;
        _ = onResult;
        _ = context;
        _ = Icons;
        _ = IntroIcon;
        _ = centerIntro;
        _ = centerIntroIcon;
        _ = iconPosition;
        _ = forceNewPopup;

        LastOptionListTitle = Title;
        LastOptionListOptions = Options;
        LastOptionListIntro = Intro ?? string.Empty;
        LastOptionListSpacingText = SpacingText;
        LastOptionListButtons = Buttons;
        return 0;
    }

#pragma warning disable S1133 // Test dummy intentionally mirrors deprecated game overload ordering.
    [Obsolete("Use ShowConversation(object? Icon)")]
    public static int ShowConversation(
        string Title,
        string? Context,
        string? Intro = null,
        List<string>? Options = null,
        bool AllowTrade = false,
        bool AllowEscape = true,
        bool AllowRenderMapBehind = false)
    {
        _ = Context;

        return ShowConversation(
            Title,
            Icon: null,
            Intro,
            Options,
            AllowTrade,
            AllowEscape,
            AllowRenderMapBehind);
    }
#pragma warning restore S1133

    public static int ShowConversation(
        string Title,
        object? Icon = null,
        string? Intro = null,
        List<string>? Options = null,
        bool AllowTrade = false,
        bool AllowEscape = true,
        bool AllowRenderMapBehind = false)
    {
        _ = Icon;
        _ = AllowTrade;
        _ = AllowEscape;
        _ = AllowRenderMapBehind;

        LastShowConversationTitle = Title;
        LastShowConversationIntro = Intro ?? string.Empty;
        LastShowConversationOptions = Options is null ? null : new List<string>(Options);
        return 0;
    }
}

internal sealed class DummyPopupMenuItem
{
    public DummyPopupMenuItem(string text)
    {
        this.text = text;
    }

    public string text;
}
