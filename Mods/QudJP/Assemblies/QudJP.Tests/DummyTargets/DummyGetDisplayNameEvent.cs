namespace QudJP.Tests.DummyTargets;

internal static class DummyGetDisplayNameEvent
{
    public static string GetFor(string objectName, string baseName)
    {
        return string.IsNullOrEmpty(objectName) ? baseName : objectName;
    }
}
