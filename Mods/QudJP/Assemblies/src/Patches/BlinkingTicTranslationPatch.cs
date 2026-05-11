using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BlinkingTicTranslationPatch
{
    private const string Context = nameof(BlinkingTicTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} Event type not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Parts.Mutation.BlinkingTic", eventType);
        AddTarget(targets, "XRL.World.Effects.BlinkingTicSickness", eventType);
        return targets;
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
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
            OwnerTranslationScope.Exit(ref activeDepth);
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
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!string.Equals(message, "You lurch suddenly!", StringComparison.Ordinal))
        {
            return false;
        }

        const string translated = "突然ぐらりとよろめいた！";
        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, string typeName, Type eventType)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, "FireEvent", new[] { eventType });
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.FireEvent target not found.", Context, targetType.FullName);
    }
}
