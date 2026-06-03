using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class InventoryActionMenuPopupHideTimingPatch
{
    private const string Context = nameof(InventoryActionMenuPopupHideTimingPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return InventoryActionMenuPopupTimingHelpers.ResolvePopupMessageMethod(Context, "Hide");
    }

    public static void Prefix(object? __instance)
    {
        try
        {
            InventoryActionMenuCloseTimingObservability.LogPopupHideRequest(
                InventoryActionMenuPopupTimingHelpers.GetPopupId(__instance));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
