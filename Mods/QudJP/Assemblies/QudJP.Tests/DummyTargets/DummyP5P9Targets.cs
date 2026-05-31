using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;

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

internal static class DummyGameTextTarget
{
    public static string? RoughConvertSecondPersonToThirdPerson(string? text, object? who)
    {
        _ = text;
        _ = who;
        return "snapjaw was vaporized.";
    }
}

internal static class DummyPopupShow
{
    public static string? LastShowMessage;
    public static string? LastShowAsyncMessage;
    public static string? LastShowYesNoMessage;
    public static string? LastShowYesNoCancelMessage;
    public static string? LastShowKeybindAsyncMessage;
    public static string? LastShowYesNoAsyncMessage;
    public static string? LastShowYesNoCancelAsyncMessage;
    public static string? LastShowSpaceMessage;

    [MethodImpl(MethodImplOptions.NoInlining)]
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Task ShowAsync(
        string Message,
        bool CopyScrap = true,
        bool Capitalize = true,
        bool DimBackground = true,
        bool LogMessage = true,
        bool PushView = false)
    {
        LastShowAsyncMessage = Message;
        return Task.CompletedTask;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Task ShowKeybindAsync(string Message, CancellationToken cancelToken)
    {
        _ = cancelToken;
        LastShowKeybindAsyncMessage = Message;
        return Task.CompletedTask;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ShowYesNo(
        string Message,
        string? Sound = null,
        bool AllowEscape = true,
        int defaultResult = 0)
    {
        LastShowYesNoMessage = Message;
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ShowYesNoCancel(
        string Message,
        string? Sound = null,
        bool AllowEscape = true,
        int defaultResult = 0)
    {
        LastShowYesNoCancelMessage = Message;
        return defaultResult;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Task<int> ShowYesNoAsync(string Message)
    {
        LastShowYesNoAsyncMessage = Message;
        return Task.FromResult(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Task<int> ShowYesNoCancelAsync(string Message)
    {
        LastShowYesNoCancelAsyncMessage = Message;
        return Task.FromResult(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShowFail(
        string Message,
        bool CopyScrap = true,
        bool Capitalize = true,
        bool DimBackground = true)
    {
        Show(Message, CopyScrap: CopyScrap, Capitalize: Capitalize, DimBackground: DimBackground);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShowSpace(
        string Message,
        string? Title = null,
        string? Sound = null,
        object? Icon = null,
        bool CopyScrap = true,
        bool Capitalize = true,
        string? Intro = null)
    {
        _ = Title;
        _ = Sound;
        _ = Icon;
        _ = CopyScrap;
        _ = Capitalize;
        _ = Intro;
        LastShowSpaceMessage = Message;
    }

    public static void Reset()
    {
        LastShowMessage = null;
        LastShowAsyncMessage = null;
        LastShowYesNoMessage = null;
        LastShowYesNoCancelMessage = null;
        LastShowKeybindAsyncMessage = null;
        LastShowYesNoAsyncMessage = null;
        LastShowYesNoCancelAsyncMessage = null;
        LastShowSpaceMessage = null;
    }
}
