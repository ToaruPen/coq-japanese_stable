using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LiquidVolumeTranslationPatch
{
    private const string Context = nameof(LiquidVolumeTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.LiquidVolume");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        if (targetType is null || inventoryActionEventType is null || gameObjectType is null || cellType is null)
        {
            Trace.TraceError("QudJP: LiquidVolumeTranslationPatch failed to resolve LiquidVolume target types.");
            yield break;
        }

        var handleEvent = AccessTools.Method(targetType, "HandleEvent", [inventoryActionEventType]);
        if (handleEvent is not null)
        {
            yield return handleEvent;
        }
        else
        {
            Trace.TraceError("QudJP: LiquidVolumeTranslationPatch.HandleEvent(InventoryActionEvent) not found.");
        }

        var pour = AccessTools.Method(
            targetType,
            "Pour",
            [typeof(bool).MakeByRefType(), gameObjectType, cellType, typeof(bool), typeof(bool), typeof(int), typeof(bool)]);
        if (pour is not null)
        {
            yield return pour;
        }
        else
        {
            Trace.TraceError("QudJP: LiquidVolumeTranslationPatch.Pour(ref bool, GameObject, Cell, bool, bool, int, bool) not found.");
        }

        var performFill = AccessTools.Method(
            targetType,
            "PerformFill",
            [gameObjectType, typeof(bool).MakeByRefType(), typeof(bool)]);
        if (performFill is not null)
        {
            yield return performFill;
        }
        else
        {
            Trace.TraceError("QudJP: LiquidVolumeTranslationPatch.PerformFill(GameObject, ref bool, bool) not found.");
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
            Trace.TraceError("QudJP: LiquidVolumeTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: LiquidVolumeTranslationPatch.Finalizer failed: {0}", ex);
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

        return LiquidVolumeFragmentTranslator.TryTranslatePopupMessage(source, route, family + "." + Context, out translated);
    }
}
