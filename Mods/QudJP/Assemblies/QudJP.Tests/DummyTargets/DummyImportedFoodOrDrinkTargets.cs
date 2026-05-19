using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyImportedFoodOrDrinkTarget
{
    internal static string FactionNameResult { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string generateFactionName(string root)
    {
        _ = root;
        return FactionNameResult;
    }
}
