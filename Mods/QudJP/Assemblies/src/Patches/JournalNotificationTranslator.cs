using System;
using System.Collections.Generic;

namespace QudJP.Patches;

internal static class JournalNotificationTranslator
{
    private const string PieceOfInformationPrefix = "You note this piece of information in the ";
    private const string NoteLocationPrefix = "You note the location of ";
    private const string DiscoverLocationPrefix = "You discover the location of ";
    private const string DiscoveredLocationPrefix = "You discovered the location of ";
    private const string JournalSectionSuffix = " section of your journal.";
    private const string JournalPathSeparator = " > ";
    private const string SultanHistoriesSource = "Sultan Histories";
    private const string SultanHistoriesTranslated = "スルタン史";

    internal static bool TryTranslate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!IsJournalNotification(stripped))
        {
            translated = source;
            return false;
        }

        var journalTranslated = TryTranslatePieceOfInformation(stripped, spans, route, out var pieceTranslated)
            ? pieceTranslated
            : JournalPatternTranslator.Translate(source, route);
        if (string.Equals(journalTranslated, source, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = journalTranslated;
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool IsJournalNotification(string source)
    {
        if (source.StartsWith(PieceOfInformationPrefix, StringComparison.Ordinal)
            && source.EndsWith(JournalSectionSuffix, StringComparison.Ordinal))
        {
            return true;
        }

        if (source.StartsWith(NoteLocationPrefix, StringComparison.Ordinal)
            && source.EndsWith(JournalSectionSuffix, StringComparison.Ordinal))
        {
            return true;
        }

        return source.StartsWith(DiscoverLocationPrefix, StringComparison.Ordinal)
            || source.StartsWith(DiscoveredLocationPrefix, StringComparison.Ordinal);
    }

    private static bool TryTranslatePieceOfInformation(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        translated = string.Empty;
        if (!TryGetPieceOfInformationSectionPath(stripped, out var sourceSectionStart, out var sourceSectionPath))
        {
            return false;
        }

        var journalTranslated = JournalPatternTranslator.Translate(stripped, route);
        if (string.Equals(journalTranslated, stripped, StringComparison.Ordinal))
        {
            return false;
        }

        translated = RestoreSectionPathBoundaryWrappers(
            journalTranslated,
            spans,
            sourceSectionStart,
            sourceSectionPath);
        return true;
    }

    private static bool TryGetPieceOfInformationSectionPath(
        string stripped,
        out int sourceSectionStart,
        out string sourceSectionPath)
    {
        sourceSectionStart = -1;
        sourceSectionPath = string.Empty;
        if (!stripped.StartsWith(PieceOfInformationPrefix, StringComparison.Ordinal)
            || !stripped.EndsWith(JournalSectionSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        sourceSectionStart = PieceOfInformationPrefix.Length;
        sourceSectionPath = stripped.Substring(
            sourceSectionStart,
            stripped.Length - sourceSectionStart - JournalSectionSuffix.Length);
        return sourceSectionPath.Length > 0;
    }

    private static string RestoreSectionPathBoundaryWrappers(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int sourceSectionStart,
        string sourceSectionPath)
    {
        if (spans.Count == 0
            || !TryFindTranslatedSectionRange(translated, sourceSectionPath, out var translatedStart, out var translatedEnd))
        {
            return translated;
        }

        var sourceSectionEnd = sourceSectionStart + sourceSectionPath.Length;
        var projectedSpans = new List<ColorSpan>();
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index == 0)
            {
                projectedSpans.Add(new ColorSpan(0, span.Token));
                continue;
            }

            if (span.Index == sourceSectionStart && ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                projectedSpans.Add(new ColorSpan(translatedStart, span.Token));
                continue;
            }

            if (span.Index == sourceSectionEnd && ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                projectedSpans.Add(new ColorSpan(translatedEnd, span.Token));
            }
        }

        return projectedSpans.Count == 0
            ? translated
            : ColorAwareTranslationComposer.Restore(translated, projectedSpans);
    }

    private static bool TryFindTranslatedSectionRange(
        string translated,
        string sourceSectionPath,
        out int translatedStart,
        out int translatedEnd)
    {
        translatedStart = -1;
        translatedEnd = -1;
        var translatedSectionPath = TranslateJournalSectionPath(sourceSectionPath);
        if (translatedSectionPath.Length == 0)
        {
            return false;
        }

        translatedStart = translated.IndexOf(translatedSectionPath, StringComparison.Ordinal);
        if (translatedStart < 0)
        {
            return false;
        }

        if (translated.IndexOf(translatedSectionPath, translatedStart + 1, StringComparison.Ordinal) >= 0)
        {
            translatedStart = -1;
            return false;
        }

        translatedEnd = translatedStart + translatedSectionPath.Length;
        return true;
    }

    private static string TranslateJournalSectionPath(string sourceSectionPath)
    {
        var parts = sourceSectionPath.Split(new[] { JournalPathSeparator }, StringSplitOptions.None);
        for (var index = 0; index < parts.Length; index++)
        {
            parts[index] = TranslateJournalPathSegment(parts[index]);
        }

        return string.Join(JournalPathSeparator, parts);
    }

    private static string TranslateJournalPathSegment(string source)
    {
        if (string.Equals(source, SultanHistoriesSource, StringComparison.Ordinal))
        {
            return SultanHistoriesTranslated;
        }

        return source;
    }
}
