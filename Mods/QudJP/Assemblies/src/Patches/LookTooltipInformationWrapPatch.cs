using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP.UI;
using XRL.UI;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LookTooltipInformationWrapPatch
{
    private const string TargetTypeName = "XRL.UI.Look";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve XRL.UI.Look. Tooltip information wrap patch will not apply.");
            return null;
        }

        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve XRL.World.GameObject. Tooltip information wrap patch will not apply.");
            return null;
        }

        var method = AccessTools.Method(targetType, "GenerateTooltipInformation", new[] { gameObjectType });
        if (method is not null)
        {
            return method;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var candidate = methods[index];
            if (string.Equals(candidate.Name, "GenerateTooltipInformation", StringComparison.Ordinal)
                && candidate.GetParameters().Length == 1)
            {
                return candidate;
            }
        }

        Trace.TraceError("QudJP: Failed to resolve Look.GenerateTooltipInformation(GameObject). Tooltip information wrap patch will not apply.");
        return null;
    }

    public static void Postfix(ref Look.TooltipInformation __result)
    {
        try
        {
            if (JapaneseBlockWrap.TryWrapTooltipLongDescription(__result.LongDescription, out var wrapped))
            {
                __result.LongDescription = wrapped;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: LookTooltipInformationWrapPatch.Postfix failed: {0}", ex);
        }
    }
}
