using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyTinkeringBuildTarget
{
    public static string PopupMessageToShow { get; set; } = string.Empty;

    public static string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PerformUITinkerBuild()
    {
        if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowFail), StringComparison.Ordinal))
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }
        else
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }

        return true;
    }
}

internal static class DummyTinkeringModTarget
{
    public static string PopupMessageToShow { get; set; } = string.Empty;

    public static string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PerformUITinkerMod()
    {
        if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowFail), StringComparison.Ordinal))
        {
            DummyPopupShow.ShowFail(PopupMessageToShow);
        }
        else if (string.Equals(PopupMethod, nameof(DummyPopupShow.ShowYesNoCancel), StringComparison.Ordinal))
        {
            _ = DummyPopupShow.ShowYesNoCancel(PopupMessageToShow);
        }
        else
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }

        return true;
    }
}
