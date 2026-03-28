using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WorldCreationProgressStepProgressTranslationPatch
{
    private const string Context = nameof(WorldCreationProgressStepProgressTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.UI.WorldCreationProgress");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: WorldCreationProgressStepProgressTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "StepProgress", new[] { typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: WorldCreationProgressStepProgressTranslationPatch.StepProgress(string, bool) not found.");
        }

        return method;
    }

    public static void Prefix(ref string StepText)
    {
        try
        {
            StepText = LoadingStatusTranslationPatch.TranslateStatusText(
                StepText,
                Context,
                "WorldCreationProgress.Exact");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: WorldCreationProgressStepProgressTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
