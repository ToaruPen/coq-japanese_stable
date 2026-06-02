using System;
using System.Runtime.CompilerServices;

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

internal sealed class DummyTradeUiVendorPopupProducerTarget
{
    private static readonly char[] DefaultVendorActionHotkeys = { 'l', 't', 'i', 'r', 'c', 'b' };

    public string PopupMessageToShow { get; set; } = string.Empty;

    public bool UseShowFailPopup { get; set; }

    public bool UseConfirmationPopup { get; set; }

    public IReadOnlyList<string>? VendorActionOptions { get; set; }

    public string? LastVendorActionSelection { get; private set; }

    public void ShowTradeScreen()
    {
        ShowConfiguredPopup();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ShowVendorActions()
    {
        var options = VendorActionOptions is null
            ? new List<string> { "Look", "Add to trade", "Identify", "Repair", "Recharge", "Read" }
            : new List<string>(VendorActionOptions);

        var index = DummyPopupGenericTarget.PickOption(
            Title: "select an action",
            Options: options.ToArray(),
            Hotkeys: BuildVendorActionHotkeys(options.Count),
            AllowEscape: true);
        LastVendorActionSelection = index >= 0 && index < options.Count ? options[index] : null;
    }

    private static char[] BuildVendorActionHotkeys(int optionCount)
    {
        if (optionCount <= DefaultVendorActionHotkeys.Length)
        {
            return DefaultVendorActionHotkeys.Take(optionCount).ToArray();
        }

        const string extraHotkeyCandidates = "abcdefghijklmnopqrstuvwxyz0123456789";
        var hotkeys = new List<char>(DefaultVendorActionHotkeys);
        foreach (var candidate in extraHotkeyCandidates)
        {
            if (hotkeys.Contains(candidate))
            {
                continue;
            }

            hotkeys.Add(candidate);
            if (hotkeys.Count == optionCount)
            {
                return hotkeys.ToArray();
            }
        }

        throw new InvalidOperationException("ShowVendorActions does not have enough dummy hotkeys.");
    }

    public void TryRemove()
    {
        DummyPopupTarget.ShowBlock(PopupMessageToShow);
    }

    public void DoVendorExamine()
    {
        ShowConfiguredPopup();
    }

    public void DoVendorRepair()
    {
        ShowConfiguredPopup();
    }

    public bool DoVendorRecharge()
    {
        ShowConfiguredPopup();
        return false;
    }

    private void ShowConfiguredPopup()
    {
        if (UseShowFailPopup)
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
            return;
        }

        if (UseConfirmationPopup)
        {
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
            return;
        }

        DummyPopupShow.Show(PopupMessageToShow);
    }
}
