using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class FriendOrFoeReasonTranslator
{
    private const string WorldPartsDictionaryFile = "world-parts.ja.json";

    private static readonly Regex InsultingTheirPattern = new(
        "^insulting their (?<noun>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PraisingTheirPattern = new(
        "^praising their (?<noun>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DestroyingNumbersPattern = new(
        "^destroying the (?<adjective>.+?) numbers$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DreamingDimensionPattern = new(
        "^dreaming (?<dimension>.+?) into being$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InventingConceptPattern = new(
        "^inventing the concept of (?<nouns>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SwappingPerceptionPattern = new(
        "^swapping how (?<first>.+?) and (?<second>.+?) are perceived$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WarpingPocketPattern = new(
        "^warping a pocket of spacetime into (?<object>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslate(string? source, out string translated)
    {
        var sourceValue = source ?? string.Empty;
        if (sourceValue.Length == 0)
        {
            translated = sourceValue;
            return false;
        }

        if (ScopedDictionaryLookup.TranslateExactOrLowerAscii(sourceValue, WorldPartsDictionaryFile) is { } exact)
        {
            translated = exact;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(sourceValue);
        if (TryTranslatePattern(
                stripped,
                spans,
                InsultingTheirPattern,
                match => TranslateCapture(match, "noun") + "を侮辱した",
                sourceValue,
                out translated)
            || TryTranslatePattern(
                stripped,
                spans,
                PraisingTheirPattern,
                match => TranslateCapture(match, "noun") + "を称賛した",
                sourceValue,
                out translated)
            || TryTranslateHeb(stripped, spans, sourceValue, out translated))
        {
            return true;
        }

        translated = sourceValue;
        return false;
    }

    private static bool TryTranslateHeb(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        if (string.Equals(stripped, "inventing the irrational numbers", StringComparison.Ordinal))
        {
            translated = RestoreWhole("無理数を発明した", stripped, spans, source);
            return true;
        }

        return TryTranslatePattern(
                stripped,
                spans,
                DestroyingNumbersPattern,
                match => TranslateCapture(match, "adjective") + "数を破壊した",
                source,
                out translated)
            || TryTranslatePattern(
                stripped,
                spans,
                DreamingDimensionPattern,
                match => TranslateCapture(match, "dimension") + "を夢見て存在させた",
                source,
                out translated)
            || TryTranslatePattern(
                stripped,
                spans,
                InventingConceptPattern,
                match => TranslateCapture(match, "nouns") + "という概念を発明した",
                source,
                out translated)
            || TryTranslatePattern(
                stripped,
                spans,
                SwappingPerceptionPattern,
                match => TranslateCapture(match, "first") + "と"
                    + TranslateCapture(match, "second") + "の知覚のされ方を入れ替えた",
                source,
                out translated)
            || TryTranslatePattern(
                stripped,
                spans,
                WarpingPocketPattern,
                match => TranslateCapture(match, "object") + "へと時空の小片を歪めた",
                source,
                out translated);
    }

    private static bool TryTranslatePattern(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Regex pattern,
        Func<Match, string> build,
        string source,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(build(match), stripped, spans, source);
        return true;
    }

    private static string TranslateCapture(Match match, string groupName)
    {
        var group = match.Groups[groupName];
        var capture = group.Value.Trim();
        if (HistorySpiceComponentLookup.TranslateExactOrLowerAscii(capture) is { } historySpice)
        {
            return historySpice;
        }

        if (StringHelpers.TranslateExactOrLowerAscii(capture) is { } exact)
        {
            return exact;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(capture, out var titlePhrase)
            ? titlePhrase
            : capture;
    }

    private static string RestoreWhole(
        string translated,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }
}
