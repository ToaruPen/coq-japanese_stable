using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyGritGateTerminalKnowledgeTarget
{
    public static string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Activate()
    {
        DummyPopupShow.Show(PopupMessageToShow);
    }
}
