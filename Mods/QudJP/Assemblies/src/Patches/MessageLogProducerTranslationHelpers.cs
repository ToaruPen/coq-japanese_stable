using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class MessageLogProducerTranslationHelpers
{
    private static readonly Dictionary<string, string> LairSuffixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["lair"] = "の巣",
        ["workshop"] = "の工房",
        ["scriptorium"] = "の写字室",
        ["kitchen"] = "の厨房",
        ["distillery"] = "の蒸留所",
        ["organ market"] = "の臓器市場",
        ["village lair"] = "の村落の巣",
        ["cradle"] = "の揺籃",
        ["chuppah"] = "のフッパー",
    };

    private static readonly string[] BiomeAdjectivesOrdered =
    {
        "psychically refractive",
        "rapidly resonating",
        "slime-drenched",
        "rust-shrouded",
        "telekinetic",
        "telepathic",
        "burgeoning",
        "illuminated",
        "retrocognate",
        "kindling",
        "blinking",
        "bubbly",
        "drowsy",
        "jittery",
        "pastless",
        "regressive",
        "slimy",
        "tarry",
        "warped",
        "naked",
        "blaring",
        "rusty",
    };

    private static readonly HashSet<string> FarmSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "farm",
        "farmstead",
        "homestead",
        "acre",
        "acreage",
        "tract",
        "orchard",
        "grove",
        "ranch",
        "pasture",
        "shire",
        "end",
        "hedge",
        "furrow",
        "hearth",
        "hold",
        "reach",
    };

    private static readonly HashSet<string> FixedBiomeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "slime patch",
        "slime field",
        "slime bog",
        "rust patch",
        "rust field",
        "rust bog",
        "fungus patch",
        "fungus grove",
        "fungus forest",
        "tar patch",
        "tar pools",
        "flaming tar pits",
    };

    private static readonly Regex GoatfolkSuffixPattern = new Regex(
        @"^goatfolk (?<kind>village|haunt)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LairPattern = new Regex(
        @"^the (?<kind>village lair|lair|workshop|scriptorium|kitchen|distillery|organ market|cradle|chuppah) of (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StrataPattern = new Regex(
        "^(?<count>\\d+) str(?:atum|ata) (?<direction>deep|high)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AsciiLetterPattern = new Regex(
        "[A-Za-z]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedAlreadyLocalizedEnglishTokenPattern = new Regex(
        @"\b(?:AV|DV|HP|MA|PV|Qud|Quickness|SP|XP)\b|\.lbs|\blbs\.",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslateZoneDisplayName(string source, string route, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var stripped))
        {
            translated = stripped;
            return !string.Equals(source, stripped, StringComparison.Ordinal);
        }

        translated = TranslateZoneDisplayNamePreservingColors(source);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "ZoneDisplayName", source, translated);
        return true;
    }

    private static string TranslateZoneDisplayNamePreservingColors(string source)
    {
        if (source.IndexOf(", ", StringComparison.Ordinal) < 0)
        {
            var translatedWholeSource = ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateVisibleZoneDisplayName);
            if (!string.Equals(translatedWholeSource, source, StringComparison.Ordinal))
            {
                return translatedWholeSource;
            }
        }

        var segments = source.Split(new[] { ", " }, StringSplitOptions.None);
        if (segments.Length == 1)
        {
            return source;
        }

        if (segments.Length >= 2
            && TryTranslateGoatfolkDisplayName(segments[0], segments[1], out var goatfolkTranslated))
        {
            if (segments.Length == 2)
            {
                return goatfolkTranslated;
            }

            var remainingSegments = new string[segments.Length - 1];
            remainingSegments[0] = goatfolkTranslated;
            for (var index = 2; index < segments.Length; index++)
            {
                remainingSegments[index - 1] = ColorAwareTranslationComposer.TranslatePreservingColors(
                    segments[index],
                    TranslateVisibleZoneDisplayName);
            }

            return string.Join(", ", remainingSegments);
        }

        var translatedSegments = new string[segments.Length];
        var anyChanged = false;
        for (var index = 0; index < segments.Length; index++)
        {
            var translatedSegment = ColorAwareTranslationComposer.TranslatePreservingColors(segments[index], TranslateVisibleZoneDisplayName);
            translatedSegments[index] = translatedSegment;
            if (!string.Equals(translatedSegment, segments[index], StringComparison.Ordinal))
            {
                anyChanged = true;
            }
        }

        return anyChanged ? string.Join(", ", translatedSegments) : source;
    }

    internal static string PreparePassByMessage(string source, string route)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            return source;
        }

        var translated = MessagePatternTranslator.Translate(source, route);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return source;
        }

        DynamicTextObservability.RecordTransform(route, "PassBy", source, translated);
        return MessageFrameTranslator.MarkDirectTranslation(translated);
    }

    internal static string PrepareZoneBannerMessage(string source, string route)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            return source;
        }

        if (TryTranslateZoneDisplayName(source, route, out var zoneTranslated)
            && !string.Equals(zoneTranslated, source, StringComparison.Ordinal))
        {
            return MessageFrameTranslator.MarkDirectTranslation(zoneTranslated);
        }

        if (ContainsJapaneseCharacters(source))
        {
            return MessageFrameTranslator.MarkDirectTranslation(source);
        }

        var translated = MessagePatternTranslator.Translate(source, route);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "ZoneBanner", source, translated);
            return MessageFrameTranslator.MarkDirectTranslation(translated);
        }

        return source;
    }

    internal static bool TryPreparePatternMessage(
        ref string source,
        string route,
        string detail,
        bool markJapaneseAsDirect = false)
    {
        if (string.IsNullOrEmpty(source)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            return false;
        }

        var patternSource = StripLeadingPatternControlHeader(source);

        if (markJapaneseAsDirect && ContainsJapaneseCharacters(patternSource))
        {
            if (ShouldMarkAlreadyLocalizedJapaneseDirectly(patternSource))
            {
                source = MessageFrameTranslator.MarkDirectTranslation(patternSource);
                return true;
            }

            var patternTranslated = MessagePatternTranslator.Translate(patternSource, route);
            if (!string.Equals(patternTranslated, patternSource, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(route, detail, source, patternTranslated);
                source = MessageFrameTranslator.MarkDirectTranslation(patternTranslated);
                return true;
            }

            source = MessageFrameTranslator.MarkDirectTranslation(patternSource);
            return true;
        }

        var translated = MessagePatternTranslator.Translate(patternSource, route);
        if (string.Equals(translated, patternSource, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, detail, source, translated);
        source = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static string StripLeadingPatternControlHeader(string source)
    {
        if (string.IsNullOrEmpty(source) || source[0] != '\u0002')
        {
            return source;
        }

        var headerEnd = source.IndexOf('\u0003');
        if (headerEnd < 0 || headerEnd >= source.Length - 1)
        {
            return source;
        }

        return source.Substring(headerEnd + 1);
    }

    private static bool ShouldMarkAlreadyLocalizedJapaneseDirectly(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return true;
        }

        var normalized = AllowedAlreadyLocalizedEnglishTokenPattern.Replace(source, string.Empty);
        return !AsciiLetterPattern.IsMatch(normalized);
    }

    private static string TranslateVisibleZoneDisplayName(string source)
    {
        if (TryTranslateZoneSegment(source, out var translated))
        {
            return translated;
        }

        var segments = source.Split(new[] { ", " }, StringSplitOptions.None);
        if (segments.Length == 1)
        {
            return source;
        }

        var translatedSegments = new string[segments.Length];
        var anyChanged = false;
        for (var index = 0; index < segments.Length; index++)
        {
            if (TryTranslateZoneSegment(segments[index], out var translatedSegment))
            {
                translatedSegments[index] = translatedSegment;
                anyChanged = true;
            }
            else
            {
                translatedSegments[index] = segments[index];
            }
        }

        return anyChanged ? string.Join(", ", translatedSegments) : source;
    }

    private static bool TryTranslateZoneSegment(string source, out string translated)
    {
        if (TryTranslateExactZoneSegment(source, out translated))
        {
            return true;
        }

        if (TryTranslateLairSegment(source, out translated))
        {
            return true;
        }

        var articleStripped = StripLeadingArticle(source);
        if (!string.Equals(articleStripped, source, StringComparison.Ordinal)
            && TryTranslateArticleIndependentZoneSegment(articleStripped, out translated))
        {
            return true;
        }

        if (TryTranslateArticleIndependentZoneSegment(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateExactZoneSegment(string source, out string translated)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated))
        {
            return true;
        }

        var articleStripped = StripLeadingArticle(source);
        if (!string.Equals(articleStripped, source, StringComparison.Ordinal)
            && StringHelpers.TryGetTranslationExactOrLowerAscii(articleStripped, out translated))
        {
            return true;
        }

        var match = StrataPattern.Match(source);
        if (match.Success)
        {
            var templateKey = "{0} strata " + match.Groups["direction"].Value;
            var template = Translator.Translate(templateKey);
            if (!string.Equals(template, templateKey, StringComparison.Ordinal))
            {
                translated = template.Replace("{0}", match.Groups["count"].Value);
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateArticleIndependentZoneSegment(string source, out string translated)
    {
        if (TryTranslateFarmSuffixSegment(source, out translated))
        {
            return true;
        }

        if (TryTranslateBiomeAdjectiveSegment(source, out translated))
        {
            return true;
        }

        if (TryTranslateBiomeChainSegment(source, out translated))
        {
            return true;
        }

        if (TryTranslateRuinsTopologySegment(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateGoatfolkDisplayName(string baseSegment, string suffixSegment, out string translated)
    {
        var translatedSuffix = ColorAwareTranslationComposer.TranslatePreservingColors(
            suffixSegment,
            TranslateVisibleGoatfolkSuffix);
        if (string.Equals(translatedSuffix, suffixSegment, StringComparison.Ordinal))
        {
            translated = string.Empty;
            return false;
        }

        var translatedBase = ColorAwareTranslationComposer.TranslatePreservingColors(baseSegment, TranslateVisibleZoneDisplayName);
        translated = translatedBase + translatedSuffix;
        return true;
    }

    private static string TranslateVisibleGoatfolkSuffix(string source)
    {
        var match = GoatfolkSuffixPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        var prefixedKey = ", " + source;
        var prefixedTranslation = Translator.Translate(prefixedKey);
        if (!string.Equals(prefixedTranslation, prefixedKey, StringComparison.Ordinal))
        {
            return prefixedTranslation;
        }

        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated))
        {
            return source;
        }

        return "、" + translated;
    }

    private static bool TryTranslateLairSegment(string source, out string translated)
    {
        var match = LairPattern.Match(source);
        if (!match.Success
            || !LairSuffixes.TryGetValue(match.Groups["kind"].Value, out var suffix))
        {
            translated = source;
            return false;
        }

        translated = TranslateZoneSubsegment(match.Groups["name"].Value) + suffix;
        return true;
    }

    private static bool TryTranslateFarmSuffixSegment(string source, out string translated)
    {
        if (!TrySplitTrailingWord(source, out var root, out var suffix)
            || !FarmSuffixes.Contains(suffix)
            || !StringHelpers.TryGetTranslationExactOrLowerAscii(suffix, out var translatedSuffix))
        {
            translated = source;
            return false;
        }

        translated = TranslateZoneSubsegment(root) + translatedSuffix;
        return true;
    }

    private static bool TryTranslateBiomeAdjectiveSegment(string source, out string translated)
    {
        var workingSource = source;
        if (workingSource.StartsWith("some ", StringComparison.OrdinalIgnoreCase))
        {
            workingSource = workingSource.Substring(5);
        }
        else if (workingSource.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
        {
            workingSource = workingSource.Substring(4);
        }

        for (var index = 0; index < BiomeAdjectivesOrdered.Length; index++)
        {
            var adjective = BiomeAdjectivesOrdered[index];
            if (workingSource.Length <= adjective.Length
                || !workingSource.StartsWith(adjective + " ", StringComparison.OrdinalIgnoreCase)
                || !StringHelpers.TryGetTranslationExactOrLowerAscii(adjective, out var translatedAdjective))
            {
                continue;
            }

            var remainder = workingSource.Substring(adjective.Length + 1);
            translated = translatedAdjective + TranslateZoneSubsegment(remainder);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateBiomeChainSegment(string source, out string translated)
    {
        var parts = source.Split(new[] { " and " }, StringSplitOptions.None);
        if (parts.Length < 2)
        {
            translated = source;
            return false;
        }

        var translatedParts = new string[parts.Length];
        // First part can be any translatable zone segment (TranslateZoneSubsegment).
        // Tail parts must be known biome names validated by TryTranslateBiomeTail
        // (FixedBiomeNames or PsychicBiomeEpithets); unknown tails abort the translation.
        translatedParts[0] = TranslateZoneSubsegment(parts[0]);
        for (var index = 1; index < parts.Length; index++)
        {
            if (!TryTranslateBiomeTail(parts[index], out var translatedPart))
            {
                translated = source;
                return false;
            }

            translatedParts[index] = translatedPart;
        }

        translated = string.Join("と", translatedParts);
        return true;
    }

    private static bool TryTranslateBiomeTail(string source, out string translated)
    {
        if ((!FixedBiomeNames.Contains(source)
                && !ZoneDisplayNameTranslationCatalog.PsychicBiomeEpithets.Contains(source))
            || !StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated))
        {
            translated = source;
            return false;
        }

        return true;
    }

    private static bool TryTranslateRuinsTopologySegment(string source, out string translated)
    {
        if (TryTranslateDoubleRuinsTopologySegment(source, out translated))
        {
            return true;
        }

        if (TryTranslatePrefixedRuinsTopologySegment(source, out translated))
        {
            return true;
        }

        if (TryTranslateSuffixedRuinsTopologySegment(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateDoubleRuinsTopologySegment(string source, out string translated)
    {
        if (!TrySplitLeadingWord(source, out var firstWord, out var remainder)
            || !TrySplitLeadingWord(remainder, out var secondWord, out var root)
            || string.IsNullOrEmpty(root)
            || !TryTranslateRuinsTopologyWord(firstWord, out var translatedFirstWord)
            || !TryTranslateRuinsTopologyWord(secondWord, out var translatedSecondWord))
        {
            translated = source;
            return false;
        }

        translated = translatedFirstWord + translatedSecondWord + TranslateZoneSubsegment(root);
        return true;
    }

    private static bool TryTranslatePrefixedRuinsTopologySegment(string source, out string translated)
    {
        if (!TrySplitLeadingWord(source, out var word, out var root)
            || string.IsNullOrEmpty(root)
            || !TryTranslateRuinsTopologyWord(word, out var translatedWord))
        {
            translated = source;
            return false;
        }

        translated = translatedWord + TranslateZoneSubsegment(root);
        return true;
    }

    private static bool TryTranslateSuffixedRuinsTopologySegment(string source, out string translated)
    {
        if (!TrySplitTrailingWord(source, out var root, out var word)
            || string.IsNullOrEmpty(root)
            || !TryTranslateRuinsTopologyWord(word, out var translatedWord))
        {
            translated = source;
            return false;
        }

        translated = TranslateZoneSubsegment(root) + translatedWord;
        return true;
    }

    private static bool TryTranslateRuinsTopologyWord(string source, out string translated)
    {
        if (!ZoneDisplayNameTranslationCatalog.RuinsTopologyWords.Contains(source)
            || !StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated))
        {
            translated = source;
            return false;
        }

        return true;
    }

    private static string TranslateZoneSubsegment(string source)
    {
        return TryTranslateZoneSegment(source, out var translated)
            ? translated
            : source;
    }

    private static bool TrySplitLeadingWord(string source, out string word, out string remainder)
    {
        var separatorIndex = source.IndexOf(' ');
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 1)
        {
            word = string.Empty;
            remainder = string.Empty;
            return false;
        }

        word = source.Substring(0, separatorIndex);
        remainder = source.Substring(separatorIndex + 1);
        return true;
    }

    private static bool TrySplitTrailingWord(string source, out string remainder, out string word)
    {
        var separatorIndex = source.LastIndexOf(' ');
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 1)
        {
            remainder = string.Empty;
            word = string.Empty;
            return false;
        }

        remainder = source.Substring(0, separatorIndex);
        word = source.Substring(separatorIndex + 1);
        return true;
    }

    private static string StripLeadingArticle(string source)
    {
        if (source.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(4);
        }

        if (source.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(3);
        }

        if (source.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(2);
        }

        if (source.StartsWith("some ", StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(5);
        }

        return source;
    }

    private static bool ContainsJapaneseCharacters(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= '\u3040' && character <= '\u30ff')
                || (character >= '\u3400' && character <= '\u9fff')
                || (character >= '\uff66' && character <= '\uff9f'))
            {
                return true;
            }
        }

        return false;
    }
}
