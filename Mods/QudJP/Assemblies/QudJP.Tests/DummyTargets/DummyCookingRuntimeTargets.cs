namespace QudJP.Tests.DummyTargets;

internal sealed class DummyCookingRuntimeTarget
{
    public string PopupMessageToSend { get; set; } = string.Empty;

    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    public void ApplyPopupEffect(DummyGameObject obj)
    {
        _ = obj;
        DummyPopupShow.Show(PopupMessageToSend);
    }

    public bool FireQueuedEffect(DummyGameEvent e)
    {
        _ = e;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public bool CheckBlinkEscape()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }

    public void Trigger()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}
