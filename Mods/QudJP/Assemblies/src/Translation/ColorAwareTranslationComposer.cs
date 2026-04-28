using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class ColorAwareTranslationComposer
{
    internal sealed class WholeBoundaryPair
    {
        internal WholeBoundaryPair(ColorSpan opening, int openingOrder, ColorSpan closing, int closingOrder)
        {
            Opening = opening;
            OpeningOrder = openingOrder;
            Closing = closing;
            ClosingOrder = closingOrder;
        }

        internal ColorSpan Opening { get; }

        internal int OpeningOrder { get; }

        internal ColorSpan Closing { get; }

        internal int ClosingOrder { get; }
    }

    internal static (string stripped, List<ColorSpan> spans) Strip(string? source)
    {
        return ColorCodePreserver.Strip(source);
    }

    internal static string Restore(string? translated, IReadOnlyList<ColorSpan>? spans)
    {
        if (spans is null || spans.Count == 0)
        {
            return translated ?? string.Empty;
        }

        return ColorCodePreserver.Restore(
            translated,
            spans as List<ColorSpan> ?? new List<ColorSpan>(spans));
    }

    internal static string RestoreRelative(string? translated, IReadOnlyList<ColorSpan>? spans, int sourceLength)
    {
        if (spans is null || spans.Count == 0)
        {
            return translated ?? string.Empty;
        }

        var relativeSpans = new List<ColorSpan>(spans.Count);
        for (var index = 0; index < spans.Count; index++)
        {
            relativeSpans.Add(spans[index].WithRelativeIndex(sourceLength));
        }

        return ColorCodePreserver.Restore(translated, relativeSpans);
    }

    internal static string TranslatePreservingColors(string? source)
    {
        return TranslatePreservingColors(source, Translator.Translate);
    }

    internal static string TranslatePreservingColors(string? source, Func<string, string> translateVisible)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var (stripped, spans) = Strip(source);
        if (stripped.Length == 0)
        {
            return source!;
        }

        var translated = translateVisible(stripped);
        return ShouldRestoreWholeRelatively(spans, stripped.Length)
            ? RestoreRelative(translated, spans, stripped.Length)
            : Restore(translated, spans);
    }

    internal static string RestoreCapture(string value, IReadOnlyList<ColorSpan>? spans, Group group)
    {
        if (spans is null || spans.Count == 0 || !group.Success)
        {
            return value;
        }

        var captureSpans = ColorCodePreserver.SliceSpans(spans, group.Index, group.Length);
        captureSpans.AddRange(ColorCodePreserver.SliceAdjacentCaptureBoundarySpans(spans, group.Index, group.Length));
        return Restore(value, captureSpans);
    }

    internal static string MarkupAwareRestoreCapture(string value, IReadOnlyList<ColorSpan>? spans, Group group)
    {
        if (spans is null || spans.Count == 0 || !group.Success)
        {
            return value;
        }

        return HasColorMarkup(value)
            ? RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(value, spans, group)
            : RestoreCapture(value, spans, group);
    }

    internal static bool HasColorMarkup(string source)
    {
        var (stripped, _) = Strip(source);
        return !string.Equals(stripped, source, StringComparison.Ordinal);
    }

    internal static string RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
        string translatedValue,
        IReadOnlyList<ColorSpan>? spans,
        Group group)
    {
        if (spans is null || spans.Count == 0 || !group.Success)
        {
            return translatedValue;
        }

        var (visible, translatedOwnedSpans) = Strip(translatedValue);
        var wholeCapturePairs = SliceWholeBoundaryPairs(spans, group.Index, group.Length);
        var preservedSourceWrappers = ProjectWholeBoundaryPairsAbsolute(wholeCapturePairs, visible.Length);
        if (preservedSourceWrappers.Count == 0)
        {
            return translatedValue;
        }

        var mergedSpans = new List<ColorSpan>(translatedOwnedSpans.Count + preservedSourceWrappers.Count);
        for (var index = 0; index < preservedSourceWrappers.Count; index++)
        {
            var span = preservedSourceWrappers[index];
            if (span.Index == 0)
            {
                mergedSpans.Add(span);
            }
        }

        mergedSpans.AddRange(translatedOwnedSpans);

        for (var index = 0; index < preservedSourceWrappers.Count; index++)
        {
            var span = preservedSourceWrappers[index];
            if (span.Index == visible.Length)
            {
                mergedSpans.Add(span);
            }
        }

        return Restore(visible, mergedSpans);
    }

    internal static List<WholeBoundaryPair> SliceWholeBoundaryPairs(
        IReadOnlyList<ColorSpan>? spans,
        int sourceStart,
        int sourceLength)
    {
        var pairs = new List<WholeBoundaryPair>();
        if (spans is null || spans.Count == 0)
        {
            return pairs;
        }

        var sourceEnd = sourceStart + sourceLength;
        var openingCandidates = new List<(ColorSpan Span, int Order)>();
        var closingCandidates = new List<(ColorSpan Span, int Order)>();

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index == sourceStart && ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                openingCandidates.Add((span, index));
            }

            if (span.Index == sourceEnd && ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                closingCandidates.Add((span, index));
            }
        }

        var pairCount = Math.Min(openingCandidates.Count, closingCandidates.Count);
        for (var index = 0; index < pairCount; index++)
        {
            pairs.Add(new WholeBoundaryPair(
                openingCandidates[index].Span,
                openingCandidates[index].Order,
                closingCandidates[index].Span,
                closingCandidates[index].Order));
        }

        return pairs;
    }

    internal static List<ColorSpan> ProjectWholeBoundaryPairsAbsolute(
        IReadOnlyList<WholeBoundaryPair> pairs,
        int translatedLength)
    {
        var projected = new List<ColorSpan>(pairs.Count * 2);
        for (var index = 0; index < pairs.Count; index++)
        {
            projected.Add(new ColorSpan(0, pairs[index].Opening.Token));
        }

        for (var index = 0; index < pairs.Count; index++)
        {
            projected.Add(new ColorSpan(translatedLength, pairs[index].Closing.Token));
        }

        return projected;
    }

    internal static List<ColorSpan> ProjectWholeBoundaryPairsRelative(
        IReadOnlyList<WholeBoundaryPair> pairs,
        int sourceLength)
    {
        var projected = new List<ColorSpan>(pairs.Count * 2);
        for (var index = 0; index < pairs.Count; index++)
        {
            projected.Add(new ColorSpan(0, pairs[index].Opening.Token, sourceLength, usesRelativeIndex: true));
        }

        for (var index = 0; index < pairs.Count; index++)
        {
            projected.Add(new ColorSpan(sourceLength, pairs[index].Closing.Token, sourceLength, usesRelativeIndex: true));
        }

        return projected;
    }

    internal static void RemoveWholeBoundaryOpenings(
        List<ColorSpan> captureSpans,
        IReadOnlyList<WholeBoundaryPair> pairs)
    {
        for (var pairIndex = 0; pairIndex < pairs.Count; pairIndex++)
        {
            var openingToken = pairs[pairIndex].Opening.Token;
            for (var spanIndex = 0; spanIndex < captureSpans.Count; spanIndex++)
            {
                var span = captureSpans[spanIndex];
                if (span.Index == 0 && string.Equals(span.Token, openingToken, StringComparison.Ordinal))
                {
                    captureSpans.RemoveAt(spanIndex);
                    break;
                }
            }
        }
    }

    internal static string RestoreSlice(string value, IReadOnlyList<ColorSpan>? spans, int startIndex, int length)
    {
        if (spans is null || spans.Count == 0)
        {
            return value;
        }

        var sliceSpans = ColorCodePreserver.SliceSpans(spans, startIndex, length);
        sliceSpans = AnchorTrailingClosingBoundarySpans(sliceSpans, value, length);
        return Restore(value, sliceSpans);
    }

    internal static List<ColorSpan> SliceBoundarySpans(
        IReadOnlyList<ColorSpan>? spans,
        Match match,
        int strippedSourceLength,
        int translatedLength)
    {
        var boundarySpans = new List<ColorSpan>();
        if (spans is null || spans.Count == 0)
        {
            return boundarySpans;
        }

        var firstCaptureStart = strippedSourceLength;
        var lastCaptureEnd = 0;
        for (var index = 1; index < match.Groups.Count; index++)
        {
            var group = match.Groups[index];
            if (!group.Success || group.Length == 0)
            {
                continue;
            }

            if (group.Index < firstCaptureStart)
            {
                firstCaptureStart = group.Index;
            }

            var groupEnd = group.Index + group.Length;
            if (groupEnd > lastCaptureEnd)
            {
                lastCaptureEnd = groupEnd;
            }
        }

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index < firstCaptureStart && firstCaptureStart > 0)
            {
                boundarySpans.Add(new ColorSpan(0, span.Token));
                continue;
            }

            if (span.Index > lastCaptureEnd && lastCaptureEnd < strippedSourceLength)
            {
                boundarySpans.Add(new ColorSpan(translatedLength, span.Token));
            }
        }

        return boundarySpans;
    }

    internal static string RestoreMatchBoundaries(
        string translated,
        IReadOnlyList<ColorSpan>? spans,
        Match match,
        int strippedSourceLength,
        int translatedFirstCaptureStart,
        int translatedLastCaptureEnd,
        bool skipAdjacentClosingBoundary)
    {
        if (spans is null || spans.Count == 0)
        {
            return translated;
        }

        var firstCaptureStart = strippedSourceLength;
        var lastCaptureEnd = 0;
        for (var index = 1; index < match.Groups.Count; index++)
        {
            var group = match.Groups[index];
            if (!group.Success || group.Length == 0)
            {
                continue;
            }

            if (group.Index < firstCaptureStart)
            {
                firstCaptureStart = group.Index;
            }

            var groupEnd = group.Index + group.Length;
            if (groupEnd > lastCaptureEnd)
            {
                lastCaptureEnd = groupEnd;
            }
        }

        if (firstCaptureStart == strippedSourceLength && lastCaptureEnd == 0)
        {
            return translated;
        }

        var prefixLength = translatedFirstCaptureStart;
        if (prefixLength < 0)
        {
            prefixLength = 0;
        }

        var suffixStart = translatedLastCaptureEnd;
        if (suffixStart < 0 || suffixStart > translated.Length)
        {
            suffixStart = translated.Length;
        }

        var prefix = prefixLength == 0 ? string.Empty : translated.Substring(0, prefixLength);
        var middleLength = suffixStart - prefixLength;
        if (middleLength < 0)
        {
            middleLength = 0;
        }

        var middle = middleLength == 0 ? string.Empty : translated.Substring(prefixLength, middleLength);
        var suffix = suffixStart >= translated.Length ? string.Empty : translated.Substring(suffixStart);

        var restoredPrefix = Restore(prefix, SlicePrefixBoundarySpans(spans, firstCaptureStart));
        var restoredSuffix = RestoreSuffixBoundarySegment(
            suffix,
            SliceSuffixBoundarySpans(
                spans,
                lastCaptureEnd,
                strippedSourceLength,
                HasInnerOpeningBoundaryBeforeCapture(spans, firstCaptureStart),
                skipAdjacentClosingBoundary));
        return restoredPrefix + middle + restoredSuffix;
    }

    private static string RestoreSuffixBoundarySegment(string suffix, IReadOnlyList<ColorSpan> spans)
    {
        if (suffix.Length == 0 || spans.Count == 0)
        {
            return suffix;
        }

        var closingQuoteIndex = suffix.IndexOf('」');
        if (closingQuoteIndex < 0 || closingQuoteIndex + 1 >= suffix.Length)
        {
            return Restore(suffix, spans);
        }

        var quotedSuffix = suffix.Substring(0, closingQuoteIndex + 1);
        var trailingSuffix = suffix.Substring(closingQuoteIndex + 1);
        return Restore(quotedSuffix, spans) + trailingSuffix;
    }

    private static List<ColorSpan> SlicePrefixBoundarySpans(IReadOnlyList<ColorSpan> spans, int prefixSourceLength)
    {
        var boundarySpans = new List<ColorSpan>();
        if (prefixSourceLength <= 0)
        {
            return boundarySpans;
        }

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index < prefixSourceLength)
            {
                boundarySpans.Add(new ColorSpan(span.Index, span.Token, prefixSourceLength, usesRelativeIndex: true));
            }
        }

        return boundarySpans;
    }

    private static List<ColorSpan> SliceSuffixBoundarySpans(
        IReadOnlyList<ColorSpan> spans,
        int suffixSourceStart,
        int strippedSourceLength,
        bool anchorClosingTokensToSourceSuffix,
        bool skipAdjacentClosingBoundary)
    {
        var boundarySpans = new List<ColorSpan>();
        var suffixSourceLength = strippedSourceLength - suffixSourceStart;
        if (suffixSourceLength <= 0)
        {
            return boundarySpans;
        }

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (skipAdjacentClosingBoundary
                && span.Index == suffixSourceStart + 1
                && ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                continue;
            }

            if (span.Index > suffixSourceStart)
            {
                var relativeIndex = span.Index - suffixSourceStart;
                if (ColorCodePreserver.IsClosingBoundaryToken(span.Token))
                {
                    if (anchorClosingTokensToSourceSuffix && relativeIndex >= suffixSourceLength)
                    {
                        boundarySpans.Add(new ColorSpan(suffixSourceLength, span.Token));
                        continue;
                    }

                    if (!anchorClosingTokensToSourceSuffix)
                    {
                        boundarySpans.Add(new ColorSpan(
                            suffixSourceLength,
                            span.Token,
                            suffixSourceLength,
                            usesRelativeIndex: true));
                        continue;
                    }
                }

                boundarySpans.Add(new ColorSpan(
                    relativeIndex,
                    span.Token,
                    suffixSourceLength,
                    usesRelativeIndex: true));
            }
        }

        return boundarySpans;
    }

    private static bool HasInnerOpeningBoundaryBeforeCapture(IReadOnlyList<ColorSpan> spans, int firstCaptureStart)
    {
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index <= 0 || span.Index >= firstCaptureStart)
            {
                continue;
            }

            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                return true;
            }
        }

        return false;
    }

    private static List<ColorSpan> AnchorTrailingClosingBoundarySpans(
        List<ColorSpan> spans,
        string translated,
        int sourceLength)
    {
        if (spans.Count == 0 || translated.Length == 0 || sourceLength <= 0)
        {
            return spans;
        }

        var leadingBoundaryLength = MeasureLeadingClosingBoundaryLength(translated);
        if (leadingBoundaryLength <= 0)
        {
            return spans;
        }

        var anchored = new List<ColorSpan>(spans.Count);
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.UsesRelativeIndex
                && span.SourceLength == sourceLength
                && span.Index >= sourceLength
                && ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                anchored.Add(new ColorSpan(leadingBoundaryLength, span.Token));
                continue;
            }

            anchored.Add(span);
        }

        return anchored;
    }

    private static int MeasureLeadingClosingBoundaryLength(string translated)
    {
        var length = 0;
        while (length < translated.Length && IsLeadingClosingBoundaryCharacter(translated[length]))
        {
            length++;
        }

        return length;
    }

    private static bool IsLeadingClosingBoundaryCharacter(char character)
    {
        return character switch
        {
            '!' or '?' or '.' or ',' or ':' or ';'
                or '！' or '？' or '。' or '、' or '：' or '；'
                or ')' or ']' or '}' or '〉' or '》' or '」' or '』' or '】' or '〕' or '〗' or '〙' or '〛' or '＞' or '）' or '］' or '｝'
                or '\'' or '"' or '’' or '”'
                => true,
            _ => false,
        };
    }

    private static bool ShouldRestoreWholeRelatively(IReadOnlyList<ColorSpan> spans, int sourceLength)
    {
        if (sourceLength <= 0)
        {
            return false;
        }

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index > 0 && span.Index < sourceLength - 1)
            {
                return false;
            }
        }

        return true;
    }
}
