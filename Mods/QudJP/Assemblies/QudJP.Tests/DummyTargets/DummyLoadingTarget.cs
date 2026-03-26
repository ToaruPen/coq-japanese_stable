namespace QudJP.Tests.DummyTargets;

internal static class DummyLoadingTarget
{
    public static string LastDescription { get; private set; } = string.Empty;

    public static bool LastWaitForUiUpdate { get; private set; }

    public static void Reset()
    {
        LastDescription = string.Empty;
        LastWaitForUiUpdate = false;
    }

    public static void SetLoadingStatus(string description, bool waitForUiUpdate = false)
    {
        LastDescription = description;
        LastWaitForUiUpdate = waitForUiUpdate;
    }
}
