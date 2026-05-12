using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyPickItemTakeAllTarget
{
    public static string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TakeAll()
    {
        return DummyPopupShow.ShowYesNo(PopupMessageToShow) == 0;
    }
}
