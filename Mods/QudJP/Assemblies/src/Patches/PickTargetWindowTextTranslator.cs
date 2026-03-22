using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class PickTargetWindowTextTranslator
{
    internal static bool TryTranslateUiText(string source, string route, out string translated)
    {
        if (TryTranslateCommandBar(source, route, out translated))
        {
            return true;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, "PickTarget.ExactLookup", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCommandBar(string source, string route, out string translated)
    {
        translated = source;
        if (source.IndexOf(" | ", StringComparison.Ordinal) < 0)
        {
            return false;
        }

        var segments = source.Split(new[] { " | " }, StringSplitOptions.None);
        var translatedSegments = new string[segments.Length];
        for (var index = 0; index < segments.Length; index++)
        {
            if (!TryTranslateCommandBarSegment(segments[index], out translatedSegments[index]))
            {
                return false;
            }
        }

        translated = string.Join(" | ", translatedSegments);
        DynamicTextObservability.RecordTransform(route, "PickTarget.CommandBar", source, translated);
        return true;
    }

    private static bool TryTranslateCommandBarSegment(string source, out string translated)
    {
        var direct = UITextSkinTranslationPatch.TranslateAsciiTokenWithCaseFallback(source);
        if (direct is not null)
        {
            translated = direct;
            return true;
        }

        if (UITextSkinTranslationPatch.LooksLikeCommandHotkeyToken(source))
        {
            translated = source;
            return true;
        }

        var parenthesizedHotkeyMatch = Regex.Match(source, "^\\((?<hotkey>[^)]+)\\)\\s+(?<label>.+)$", RegexOptions.CultureInvariant);
        if (parenthesizedHotkeyMatch.Success)
        {
            var translatedLabel = UITextSkinTranslationPatch.TranslateAsciiTokenWithCaseFallback(parenthesizedHotkeyMatch.Groups["label"].Value);
            if (translatedLabel is not null)
            {
                translated = $"({parenthesizedHotkeyMatch.Groups["hotkey"].Value}) {translatedLabel}";
                return true;
            }
        }

        var hotkeyPrefixMatch = Regex.Match(source, "^(?<hotkey>\\S+)\\s+(?<label>.+)$", RegexOptions.CultureInvariant);
        if (hotkeyPrefixMatch.Success)
        {
            var translatedLabel = UITextSkinTranslationPatch.TranslateAsciiTokenWithCaseFallback(hotkeyPrefixMatch.Groups["label"].Value);
            if (translatedLabel is not null)
            {
                translated = $"{hotkeyPrefixMatch.Groups["hotkey"].Value} {translatedLabel}";
                return true;
            }
        }

        var hyphenatedHotkeyMatch = Regex.Match(source, "^(?<hotkey>[^\\s|-]+)-(?<label>.+)$", RegexOptions.CultureInvariant);
        if (hyphenatedHotkeyMatch.Success)
        {
            var translatedLabel = UITextSkinTranslationPatch.TranslateAsciiTokenWithCaseFallback(hyphenatedHotkeyMatch.Groups["label"].Value);
            if (translatedLabel is not null)
            {
                translated = $"{hyphenatedHotkeyMatch.Groups["hotkey"].Value}-{translatedLabel}";
                return true;
            }
        }

        var hotkeySuffixMatch = Regex.Match(source, "^(?<label>.+?)\\s+\\((?<hotkey>[^)]+)\\)(?<suffix>\\)?)$", RegexOptions.CultureInvariant);
        if (hotkeySuffixMatch.Success)
        {
            var translatedLabel = UITextSkinTranslationPatch.TranslateAsciiTokenWithCaseFallback(hotkeySuffixMatch.Groups["label"].Value);
            if (translatedLabel is not null)
            {
                translated = $"{translatedLabel} ({hotkeySuffixMatch.Groups["hotkey"].Value}){hotkeySuffixMatch.Groups["suffix"].Value}";
                return true;
            }
        }

        translated = source;
        return false;
    }
}
