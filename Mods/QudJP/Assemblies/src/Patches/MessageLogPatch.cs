using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

public static class MessageLogPatch
{
    private static readonly Regex SubjectDirectionDisappearsPattern = new(
        "^(?:The |the |[Aa]n? )?(?<subject>.+?) to the (?<direction>north|south|east|west|northeast|northwest|southeast|southwest) disappears\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

            if (LiquidVolumeTranslationPatch.TryTranslateMessageLogMessage(ref Message, Color))
            {
                return true;
            }

            if (HiddenRenderTranslationPatch.TryTranslateMessageLogMessage(ref Message, Color))
            {
                return true;
            }

            if (TryTranslateSubjectDirectionDisappears(stripped, spans, Message, out var disappearsTranslated))
            {
                Message = disappearsTranslated;
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

    private static bool TryTranslateSubjectDirectionDisappears(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = SubjectDirectionDisappearsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var direction = TranslateDirection(match.Groups["direction"].Value);
        if (direction is null)
        {
            translated = source;
            return false;
        }

        var subject = ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["subject"].Value,
            spans,
            match.Groups["subject"]).Trim();
        subject = ColorAwareTranslationComposer.TranslatePreservingColors(
            subject,
            label => StringHelpers.StripLeadingEnglishArticle(
                label,
                includeCapitalizedDefiniteArticle: true,
                includeCapitalizedIndefiniteArticle: true));
        subject = AppendDefaultColorAfterInlineColor(subject);

        var core = $"{direction}にいる{subject}が姿を消した。";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            core,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string? TranslateDirection(string direction)
    {
        return direction switch
        {
            "north" => "北",
            "south" => "南",
            "east" => "東",
            "west" => "西",
            "northeast" => "北東",
            "northwest" => "北西",
            "southeast" => "南東",
            "southwest" => "南西",
            _ => null,
        };
    }

    private static string AppendDefaultColorAfterInlineColor(string source)
    {
        return source.IndexOf('&') < 0
            ? source
            : source + "&y";
    }
}
