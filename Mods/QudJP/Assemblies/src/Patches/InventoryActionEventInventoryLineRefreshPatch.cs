using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class InventoryActionEventInventoryLineRefreshPatch
{
    private const string Context = nameof(InventoryActionEventInventoryLineRefreshPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var inventoryActionEventType = GameTypeResolver.FindType(
            "XRL.World.InventoryActionEvent",
            "InventoryActionEvent");
        var gameObjectType = GameTypeResolver.FindType("XRL.World.GameObject", "GameObject");
        var cellType = GameTypeResolver.FindType("XRL.World.Cell", "Cell");
        var iEventType = GameTypeResolver.FindType("XRL.World.IEvent", "IEvent");
        var inventoryType = GameTypeResolver.FindType("XRL.World.IInventory", "IInventory");
        if (inventoryActionEventType is null
            || gameObjectType is null
            || cellType is null
            || iEventType is null
            || inventoryType is null)
        {
            Trace.TraceError(
                "QudJP: {0} failed to resolve InventoryActionEvent dependencies.",
                Context);
            yield break;
        }

        var commonParameters = new[]
        {
            gameObjectType,
            gameObjectType,
            gameObjectType,
            typeof(string),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(int),
            typeof(int),
            typeof(int),
            gameObjectType,
            cellType,
            cellType,
            inventoryType,
        };

        foreach (var parameters in new[]
        {
            commonParameters,
            Prepend(typeof(bool).MakeByRefType(), commonParameters),
            Prepend(iEventType.MakeByRefType(), commonParameters),
            Prepend(inventoryActionEventType.MakeByRefType(), commonParameters),
        })
        {
            var method = AccessTools.Method(inventoryActionEventType, "Check", parameters);
            if (method is null)
            {
                Trace.TraceError(
                    "QudJP: {0}.Check overload with {1} parameter(s) not found.",
                    Context,
                    parameters.Length);
                continue;
            }

            yield return method;
        }
    }

    public static void Prefix(
        [HarmonyArgument("Actor")] object? actor,
        [HarmonyArgument("Item")] object? item,
        ref InventoryLineRefreshCoordinator.DisplaySnapshot __state)
    {
        try
        {
            __state = InventoryLineRefreshCoordinator.CaptureDisplaySnapshot(item, actor);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
            __state = default;
        }
    }

    public static void Postfix(
        [HarmonyArgument("Actor")] object? actor,
        [HarmonyArgument("Item")] object? item,
        InventoryLineRefreshCoordinator.DisplaySnapshot __state)
    {
        try
        {
            if (!InventoryLineRefreshCoordinator.RefreshAfterInventoryActionIfChanged(item, actor, __state))
            {
                _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItem(
                    item,
                    requiresResort: false);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static Type[] Prepend(Type first, Type[] tail)
    {
        var result = new Type[tail.Length + 1];
        result[0] = first;
        Array.Copy(tail, 0, result, 1, tail.Length);
        return result;
    }
}
