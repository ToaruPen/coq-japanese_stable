namespace QudJP.Tests.DummyTargets;

internal static class DummyWorldCreationProgressTarget
{
    public static string LastNextStepText { get; private set; } = string.Empty;

    public static int LastNextStepTotalSteps { get; private set; }

    public static string LastStepProgressText { get; private set; } = string.Empty;

    public static bool LastStepProgressLast { get; private set; }

    public static void Reset()
    {
        LastNextStepText = string.Empty;
        LastNextStepTotalSteps = 0;
        LastStepProgressText = string.Empty;
        LastStepProgressLast = false;
    }

    public static void NextStep(string Text, int TotalSteps)
    {
        LastNextStepText = Text;
        LastNextStepTotalSteps = TotalSteps;
    }

    public static void StepProgress(string StepText, bool Last = false)
    {
        LastStepProgressText = StepText;
        LastStepProgressLast = Last;
    }
}
