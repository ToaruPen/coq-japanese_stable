using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryAndEquipmentStatusScreenNameRefreshPatch
{
    private const string Context = nameof(InventoryAndEquipmentStatusScreenNameRefreshPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.InventoryAndEquipmentStatusScreen", "InventoryAndEquipmentStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.UpdateViewFromData() not found.", Context);
        }

        return method;
    }

    [HarmonyPriority(Priority.Last)]
    public static void Prefix(object? __instance)
    {
        try
        {
            _ = InventoryNameRefreshCoordinator.ResetDirtyInventoryNameCachesBeforeRefresh(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
