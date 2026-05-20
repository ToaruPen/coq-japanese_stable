using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyFriendOrFoeReasonTarget
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string replacePlaceholders(string reason)
    {
        return reason;
    }
}
