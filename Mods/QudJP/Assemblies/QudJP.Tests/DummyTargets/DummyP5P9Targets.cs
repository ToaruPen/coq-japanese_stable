using System.Threading.Tasks;

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
    public static string? LastShowYesNoAsyncMessage;
    public static string? LastShowSpaceMessage;
    public static string? LastShowSpaceTitle;

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

    public static Task<int> ShowYesNoAsync(string Message)
    {
        LastShowYesNoAsyncMessage = Message;
        return Task.FromResult(0);
    }

    public static void ShowSpace(
        string Message,
        string Title = "",
        string? Sound = null,
        object? SeparatorImage = null,
        bool CopyScrap = true,
        bool Capitalize = true,
        string? DimBackground = null)
    {
        _ = Sound;
        _ = SeparatorImage;
        _ = CopyScrap;
        _ = Capitalize;
        _ = DimBackground;
        LastShowSpaceMessage = Message;
        LastShowSpaceTitle = Title;
    }

    public static void Reset()
    {
        LastShowMessage = null;
        LastShowYesNoMessage = null;
        LastShowYesNoAsyncMessage = null;
        LastShowSpaceMessage = null;
        LastShowSpaceTitle = null;
    }
}
