using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DescriptionTextTranslator
{
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
        translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TryTranslateVisibleSegment(visible, route, out var candidate)
                ? candidate
                : visible);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateVisibleSegment(string source, string route, out string translated)
    {
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

    private static bool ShouldSkipExactLeafTranslation(string source)
    {
        return StatAbbreviationPattern.IsMatch(source)
            || SignedStatAbbreviationPattern.IsMatch(source);
    }
}
