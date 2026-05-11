namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCampfirePreserveTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public bool Preserve()
    {
        DummyPopupShow.Show(PopupMessageToSend);
        return true;
    }

    public bool PreserveExotic()
    {
        DummyPopupShow.Show(PopupMessageToSend, Sound: "Sounds/UI/ui_notification");
        return true;
    }
}
