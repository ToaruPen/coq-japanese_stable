using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class IronshankTranslationPatch
{
    private const string Context = nameof(IronshankTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Effects.Ironshank");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target or Event type not found.", Context);
            yield break;
        }

        var fireEvent = AccessTools.Method(targetType, "FireEvent", [eventType]);
        if (fireEvent is not null)
        {
            yield return fireEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.FireEvent(Event) target not found.", Context);
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

        if (!string.Equals(
                message,
                "You feel the cartilage stretch as your leg bones grind together at the joints.",
                StringComparison.Ordinal))
        {
            return false;
        }

        var translated = "足の骨が軋み、軟骨が伸びるのを感じた。";
        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }
}
