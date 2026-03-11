namespace QudJP.Tests.DummyTargets;

internal static class DummyHistoricStringExpander
{
    public static string ExpandString(
        string input,
        object? entity = null,
        object? history = null,
        object? vars = null,
        object? random = null)
    {
        _ = entity;
        _ = history;
        _ = vars;
        _ = random;
        return input;
    }
}
