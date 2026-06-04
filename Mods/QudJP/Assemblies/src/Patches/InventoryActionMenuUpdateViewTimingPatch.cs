using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class InventoryActionMenuUpdateViewTimingPatch
{
    private const string Context = nameof(InventoryActionMenuUpdateViewTimingPatch);
    private const string TargetTypeName = "Qud.UI.InventoryAndEquipmentStatusScreen";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.UpdateViewFromData target not found.", Context);
        }

        return method;
    }

    public static bool Prefix(ref InventoryActionMenuCloseTimingObservability.TimingScope __state)
    {
        try
        {
            if (InventoryActionMenuCloseTimingObservability.ShouldSuppressInventoryRefreshAfterCancel())
            {
                InventoryActionMenuCloseTimingObservability.LogInventoryRefreshSuppressed();
                __state = InventoryActionMenuCloseTimingObservability.TimingScope.Empty;
                return false;
            }

            __state = InventoryActionMenuCloseTimingObservability.BeginInventoryRefresh();
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
            __state = InventoryActionMenuCloseTimingObservability.TimingScope.Empty;
            return true;
        }
    }

    public static void Postfix(InventoryActionMenuCloseTimingObservability.TimingScope __state)
    {
        try
        {
            InventoryActionMenuCloseTimingObservability.EndInventoryRefresh(__state);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
