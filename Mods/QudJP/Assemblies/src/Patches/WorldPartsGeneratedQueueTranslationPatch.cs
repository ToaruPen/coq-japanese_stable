using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WorldPartsGeneratedQueueTranslationPatch
{
    private const string Context = nameof(WorldPartsGeneratedQueueTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var realityStabilizeEventType = AccessTools.TypeByName("XRL.World.RealityStabilizeEvent");
        if (eventType is null || gameObjectType is null || realityStabilizeEventType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            yield break;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.GelatenousPalmProperties", "FireEvent", new[] { eventType }))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.GraveMoss", "Trigger", Type.EmptyTypes))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.QuantumRippler", "HandleEvent", new[] { realityStabilizeEventType }))
        {
            yield return target;
        }

        foreach (var target in ResolveTarget("XRL.World.Parts.ReclamationCist", "PerformReclamationOf", new[] { gameObjectType }))
        {
            yield return target;
        }
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

        if (!DoesVerbRouteTranslator.TryTranslateMarkedMessage(message, out var translated)
            && !DoesVerbRouteTranslator.TryTranslatePlainSentence(message, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}.", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
