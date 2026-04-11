using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DescriptionTextTranslator
{
    private static readonly Regex FactionDispositionPattern =
        new Regex("^(?<relation>Loved by|Admired by|Hated by|Disliked by) (?<target>.+?)(?: for (?<reason>.+?))?\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LabeledListPattern =
        new Regex("^(?<label>Physical features:|Equipped:) (?<items>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StatAbbreviationPattern =
        new Regex("^[A-Z]{2,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SignedStatAbbreviationPattern =
        new Regex("^[+-]\\d+\\s+[A-Z]{2,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Keep TranslateShortDescription and TranslateLongDescription separate even though they
    // currently delegate to TranslateDescriptionText, so short/long description routes can
    // diverge later without changing their patch call sites.
    internal static string TranslateShortDescription(string source, string route)
    {
        return TranslateDescriptionText(source, route);
    }

    internal static string TranslateLongDescription(string source, string route)
    {
        return TranslateDescriptionText(source, route);
    }

    private static string TranslateDescriptionText(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (TryTranslateSegmentPreservingColors(source, route, out var wholeTranslated))
        {
            return wholeTranslated;
        }

        if (source.IndexOf('\n') < 0)
        {
            return source;
        }

        var newline = source.Contains("\r\n") ? "\r\n" : "\n";
        var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryTranslateSegmentPreservingColors(lines[index], route, out var translatedLine))
            {
                continue;
            }

            lines[index] = translatedLine;
            changed = true;
        }

        return changed ? string.Join(newline, lines) : source;
    }

    private static bool TryTranslateSegmentPreservingColors(string source, string route, out string translated)
    {
        if (TryTranslateFactionDispositionLinePreservingColors(source, route, out translated))
        {
            return true;
        }

        translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TryTranslateVisibleSegment(visible, route, out var candidate)
                ? candidate
                : visible);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateVisibleSegment(string source, string route, out string translated)
    {
        if (TryTranslateLabeledList(source, route, out translated))
        {
            return true;
        }

        if (WorldModsTextTranslator.TryTranslate(source, route, "Description.WorldMods", out translated))
        {
            return true;
        }

        if (StatusLineTranslationHelpers.TryTranslateCompareStatusLine(source, route, "Description.CompareStatus", out translated))
        {
            return true;
        }

        if (StatusLineTranslationHelpers.TryTranslateCompareStatusSequence(source, route, "Description.CompareSequence", out translated))
        {
            return true;
        }

        if (StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(source, route, "Description.ActiveEffects", out translated))
        {
            return true;
        }

        if (ShouldSkipExactLeafTranslation(source))
        {
            translated = source;
            return false;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "Description.ExactLeaf", source, translated);
            return true;
        }

        translated = MessagePatternTranslator.Translate(source, route);
        if (!string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "Description.Pattern", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateFactionDispositionLinePreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = FactionDispositionPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var relation = match.Groups["relation"].Value switch
        {
            "Loved by" => "愛されている",
            "Admired by" => "敬愛されている",
            "Hated by" => "憎まれている",
            "Disliked by" => "嫌われている",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(relation))
        {
            translated = source;
            return false;
        }

        relation = RestoreBalancedCapture(relation, spans, match.Groups["relation"]);
        var target = RestoreBalancedCapture(match.Groups["target"].Value, spans, match.Groups["target"]);
        var reasonGroup = match.Groups["reason"];
        if (!reasonGroup.Success)
        {
            translated = target + "に" + relation + "。";
            translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
            DynamicTextObservability.RecordTransform(route, "Description.FactionDisposition", source, translated);
            return true;
        }

        var reason = TranslateDispositionReason(reasonGroup.Value, route);
        reason = RestoreBalancedCapture(reason, spans, reasonGroup);
        translated = target + "に" + relation + "。理由: " + reason + "。";
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.FactionDisposition", source, translated);
        return true;
    }

    private static string RestoreBalancedCapture(string value, IReadOnlyList<ColorSpan>? spans, Group group)
    {
        if (spans is null || spans.Count == 0 || !group.Success)
        {
            return value;
        }

        var captureSpans = ColorCodePreserver.SliceSpans(spans, group.Index, group.Length);
        captureSpans.AddRange(ColorCodePreserver.SliceAdjacentCaptureBoundarySpans(spans, group.Index, group.Length));
        captureSpans = FilterBalancedBoundarySpans(captureSpans);
        return captureSpans.Count == 0
            ? value
            : ColorAwareTranslationComposer.Restore(value, captureSpans);
    }

    private static List<ColorSpan> FilterBalancedBoundarySpans(List<ColorSpan> spans)
    {
        if (spans.Count == 0)
        {
            return spans;
        }

        var keep = new bool[spans.Count];
        var openingStack = new Stack<int>();
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                openingStack.Push(index);
                continue;
            }

            if (!ColorCodePreserver.IsClosingBoundaryToken(span.Token) || openingStack.Count == 0)
            {
                continue;
            }

            var openingIndex = openingStack.Pop();
            keep[openingIndex] = true;
            keep[index] = true;
        }

        var filtered = new List<ColorSpan>();
        for (var index = 0; index < spans.Count; index++)
        {
            if (keep[index])
            {
                filtered.Add(spans[index]);
            }
        }

        return filtered;
    }

    private static string RestoreWholeLineBoundaryWrappers(string translated, IReadOnlyList<ColorSpan>? spans, int sourceLength)
    {
        if (spans is null || spans.Count == 0)
        {
            return translated;
        }

        var wholeLineSpans = SliceWholeLineBoundarySpans(spans, sourceLength, translated.Length);
        return wholeLineSpans.Count == 0
            ? translated
            : ColorAwareTranslationComposer.Restore(translated, wholeLineSpans);
    }

    private static List<ColorSpan> SliceWholeLineBoundarySpans(IReadOnlyList<ColorSpan> spans, int sourceLength, int translatedLength)
    {
        var wholeLinePairs = new List<(ColorSpan Opening, int OpeningOrder, ColorSpan Closing, int ClosingOrder)>();
        var openingStack = new Stack<(ColorSpan Span, int Order)>();

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                openingStack.Push((span, index));
                continue;
            }

            if (!ColorCodePreserver.IsClosingBoundaryToken(span.Token) || openingStack.Count == 0)
            {
                continue;
            }

            var opening = openingStack.Pop();
            if (opening.Span.Index == 0 && span.Index == sourceLength)
            {
                wholeLinePairs.Add((opening.Span, opening.Order, span, index));
            }
        }

        if (wholeLinePairs.Count == 0)
        {
            return new List<ColorSpan>();
        }

        var result = new List<ColorSpan>(wholeLinePairs.Count * 2);
        var openingOrderedPairs = wholeLinePairs.OrderBy(static pair => pair.OpeningOrder).ToArray();
        for (var index = 0; index < openingOrderedPairs.Length; index++)
        {
            var pair = openingOrderedPairs[index];
            result.Add(new ColorSpan(0, pair.Opening.Token));
        }

        var closingOrderedPairs = wholeLinePairs.OrderBy(static pair => pair.ClosingOrder).ToArray();
        for (var index = 0; index < closingOrderedPairs.Length; index++)
        {
            var pair = closingOrderedPairs[index];
            result.Add(new ColorSpan(translatedLength, pair.Closing.Token));
        }

        return result;
    }

    private static string TranslateDispositionReason(string source, string route)
    {
        if (ShouldSkipExactLeafTranslation(source))
        {
            return source;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            return translated;
        }

        translated = MessagePatternTranslator.Translate(source, route);
        return translated;
    }

    private static bool TryTranslateLabeledList(string source, string route, out string translated)
    {
        var match = LabeledListPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var label = match.Groups["label"].Value switch
        {
            "Physical features:" => "身体的特徴:",
            "Equipped:" => "装備:",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(label))
        {
            translated = source;
            return false;
        }

        var parts = match.Groups["items"].Value.Split(new[] { ", " }, StringSplitOptions.None);
        for (var index = 0; index < parts.Length; index++)
        {
            if (StringHelpers.TryGetTranslationExactOrLowerAscii(parts[index], out var translatedPart))
            {
                parts[index] = translatedPart;
            }
        }

        translated = label + " " + string.Join("、", parts);
        DynamicTextObservability.RecordTransform(route, "Description.LabeledList", source, translated);
        return true;
    }

    private static bool ShouldSkipExactLeafTranslation(string source)
    {
        return StatAbbreviationPattern.IsMatch(source)
            || SignedStatAbbreviationPattern.IsMatch(source);
    }
}
