using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SelectableTextMenuItemSelectChangedProbePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("Qud.UI.SelectableTextMenuItem:SelectChanged");
        if (method is not null)
        {
            return method;
        }

        var targetType = AccessTools.TypeByName("Qud.UI.SelectableTextMenuItem");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve SelectableTextMenuItem.SelectChanged(). Probe patch will not apply.");
            return null;
        }

        var fallback = AccessTools.Method(targetType, "SelectChanged");
        if (fallback is null)
        {
            Trace.TraceError("QudJP: Failed to resolve SelectableTextMenuItem.SelectChanged(). Probe patch will not apply.");
        }

        return fallback;
    }

    public static void Prefix(object __instance)
    {
        try
        {
            _ = __instance;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SelectableTextMenuItemSelectChangedProbePatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(object __instance)
    {
        try
        {
            _ = __instance;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SelectableTextMenuItemSelectChangedProbePatch.Postfix failed: {0}", ex);
        }
    }
}
