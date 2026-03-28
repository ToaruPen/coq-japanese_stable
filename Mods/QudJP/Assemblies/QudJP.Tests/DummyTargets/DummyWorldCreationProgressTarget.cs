namespace QudJP.Tests.DummyTargets;

internal static class DummyWorldCreationProgressTarget
{
    public static string LastStepText { get; private set; } = string.Empty;

    public static bool LastLastFlag { get; private set; }

    public static void Reset()
    {
        LastStepText = string.Empty;
        LastLastFlag = false;
    }

    public static void StepProgress(string StepText, bool Last = false)
    {
        LastStepText = StepText;
        LastLastFlag = Last;
    }
}
