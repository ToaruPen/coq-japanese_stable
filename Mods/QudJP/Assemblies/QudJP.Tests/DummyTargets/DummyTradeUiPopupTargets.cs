namespace QudJP.Tests.DummyTargets;

internal static class DummyTradeUiPopupTarget
{
    public static string LastShowMessage { get; private set; } = string.Empty;

    public static string LastShowYesNoMessage { get; private set; } = string.Empty;

    public static string LastShowBlockMessage { get; private set; } = string.Empty;

    public static void Reset()
    {
        LastShowMessage = string.Empty;
        LastShowYesNoMessage = string.Empty;
        LastShowBlockMessage = string.Empty;
    }

    public static void Show(
        string Message,
        string? Title = null,
        string Sound = "Sounds/UI/ui_notification",
        bool CopyScrap = true,
        bool Capitalize = true,
        bool DimBackground = true,
        bool LogMessage = true,
        object? PopupLocation = null)
    {
        _ = Title;
        _ = Sound;
        _ = CopyScrap;
        _ = Capitalize;
        _ = DimBackground;
        _ = LogMessage;
        _ = PopupLocation;

        LastShowMessage = Message;
    }

    public static int ShowYesNo(
        string Message,
        string Sound = "Sounds/UI/ui_notification",
        bool AllowEscape = true,
        int defaultResult = 0)
    {
        _ = Sound;
        _ = AllowEscape;
        _ = defaultResult;

        LastShowYesNoMessage = Message;
        return defaultResult;
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
        _ = Title;
        _ = Sound;
        _ = CopyScrap;
        _ = Capitalize;
        _ = DimBackground;
        _ = LogMessage;
        _ = PopupLocation;

        LastShowBlockMessage = Message;
        return 0;
    }
}
