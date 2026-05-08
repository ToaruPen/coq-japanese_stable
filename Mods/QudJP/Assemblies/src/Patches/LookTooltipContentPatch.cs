using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LookTooltipContentPatch
{
    private const string TargetTypeName = "XRL.UI.Look";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is not null)
        {
            var method = AccessTools.Method(TargetTypeName + ":GenerateTooltipContent", new[] { gameObjectType });
            if (method is not null)
            {
                return method;
            }
        }

        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is not null)
        {
            var methods = AccessTools.GetDeclaredMethods(targetType);
            for (var index = 0; index < methods.Count; index++)
            {
                var candidate = methods[index];
                if (string.Equals(candidate.Name, "GenerateTooltipContent", StringComparison.Ordinal)
                    && candidate.ReturnType == typeof(string)
                    && candidate.GetParameters().Length == 1)
                {
                    return candidate;
                }
            }
        }

        Trace.TraceError("QudJP: Failed to resolve Look.GenerateTooltipContent(GameObject). Patch will not apply.");
        return null;
    }

    public static void Postfix(object __instance, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            __result = TranslateTooltipContent(__result);
#if QUDJP_DEV_BUILD
            var translatedTooltipContent = __result;
            RuntimeDiagnostics.LogVerboseProbe(() => BuildTooltipContentProbe(translatedTooltipContent));
#endif
#if HAS_TMP
            DelayedSceneProbeScheduler.ScheduleCompareSceneProbe(__instance);
#else
            _ = __instance;
#endif
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LookTooltipContentPatch.Postfix failed: {0}", ex);
        }
    }

    internal static string TranslateTooltipContent(string source)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        return DescriptionTextTranslator.TranslateLongDescription(source, nameof(LookTooltipContentPatch));
    }

#if QUDJP_DEV_BUILD
    private static string BuildTooltipContentProbe(string content)
    {
        var normalized = content.Replace("\r", "\\r")
            .Replace("\n", "\\n");
#pragma warning disable CA1845
        var truncated = normalized.Length <= 240 ? normalized : normalized.Substring(0, 240) + "...";
#pragma warning restore CA1845
        return "[QudJP] LookTooltipContentProbe/v1: len="
            + content.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " content='"
            + truncated
            + "'";
    }

#endif
}
