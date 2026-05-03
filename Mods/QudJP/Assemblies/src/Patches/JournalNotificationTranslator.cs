using System;
using System.Collections.Generic;
using System.Diagnostics;

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

        translated = RemoveUnmatchedSectionPathCloseIfNeeded(stripped, journalTranslated);
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

    private static bool TryGetNoteLocationSectionPath(string stripped, out string sourceSectionPath)
    {
        sourceSectionPath = string.Empty;
        if (!stripped.StartsWith(NoteLocationPrefix, StringComparison.Ordinal)
            || !stripped.EndsWith(JournalSectionSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var sectionEnd = stripped.Length - JournalSectionSuffix.Length;
        var separator = " in the ";
        var separatorIndex = stripped.LastIndexOf(separator, sectionEnd - 1, sectionEnd, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return false;
        }

        var sectionStart = separatorIndex + separator.Length;
        sourceSectionPath = stripped.Substring(sectionStart, sectionEnd - sectionStart);
        return sourceSectionPath.Length > 0;
    }

    private static string RemoveUnmatchedSectionPathCloseIfNeeded(string stripped, string translated)
    {
        if (!TryGetNoteLocationSectionPath(stripped, out var sourceSectionPath))
        {
            return translated;
        }

        var translatedSectionPath = TranslateJournalSectionPath(sourceSectionPath);
        if (translatedSectionPath.Length == 0)
        {
            return translated;
        }

        var extraClose = translatedSectionPath + "}}";
        var index = translated.IndexOf(extraClose, StringComparison.Ordinal);
        if (index < 0 || translated.IndexOf(extraClose, index + extraClose.Length, StringComparison.Ordinal) >= 0)
        {
            return translated;
        }

        return translated.Remove(index + translatedSectionPath.Length, 2);
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

        try
        {
            if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
                && !string.Equals(source, translated, StringComparison.Ordinal))
            {
                return translated;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: JournalNotificationTranslator could not translate journal path segment '{0}': {1}", source, ex.Message);
        }

        return source;
    }
}
