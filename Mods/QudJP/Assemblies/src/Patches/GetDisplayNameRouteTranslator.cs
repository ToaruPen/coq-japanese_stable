using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using QudJP;

namespace QudJP.Patches;

internal static class GetDisplayNameRouteTranslator
{
    private static readonly string[] DisplayNameDictionaryFiles =
    {
        "ui-displayname-adjectives.ja.json",
    };
    private static readonly string[] LiquidPhraseDictionaryFiles =
    {
        "ui-liquid-adjectives.ja.json",
        "ui-liquids.ja.json",
    };
    private static readonly Regex BracketedDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+\\[(?<state>.+)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ParenthesizedDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+\\((?<state>[A-Za-z][A-Za-z\\s-]*)\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex QuantityDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+x(?<count>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MkTierDisplayNameSuffixPattern =
        new Regex(
            "^(?<base>.+?)\\s+mk\\s+(?<tier>[IVXLC]+)(?:\\s+<(?<code>[^>]+)>)?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AngleCodeDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+<(?<code>[^>]+)>$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingMarkupWrappedModifierPattern =
        new Regex(
            "^(?<modifier>\\{\\{[^|}]+\\|[A-Za-z][A-Za-z\\s\\-']*\\}\\}|\\[\\{\\{[^|}]+\\|[A-Za-z][A-Za-z\\s\\-']*\\}\\}\\])\\s+(?<rest>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PrepositionalStateTemplatePattern =
        new Regex(
            "^(?<template>sitting on|lying on|enclosed in|engulfed by|auto-collecting) (?<target>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex QuantifiedLiquidStatePattern =
        new Regex(
            "^(?<amount>\\d+)\\s+drams? of (?<liquid>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnglishWordPattern =
        new Regex("[A-Za-z]{2,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool IsAlreadyLocalizedDisplayNameText(string source)
    {
        return IsAlreadyLocalizedBracketedDisplayName(source)
            || IsAlreadyLocalizedParenthesizedDisplayName(source);
    }

    internal static bool IsAlreadyLocalizedDisplayNameStateText(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return JapaneseCharacterPattern.IsMatch(source)
            && !EnglishWordPattern.IsMatch(source);
    }

    internal static string TranslatePreservingColors(string? source, string? context = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var route = ObservabilityHelpers.ExtractPrimaryContext(context);
        if (route is null)
        {
            Trace.TraceWarning("QudJP: GetDisplayNameRouteTranslator could not extract a primary context; falling back to route name.");
            route = nameof(GetDisplayNameRouteTranslator);
        }

        using var _ = Translator.PushLogContext(context);

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.Length == 0)
        {
            return source!;
        }

        using var __ = Translator.PushMissingKeyLoggingSuppression(
            IsAlreadyLocalizedDisplayNameText(stripped)
            || IsAlreadyLocalizedDisplayNameStateText(stripped)
            || IsAlreadyLocalizedBracketLabel(stripped)
            ||
            UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(stripped, context));

        if (IsAlreadyLocalizedDisplayNameText(stripped))
        {
            return source!;
        }

        if (TryTranslateLeadingMarkupWrappedModifier(source!, route, out var markupLeadingTranslation))
        {
            return markupLeadingTranslation;
        }

        if (TryTranslateDisplayNameRouteText(stripped, route, out var translated))
        {
            return ColorAwareTranslationComposer.Restore(translated, spans);
        }

        return source!;
    }

    private static bool TryTranslateDisplayNameRouteText(string source, string route, out string translated)
    {
        translated = source;
        var transformed = source;
        var changed = false;

        if (TryTranslateParenthesizedDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateBracketedDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateQuantityDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateMkTierDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateAngleCodeDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateExactDisplayNameLookup(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateTrimmedDisplayNameLookup(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateGeneratedTitleSuffix(transformed, route, out var titleTranslated))
        {
            transformed = titleTranslated;
            changed = true;
        }

        if (TryTranslateMixedDisplayName(transformed, route, out var modifierTranslated))
        {
            translated = modifierTranslated;
            return true;
        }

        if (TryTranslateLiquidPrepositionDisplayName(transformed, route, out var ofPhraseTranslated))
        {
            translated = ofPhraseTranslated;
            return true;
        }

        if (TryTranslateGeneratedProperNameModifier(transformed, route, out var properNameModifierTranslated))
        {
            translated = properNameModifierTranslated;
            return true;
        }

        if (changed)
        {
            translated = transformed;
            return true;
        }

        return false;
    }

    private static bool TryTranslateParenthesizedDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = ParenthesizedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var stateSource = match.Groups["state"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        var translatedState = TranslateDisplayNameState(stateSource, route);

        translated = translatedBase + " (" + translatedState + ")";
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return IsStableDisplayNameFragment(baseSource, route) || IsStableDisplayNameState(stateSource);
        }

        DynamicTextObservability.RecordTransform(route, "DisplayName.ParenthesizedSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateBracketedDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = BracketedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var stateSource = match.Groups["state"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        var translatedState = TranslateDisplayNameState(stateSource, route);

        if (string.Equals(translatedBase, baseSource, StringComparison.Ordinal)
            && string.Equals(translatedState, stateSource, StringComparison.Ordinal))
        {
            translated = source;
            return IsStableDisplayNameFragment(baseSource, route) && IsStableDisplayNameState(stateSource);
        }

        translated = translatedBase + " [" + translatedState + "]";
        DynamicTextObservability.RecordTransform(route, "DisplayName.BracketedSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateQuantityDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = QuantityDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        translated = translatedBase + " x" + match.Groups["count"].Value;
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.QuantitySuffix", source, translated);
            return true;
        }

        return IsStableDisplayNameFragment(baseSource, route);
    }

    private static bool TryTranslateMkTierDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = MkTierDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        translated = translatedBase + " mk " + match.Groups["tier"].Value;
        var code = match.Groups["code"].Value;
        if (code.Length > 0)
        {
            translated += " <" + code + ">";
        }

        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.MkTierSuffix", source, translated);
            return true;
        }

        return IsStableDisplayNameFragment(baseSource, route);
    }

    private static bool TryTranslateAngleCodeDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = AngleCodeDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        translated = translatedBase + " <" + match.Groups["code"].Value + ">";
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.AngleCodeSuffix", source, translated);
            return true;
        }

        return IsStableDisplayNameFragment(baseSource, route);
    }

    private static bool TryTranslateLeadingMarkupWrappedModifier(string source, string route, out string translated)
    {
        var match = LeadingMarkupWrappedModifierPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var modifier = match.Groups["modifier"].Value;
        var translatedModifier = TranslateDisplayNameModifier(modifier);
        if (translatedModifier is null)
        {
            translated = source;
            return false;
        }

        var restSource = match.Groups["rest"].Value;
        var rest = TranslateDisplayNameFragment(restSource, route);
        translated = translatedModifier + (ShouldElideModifierSpace(restSource) ? string.Empty : " ") + rest;
        DynamicTextObservability.RecordTransform(route, "DisplayName.MarkupLeadingModifier", source, translated);
        return true;
    }

    private static bool TryTranslateMixedDisplayName(string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || !JapaneseCharacterPattern.IsMatch(source))
        {
            return false;
        }

        var separatorIndex = source.IndexOf(' ');
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 1)
        {
            return false;
        }

        var modifier = source.Substring(0, separatorIndex);
        if (!IsAsciiModifierToken(modifier))
        {
            return false;
        }

        var rest = source.Substring(separatorIndex + 1);
        var translatedModifier = TranslateDisplayNameExactOrLowerAscii(modifier);
        if (translatedModifier is null)
        {
            return false;
        }

        var translatedRest = TranslateDisplayNameFragment(rest, route);
        translated = translatedModifier + translatedRest;
        DynamicTextObservability.RecordTransform(route, "DisplayName.MixedModifier", source, translated);
        return true;
    }

    private static bool TryTranslateLiquidPrepositionDisplayName(string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || !JapaneseCharacterPattern.IsMatch(source))
        {
            return false;
        }

        var separatorIndex = source.IndexOf(" of ", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 4)
        {
            return false;
        }

        var head = source.Substring(0, separatorIndex);
        var tail = source.Substring(separatorIndex + 4);
        if (tail.Length == 0 || !LooksLikeAsciiPhrase(tail))
        {
            return false;
        }

        if (!LooksLikeLocalizedLiquidDisplayNameHead(head))
        {
            return false;
        }

        var translatedTail = TranslateAsciiPhrase(tail);
        if (translatedTail is null)
        {
            return false;
        }

        translated = translatedTail + "の" + head;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LiquidPreposition", source, translated);
        return true;
    }

    private static bool TryTranslateGeneratedProperNameModifier(string source, string route, out string translated)
    {
        translated = source;
        var separatorIndex = source.IndexOf(' ');
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 1)
        {
            return false;
        }

        var modifier = source.Substring(0, separatorIndex);
        if (!IsAsciiModifierToken(modifier))
        {
            return false;
        }

        var rest = source.Substring(separatorIndex + 1);
        if (!LooksLikeGeneratedProperName(rest))
        {
            return false;
        }

        var translatedModifier = TranslateDisplayNameExactOrLowerAscii(modifier);
        if (translatedModifier is null)
        {
            return false;
        }

        translated = translatedModifier + rest;
        DynamicTextObservability.RecordTransform(route, "DisplayName.ProperNameModifier", source, translated);
        return true;
    }

    private static bool TryTranslateGeneratedTitleSuffix(string source, string route, out string translated)
    {
        translated = source;
        var separatorIndex = source.IndexOf(", ", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 2)
        {
            return false;
        }

        var suffix = source.Substring(separatorIndex + 2);
        var translatedSuffix = Translator.Translate(suffix);
        if (string.Equals(translatedSuffix, suffix, StringComparison.Ordinal))
        {
            return false;
        }

        translated = source.Substring(0, separatorIndex) + "、" + translatedSuffix;
        DynamicTextObservability.RecordTransform(route, "DisplayName.TitleSuffix", source, translated);
        return true;
    }

    private static string TranslateDisplayNameFragment(string source, string route)
    {
        if (IsStableDisplayNameFragment(source, route))
        {
            return source;
        }

        if (TryTranslateDisplayNameRouteText(source, route, out var translated))
        {
            return translated;
        }

        var direct = TranslateDisplayNameExactOrLowerAscii(source);
        if (direct is not null)
        {
            return direct;
        }

        return source;
    }

    private static string TranslateDisplayNameState(string source, string route)
    {
        if (IsAlreadyLocalizedDisplayNameStateText(source))
        {
            return source;
        }

        if (TryTranslateBracketedStateExact(source, out var exact))
        {
            return exact;
        }

        if (TryTranslateQuantifiedLiquidState(source, route, out var quantifiedLiquid))
        {
            return quantifiedLiquid;
        }

        if (TryTranslateDisplayNameStateTemplate(source, route, out var translated))
        {
            return translated;
        }

        var direct = TranslateAsciiTokenWithCaseFallback(source);
        if (direct is not null)
        {
            return direct;
        }

        return source;
    }

    private static bool TryTranslateQuantifiedLiquidState(string source, string route, out string translated)
    {
        var match = QuantifiedLiquidStatePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var liquidSource = match.Groups["liquid"].Value;
        var translatedLiquid = TranslateAsciiPhrase(liquidSource);
        if (translatedLiquid is null)
        {
            var direct = Translator.Translate(liquidSource);
            if (string.Equals(direct, liquidSource, StringComparison.Ordinal))
            {
                translated = source;
                return false;
            }

            translatedLiquid = direct;
        }

        translated = match.Groups["amount"].Value + "ドラムの" + translatedLiquid;
        DynamicTextObservability.RecordTransform(route, "DisplayName.QuantifiedLiquidState", source, translated);
        return true;
    }

    private static bool TryTranslateBracketedStateExact(string source, out string translated)
    {
        var bracketed = "[" + source + "]";
        var direct = ScopedDictionaryLookup.TranslateExactOrLowerAscii(bracketed, DisplayNameDictionaryFiles);
        if (direct is null)
        {
            using var _ = Translator.PushMissingKeyLoggingSuppression(true);
            var global = Translator.Translate(bracketed);
            if (!string.Equals(global, bracketed, StringComparison.Ordinal))
            {
                direct = global;
            }
        }

        if (direct is null)
        {
            translated = source;
            return false;
        }

        translated = UnwrapSingleBracketPair(direct);
        return true;
    }

    private static bool TryTranslateDisplayNameStateTemplate(string source, string route, out string translated)
    {
        var match = PrepositionalStateTemplatePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = match.Groups["template"].Value + " {0}";
        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var translatedTarget = TranslateDisplayNameStateTarget(match.Groups["target"].Value, route);
        translated = translatedTemplate.Replace("{0}", translatedTarget);
        DynamicTextObservability.RecordTransform(route, "DisplayName.BracketedStateTemplate", source, translated);
        return true;
    }

    private static string TranslateDisplayNameStateTarget(string source, string route)
    {
        var target = StringHelpers.StripLeadingEnglishArticle(source);
        return TranslateDisplayNameFragment(target, route);
    }

    private static string UnwrapSingleBracketPair(string source)
    {
        if (source.Length >= 2
            && source[0] == '['
            && source[source.Length - 1] == ']')
        {
            return source.Substring(1, source.Length - 2);
        }

        return source;
    }

    private static bool IsStableDisplayNameFragment(string source, string route)
    {
        if (UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(source, route))
        {
            return true;
        }

        return !EnglishWordPattern.IsMatch(source);
    }

    private static bool IsStableDisplayNameState(string source)
    {
        return !EnglishWordPattern.IsMatch(source);
    }

    private static bool IsAsciiModifierToken(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || character == '-'
                || character == '\'')
            {
                continue;
            }

            return false;
        }

        return source.Length > 0;
    }

    private static bool LooksLikeLocalizedLiquidDisplayNameHead(string source)
    {
        return source.EndsWith("水たまり", StringComparison.Ordinal)
            || source.EndsWith("池", StringComparison.Ordinal);
    }

    private static bool LooksLikeAsciiPhrase(string source)
    {
        var hasLetter = false;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z'))
            {
                hasLetter = true;
                continue;
            }

            if (character == ' ' || character == '-' || character == '\'')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }

    private static string? TranslateAsciiPhrase(string source)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, LiquidPhraseDictionaryFiles);
        if (scoped is not null)
        {
            return scoped;
        }

        using var __ = Translator.PushMissingKeyLoggingSuppression(true);
        var direct = Translator.Translate(source);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        var parts = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < parts.Length; index++)
        {
            var translatedPart = ScopedDictionaryLookup.TranslateExactOrLowerAscii(parts[index], LiquidPhraseDictionaryFiles);
            if (translatedPart is null)
            {
                translatedPart = Translator.Translate(parts[index]);
            }

            if (string.Equals(translatedPart, parts[index], StringComparison.Ordinal))
            {
                return null;
            }

            builder.Append(translatedPart);
        }

        return builder.ToString();
    }

    private static bool LooksLikeGeneratedProperName(string source)
    {
        var hasUppercase = false;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (character >= 'A' && character <= 'Z')
            {
                hasUppercase = true;
                continue;
            }

            if ((character >= 'a' && character <= 'z')
                || character == '-'
                || character == '\''
                || character == ' '
                || character == ',')
            {
                continue;
            }

            return false;
        }

        return hasUppercase;
    }

    private static bool ShouldElideModifierSpace(string source)
    {
        if (LooksLikeGeneratedProperName(source))
        {
            return true;
        }

        var bracketedMatch = BracketedDisplayNameSuffixPattern.Match(source);
        if (bracketedMatch.Success && LooksLikeGeneratedProperName(bracketedMatch.Groups["base"].Value))
        {
            return true;
        }

        var parenthesizedMatch = ParenthesizedDisplayNameSuffixPattern.Match(source);
        return parenthesizedMatch.Success && LooksLikeGeneratedProperName(parenthesizedMatch.Groups["base"].Value);
    }

    private static string? TranslateAsciiTokenWithCaseFallback(string source)
    {
        return TranslateDisplayNameExactOrLowerAscii(source);
    }

    private static bool TryTranslateExactDisplayNameLookup(string source, string route, out string translated)
    {
        var direct = TranslateDisplayNameExactOrLowerAscii(source);
        if (direct is not null)
        {
            translated = direct;
            DynamicTextObservability.RecordTransform(route, "DisplayName.ExactLookup", source, translated);
            return true;
        }
        translated = source;
        return false;
    }

    private static bool TryTranslateTrimmedDisplayNameLookup(string source, string route, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();
        if (trimmed.Length == 0 || trimmed.Length == source.Length)
        {
            return false;
        }

        var trimmedTranslation = TranslateDisplayNameExactOrLowerAscii(trimmed);
        if (trimmedTranslation is null)
        {
            return false;
        }

        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        translated =
            source.Substring(0, leadingLength) +
            trimmedTranslation +
            source.Substring(source.Length - trailingLength, trailingLength);
        DynamicTextObservability.RecordTransform(route, "DisplayName.TrimmedLookup", source, translated);
        return true;
    }

    private static string? TryTranslateDisplayNameScopedExact(string source)
    {
        return ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, DisplayNameDictionaryFiles);
    }

    private static string? TranslateDisplayNameExactOrLowerAscii(string source)
    {
        var scoped = TryTranslateDisplayNameScopedExact(source);
        if (scoped is not null)
        {
            return scoped;
        }

        return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
            ? translated
            : null;
    }

    private static string? TranslateDisplayNameModifier(string source)
    {
        var bracketWrapped = source.Length >= 2
            && source[0] == '['
            && source[source.Length - 1] == ']';
        var core = bracketWrapped
            ? source.Substring(1, source.Length - 2)
            : source;

        var direct = TryTranslateDisplayNameScopedExact(core);
        if (direct is null)
        {
            var (stripped, _) = ColorAwareTranslationComposer.Strip(core);
            if (!string.Equals(stripped, core, StringComparison.Ordinal))
            {
                direct = TryTranslateDisplayNameScopedExact(stripped);
            }
        }

        if (direct is null)
        {
            var global = Translator.Translate(source);
            if (string.Equals(global, source, StringComparison.Ordinal))
            {
                return null;
            }

            return global;
        }

        return bracketWrapped
            ? "[" + direct + "]"
            : direct;
    }

    private static bool IsAlreadyLocalizedBracketedDisplayName(string source)
    {
        var match = BracketedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        return ContainsJapanese(match.Groups["base"].Value)
            && IsAlreadyLocalizedDisplayNameStateText(match.Groups["state"].Value);
    }

    private static bool IsAlreadyLocalizedParenthesizedDisplayName(string source)
    {
        var match = ParenthesizedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        return ContainsJapanese(match.Groups["base"].Value)
            && IsAlreadyLocalizedDisplayNameStateText(match.Groups["state"].Value);
    }

    private static bool ContainsJapanese(string source)
    {
        return !string.IsNullOrEmpty(source) && JapaneseCharacterPattern.IsMatch(source);
    }

    private static bool IsAlreadyLocalizedBracketLabel(string source)
    {
        return source.Length >= 2
            && source[0] == '['
            && source[source.Length - 1] == ']'
            && ContainsJapanese(source);
    }
}
