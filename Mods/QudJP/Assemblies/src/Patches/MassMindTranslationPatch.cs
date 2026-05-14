using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MassMindTranslationPatch
{
    private const string Context = nameof(MassMindTranslationPatch);

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

        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.MassMind");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "FireEvent", [eventType]);
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.FireEvent target not found.", Context, targetType.FullName);
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(message);
        var translated = stripped switch
        {
            "You feel a small ripple in space and time." => "時空に小さな波紋を感じた。",
            "Someone reaches through the aggregate mind and exhausts your power!" => "誰かが集合精神を通じて手を伸ばし、あなたの力を消耗させた！",
            "You innervate your mind at someone's expense." => "誰かを犠牲にして精神を活性化した。",
            _ => null,
        };
        if (translated is null)
        {
            return false;
        }

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            message);
        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, restored);
        message = MessageFrameTranslator.MarkDirectTranslation(restored);
        return true;
    }
}
