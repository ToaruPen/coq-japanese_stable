namespace QudJP.Tests.DummyTargets;

internal sealed class DummySpindleNegotiationTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public bool FireEvent(DummyEvent e)
    {
        _ = e;
        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}
