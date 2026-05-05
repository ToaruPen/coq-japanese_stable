using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MessageLogPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(
            "XRL.Messages.MessageQueue:AddPlayerMessage",
            new[] { typeof(string), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve MessageQueue.AddPlayerMessage(string, string, bool). Patch will not apply.");
        }

        return method;
    }

    public static bool Prefix(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            _ = Color;
            _ = Capitalize;

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(Message, out var markedText))
            {
                FinalOutputObservability.RecordDirectMarker(
                    nameof(MessageLogPatch),
                    nameof(MessageLogPatch),
                    FinalOutputObservability.DetailDirectMarker,
                    Message,
                    markedText);
                Message = markedText;
                return true;
            }

            var patternMessage = Message;
            if (HasLeadingControlHeader(patternMessage)
                && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(
                        ref patternMessage,
                        nameof(MessageLogPatch),
                        "MarkedControlHeader",
                        markJapaneseAsDirect: true))
            {
                _ = MessageFrameTranslator.TryStripDirectTranslationMarker(patternMessage, out Message);
                return true;
            }

            if (DoesVerbRouteTranslator.TryTranslateMarkedMessage(Message, out var doesVerbTranslated))
            {
                DynamicTextObservability.RecordTransform(
                    nameof(DoesFragmentMarkingPatch),
                    "DoesVerb.MarkedMessage",
                    Message,
                    doesVerbTranslated);
                Message = doesVerbTranslated;
                return true;
            }

            if (JournalNotificationTranslator.TryTranslate(
                    Message,
                    nameof(MessageLogPatch),
                    "MessageLog.JournalNotification",
                    out var journalNotificationTranslated))
            {
                Message = journalNotificationTranslated;
                return true;
            }

            var (stripped, spans) = ColorAwareTranslationComposer.Strip(Message);
            if (DeathWrapperFamilyTranslator.TryTranslateMessage(stripped, spans, out var deathTranslated))
            {
                Message = deathTranslated;
                return true;
            }

            SinkObservation.LogUnclaimed(
                nameof(MessageLogPatch),
                nameof(MessageLogPatch),
                SinkObservation.ObservationOnlyDetail,
                Message,
                stripped);

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageLogPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    private static bool HasLeadingControlHeader(string? message)
    {
        return !string.IsNullOrEmpty(message) && message![0] == '\u0002';
    }
}
