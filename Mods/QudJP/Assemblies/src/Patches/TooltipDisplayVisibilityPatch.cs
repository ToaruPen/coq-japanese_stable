using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TooltipDisplayVisibilityPatch
{
    private const string TargetTypeName = "ModelShark.Tooltip";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve ModelShark.Tooltip. Display visibility patch will not apply.");
            return null;
        }

        var method = AccessTools.Method(targetType, "Display", new[] { typeof(float) });
        if (method is not null)
        {
            return method;
        }

        Trace.TraceError("QudJP: Failed to resolve Tooltip.Display(float). Display visibility patch will not apply.");
        return null;
    }

    public static void Postfix(object __instance)
    {
        try
        {
#if HAS_TMP
            _ = QudJP.TooltipTextRepairer.TryRestoreLookerTooltipVisibility(__instance);
#else
            _ = __instance;
#endif
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: TooltipDisplayVisibilityPatch.Postfix failed: {0}", ex);
        }
    }
}
