namespace QudJP.Tests.DummyTargets;

internal sealed class DummyBedProducerTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public void AttemptSleep(DummyGameObject actor, out bool sleepSuccessful, out bool moveFailed, out bool broke)
    {
        _ = actor;
        sleepSuccessful = false;
        moveFailed = false;
        broke = false;
        DummyPopupShow.Show(MessageToSend, LogMessage: false);
    }
}

internal sealed class DummyChairProducerTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public bool SitDown(DummyGameObject actor, DummyGameEvent? fromEvent = null)
    {
        _ = actor;
        _ = fromEvent;
        DummyPopupShow.Show(MessageToSend, LogMessage: false);
        return false;
    }
}
