using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCodeCompressorTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void loadCode()
    {
        DummyPopupShow.ShowAsync(PopupMessageToShow).GetAwaiter().GetResult();
    }
}
