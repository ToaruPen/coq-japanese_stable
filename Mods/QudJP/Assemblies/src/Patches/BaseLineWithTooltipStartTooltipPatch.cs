using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BaseLineWithTooltipStartTooltipPatch
{
    private const string TargetTypeName = "Qud.UI.BaseLineWithTooltip";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve Qud.UI.BaseLineWithTooltip. Patch will not apply.");
            return null;
        }

        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var rectTransformType = AccessTools.TypeByName("UnityEngine.RectTransform");
        if (gameObjectType is null || rectTransformType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve StartTooltip parameter types. Patch will not apply.");
            return null;
        }

        var method = AccessTools.Method(
            targetType,
            "StartTooltip",
            new[] { gameObjectType, gameObjectType, typeof(bool), rectTransformType },
            null);
        if (method is not null)
        {
            return method;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var candidate = methods[index];
            if (string.Equals(candidate.Name, "StartTooltip", StringComparison.Ordinal)
                && candidate.ReturnType == typeof(void)
                && candidate.GetParameters().Length == 4)
            {
                return candidate;
            }
        }

        Trace.TraceError("QudJP: Failed to resolve BaseLineWithTooltip.StartTooltip(...). Patch will not apply.");
        return null;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            DelayedSceneProbeScheduler.ScheduleCompareSceneProbe(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: BaseLineWithTooltipStartTooltipPatch.Postfix failed: {0}", ex);
        }
    }
}
