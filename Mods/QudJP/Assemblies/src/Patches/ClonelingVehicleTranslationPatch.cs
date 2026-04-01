using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ClonelingVehicleTranslationPatch
{
    private const string Context = nameof(ClonelingVehicleTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch failed to resolve InventoryActionEvent.");
            yield break;
        }

        foreach (var method in ResolveTargetMethods(inventoryActionEventType))
        {
            yield return method;
        }
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0)
        {
            translated = source;
            return false;
        }

        return ClonelingVehicleFragmentTranslator.TryTranslatePopupMessage(source, route, family + "." + Context, out translated);
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0 || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (!ClonelingVehicleFragmentTranslator.TryTranslateQueuedMessage(
                message,
                nameof(ClonelingVehicleTranslationPatch),
                Context + ".Queued",
                out var translated))
        {
            return false;
        }

        message = translated;
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTargetMethods(Type inventoryActionEventType)
    {
        var clonelingType = AccessTools.TypeByName("XRL.World.Parts.Cloneling");
        if (clonelingType is not null)
        {
            var handleEvent = AccessTools.Method(clonelingType, "HandleEvent", [inventoryActionEventType]);
            if (handleEvent is not null)
            {
                yield return handleEvent;
            }
            else
            {
                Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch.Cloneling.HandleEvent(InventoryActionEvent) not found.");
            }

            var attemptCloning = AccessTools.Method(clonelingType, "AttemptCloning", Type.EmptyTypes);
            if (attemptCloning is not null)
            {
                yield return attemptCloning;
            }
            else
            {
                Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch.Cloneling.AttemptCloning() not found.");
            }
        }
        else
        {
            Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch failed to resolve Cloneling.");
        }

        var vehicleRepairType = AccessTools.TypeByName("XRL.World.Parts.VehicleRepair");
        if (vehicleRepairType is not null)
        {
            var handleEvent = AccessTools.Method(vehicleRepairType, "HandleEvent", [inventoryActionEventType]);
            if (handleEvent is not null)
            {
                yield return handleEvent;
            }
            else
            {
                Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch.VehicleRepair.HandleEvent(InventoryActionEvent) not found.");
            }
        }
        else
        {
            Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch failed to resolve VehicleRepair.");
        }

        var vehicleRecallType = AccessTools.TypeByName("XRL.World.Parts.VehicleRecall");
        if (vehicleRecallType is not null)
        {
            var handleEvent = AccessTools.Method(vehicleRecallType, "HandleEvent", [inventoryActionEventType]);
            if (handleEvent is not null)
            {
                yield return handleEvent;
            }
            else
            {
                Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch.VehicleRecall.HandleEvent(InventoryActionEvent) not found.");
            }
        }
        else
        {
            Trace.TraceError("QudJP: ClonelingVehicleTranslationPatch failed to resolve VehicleRecall.");
        }
    }
}
