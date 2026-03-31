using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyAutoActInterruptBecauseTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Interrupt(string because, DummyCell? indicateCell = null, DummyGameObject? indicateObject = null, bool isThreat = false)
    {
        _ = because;
        _ = indicateCell;
        _ = indicateObject;
        _ = isThreat;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyAutoActInterruptObjectTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Interrupt(DummyGameObject becauseOf, bool showIndicator = true, bool isThreat = false)
    {
        _ = becauseOf;
        _ = showIndicator;
        _ = isThreat;
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
    }
}

internal sealed class DummyAutoActResetTarget
{
    public string MessageToSend { get; set; } = string.Empty;

    public string? ColorToSend { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ResetAutoexploreProperties()
    {
        DummyMessageQueue.AddPlayerMessage(MessageToSend, ColorToSend, Capitalize: false);
        return true;
    }
}
