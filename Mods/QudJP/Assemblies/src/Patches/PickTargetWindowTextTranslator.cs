using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class PickTargetWindowTextTranslator
{
    private const string DictionaryFile = "ui-pick-target.ja.json";
    private static readonly string[] CommandBarContexts = { "PickTarget.CommandBar" };
    private static readonly string[] ExactLabelContexts = { "PickTarget.DirectionPrompt", "PickTarget.Digging.Label" };

    internal static bool TryTranslateUiText(string source, string route, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var withoutMarker))
        {
            if (TryTranslateUiText(withoutMarker, route, out var translatedWithoutMarker)
                && !string.Equals(withoutMarker, translatedWithoutMarker, StringComparison.Ordinal))
            {
                translated = MessageFrameTranslator.MarkDirectTranslation(translatedWithoutMarker);
                return true;
            }

            translated = source;
            return false;
        }

        if (TryTranslateCommandBar(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateExactLabel(source, out translated))
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

    private static bool TryTranslateExactLabel(string source, out string translated)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        var direct = TranslatePickTargetExactLabelToken(visible);
        if (direct is null)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => direct);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool TryTranslateCommandBarSegment(string source, out string translated)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        if (IsOwnerRouteCommandBarToken(visible))
        {
            translated = source;
            return true;
        }

        var direct = TranslatePickTargetCommandBarToken(visible);
        if (direct is not null)
        {
            translated = ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => direct);
            return true;
        }

        if (UITextSkinTranslationPatch.LooksLikeCommandHotkeyToken(visible))
        {
            translated = source;
            return true;
        }

        var parenthesizedHotkeyMatch = Regex.Match(source, "^\\((?<hotkey>[^)]+)\\)\\s+(?<label>.+)$", RegexOptions.CultureInvariant);
        if (parenthesizedHotkeyMatch.Success)
        {
            var label = parenthesizedHotkeyMatch.Groups["label"].Value;
            var visibleLabel = ColorAwareTranslationComposer.GetVisibleText(label);
            if (IsOwnerRouteCommandBarToken(visibleLabel))
            {
                translated = source;
                return true;
            }

            var translatedLabel = TranslatePickTargetCommandBarToken(visibleLabel);
            if (translatedLabel is not null)
            {
                translated = $"({parenthesizedHotkeyMatch.Groups["hotkey"].Value}) {ColorAwareTranslationComposer.TranslatePreservingColors(label, _ => translatedLabel)}";
                return true;
            }
        }

        var sourceParenthesizedHotkeyMatch = Regex.Match(source, "^(?<label>.+?)\\s+\\((?<hotkey>.+)\\)(?<suffix>\\)?)$", RegexOptions.CultureInvariant);
        if (sourceParenthesizedHotkeyMatch.Success)
        {
            var label = sourceParenthesizedHotkeyMatch.Groups["label"].Value;
            var visibleLabel = ColorAwareTranslationComposer.GetVisibleText(label);
            if (IsOwnerRouteCommandBarToken(visibleLabel))
            {
                translated = source;
                return true;
            }

            var translatedLabel = TranslatePickTargetCommandBarToken(visibleLabel);
            if (translatedLabel is not null)
            {
                translated = $"{ColorAwareTranslationComposer.TranslatePreservingColors(label, _ => translatedLabel)} ({sourceParenthesizedHotkeyMatch.Groups["hotkey"].Value}){sourceParenthesizedHotkeyMatch.Groups["suffix"].Value}";
                return true;
            }
        }

        var hotkeyPrefixMatch = Regex.Match(source, "^(?<hotkey>\\S+)\\s+(?<label>.+)$", RegexOptions.CultureInvariant);
        if (hotkeyPrefixMatch.Success)
        {
            var label = hotkeyPrefixMatch.Groups["label"].Value;
            var visibleLabel = ColorAwareTranslationComposer.GetVisibleText(label);
            if (IsOwnerRouteCommandBarToken(visibleLabel))
            {
                translated = source;
                return true;
            }

            var translatedLabel = TranslatePickTargetCommandBarToken(visibleLabel);
            if (translatedLabel is not null)
            {
                translated = $"{hotkeyPrefixMatch.Groups["hotkey"].Value} {ColorAwareTranslationComposer.TranslatePreservingColors(label, _ => translatedLabel)}";
                return true;
            }
        }

        var hyphenatedHotkeyMatch = Regex.Match(source, "^(?<hotkey>.+)-(?<label>[^\\s|-]+)$", RegexOptions.CultureInvariant);
        if (hyphenatedHotkeyMatch.Success)
        {
            var label = hyphenatedHotkeyMatch.Groups["label"].Value;
            var visibleLabel = ColorAwareTranslationComposer.GetVisibleText(label);
            if (IsOwnerRouteCommandBarToken(visibleLabel))
            {
                translated = source;
                return true;
            }

            var translatedLabel = TranslatePickTargetCommandBarToken(visibleLabel);
            if (translatedLabel is not null)
            {
                translated = $"{hyphenatedHotkeyMatch.Groups["hotkey"].Value}-{ColorAwareTranslationComposer.TranslatePreservingColors(label, _ => translatedLabel)}";
                return true;
            }
        }

        var markupWrappedHotkeyMatch = Regex.Match(source, "^(?<hotkey>\\{\\{[^|}]+\\|[^}]+\\}\\})-(?<label>.+)$", RegexOptions.CultureInvariant);
        if (markupWrappedHotkeyMatch.Success)
        {
            var label = markupWrappedHotkeyMatch.Groups["label"].Value;
            var visibleLabel = ColorAwareTranslationComposer.GetVisibleText(label);
            if (IsOwnerRouteCommandBarToken(visibleLabel))
            {
                translated = source;
                return true;
            }

            var translatedLabel = TranslatePickTargetCommandBarToken(visibleLabel);
            if (translatedLabel is not null)
            {
                translated = $"{markupWrappedHotkeyMatch.Groups["hotkey"].Value}-{ColorAwareTranslationComposer.TranslatePreservingColors(label, _ => translatedLabel)}";
                return true;
            }
        }

        var hotkeySuffixMatch = Regex.Match(source, "^(?<label>.+?)\\s+\\((?<hotkey>[^)]+)\\)(?<suffix>\\)?)$", RegexOptions.CultureInvariant);
        if (hotkeySuffixMatch.Success)
        {
            var label = hotkeySuffixMatch.Groups["label"].Value;
            var visibleLabel = ColorAwareTranslationComposer.GetVisibleText(label);
            if (IsOwnerRouteCommandBarToken(visibleLabel))
            {
                translated = source;
                return true;
            }

            var translatedLabel = TranslatePickTargetCommandBarToken(visibleLabel);
            if (translatedLabel is not null)
            {
                translated = $"{ColorAwareTranslationComposer.TranslatePreservingColors(label, _ => translatedLabel)} ({hotkeySuffixMatch.Groups["hotkey"].Value}){hotkeySuffixMatch.Groups["suffix"].Value}";
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool IsOwnerRouteCommandBarToken(string source)
    {
        return string.Equals(source, "Fire Missile Weapon", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "Reload", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TranslatePickTargetExactLabelToken(string source)
    {
        return TranslatePickTargetToken(
            source,
            allowActivatedAbilityName: false,
            allowUiTokenFallback: false,
            scopedContexts: ExactLabelContexts);
    }

    private static string? TranslatePickTargetCommandBarToken(string source)
    {
        return TranslatePickTargetToken(
            source,
            allowActivatedAbilityName: true,
            allowUiTokenFallback: true,
            scopedContexts: CommandBarContexts);
    }

    private static string? TranslatePickTargetToken(
        string source,
        bool allowActivatedAbilityName = false,
        bool allowUiTokenFallback = true,
        string[]? scopedContexts = null)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, DictionaryFile);
        if (scoped is not null)
        {
            return scoped;
        }

        if (scopedContexts is not null)
        {
            for (var index = 0; index < scopedContexts.Length; index++)
            {
                var contextual = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(
                    source,
                    scopedContexts[index],
                    DictionaryFile);
                if (contextual is not null)
                {
                    return contextual;
                }
            }
        }

        if (allowActivatedAbilityName
            && ActivatedAbilityNameTranslator.TryTranslateVisibleName(source, out var abilityName))
        {
            return abilityName;
        }

        if (!allowUiTokenFallback)
        {
            return null;
        }

        return UITextSkinTranslationPatch.TranslateAsciiTokenWithCaseFallback(source);
    }
}
