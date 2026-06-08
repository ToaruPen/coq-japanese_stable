using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class InventoryActionProcessInventoryLineRefreshPatch
{
    private const string Context = nameof(InventoryActionProcessInventoryLineRefreshPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var inventoryActionType = GameTypeResolver.FindType("XRL.World.InventoryAction", "InventoryAction");
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        if (inventoryActionType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve InventoryAction or GameObject.", Context);
            return null;
        }

        var method = AccessTools.Method(
            inventoryActionType,
            "Process",
            new[] { gameObjectType, gameObjectType, typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Process(GameObject, GameObject, bool) target not found.", Context);
        }

        return method;
    }

    public static void Prefix(
        object? GO,
        object? Owner,
        ref InventoryLineRefreshCoordinator.DisplaySnapshot __state)
    {
        try
        {
            __state = InventoryLineRefreshCoordinator.CaptureDisplaySnapshot(GO, Owner);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
            __state = default;
        }
    }

    public static void Postfix(
        object? GO,
        object? Owner,
        InventoryLineRefreshCoordinator.DisplaySnapshot __state)
    {
        try
        {
            _ = InventoryLineRefreshCoordinator.RefreshAfterInventoryActionIfChanged(GO, Owner, __state);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
