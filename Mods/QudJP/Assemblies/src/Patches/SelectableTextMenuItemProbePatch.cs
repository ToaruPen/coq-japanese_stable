using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SelectableTextMenuItemProbePatch
{
    private const string TargetTypeName = "Qud.UI.SelectableTextMenuItem";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: SelectableTextMenuItemProbePatch target type '{TargetTypeName}' not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "Update");
        if (method is null)
        {
            Trace.TraceError($"QudJP: SelectableTextMenuItemProbePatch method 'Update' not found on '{TargetTypeName}'.");
        }

        return method;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            if (SelectableTextMenuItemObservability.TryBuildState(__instance, "update-postfix", out var logLine)
                && !string.IsNullOrEmpty(logLine))
            {
                QudJPMod.LogToUnity(logLine!);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SelectableTextMenuItemProbePatch.Postfix failed: {0}", ex);
        }
    }
}
