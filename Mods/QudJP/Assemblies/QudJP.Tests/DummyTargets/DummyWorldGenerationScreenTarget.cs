namespace QudJP.Tests.DummyTargets;

internal sealed class DummyWorldGenerationScreenTarget
{
    public string LastMessage { get; private set; } = string.Empty;

    public void _AddMessage(string message)
    {
        LastMessage = message;
    }
}
