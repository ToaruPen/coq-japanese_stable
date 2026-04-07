using System.Threading;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyPopupMessageTarget
{
    public static string LastMessage { get; private set; } = string.Empty;

    public static string LastTitle { get; private set; } = string.Empty;

    public static string LastContextTitle { get; private set; } = string.Empty;

    public static string LastRenderedBodyText { get; private set; } = string.Empty;

    public static string? LastWantsSpecificPrompt { get; private set; }

    public static DummyPopupMessageItem[]? LastButtons { get; private set; }

    public static DummyPopupMessageItem[]? LastItems { get; private set; }

    public static void Reset()
    {
        LastMessage = string.Empty;
        LastTitle = string.Empty;
        LastContextTitle = string.Empty;
        LastRenderedBodyText = string.Empty;
        LastWantsSpecificPrompt = null;
        LastButtons = null;
        LastItems = null;
    }

#pragma warning disable CA1068, S2325
    public void ShowPopup(
        string message,
        List<DummyPopupMessageItem>? buttons = null,
        Action<DummyPopupMessageItem>? commandCallback = null,
        List<DummyPopupMessageItem>? items = null,
        Action<DummyPopupMessageItem>? selectedItemCallback = null,
        string? title = null,
        bool includeInput = false,
        string? inputDefault = null,
        int DefaultSelected = 0,
        Action? onHide = null,
        object? contextRender = null,
        string? contextTitle = null,
        object? afterRender = null,
        bool showContextFrame = true,
        bool pushView = true,
        CancellationToken cancelToken = default,
        bool askingNumber = false,
        string RestrictChars = "",
        string? WantsSpecificPrompt = null,
        object? PopupLocation = null,
        string? PopupID = null)
    {
        _ = commandCallback;
        _ = selectedItemCallback;
        _ = includeInput;
        _ = inputDefault;
        _ = DefaultSelected;
        _ = onHide;
        _ = contextRender;
        _ = afterRender;
        _ = showContextFrame;
        _ = pushView;
        _ = cancelToken;
        _ = askingNumber;
        _ = RestrictChars;
        _ = PopupLocation;
        _ = PopupID;

        LastMessage = message;
        LastTitle = title ?? string.Empty;
        LastContextTitle = contextTitle ?? string.Empty;
        LastRenderedBodyText = "{{y|" + message + "}}";
        LastWantsSpecificPrompt = WantsSpecificPrompt;
        LastButtons = buttons?.ToArray();
        LastItems = items?.ToArray();
    }
#pragma warning restore CA1068, S2325
}

internal sealed class DummyPopupMessageItem
{
    public DummyPopupMessageItem(string text, string hotkey, string command)
    {
        this.text = text;
        this.hotkey = hotkey;
        this.command = command;
    }

    public string text;

    public string hotkey;

    public string command;
}
