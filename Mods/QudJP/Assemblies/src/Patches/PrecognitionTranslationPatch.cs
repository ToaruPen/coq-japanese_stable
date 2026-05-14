using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PrecognitionTranslationPatch
{
    private const string Context = nameof(PrecognitionTranslationPatch);
    private const string DictionaryFile = "ui-messagelog-world.ja.json";
    private const string PrecognitionContext = "XRL.World.Parts.Mutation.Precognition";
    private const string GenericEffectContext = "XRL.World.Effects.Generic";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var precognitionType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Precognition");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var partType = AccessTools.TypeByName("XRL.World.IPart");
        if (precognitionType is null || eventType is null || gameObjectType is null || partType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var fireEvent = AccessTools.Method(precognitionType, "FireEvent", [eventType]);
        if (fireEvent is not null)
        {
            yield return fireEvent;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.FireEvent(Event) not found.", Context);
        }

        var onBeforeDie = AccessTools.Method(
            precognitionType,
            "OnBeforeDie",
            [
                gameObjectType,
                typeof(Guid),
                typeof(Guid),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(long).MakeByRefType(),
                typeof(bool),
                typeof(bool),
                partType,
            ]);
        if (onBeforeDie is not null)
        {
            yield return onBeforeDie;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.OnBeforeDie(...) not found.", Context);
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

        if (!TryTranslatePrecognitionMessage(message, out var translated, out var detail))
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

    private static bool TryTranslatePrecognitionMessage(
        string source,
        out string translated,
        out string detail)
    {
        var context = source switch
        {
            "You peer into the future." => PrecognitionContext,
            "You sense a subtle psychic disturbance." => GenericEffectContext,
            "Your focus returns to the present." => PrecognitionContext,
            _ => null,
        };
        detail = source switch
        {
            "You peer into the future." => "PeerIntoFuture",
            "You sense a subtle psychic disturbance." => "PsychicDisturbance",
            "Your focus returns to the present." => "FocusReturns",
            _ => string.Empty,
        };

        if (context is null)
        {
            translated = source;
            return false;
        }

        var dictionaryTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
            source,
            context,
            DictionaryFile);
        if (string.IsNullOrEmpty(dictionaryTranslation)
            || string.Equals(dictionaryTranslation, source, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = dictionaryTranslation!;
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }
}
