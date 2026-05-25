using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class WaterRitualTextTranslator
{
    private static readonly Regex PerformRitualPattern = new(
        "^You share your (?<liquid>.+?) with (?<speaker>.+?) and begin the water ritual\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReputationPattern = new(
        "^Your reputation with (?<faction>.+?) (?<direction>increased|decreased) by (?<amount>-?\\d+) to (?<value>-?\\d+)\\.(?<tail>[\\s\\S]*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BecauseReputationPattern = new(
        "^Because they (?<attitude>love|admire|regard|dislike|despise) (?<speaker>.+?), your reputation with (?<faction>.+?) (?<direction>increased|decreased) by (?<amount>-?\\d+) to (?<value>-?\\d+)\\.(?<tail>[\\s\\S]*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex VillageFactionPattern = new(
        "^(?:(?:the|The) )?villagers of (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StandingByPattern = new(
        "^You are now (?<standing>.+?) by (?<faction>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StandingToYouPattern = new(
        "^(?<faction>.+?) (?<verb>are|is) now (?<standing>.+?) to you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslateMessage(
        string source,
        string route,
        string family,
        out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateCore(source, out translated, out var detail))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + detail, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslateReputationMessage(
        string source,
        string route,
        string family,
        out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateReputationCore(source, out translated, out var detail))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + detail, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    internal static string TranslateLiquidVisible(string source)
    {
        var trimmed = StringHelpers.StripLeadingEnglishArticle(source.Trim(), includeCapitalizedDefiniteArticle: true);
        if (TryGetTranslationExactOrLowerAscii(trimmed, out var translated))
        {
            return translated;
        }

        return trimmed switch
        {
            "water" => "水",
            "fresh water" => "真水",
            "blood" => "血",
            "oil" => "油",
            _ => trimmed,
        };
    }

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        if (TryTranslatePattern(
                PerformRitualPattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "speaker")}と{RestoreTranslated(match, spans, "liquid", TranslateLiquidVisible)}を分かち合い、水の儀式を始めた。",
                out translated))
        {
            detail = "PerformRitual";
            return true;
        }

        if (TryTranslateReputationCore(source, out translated, out detail))
        {
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateReputationCore(string source, out string translated, out string detail)
    {
        if (TryTranslatePattern(
                BecauseReputationPattern,
                source,
                TranslateBecauseReputation,
                out translated))
        {
            detail = "ReputationBecause";
            return true;
        }

        if (TryTranslatePattern(
                ReputationPattern,
                source,
                TranslateReputation,
                out translated))
        {
            detail = "Reputation";
            return true;
        }

        if (TryTranslateStandingChange(source, out translated))
        {
            detail = "ReputationStanding";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string TranslateReputation(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return FormatReputationChange(
            RestoreTranslated(match, spans, "faction", TranslateFactionVisible),
            match.Groups["direction"].Value,
            Restore(match, spans, "amount"),
            Restore(match, spans, "value"))
            + TranslateTail(match, spans);
    }

    private static string TranslateBecauseReputation(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var speaker = Restore(match, spans, "speaker");
        var attitude = TranslateAttitude(match.Groups["attitude"].Value);
        return speaker + "を" + attitude + "ため、"
            + FormatReputationChange(
                RestoreTranslated(match, spans, "faction", TranslateFactionVisible),
                match.Groups["direction"].Value,
                Restore(match, spans, "amount"),
                Restore(match, spans, "value"))
            + TranslateTail(match, spans);
    }

    private static string FormatReputationChange(string faction, string direction, string amount, string value)
    {
        var directionText = string.Equals(direction, "increased", StringComparison.Ordinal) ? "増加" : "減少";
        return faction + "との評判が" + amount + directionText + "し、" + value + "になった。";
    }

    private static string TranslateAttitude(string source)
    {
        return source switch
        {
            "love" => "愛している",
            "admire" => "尊敬している",
            "regard" => "評価している",
            "dislike" => "よく思っていない",
            "despise" => "ひどく嫌っている",
            _ => source,
        };
    }

    private static string TranslateTail(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var tailGroup = match.Groups["tail"];
        if (!tailGroup.Success || tailGroup.Length == 0)
        {
            return string.Empty;
        }

        var tail = ColorAwareTranslationComposer.RestoreCapture(tailGroup.Value, spans, tailGroup);
        var separatorLength = GetLeadingSeparatorLength(tail);
        var separator = tail.Substring(0, separatorLength);
        var content = tail.Substring(separatorLength);

        return TryTranslateStandingChange(content, out var translated)
            ? separator + translated
            : tail;
    }

    private static int GetLeadingSeparatorLength(string source)
    {
        var index = 0;
        while (index < source.Length && char.IsWhiteSpace(source[index]))
        {
            index++;
        }

        return index;
    }

    private static bool TryTranslateStandingChange(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        var standingByMatch = StandingByPattern.Match(stripped);
        if (standingByMatch.Success)
        {
            translated =
                ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                    RestoreTranslated(standingByMatch, spans, "faction", TranslateFactionVisible)
                    + "から"
                    + RestoreTranslated(standingByMatch, spans, "standing", TranslateStandingVisible)
                    + "と見なされるようになった。",
                    spans,
                    stripped.Length,
                    source);
            return true;
        }

        var standingToYouMatch = StandingToYouPattern.Match(stripped);
        if (standingToYouMatch.Success)
        {
            translated =
                ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                    RestoreTranslated(standingToYouMatch, spans, "faction", TranslateFactionVisible)
                    + "はあなたを"
                    + RestoreTranslated(standingToYouMatch, spans, "standing", TranslateStandingVisible)
                    + "と見なすようになった。",
                    spans,
                    stripped.Length,
                    source);
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateStandingVisible(string source)
    {
        var trimmed = source.Trim();
        var articleStripped = StringHelpers.StripLeadingEnglishArticle(trimmed, includeCapitalizedDefiniteArticle: true);
        return articleStripped switch
        {
            "loved" => "崇拝されている",
            "favored" => "好意的",
            "indifferent" => "中立",
            "disliked" => "反感を持たれている",
            "despised" => "憎悪されている",
            _ => articleStripped,
        };
    }

    internal static string TranslateFactionVisible(string source)
    {
        var trimmed = source.Trim();
        if (TryGetTranslationExactOrLowerAscii(trimmed, out var translated))
        {
            return translated;
        }

        var villageMatch = VillageFactionPattern.Match(trimmed);
        if (villageMatch.Success)
        {
            return TranslateFactionVisible(villageMatch.Groups["name"].Value) + "の村人たち";
        }

        var articleStripped = StringHelpers.StripLeadingEnglishArticle(trimmed, includeCapitalizedDefiniteArticle: true);
        if (!string.Equals(articleStripped, trimmed, StringComparison.Ordinal)
            && TryGetTranslationExactOrLowerAscii(articleStripped, out translated))
        {
            return translated;
        }

        return articleStripped;
    }

    private static bool TryGetTranslationExactOrLowerAscii(string source, out string translated)
    {
        try
        {
            return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException || ex is FileNotFoundException)
        {
            translated = source;
            return false;
        }
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        translated = RestoreLeadingSourceAmpersandColor(source, translated);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return RestoreCaptureWithTrailingRuntimeColor(group.Value, spans, group).Trim();
    }

    private static string RestoreTranslated(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string groupName,
        Func<string, string> translate)
    {
        var group = match.Groups[groupName];
        return RestoreCaptureWithTrailingRuntimeColor(translate(group.Value), spans, group).Trim();
    }

    private static string RestoreCaptureWithTrailingRuntimeColor(
        string value,
        IReadOnlyList<ColorSpan> spans,
        Group group)
    {
        var restored = ColorAwareTranslationComposer.RestoreCapture(value, spans, group);
        var trailing = FindRuntimeColorTokenAt(spans, group.Index + group.Length);
        return trailing is null || restored.EndsWith(trailing, StringComparison.Ordinal)
            ? restored
            : restored + trailing;
    }

    private static string RestoreLeadingSourceAmpersandColor(string source, string translated)
    {
        if (source.Length < 2
            || source[0] != '&'
            || !IsRuntimeColorToken(source.Substring(0, 2))
            || translated.StartsWith(source.Substring(0, 2), StringComparison.Ordinal))
        {
            return translated;
        }

        return source.Substring(0, 2) + translated;
    }

    private static string? FindRuntimeColorTokenAt(IReadOnlyList<ColorSpan> spans, int index)
    {
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            if (span.Index == index && IsRuntimeColorToken(span.Token))
            {
                return span.Token;
            }
        }

        return null;
    }

    private static bool IsRuntimeColorToken(string token)
    {
        return token.Length == 2 && (token[0] == '&' || token[0] == '^');
    }
}
