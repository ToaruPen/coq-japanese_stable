using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyAbsorbablePsycheTarget
{
    public static string PopupMessageToShow { get; set; } = string.Empty;

    public static string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool HandleEvent()
    {
        if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowYesNo), StringComparison.Ordinal))
        {
            DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }
        else
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }

        return true;
    }
}
