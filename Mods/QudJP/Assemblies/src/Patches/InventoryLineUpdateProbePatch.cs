using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryLineUpdateProbePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("Qud.UI.InventoryLine:Update");
        if (method is not null)
        {
            return method;
        }

        var targetType = AccessTools.TypeByName("Qud.UI.InventoryLine");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve InventoryLine.Update(). Probe patch will not apply.");
            return null;
        }

        var fallback = AccessTools.Method(targetType, "Update");
        if (fallback is null)
        {
            Trace.TraceError("QudJP: Failed to resolve InventoryLine.Update(). Probe patch will not apply.");
        }

        return fallback;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            _ = __instance;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryLineUpdateProbePatch.Postfix failed: {0}", ex);
        }
    }
}
