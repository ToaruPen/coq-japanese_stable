using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyBeginBeingUnequippedFailureMessageTarget
{
    public string? FailureMessage { get; private set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddFailureMessage(string Message)
    {
        if (string.IsNullOrEmpty(FailureMessage))
        {
            FailureMessage = Message;
        }
        else if (!FailureMessage.Contains(Message, StringComparison.Ordinal))
        {
            FailureMessage += " " + Message;
        }
    }
}
