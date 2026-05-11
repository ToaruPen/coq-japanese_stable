using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HealingTranslationPatch
{
    private const string Context = nameof(HealingTranslationPatch);
    private const string InterruptedSource = "Your healing is interrupted!";
    private const string InterruptedTranslation = "治癒が中断された！";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.Healing");
        var useEnergyEventType = AccessTools.TypeByName("XRL.World.UseEnergyEvent");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        if (useEnergyEventType is not null)
        {
            AddTarget(targets, targetType, "HandleEvent", new[] { useEnergyEventType });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent UseEnergyEvent type not found.", Context);
        }

        if (eventType is not null)
        {
            AddTarget(targets, targetType, "FireEvent", new[] { eventType });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.FireEvent Event type not found.", Context);
        }

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

        if (!string.Equals(message, InterruptedSource, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, InterruptedTranslation);
        message = InterruptedTranslation;
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
