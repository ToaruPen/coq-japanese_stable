using System;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class RelicGeneratedNameTranslator
{
    private const string ZoneDisplayDictionaryFile = "ui-zone-display.ja.json";

    private static readonly Regex OfThePattern = new(
        "^(?<item>[A-Z][A-Za-z' -]+?) of the (?<descriptor>[A-Z][A-Za-z' -]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RegionPattern = new(
        "^(?<item>[A-Z][A-Za-z' -]+?) of (?<region>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PossessivePattern = new(
        "^(?<owner>[A-Z][A-Za-z' -]+?)(?:'s|') (?<descriptor>[A-Z][A-Za-z' -]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HyphenPairPattern = new(
        "^(?<left>[A-Z][A-Za-z']+)-(?<right>[A-Z][A-Za-z']+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex DescriptorPattern = new(
        "^(?<descriptor>[A-Z][A-Za-z' -]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source!, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (!TryTranslateCore(stripped, out var translatedCore))
        {
            translated = original;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            original);
        return true;
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        if (source.StartsWith("The ", StringComparison.Ordinal)
            && TryTranslateCore(source.Substring("The ".Length), out translated))
        {
            return true;
        }

        var ofMatch = OfThePattern.Match(source);
        if (ofMatch.Success
            && TryTranslateTitlePhrase(ofMatch.Groups["item"].Value, out var item)
            && TryTranslateTitlePhrase(ofMatch.Groups["descriptor"].Value, out var descriptor))
        {
            translated = descriptor + "の" + item;
            return true;
        }

        var regionMatch = RegionPattern.Match(source);
        var regionItem = regionMatch.Groups["item"].Value;
        var region = regionMatch.Groups["region"].Value;
        if (regionMatch.Success
            && (!region.StartsWith("the ", StringComparison.Ordinal) || ContainsSpace(regionItem))
            && TryTranslateTitlePhrase(regionItem, out item))
        {
            translated = TranslateRegion(region) + "の" + item;
            return true;
        }

        var possessiveMatch = PossessivePattern.Match(source);
        if (possessiveMatch.Success
            && TryTranslateOwner(possessiveMatch.Groups["owner"].Value, out var owner)
            && TryTranslateTitlePhrase(possessiveMatch.Groups["descriptor"].Value, out descriptor))
        {
            translated = owner + "の" + descriptor;
            return true;
        }

        var hyphenMatch = HyphenPairPattern.Match(source);
        if (hyphenMatch.Success
            && TryTranslateTitlePhrase(hyphenMatch.Groups["left"].Value, out var left)
            && TryTranslateTitlePhrase(hyphenMatch.Groups["right"].Value, out var right))
        {
            translated = left + "・" + right;
            return true;
        }

        var descriptorMatch = DescriptorPattern.Match(source);
        if (descriptorMatch.Success
            && TryTranslateTitlePhrase(descriptorMatch.Groups["descriptor"].Value, out descriptor)
            && !string.Equals(descriptor, source, StringComparison.Ordinal))
        {
            translated = descriptor;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool ContainsSpace(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == ' ')
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTranslateOwner(string source, out string translated)
    {
        if (TryTranslateTitlePhrase(source, out translated))
        {
            return true;
        }

        translated = source;
        return true;
    }

    private static string TranslateRegion(string source)
    {
        var trimmed = source.Trim();
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(trimmed);
        if (scoped is not null)
        {
            return scoped;
        }

        var articleless = StringHelpers.StripLeadingEnglishArticle(trimmed);
        scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(articleless);
        if (scoped is not null)
        {
            return scoped;
        }

        scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(articleless, ZoneDisplayDictionaryFile);
        if (scoped is not null)
        {
            return scoped;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(articleless, out var titlePhrase)
            ? titlePhrase
            : articleless;
    }

    private static bool TryTranslateTitlePhrase(string source, out string translated)
    {
        var words = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            translated = source;
            return false;
        }

        var result = string.Empty;
        for (var index = 0; index < words.Length; index++)
        {
            if (!TryTranslateHyphenatedWord(words[index], out var word))
            {
                translated = source;
                return false;
            }

            result += word;
        }

        translated = result;
        return true;
    }

    private static bool TryTranslateHyphenatedWord(string source, out string translated)
    {
        var parts = source.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            translated = source;
            return false;
        }

        var result = string.Empty;
        for (var index = 0; index < parts.Length; index++)
        {
            if (!TryTranslateWord(parts[index], out var part))
            {
                translated = source;
                return false;
            }

            if (index > 0)
            {
                result += "・";
            }

            result += part;
        }

        translated = result;
        return true;
    }

    private static bool TryTranslateWord(string source, out string translated)
    {
        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var lower = StringHelpers.LowerAscii(source);
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(lower);
        if (scoped is not null)
        {
            translated = scoped;
            return true;
        }

        var direct = Translator.Translate(lower);
        if (!string.Equals(direct, lower, StringComparison.Ordinal))
        {
            translated = direct;
            return true;
        }

        translated = source;
        return false;
    }
}
