using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal static class DummyQudHistoryHelpersTarget
{
    internal static string SultanateYearNameResult { get; set; } = string.Empty;
    internal static string HistoricItemNameResult { get; set; } = string.Empty;

    internal static void ResetForTests()
    {
        SultanateYearNameResult = string.Empty;
        HistoricItemNameResult = string.Empty;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GenerateSultanateYearName()
    {
        return SultanateYearNameResult;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string NameItem(string obj, object history, object entity)
    {
        return HistoricItemNameResult;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string NameItemNounRoot(string obj, object history, object entity)
    {
        return HistoricItemNameResult;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string NameItemAdjRoot(string obj, object history, object entity)
    {
        return HistoricItemNameResult;
    }
}
