using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WorldCreationProgressTranslationPatch
{
    private const string Context = nameof(WorldCreationProgressTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = GameTypeResolver.FindType("XRL.UI.WorldCreationProgress", "WorldCreationProgress");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: WorldCreationProgressTranslationPatch target type not found.");
            yield break;
        }

        var nextStep = AccessTools.Method(targetType, "NextStep", new[] { typeof(string), typeof(int) });
        if (nextStep is not null)
        {
            yield return nextStep;
        }
        else
        {
            Trace.TraceError("QudJP: WorldCreationProgressTranslationPatch.NextStep(string, int) not found.");
        }

        var stepProgress = AccessTools.Method(targetType, "StepProgress", new[] { typeof(string), typeof(bool) });
        if (stepProgress is not null)
        {
            yield return stepProgress;
        }
        else
        {
            Trace.TraceError("QudJP: WorldCreationProgressTranslationPatch.StepProgress(string, bool) not found.");
        }
    }

    public static void Prefix(MethodBase __originalMethod, ref string __0)
    {
        try
        {
            if (string.IsNullOrEmpty(__0))
            {
                return;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(__0, out var stripped))
            {
                __0 = stripped;
                return;
            }

            var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
                __0,
                static visible => StringHelpers.TranslateExactOrLowerAsciiFallback(visible));
            if (!string.Equals(translated, __0, StringComparison.Ordinal))
            {
                var family = __originalMethod?.Name == "StepProgress"
                    ? "WorldCreationProgress.StepProgress"
                    : "WorldCreationProgress.NextStep";
                DynamicTextObservability.RecordTransform(Context, family, __0, translated);
                __0 = translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: WorldCreationProgressTranslationPatch.Prefix failed: {0}", ex);
        }
    }
}
