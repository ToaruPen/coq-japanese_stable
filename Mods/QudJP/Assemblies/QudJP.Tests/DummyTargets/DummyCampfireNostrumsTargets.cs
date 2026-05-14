namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCampfireNostrumsTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool UseFailurePopup { get; set; } = true;

    public void NostrumsStopBleeding()
    {
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public void NostrumsTreatPoison()
    {
        if (UseFailurePopup)
        {
            DummyPopupShow.ShowFail(PopupMessageToSend);
            return;
        }

        DummyPopupShow.Show(PopupMessageToSend);
    }
}
