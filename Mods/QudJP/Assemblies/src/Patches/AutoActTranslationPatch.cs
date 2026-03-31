using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AutoActTranslationPatch
{
    private const string Context = nameof(AutoActTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var autoActType = AccessTools.TypeByName("XRL.World.Capabilities.AutoAct");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (autoActType is null || cellType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(
            targets,
            AccessTools.Method(autoActType, "Interrupt", new[] { typeof(string), cellType, gameObjectType, typeof(bool) }),
            "AutoAct.Interrupt(string, Cell, GameObject, bool)");
        AddTarget(
            targets,
            AccessTools.Method(autoActType, "Interrupt", new[] { gameObjectType, typeof(bool), typeof(bool) }),
            "AutoAct.Interrupt(GameObject, bool, bool)");
        AddTarget(
            targets,
            AccessTools.Method(autoActType, "ResetAutoexploreProperties", Type.EmptyTypes),
            "AutoAct.ResetAutoexploreProperties()");

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: {0} resolved zero target methods.", Context);
        }

        return targets;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
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
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "AutoAct");
    }

    private static void AddTarget(List<MethodBase> targets, MethodBase? method, string description)
    {
        if (method is null)
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve {1}.", Context, description);
            return;
        }

        targets.Add(method);
    }
}
