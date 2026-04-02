namespace QudJP.Tests.DummyTargets;

internal sealed class DummyGameObjectDie
{
    public string? LastReason;
    public string? LastThirdPersonReason;

    public bool Die(
        object? Killer = null,
        string? KillerText = null,
        string? Reason = null,
        string? ThirdPersonReason = null,
        bool Accidental = false)
    {
        LastReason = Reason;
        LastThirdPersonReason = ThirdPersonReason;
        return true;
    }

    public void Reset()
    {
        LastReason = null;
        LastThirdPersonReason = null;
    }
}

internal static class DummyPopupShow
{
    public static string? LastShowMessage;
    public static string? LastShowYesNoMessage;

    public static void Show(
        string Message,
        string? Title = null,
        string? Sound = null,
        bool CopyScrap = true,
        bool Capitalize = true,
        bool DimBackground = true,
        bool LogMessage = true)
    {
        LastShowMessage = Message;
    }

    public static int ShowYesNo(
        string Message,
        string? Sound = null,
        bool AllowEscape = true,
        int defaultResult = 0)
    {
        LastShowYesNoMessage = Message;
        return 0;
    }

    public static void ShowFail(
        string Message,
        bool CopyScrap = true,
        bool Capitalize = true,
        bool DimBackground = true)
    {
        LastShowMessage = Message;
    }

    public static void Reset()
    {
        LastShowMessage = null;
        LastShowYesNoMessage = null;
    }
}
