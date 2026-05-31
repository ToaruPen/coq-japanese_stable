using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StomachTranslationPatch
{
    private const string Context = nameof(StomachTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var stomachType = AccessTools.TypeByName("XRL.World.Parts.Stomach");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (stomachType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var fireEvent = AccessTools.Method(stomachType, "FireEvent", [eventType]);
        if (fireEvent is not null)
        {
            yield return fireEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.FireEvent(Event) not found.", Context);
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

        if (!TryTranslate(message, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslate(string source, out string translated, out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (string.Equals(stripped, "You drank way too much!", StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.RestoreRelative("飲みすぎた！", spans, stripped.Length);
            detail = "StomachOverdrink";
            return true;
        }

        if (string.Equals(stripped, "You drank way too much! You vomit!", StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.RestoreRelative("飲みすぎた！ 吐いた！", spans, stripped.Length);
            detail = "StomachOverdrinkVomiting";
            return true;
        }

        if (string.Equals(stripped, "The moisture is sucked out of your body.", StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.RestoreRelative("体から水分が吸い出された。", spans, stripped.Length);
            detail = "StomachMoistureBody";
            return true;
        }

        if (string.Equals(stripped, "The moisture is sucked out of your throat.", StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.RestoreRelative("喉から水分が吸い出された。", spans, stripped.Length);
            detail = "StomachMoistureThroat";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }
}
