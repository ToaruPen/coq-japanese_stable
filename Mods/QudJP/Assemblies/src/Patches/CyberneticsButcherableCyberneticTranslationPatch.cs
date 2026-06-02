using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsButcherableCyberneticTranslationPatch
{
    private const string Context = nameof(CyberneticsButcherableCyberneticTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsButcherableCybernetic");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        if (targetType is null || gameObjectType is null || cellType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var trackingType = typeof(List<>).MakeGenericType(gameObjectType);
        var method = AccessTools.Method(
            targetType,
            "AttemptButcher",
            [
                gameObjectType,
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(int),
                cellType,
                trackingType,
            ]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.AttemptButcher target not found.", Context);
            yield break;
        }

        yield return method;
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

        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!OwnerTranslationScope.IsActive(activeDepth))
        {
            return false;
        }

        var source = message;
        var patternTranslated = MessagePatternTranslator.Translate(source, Context);
        if (!string.Equals(patternTranslated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(
                "MessageQueue.AddPlayerMessage",
                Context + ".DoesVerb",
                source,
                patternTranslated);
            message = MessageFrameTranslator.MarkDirectTranslation(patternTranslated);
            return true;
        }

        if (!DoesVerbRouteTranslator.TryTranslateMarkedMessage(message, out var translated)
            && !DoesVerbRouteTranslator.TryTranslatePlainSentence(message, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + ".DoesVerb",
            source,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }
}
