using System;
using System.Text;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class MessageLogProducerTranslationHelpers
{
    private static readonly Regex StrataPattern = new Regex(
        "^(?<count>\\d+) str(?:atum|ata) (?<direction>deep|high)$",
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
        var segments = source.Split(new[] { ", " }, StringSplitOptions.None);
        if (segments.Length == 1)
        {
            return ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateVisibleZoneDisplayName);
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

    internal static string PreparePatternMessage(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            return source;
        }

        var leadingLineBreakLength = GetLeadingLineBreakLength(source);
        var core = leadingLineBreakLength == 0 ? source : source.Substring(leadingLineBreakLength);
        if (core.Length == 0)
        {
            return source;
        }

        var (visible, _) = ColorAwareTranslationComposer.Strip(core);
        if (UITextSkinTranslationPatch.IsProbablyAlreadyLocalizedText(visible))
        {
            return MessageFrameTranslator.MarkDirectTranslation(source);
        }

        if (!MessagePatternTranslator.TryTranslateWithoutLogging(core, out var translatedCore))
        {
            return source;
        }

        var translated = leadingLineBreakLength == 0
            ? translatedCore
            : PrependLeadingLineBreaks(source, leadingLineBreakLength, translatedCore);
        DynamicTextObservability.RecordTransform(route, "Pattern", source, translated);
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

    private static int GetLeadingLineBreakLength(string source)
    {
        var index = 0;
        while (index < source.Length && (source[index] == '\r' || source[index] == '\n'))
        {
            index++;
        }

        return index;
    }

    private static string PrependLeadingLineBreaks(string source, int leadingLineBreakLength, string translatedCore)
    {
        var builder = new StringBuilder(leadingLineBreakLength + translatedCore.Length);
        builder.Append(source, 0, leadingLineBreakLength);
        builder.Append(translatedCore);
        return builder.ToString();
    }
}
