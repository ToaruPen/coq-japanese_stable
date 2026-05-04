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

    private static readonly Regex BrainDispositionLinePattern =
        new Regex("^(?<label>Base demeanor:|Engagement style:) (?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex VillageDispositionTargetPattern =
        new Regex("^(?:the|The) villagers of (?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StatAbbreviationPattern =
        new Regex("^[A-Z]{2,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SignedStatAbbreviationPattern =
        new Regex("^[+-]\\d+\\s+[A-Z]{2,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AsciiLetterPattern =
        new Regex("[A-Za-z]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PreservedWeightUnitPattern =
        new Regex("(?:\\.lbs|lbs\\.)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedLocalizedEnglishTokenPattern =
        new Regex("(?<![A-Za-z])(?:AV|DV|HP|MA|PV|Qud|Quickness|SP|XP)(?![A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AddsCookingEffectsPattern =
        new Regex("^Adds (?<effect>.+?) effects to cooked meals\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

        if (TryTranslateSegmentPreservingColors(
            source,
            route,
            allowMessagePatternTranslation: source.IndexOf('\n') < 0,
            out var wholeTranslated))
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
        string? activeBoundaryToken = null;
        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryTranslatePossiblySplitColorLine(lines[index], route, ref activeBoundaryToken, out var translatedLine))
            {
                continue;
            }

            lines[index] = translatedLine;
            changed = true;
        }

        return changed ? string.Join(newline, lines) : source;
    }

    private static bool TryTranslatePossiblySplitColorLine(
        string source,
        string route,
        ref string? activeBoundaryToken,
        out string translated)
    {
        var syntheticPrefix = string.Empty;
        var syntheticSuffix = string.Empty;
        var lineClosesActiveBoundary = false;
        var danglingOpenToken = string.Empty;

        if (!string.IsNullOrEmpty(activeBoundaryToken))
        {
            syntheticPrefix = activeBoundaryToken!;
            if (HasColorBoundaryClosing(source, activeBoundaryToken!))
            {
                lineClosesActiveBoundary = true;
            }
        }

        if (HasColorBoundaryOpening(source) && TryFindDanglingBoundaryOpening(source, out danglingOpenToken))
        {
            syntheticSuffix = GetSyntheticClosingToken(danglingOpenToken);
        }

        if (!string.IsNullOrEmpty(activeBoundaryToken) && !lineClosesActiveBoundary)
        {
            syntheticSuffix += GetSyntheticClosingToken(activeBoundaryToken!);
        }

        var sourceForTranslation = syntheticPrefix + source + syntheticSuffix;
        if (!TryTranslateSegmentPreservingColors(
            sourceForTranslation,
            route,
            allowMessagePatternTranslation: true,
            out var translatedWithSyntheticBoundaries))
        {
            if (lineClosesActiveBoundary)
            {
                activeBoundaryToken = null;
            }
            else if (!string.IsNullOrEmpty(danglingOpenToken))
            {
                activeBoundaryToken = danglingOpenToken;
            }

            translated = source;
            return false;
        }

        if (syntheticPrefix.Length > 0
            && translatedWithSyntheticBoundaries.StartsWith(syntheticPrefix, StringComparison.Ordinal))
        {
            translatedWithSyntheticBoundaries = translatedWithSyntheticBoundaries.Substring(syntheticPrefix.Length);
        }

        if (syntheticSuffix.Length > 0
            && translatedWithSyntheticBoundaries.EndsWith(syntheticSuffix, StringComparison.Ordinal))
        {
            translatedWithSyntheticBoundaries = translatedWithSyntheticBoundaries.Substring(
                0,
                translatedWithSyntheticBoundaries.Length - syntheticSuffix.Length);
        }

        if (lineClosesActiveBoundary)
        {
            activeBoundaryToken = null;
        }
        else if (!string.IsNullOrEmpty(danglingOpenToken))
        {
            activeBoundaryToken = danglingOpenToken;
        }

        translated = translatedWithSyntheticBoundaries;
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool TryFindDanglingBoundaryOpening(string source, out string token)
    {
        token = string.Empty;
        var (_, spans) = ColorAwareTranslationComposer.Strip(source);
        var stack = new Stack<string>();
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (IsSelfContainedBoundaryToken(span.Token))
            {
                continue;
            }

            if (ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }

                continue;
            }

            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                stack.Push(span.Token);
            }
        }

        if (stack.Count > 0)
        {
            token = stack.Peek();
            return true;
        }

        var openingIndex = source.LastIndexOf("{{", StringComparison.Ordinal);
        if (openingIndex < 0)
        {
            return false;
        }

        var pipeIndex = source.IndexOf('|', openingIndex);
        if (pipeIndex < 0)
        {
            return false;
        }

        var closingIndex = source.IndexOf("}}", pipeIndex + 1, StringComparison.Ordinal);
        if (closingIndex >= 0)
        {
            return false;
        }

        token = source.Substring(openingIndex, (pipeIndex - openingIndex) + 1);
        return true;
    }

    private static bool HasColorBoundaryOpening(string source)
    {
        var (_, spans) = ColorAwareTranslationComposer.Strip(source);
        return spans.Any(static span => ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            || TryFindDanglingBoundaryOpening(source, out _);
    }

    private static bool HasColorBoundaryClosing(string source, string openingToken)
    {
        if (openingToken.StartsWith("{{", StringComparison.Ordinal))
        {
            var depth = 1;
            for (var index = 0; index + 1 < source.Length; index++)
            {
                if (source[index] == '{' && source[index + 1] == '{')
                {
                    depth++;
                    index++;
                    continue;
                }

                if (source[index] == '}' && source[index + 1] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return true;
                    }

                    index++;
                }
            }

            return false;
        }

        var closingToken = GetSyntheticClosingToken(openingToken);
        var (_, spans) = ColorAwareTranslationComposer.Strip(source);
        var spanDepth = 1;
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (IsSelfContainedBoundaryToken(span.Token))
            {
                continue;
            }

            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                spanDepth++;
                continue;
            }

            if (string.Equals(span.Token, closingToken, StringComparison.OrdinalIgnoreCase))
            {
                spanDepth--;
                if (spanDepth == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetSyntheticClosingToken(string openingToken)
    {
        if (openingToken.StartsWith("{{", StringComparison.Ordinal))
        {
            return "}}";
        }

        if (openingToken.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
        {
            return "</color>";
        }

        return openingToken;
    }

    private static bool IsSelfContainedBoundaryToken(string token)
    {
        return token.Length == 2 && (token[0] == '&' || token[0] == '^');
    }

    private static bool TryTranslateSegmentPreservingColors(
        string source,
        string route,
        bool allowMessagePatternTranslation,
        out string translated)
    {
        if (TryTranslateBrainDispositionLinePreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateFactionDispositionLinePreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateSultanShrineWrapperPreservingColors(source, route, out translated))
        {
            return true;
        }

        translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TryTranslateVisibleSegment(visible, route, allowMessagePatternTranslation, out var candidate)
                ? candidate
                : visible);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateSultanShrineWrapperPreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (SultanShrineWrapperTranslator.TryTranslateMessage(stripped, spans, route, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateVisibleSegment(
        string source,
        string route,
        bool allowMessagePatternTranslation,
        out string translated)
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

        if (TryTranslateAddsCookingEffectsLine(source, route, out translated))
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

        if (!allowMessagePatternTranslation || ShouldSkipMessagePatternTranslation(source))
        {
            translated = source;
            return false;
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

    private static bool TryTranslateAddsCookingEffectsLine(string source, string route, out string translated)
    {
        var match = AddsCookingEffectsPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var effect = match.Groups["effect"].Value;
        var translatedEffect = StringHelpers.TranslateExactOrLowerAsciiFallback(effect);
        if (string.Equals(translatedEffect, effect, StringComparison.Ordinal)
            && !ContainsJapaneseCharacters(effect))
        {
            translated = source;
            return false;
        }

        translated = translatedEffect + "の効果を調理した食事に加える。";
        DynamicTextObservability.RecordTransform(route, "Description.CookingEffects", source, translated);
        return true;
    }

    private static bool TryTranslateBrainDispositionLinePreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = BrainDispositionLinePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var label = match.Groups["label"].Value switch
        {
            "Base demeanor:" => "基本態度:",
            "Engagement style:" => "交戦スタイル:",
            _ => string.Empty,
        };
        var rawValue = match.Groups["value"].Value;
        var value = rawValue switch
        {
            "aggressive" => "攻撃的",
            "defensive" => "防御的",
            "docile" => "温和",
            _ => rawValue,
        };
        if (string.IsNullOrEmpty(label))
        {
            translated = source;
            return false;
        }

        label = RestoreBalancedCapture(label, spans, match.Groups["label"]);
        value = RestoreBalancedCapture(value, spans, match.Groups["value"]);
        translated = label + " " + value;
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.BrainDispositionLine", source, translated);
        return true;
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
        var targetGroup = match.Groups["target"];
        var isVillageDispositionTarget = VillageDispositionTargetPattern.IsMatch(targetGroup.Value);
        string target;
        if (!TryTranslateVillageDispositionTarget(targetGroup, spans, out target))
        {
            if (isVillageDispositionTarget)
            {
                target = RestoreBalancedCapture(targetGroup.Value, spans, targetGroup);
            }
            else
            {
                var strippedTarget = StringHelpers.StripLeadingEnglishArticle(
                    targetGroup.Value,
                    includeCapitalizedDefiniteArticle: true);
                target = TranslateDispositionTarget(targetGroup.Value);

                if (!string.Equals(strippedTarget, targetGroup.Value, StringComparison.Ordinal) && spans is not null && spans.Count > 0)
                {
                    var articleLength = targetGroup.Value.Length - strippedTarget.Length;
                    var strippedStart = targetGroup.Index + articleLength;
                    var hasWrapperCrossingStrippedStart = false;
                    var targetEnd = targetGroup.Index + targetGroup.Length;
                    var openingStack = new Stack<int>();
                    for (var index = 0; index < spans.Count; index++)
                    {
                        var span = spans[index];
                        if (span.Index < targetGroup.Index || span.Index > targetEnd)
                        {
                            continue;
                        }

                        if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
                        {
                            openingStack.Push(span.Index);
                            continue;
                        }

                        if (!ColorCodePreserver.IsClosingBoundaryToken(span.Token) || openingStack.Count == 0)
                        {
                            continue;
                        }

                        var openingIndex = openingStack.Pop();
                        if (openingIndex < strippedStart && span.Index > strippedStart)
                        {
                            hasWrapperCrossingStrippedStart = true;
                            break;
                        }
                    }

                    target = hasWrapperCrossingStrippedStart
                        ? RestoreBalancedCapture(target, spans, targetGroup)
                        : RestoreCaptureAtOffset(target, spans, strippedStart, strippedTarget.Length);
                }
                else
                {
                    target = RestoreBalancedCapture(target, spans, targetGroup);
                }
            }
        }
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

        var wholeLinePairs = ColorAwareTranslationComposer.SliceWholeBoundaryPairs(spans, sourceStart: 0, sourceLength);
        var wholeLineSpans = ColorAwareTranslationComposer.ProjectWholeBoundaryPairsAbsolute(wholeLinePairs, translated.Length);
        return wholeLineSpans.Count == 0
            ? translated
            : ColorAwareTranslationComposer.Restore(translated, wholeLineSpans);
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

        if (ShouldSkipMessagePatternTranslation(source))
        {
            return source;
        }

        translated = MessagePatternTranslator.Translate(source, route);
        return translated;
    }

    private static string TranslateDispositionTarget(string source)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            return translated;
        }

        var strippedArticle = StringHelpers.StripLeadingEnglishArticle(source, includeCapitalizedDefiniteArticle: true);
        if (string.Equals(strippedArticle, source, StringComparison.Ordinal))
        {
            return source;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(strippedArticle, out translated)
            && !string.Equals(strippedArticle, translated, StringComparison.Ordinal))
        {
            return translated;
        }

        return ContainsJapaneseCharacters(strippedArticle)
            ? strippedArticle
            : source;
    }

    private static bool TryTranslateVillageDispositionTarget(Group targetGroup, IReadOnlyList<ColorSpan>? spans, out string translated)
    {
        var match = VillageDispositionTargetPattern.Match(targetGroup.Value);
        if (!match.Success)
        {
            translated = targetGroup.Value;
            return false;
        }

        var translatedTemplate = Translator.Translate("The villagers of {0}");
        if (string.Equals(translatedTemplate, "The villagers of {0}", StringComparison.Ordinal))
        {
            translated = targetGroup.Value;
            return false;
        }

        var translatedName = RestoreCaptureAtOffset(
            match.Groups["name"].Value,
            spans,
            targetGroup.Index + match.Groups["name"].Index,
            match.Groups["name"].Length);
        translated = translatedTemplate.Replace("{0}", translatedName);

        var targetSpans = spans is not null && spans.Count > 0
            ? ColorCodePreserver.SliceSpans(spans, targetGroup.Index, targetGroup.Length)
            : null;

        var targetBoundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(
            targetSpans,
            match,
            targetGroup.Length,
            translated.Length);
        if (targetSpans is not null && targetSpans.Count > 0)
        {
            var nameStart = match.Groups["name"].Index;
            var openingStack = new Stack<ColorSpan>();
            for (var index = 0; index < targetSpans.Count; index++)
            {
                var span = targetSpans[index];
                if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
                {
                    openingStack.Push(span);
                    continue;
                }

                if (!ColorCodePreserver.IsClosingBoundaryToken(span.Token) || span.Index != targetGroup.Length || openingStack.Count == 0)
                {
                    continue;
                }

                var opening = openingStack.Pop();
                if (opening.Index < nameStart)
                {
                    targetBoundarySpans.Add(new ColorSpan(translated.Length, span.Token));
                }
            }
        }

        translated = ColorAwareTranslationComposer.Restore(translated, targetBoundarySpans);
        return true;
    }

    private static string RestoreCaptureAtOffset(string value, IReadOnlyList<ColorSpan>? spans, int startIndex, int length)
    {
        if (spans is null || spans.Count == 0 || length < 0)
        {
            return value;
        }

        var captureSpans = ColorCodePreserver.SliceSpans(spans, startIndex, length);
        captureSpans.AddRange(ColorCodePreserver.SliceAdjacentCaptureBoundarySpans(spans, startIndex, length));
        captureSpans = FilterBalancedBoundarySpans(captureSpans);
        return captureSpans.Count == 0
            ? value
            : ColorAwareTranslationComposer.Restore(value, captureSpans);
    }

    private static bool ContainsJapaneseCharacters(string source)
    {
        return !string.IsNullOrEmpty(source) && JapaneseCharacterPattern.IsMatch(source);
    }

    private static bool ShouldSkipMessagePatternTranslation(string source)
    {
        if (!ContainsJapaneseCharacters(source))
        {
            return false;
        }

        var normalized = PreservedWeightUnitPattern.Replace(source, string.Empty);
        normalized = AllowedLocalizedEnglishTokenPattern.Replace(normalized, string.Empty);
        return !AsciiLetterPattern.IsMatch(normalized);
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
