using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryAndEquipmentStatusScreenShowRepairPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.InventoryAndEquipmentStatusScreen", "InventoryAndEquipmentStatusScreen");
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        var statusScreensScreenType = GameTypeResolver.FindType("Qud.UI.StatusScreensScreen", "StatusScreensScreen");
        if (targetType is null || gameObjectType is null || statusScreensScreenType is null)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenShowRepairPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "ShowScreen", new[] { gameObjectType, statusScreensScreenType });
        if (method is null)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenShowRepairPatch.ShowScreen(...) not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
#if HAS_TMP && QUDJP_DEV_BUILD
            DelayedInventoryLineRepairScheduler.ScheduleVisibleInventoryProbeSnapshotsAfterDelay(__instance);
#endif
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryAndEquipmentStatusScreenShowRepairPatch.Postfix failed: {0}", ex);
        }
    }
}
