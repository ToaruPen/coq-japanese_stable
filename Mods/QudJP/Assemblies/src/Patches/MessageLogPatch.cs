using System;
using System.Diagnostics;

namespace QudJP.Patches;

public static class MessageLogPatch
{
    public static bool Prefix(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            _ = Color;
            _ = Capitalize;

            if (CampfirePreserveTranslationPatch.TryTranslateMessageLogMessage(
                    Message,
                    nameof(MessageLogPatch),
                    "MessageLog.CampfirePreserve",
                    out var campfirePreserveTranslated))
            {
                Message = campfirePreserveTranslated;
                return true;
            }

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

            if (WaterRitualTextTranslator.TryTranslateMessage(
                    Message,
                    nameof(MessageLogPatch),
                    "MessageLog.WaterRitual",
                    out var waterRitualTranslated))
            {
                Message = waterRitualTranslated;
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
