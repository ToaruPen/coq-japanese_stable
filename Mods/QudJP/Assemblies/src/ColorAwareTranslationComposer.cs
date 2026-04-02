using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class ColorAwareTranslationComposer
{
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

    internal static string RestoreSlice(string value, IReadOnlyList<ColorSpan>? spans, int startIndex, int length)
    {
        if (spans is null || spans.Count == 0)
        {
            return value;
        }

        return Restore(value, ColorCodePreserver.SliceSpans(spans, startIndex, length));
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
        int translatedLastCaptureEnd)
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
        var restoredSuffix = Restore(suffix, SliceSuffixBoundarySpans(spans, lastCaptureEnd, strippedSourceLength));
        return restoredPrefix + middle + restoredSuffix;
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
        int strippedSourceLength)
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
            if (span.Index == suffixSourceStart + 1
                && ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                continue;
            }

            if (span.Index > suffixSourceStart)
            {
                var relativeIndex = span.Index - suffixSourceStart;
                if (ColorCodePreserver.IsClosingBoundaryToken(span.Token)
                    && relativeIndex >= suffixSourceLength)
                {
                    boundarySpans.Add(new ColorSpan(suffixSourceLength, span.Token));
                    continue;
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
