using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class EquipmentApiInventoryActionMenuRefreshPatch
{
    private const string Context = nameof(EquipmentApiInventoryActionMenuRefreshPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var equipmentApiType = GameTypeResolver.FindType("Qud.API.EquipmentAPI", "EquipmentAPI");
        var inventoryActionType = GameTypeResolver.FindType("XRL.World.InventoryAction", "InventoryAction");
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (equipmentApiType is null || inventoryActionType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve EquipmentAPI, InventoryAction, or GameObject.", Context);
            return null;
        }

        var actionTableType = typeof(Dictionary<,>).MakeGenericType(typeof(string), inventoryActionType);
        var comparerType = typeof(IComparer<>).MakeGenericType(inventoryActionType);
        var method = AccessTools.Method(
            equipmentApiType,
            "ShowInventoryActionMenu",
            new[]
            {
                actionTableType,
                gameObjectType,
                gameObjectType,
                typeof(bool),
                typeof(bool),
                typeof(string),
                comparerType,
                typeof(bool),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ShowInventoryActionMenu target not found.", Context);
        }

        return method;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(object? __1, object? __2)
    {
        try
        {
            _ = InventoryLineRefreshCoordinator.TryRefreshChangedInventoryLinesBeforeActionMenuOpen(__2, __1);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
