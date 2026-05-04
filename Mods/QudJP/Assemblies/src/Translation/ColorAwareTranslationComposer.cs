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
        if (HasColorMarkup(translated))
        {
            return RestoreTranslatedMarkupPreservingSourceOwnership(translated, spans, stripped);
        }

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

    private static string RestoreTranslatedMarkupPreservingSourceOwnership(
        string translatedValue,
        IReadOnlyList<ColorSpan>? spans,
        string sourceVisible)
    {
        if (spans is null || spans.Count == 0 || sourceVisible.Length == 0)
        {
            return translatedValue;
        }

        var wholeSourcePairs = ExtractTrueWholeBoundaryPairs(spans, sourceStart: 0, sourceVisible.Length);
        var restored = wholeSourcePairs.Count > 0
            ? RestoreWholeBoundaryPairsPreservingTranslatedOwnership(translatedValue, wholeSourcePairs)
            : translatedValue;

        return RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            restored,
            spans,
            sourceVisible,
            suppressNestedSameFamilyWrappers: wholeSourcePairs.Count == 0);
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

        var wholeCapturePairs = ExtractTrueWholeBoundaryPairs(spans, group.Index, group.Length);
        return RestoreWholeBoundaryPairsPreservingTranslatedOwnership(translatedValue, wholeCapturePairs);
    }

    internal static string RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
        string translatedValue,
        IReadOnlyList<ColorSpan>? spans,
        int sourceLength)
    {
        if (spans is null || spans.Count == 0 || sourceLength <= 0)
        {
            return translatedValue;
        }

        var wholeSourcePairs = ExtractTrueWholeBoundaryPairs(spans, sourceStart: 0, sourceLength);
        return RestoreWholeBoundaryPairsPreservingTranslatedOwnership(translatedValue, wholeSourcePairs);
    }

    internal static string RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
        string translatedValue,
        IReadOnlyList<ColorSpan>? spans,
        string sourceVisible,
        bool suppressNestedSameFamilyWrappers = true)
    {
        if (spans is null || spans.Count == 0 || sourceVisible.Length == 0)
        {
            return translatedValue;
        }

        var sourceOwnedBoundaryPairs = SelectSourceOwnedBoundaryPairs(
            ExtractTrueBoundaryPairs(spans, sourceStart: 0, sourceVisible.Length),
            suppressNestedSameFamilyWrappers);
        if (sourceOwnedBoundaryPairs.Count == 0)
        {
            return translatedValue;
        }

        var (visible, translatedOwnedSpans) = Strip(translatedValue);
        var translatedBoundaryPairs = ExtractTrueBoundaryPairs(
            translatedOwnedSpans,
            sourceStart: 0,
            visible.Length);
        var mappedPairs = new List<MappedBoundaryPair>();
        for (var pairIndex = 0; pairIndex < sourceOwnedBoundaryPairs.Count; pairIndex++)
        {
            var pair = sourceOwnedBoundaryPairs[pairIndex];
            if (!TryMapSourceVisibleRange(
                    sourceVisible,
                    visible,
                    pair.Opening.Index,
                    pair.Closing.Index,
                    out var translatedStart,
                    out var translatedEnd))
            {
                continue;
            }

            if (HasSameTranslatedBoundaryPair(
                    translatedBoundaryPairs,
                    translatedStart,
                    translatedEnd,
                    pair.Opening.Token,
                    pair.Closing.Token))
            {
                continue;
            }

            mappedPairs.Add(new MappedBoundaryPair(
                translatedStart,
                translatedEnd,
                pair.Opening.Token,
                pair.OpeningOrder,
                pair.Closing.Token,
                pair.ClosingOrder));
        }

        if (mappedPairs.Count == 0)
        {
            return translatedValue;
        }

        var sourceOpenings = new List<ColorSpan>(mappedPairs.Count);
        var openingOrderedPairs = new List<MappedBoundaryPair>(mappedPairs);
        openingOrderedPairs.Sort(static (left, right) => left.OpeningOrder.CompareTo(right.OpeningOrder));
        for (var index = 0; index < openingOrderedPairs.Count; index++)
        {
            var pair = openingOrderedPairs[index];
            sourceOpenings.Add(new ColorSpan(pair.Start, pair.OpeningToken));
        }

        var sourceClosings = new List<ColorSpan>(mappedPairs.Count);
        var closingOrderedPairs = new List<MappedBoundaryPair>(mappedPairs);
        closingOrderedPairs.Sort(static (left, right) => left.ClosingOrder.CompareTo(right.ClosingOrder));
        for (var index = 0; index < closingOrderedPairs.Count; index++)
        {
            var pair = closingOrderedPairs[index];
            sourceClosings.Add(new ColorSpan(pair.End, pair.ClosingToken));
        }

        var mergedSpans = new List<ColorSpan>(sourceOpenings.Count + translatedOwnedSpans.Count + sourceClosings.Count);
        mergedSpans.AddRange(sourceOpenings);
        mergedSpans.AddRange(translatedOwnedSpans);
        mergedSpans.AddRange(sourceClosings);
        return Restore(visible, NormalizeSpansForRestoreOrder(mergedSpans));
    }

    private static List<WholeBoundaryPair> ExtractTrueBoundaryPairs(
        IReadOnlyList<ColorSpan> spans,
        int sourceStart,
        int sourceLength)
    {
        var pairs = new List<WholeBoundaryPair>();
        if (sourceStart < 0 || sourceLength <= 0)
        {
            return pairs;
        }

        var sourceEnd = sourceStart + sourceLength;
        var stack = new List<(ColorSpan Span, int Order)>();

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index < sourceStart || span.Index > sourceEnd)
            {
                continue;
            }

            if (ColorCodePreserver.IsClosingBoundaryToken(span.Token) && stack.Count > 0)
            {
                var opening = stack[stack.Count - 1];
                if (BoundaryTokensMatch(opening.Span.Token, span.Token))
                {
                    stack.RemoveAt(stack.Count - 1);
                    if (opening.Span.Index < span.Index)
                    {
                        pairs.Add(new WholeBoundaryPair(opening.Span, opening.Order, span, index));
                    }

                    continue;
                }
            }

            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                stack.Add((span, index));
            }
        }

        pairs.Sort(static (left, right) => left.OpeningOrder.CompareTo(right.OpeningOrder));
        return pairs;
    }

    private static bool TryMapSourceVisibleRange(
        string sourceVisible,
        string translatedVisible,
        int sourceStart,
        int sourceEnd,
        out int translatedStart,
        out int translatedEnd)
    {
        translatedStart = 0;
        translatedEnd = 0;
        if (sourceStart < 0 || sourceEnd <= sourceStart || sourceEnd > sourceVisible.Length)
        {
            return false;
        }

        if (sourceStart == 0 && sourceEnd == sourceVisible.Length)
        {
            translatedEnd = translatedVisible.Length;
            return true;
        }

        var sourceSegment = sourceVisible.Substring(sourceStart, sourceEnd - sourceStart);
        if (TryFindUniqueOccurrence(translatedVisible, sourceSegment, out var exactIndex))
        {
            translatedStart = exactIndex;
            translatedEnd = exactIndex + sourceSegment.Length;
            return true;
        }

        var sourcePrefix = sourceVisible.Substring(0, sourceStart);
        var sourceSuffix = sourceVisible.Substring(sourceEnd);
        var prefixMatches = sourcePrefix.Length == 0
            || translatedVisible.StartsWith(sourcePrefix, StringComparison.Ordinal);
        var suffixMatches = sourceSuffix.Length == 0
            || translatedVisible.EndsWith(sourceSuffix, StringComparison.Ordinal);

        if (sourceStart == 0 && sourceSuffix.Length > 0 && suffixMatches)
        {
            translatedEnd = translatedVisible.Length - sourceSuffix.Length;
            return translatedStart <= translatedEnd;
        }

        if (sourceEnd == sourceVisible.Length && sourcePrefix.Length > 0 && prefixMatches)
        {
            translatedStart = sourcePrefix.Length;
            translatedEnd = translatedVisible.Length;
            return translatedStart <= translatedEnd;
        }

        if (sourcePrefix.Length > 0
            && sourceSuffix.Length > 0
            && prefixMatches
            && suffixMatches)
        {
            translatedStart = sourcePrefix.Length;
            translatedEnd = translatedVisible.Length - sourceSuffix.Length;
            return translatedStart <= translatedEnd;
        }

        return false;
    }

    private static bool TryFindUniqueOccurrence(string value, string segment, out int occurrenceIndex)
    {
        occurrenceIndex = -1;
        if (segment.Length == 0)
        {
            return false;
        }

        var searchStart = 0;
        while (searchStart <= value.Length - segment.Length)
        {
            var index = value.IndexOf(segment, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            if (occurrenceIndex >= 0)
            {
                occurrenceIndex = -1;
                return false;
            }

            occurrenceIndex = index;
            searchStart = index + 1;
        }

        return occurrenceIndex >= 0;
    }

    private static bool HasSameTranslatedBoundaryPair(
        IReadOnlyList<WholeBoundaryPair> translatedBoundaryPairs,
        int translatedStart,
        int translatedEnd,
        string openingToken,
        string closingToken)
    {
        for (var pairIndex = 0; pairIndex < translatedBoundaryPairs.Count; pairIndex++)
        {
            var pair = translatedBoundaryPairs[pairIndex];
            if (pair.Opening.Index == translatedStart
                && pair.Closing.Index == translatedEnd
                && string.Equals(pair.Opening.Token, openingToken, StringComparison.Ordinal)
                && string.Equals(pair.Closing.Token, closingToken, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<ColorSpan> NormalizeSpansForRestoreOrder(IReadOnlyList<ColorSpan> spans)
    {
        var orderedSpans = new List<OrderedColorSpan>(spans.Count);
        for (var index = 0; index < spans.Count; index++)
        {
            orderedSpans.Add(new OrderedColorSpan(spans[index], index));
        }

        orderedSpans.Sort(static (left, right) =>
        {
            var indexComparison = left.Span.Index.CompareTo(right.Span.Index);
            if (indexComparison != 0)
            {
                return indexComparison;
            }

            var rankComparison = GetRestoreBoundaryRank(left.Span.Token).CompareTo(
                GetRestoreBoundaryRank(right.Span.Token));
            if (rankComparison != 0)
            {
                return rankComparison;
            }

            return left.Order.CompareTo(right.Order);
        });

        var normalized = new List<ColorSpan>(orderedSpans.Count);
        for (var index = 0; index < orderedSpans.Count; index++)
        {
            normalized.Add(orderedSpans[index].Span);
        }

        return normalized;
    }

    private static int GetRestoreBoundaryRank(string token)
    {
        var isOpening = ColorCodePreserver.IsOpeningBoundaryToken(token);
        var isClosing = ColorCodePreserver.IsClosingBoundaryToken(token);
        if (isClosing && !isOpening)
        {
            return 0;
        }

        if (isOpening && !isClosing)
        {
            return 1;
        }

        return 2;
    }

    private static List<WholeBoundaryPair> ExtractTrueWholeBoundaryPairs(
        IReadOnlyList<ColorSpan> spans,
        int sourceStart,
        int sourceLength)
    {
        var pairs = new List<WholeBoundaryPair>();
        if (sourceStart < 0 || sourceLength <= 0)
        {
            return pairs;
        }

        var sourceEnd = sourceStart + sourceLength;
        var stack = new List<(ColorSpan Span, int Order)>();

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index < sourceStart || span.Index > sourceEnd)
            {
                continue;
            }

            if (ColorCodePreserver.IsClosingBoundaryToken(span.Token) && stack.Count > 0)
            {
                var opening = stack[stack.Count - 1];
                if (BoundaryTokensMatch(opening.Span.Token, span.Token))
                {
                    stack.RemoveAt(stack.Count - 1);
                    if (opening.Span.Index == sourceStart && span.Index == sourceEnd)
                    {
                        pairs.Add(new WholeBoundaryPair(opening.Span, opening.Order, span, index));
                    }

                    continue;
                }
            }

            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                stack.Add((span, index));
            }
        }

        pairs.Sort(static (left, right) => left.OpeningOrder.CompareTo(right.OpeningOrder));
        return pairs;
    }

    private static bool BoundaryTokensMatch(string openingToken, string closingToken)
    {
        if (openingToken.StartsWith("{{", StringComparison.Ordinal)
            && openingToken.EndsWith("|", StringComparison.Ordinal))
        {
            return string.Equals(closingToken, "}}", StringComparison.Ordinal);
        }

        if (openingToken.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(closingToken, "</color>", StringComparison.OrdinalIgnoreCase);
        }

        if (openingToken.Length == 2
            && closingToken.Length == 2
            && (openingToken[0] == '&' || openingToken[0] == '^'))
        {
            return openingToken[0] == closingToken[0];
        }

        return false;
    }

    private static string RestoreWholeBoundaryPairsPreservingTranslatedOwnership(
        string translatedValue,
        IReadOnlyList<WholeBoundaryPair> wholeBoundaryPairs)
    {
        if (wholeBoundaryPairs.Count == 0)
        {
            return translatedValue;
        }

        var sourceOwnedBoundaryPairs = SelectSourceOwnedBoundaryPairs(wholeBoundaryPairs);
        if (sourceOwnedBoundaryPairs.Count == 0)
        {
            return translatedValue;
        }

        var (visible, translatedOwnedSpans) = Strip(translatedValue);
        var mergedBoundaryPairs = MergeTranslatedOwnedWholeBoundaryPairs(
            sourceOwnedBoundaryPairs,
            translatedOwnedSpans,
            visible.Length);
        sourceOwnedBoundaryPairs = mergedBoundaryPairs.BoundaryPairs;
        translatedOwnedSpans = mergedBoundaryPairs.InnerTranslatedOwnedSpans;
        if (sourceOwnedBoundaryPairs.Count == 0)
        {
            return translatedValue;
        }

        var preservedSourceWrappers = ProjectWholeBoundaryPairsAbsolute(sourceOwnedBoundaryPairs, visible.Length);
        if (preservedSourceWrappers.Count == 0)
        {
            return translatedValue;
        }

        for (var index = 0; index < preservedSourceWrappers.Count; index++)
        {
            var span = preservedSourceWrappers[index];
            if (span.Index == 0 && ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                translatedOwnedSpans.Insert(index, span);
            }
        }

        for (var index = 0; index < preservedSourceWrappers.Count; index++)
        {
            var span = preservedSourceWrappers[index];
            if (span.Index == visible.Length && ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                translatedOwnedSpans.Add(span);
            }
        }

        return ColorAwareTranslationComposer.Restore(visible, translatedOwnedSpans);
    }

    private static (List<WholeBoundaryPair> BoundaryPairs, List<ColorSpan> InnerTranslatedOwnedSpans)
        MergeTranslatedOwnedWholeBoundaryPairs(
        IReadOnlyList<WholeBoundaryPair> sourceOwnedBoundaryPairs,
        List<ColorSpan> translatedOwnedSpans,
        int translatedVisibleLength)
    {
        var translatedWholeBoundaryPairs = ExtractTrueWholeBoundaryPairs(
            translatedOwnedSpans,
            sourceStart: 0,
            translatedVisibleLength);
        if (translatedWholeBoundaryPairs.Count == 0)
        {
            return (
                FilterSourcePairsConflictingWithLeadingTranslatedMarkup(sourceOwnedBoundaryPairs, translatedOwnedSpans),
                translatedOwnedSpans);
        }

        var boundaryPairs = new List<WholeBoundaryPair>(sourceOwnedBoundaryPairs.Count);
        var consumedTranslatedSpans = new HashSet<ColorSpan>();
        for (var sourceIndex = 0; sourceIndex < sourceOwnedBoundaryPairs.Count; sourceIndex++)
        {
            var sourcePair = sourceOwnedBoundaryPairs[sourceIndex];
            var translatedPair = FindUnconsumedSameWholeBoundaryPair(
                translatedWholeBoundaryPairs,
                sourcePair,
                consumedTranslatedSpans);
            if (translatedPair is null)
            {
                if (HasTranslatedOwnedSameOpeningAtStart(
                        translatedOwnedSpans,
                        sourcePair.Opening.Token,
                        consumedTranslatedSpans))
                {
                    continue;
                }

                boundaryPairs.Add(sourcePair);
                continue;
            }

            boundaryPairs.Add(new WholeBoundaryPair(
                translatedPair.Opening,
                sourcePair.OpeningOrder,
                translatedPair.Closing,
                sourcePair.ClosingOrder));
            consumedTranslatedSpans.Add(translatedPair.Opening);
            consumedTranslatedSpans.Add(translatedPair.Closing);
        }

        if (consumedTranslatedSpans.Count == 0)
        {
            return (boundaryPairs, translatedOwnedSpans);
        }

        var innerTranslatedOwnedSpans = new List<ColorSpan>(translatedOwnedSpans.Count - consumedTranslatedSpans.Count);
        for (var spanIndex = 0; spanIndex < translatedOwnedSpans.Count; spanIndex++)
        {
            var span = translatedOwnedSpans[spanIndex];
            if (!consumedTranslatedSpans.Contains(span))
            {
                innerTranslatedOwnedSpans.Add(span);
            }
        }

        return (boundaryPairs, innerTranslatedOwnedSpans);
    }

    private static List<WholeBoundaryPair> FilterSourcePairsConflictingWithLeadingTranslatedMarkup(
        IReadOnlyList<WholeBoundaryPair> sourceOwnedBoundaryPairs,
        IReadOnlyList<ColorSpan> translatedOwnedSpans)
    {
        var boundaryPairs = new List<WholeBoundaryPair>(sourceOwnedBoundaryPairs.Count);
        var consumedTranslatedSpans = new HashSet<ColorSpan>();
        for (var sourceIndex = 0; sourceIndex < sourceOwnedBoundaryPairs.Count; sourceIndex++)
        {
            var sourcePair = sourceOwnedBoundaryPairs[sourceIndex];
            if (!HasTranslatedOwnedSameOpeningAtStart(
                    translatedOwnedSpans,
                    sourcePair.Opening.Token,
                    consumedTranslatedSpans))
            {
                boundaryPairs.Add(sourcePair);
            }
        }

        return boundaryPairs;
    }

    private static bool HasTranslatedOwnedSameOpeningAtStart(
        IReadOnlyList<ColorSpan> translatedOwnedSpans,
        string sourceOpeningToken,
        ISet<ColorSpan> consumedTranslatedSpans)
    {
        for (var index = 0; index < translatedOwnedSpans.Count; index++)
        {
            var span = translatedOwnedSpans[index];
            if (!consumedTranslatedSpans.Contains(span)
                && span.Index == 0
                && ColorCodePreserver.IsOpeningBoundaryToken(span.Token)
                && string.Equals(span.Token, sourceOpeningToken, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static WholeBoundaryPair? FindUnconsumedSameWholeBoundaryPair(
        IReadOnlyList<WholeBoundaryPair> translatedWholeBoundaryPairs,
        WholeBoundaryPair sourcePair,
        ISet<ColorSpan> consumedTranslatedSpans)
    {
        for (var translatedIndex = 0; translatedIndex < translatedWholeBoundaryPairs.Count; translatedIndex++)
        {
            var translatedPair = translatedWholeBoundaryPairs[translatedIndex];
            if (!consumedTranslatedSpans.Contains(translatedPair.Opening)
                && !consumedTranslatedSpans.Contains(translatedPair.Closing)
                && string.Equals(translatedPair.Opening.Token, sourcePair.Opening.Token, StringComparison.Ordinal)
                && string.Equals(translatedPair.Closing.Token, sourcePair.Closing.Token, StringComparison.Ordinal))
            {
                return translatedPair;
            }
        }

        return null;
    }

    private static List<WholeBoundaryPair> SelectSourceOwnedBoundaryPairs(
        IReadOnlyList<WholeBoundaryPair> pairs,
        bool suppressNestedSameFamilyWrappers = true)
    {
        var result = new List<WholeBoundaryPair>();
        for (var candidateIndex = 0; candidateIndex < pairs.Count; candidateIndex++)
        {
            var candidate = pairs[candidateIndex];
            var isNestedInsideSameFamilyPair = false;
            for (var otherIndex = 0; otherIndex < pairs.Count; otherIndex++)
            {
                if (otherIndex == candidateIndex)
                {
                    continue;
                }

                var other = pairs[otherIndex];
                if (other.OpeningOrder < candidate.OpeningOrder
                    && other.ClosingOrder > candidate.ClosingOrder
                    && IsConflictingNestedWrapper(
                        candidate.Opening.Token,
                        other.Opening.Token,
                        suppressNestedSameFamilyWrappers))
                {
                    isNestedInsideSameFamilyPair = true;
                    break;
                }
            }

            if (!isNestedInsideSameFamilyPair)
            {
                result.Add(candidate);
            }
        }

        result.Sort(static (left, right) => left.OpeningOrder.CompareTo(right.OpeningOrder));
        return result;
    }

    private static bool IsSameWrapperFamily(string leftOpeningToken, string rightOpeningToken)
    {
        return string.Equals(
            GetWrapperFamily(leftOpeningToken),
            GetWrapperFamily(rightOpeningToken),
            StringComparison.Ordinal);
    }

    private static bool IsConflictingNestedWrapper(
        string leftOpeningToken,
        string rightOpeningToken,
        bool suppressNestedSameFamilyWrappers)
    {
        return suppressNestedSameFamilyWrappers
            ? IsSameWrapperFamily(leftOpeningToken, rightOpeningToken)
            : string.Equals(leftOpeningToken, rightOpeningToken, StringComparison.Ordinal);
    }

    private static string GetWrapperFamily(string openingToken)
    {
        if (openingToken.StartsWith("{{", StringComparison.Ordinal)
            && openingToken.EndsWith("|", StringComparison.Ordinal))
        {
            return "qud";
        }

        if (openingToken.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
        {
            return "tmp-color";
        }

        if (openingToken.Length == 2 && openingToken[0] == '&')
        {
            return "foreground";
        }

        if (openingToken.Length == 2 && openingToken[0] == '^')
        {
            return "background";
        }

        return openingToken;
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
        var openingOrderedPairs = new List<WholeBoundaryPair>(pairs);
        openingOrderedPairs.Sort(static (left, right) => left.OpeningOrder.CompareTo(right.OpeningOrder));
        for (var index = 0; index < pairs.Count; index++)
        {
            projected.Add(new ColorSpan(0, openingOrderedPairs[index].Opening.Token));
        }

        var closingOrderedPairs = new List<WholeBoundaryPair>(pairs);
        closingOrderedPairs.Sort(static (left, right) => left.ClosingOrder.CompareTo(right.ClosingOrder));
        for (var index = 0; index < pairs.Count; index++)
        {
            projected.Add(new ColorSpan(translatedLength, closingOrderedPairs[index].Closing.Token));
        }

        return projected;
    }

    internal static List<ColorSpan> ProjectWholeBoundaryPairsRelative(
        IReadOnlyList<WholeBoundaryPair> pairs,
        int sourceLength)
    {
        var projected = new List<ColorSpan>(pairs.Count * 2);
        var openingOrderedPairs = new List<WholeBoundaryPair>(pairs);
        openingOrderedPairs.Sort(static (left, right) => left.OpeningOrder.CompareTo(right.OpeningOrder));
        for (var index = 0; index < pairs.Count; index++)
        {
            projected.Add(new ColorSpan(
                0,
                openingOrderedPairs[index].Opening.Token,
                sourceLength,
                usesRelativeIndex: true));
        }

        var closingOrderedPairs = new List<WholeBoundaryPair>(pairs);
        closingOrderedPairs.Sort(static (left, right) => left.ClosingOrder.CompareTo(right.ClosingOrder));
        for (var index = 0; index < pairs.Count; index++)
        {
            projected.Add(new ColorSpan(
                sourceLength,
                closingOrderedPairs[index].Closing.Token,
                sourceLength,
                usesRelativeIndex: true));
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

    private readonly struct MappedBoundaryPair
    {
        internal MappedBoundaryPair(
            int start,
            int end,
            string openingToken,
            int openingOrder,
            string closingToken,
            int closingOrder)
        {
            Start = start;
            End = end;
            OpeningToken = openingToken;
            OpeningOrder = openingOrder;
            ClosingToken = closingToken;
            ClosingOrder = closingOrder;
        }

        internal int Start { get; }

        internal int End { get; }

        internal string OpeningToken { get; }

        internal int OpeningOrder { get; }

        internal string ClosingToken { get; }

        internal int ClosingOrder { get; }
    }

    private readonly struct OrderedColorSpan
    {
        internal OrderedColorSpan(ColorSpan span, int order)
        {
            Span = span;
            Order = order;
        }

        internal ColorSpan Span { get; }

        internal int Order { get; }
    }
}
