using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ConversationCheckLostPopupTranslationPatch
{
    private const string Context = nameof(ConversationCheckLostPopupTranslationPatch);
    private const string ListenerNoLongerLostSource = "You ask about your location and are no longer lost.";
    private const string SpeakerNoLongerLostNeedle = " location and ";
    private const string SpeakerNoLongerLostSuffix = " no longer lost";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.UI.ConversationUI");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "CheckLost", Type.EmptyTypes);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.CheckLost target not found.", Context);
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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = family;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (string.Equals(source, ListenerNoLongerLostSource, StringComparison.Ordinal))
        {
            translated = Translator.Translate(source);
            if (string.Equals(translated, source, StringComparison.Ordinal))
            {
                return false;
            }

            Record(route, "ListenerNoLongerLost", source, translated);
            return true;
        }

        if (IsSpeakerNoLongerLostCandidate(source)
            && (DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out translated)
                || DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated)))
        {
            Record(route, "SpeakerNoLongerLost", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool IsSpeakerNoLongerLostCandidate(string source)
    {
        return source.Contains(SpeakerNoLongerLostNeedle)
            && source.Contains(SpeakerNoLongerLostSuffix);
    }

    private static void Record(string route, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
    }
}
