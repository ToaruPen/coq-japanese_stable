using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyQudHistoryFactoryTarget
{
    internal static string RuinsSiteNameResult { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string NameRuinsSite(object history, out bool Proper, out string nameRoot)
    {
        Proper = true;
        nameRoot = "Ibul";
        return RuinsSiteNameResult;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void GenerateCultName(DummyHistoricEntity sultan, object history)
    {
        _ = sultan;
        _ = history;
    }
}
