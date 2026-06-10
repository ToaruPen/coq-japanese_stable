using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class InventoryAndEquipmentStatusScreenInventoryLineSortPatch
{
    private const string Context = nameof(InventoryAndEquipmentStatusScreenInventoryLineSortPatch);

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
    public static void Postfix(object? __instance)
    {
        try
        {
            _ = InventoryLineRefreshCoordinator.TryResortInventoryLinesAfterFullRefresh(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
