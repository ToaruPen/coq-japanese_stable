using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryActionMenuCursorSoundPopupHideContextPatch
{
    private const string Context = nameof(InventoryActionMenuCursorSoundPopupHideContextPatch);
    private const string TargetTypeName = "Qud.UI.PopupMessage";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            return null;
        }

        var method = AccessTools.Method(targetType, "Hide", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} method 'Hide()' not found on '{1}'.", Context, TargetTypeName);
        }

        return method;
    }

    public static void Prefix(object? __instance)
    {
        try
        {
            InventoryActionMenuCursorSoundPatch.ForgetPopupController(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
