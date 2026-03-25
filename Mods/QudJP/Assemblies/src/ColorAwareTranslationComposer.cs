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

        return Restore(translateVisible(stripped), spans);
    }

    internal static string RestoreCapture(string value, IReadOnlyList<ColorSpan>? spans, Group group)
    {
        if (spans is null || spans.Count == 0 || !group.Success)
        {
            return value;
        }

        return RestoreSlice(value, spans, group.Index, group.Length);
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
            if (!group.Success)
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
}
