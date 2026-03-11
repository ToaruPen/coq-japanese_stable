namespace QudJP.Tests.DummyTargets;

internal static class DummyMessageQueue
{
    public static string LastMessage { get; private set; } = string.Empty;

    public static string? LastColor { get; private set; }

    public static bool LastCapitalize { get; private set; }

    public static bool LastUsePopup => LastCapitalize;

    public static void AddPlayerMessage(string Message, string? Color = null, bool Capitalize = true)
    {
        LastMessage = Message;
        LastColor = Color;
        LastCapitalize = Capitalize;
    }

    public static void Reset()
    {
        LastMessage = string.Empty;
        LastColor = null;
        LastCapitalize = false;
    }
}
