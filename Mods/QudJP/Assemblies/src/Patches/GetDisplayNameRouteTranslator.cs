using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using QudJP;

namespace QudJP.Patches;

internal static class GetDisplayNameRouteTranslator
{
    private const string DisplayNameStateTemplateContext = "GetDisplayName.StateTemplate";
    private const string DisplayNameStateTemplateDictionaryFile = "Scoped/ui-displayname-state-templates.ja.json";

    private static readonly string[] DisplayNameDictionaryFiles =
    {
        "ui-displayname-adjectives.ja.json",
        "ui-displayname-atomic.ja.json",
    };
    private static readonly string[] LiquidPhraseDictionaryFiles =
    {
        "ui-liquid-adjectives.ja.json",
        "ui-liquids.ja.json",
        "ui-displayname-adjectives.ja.json",
    };
    private static readonly Regex BracketedDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+\\[(?<state>.+)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ArmorStatsDisplayNameSuffixPattern =
        new Regex(@"^(?<base>.+?) (?<stats>\x04-?\d+ \t-?\d+)(?: \[(?<state>.+)\])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CompactWeaponStatsDisplayNameSuffixPattern =
        new Regex(
            @"^(?<base>.+?) (?<stats>(?:\x1a[^\[\r\n]*(?: \x03[^\[\r\n]+)?|\x03[^\[\r\n]+))(?: \[(?<state>.+)\])?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ParenthesizedDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+\\((?<state>[A-Za-z][A-Za-z\\s-]*)\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex QuantityDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+x(?<count>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EditionTitleSuffixPattern =
        new Regex("^(?<number>\\d+)(?:st|nd|rd|th) Edition$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
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
    private static readonly Regex ParenthesizedColoredChargeStatusPattern =
        new Regex(
            "(?<prefix>\\()(?<status>\\{\\{[^|}]+\\|[^{}]*\\}\\})(?<suffix>\\))",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PrepositionalStateTemplatePattern =
        new Regex(
            "^(?<template>sitting on|lying on|enclosed in|engulfed by|auto-collecting|stuck in|grabbed by) (?<target>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex QuantifiedLiquidStatePattern =
        new Regex(
            "^(?<amount>\\d+)\\s+drams? of (?<liquid>.+?)(?:,\\s+(?<state>[A-Za-z][A-Za-z\\s-]*))?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LiquidStatePattern =
        new Regex(
            "^(?<liquid>.+?),\\s+(?<state>[A-Za-z][A-Za-z\\s-]*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TimedDisplayNameStatePattern =
        new Regex("^(?<count>\\d+) sec$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CookingServingsDisplayNameStatePattern =
        new Regex("^(?<count>\\d+) cooking servings?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnergyCellsDisplayNameStatePattern =
        new Regex("^(?<count>\\d+) cells?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ChapterDisplayNameStatePattern =
        new Regex("^(?<owner>.+?) chapter$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GeneratedCanvasTentPattern =
        new Regex(
            "^(?<body>[A-Za-z][A-Za-z\\s-]*?) tent$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GeneratedRandomStatuePattern =
        new Regex(
            "^(?<material>[A-Za-z][A-Za-z\\s-]*?) statue of (?<subject>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnglishWordPattern =
        new Regex("[A-Za-z]{2,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const string GeneratedCanvasTentComponentContext = "GetDisplayName.GeneratedCanvasTent.Component";
    private const string GeneratedRandomStatueComponentContext = "GetDisplayName.GeneratedRandomStatue.Component";

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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            return source!;
        }

        using var logScope = Translator.PushLogContext(context);

        if (TryHandleParenthesizedColoredChargeStatusFallback(source!, route, out var chargeStatusFallback))
        {
            return chargeStatusFallback;
        }

        if (TryTranslateParenthesizedColoredChargeStatus(source!, route, out var chargeStatusTranslation))
        {
            source = chargeStatusTranslation;
        }

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

        if (TryTranslateArmorStatsDisplayNameSuffix(stripped, spans, route, out var armorStatsTranslation))
        {
            return armorStatsTranslation;
        }

        if (TryTranslateCompactWeaponStatsDisplayNameSuffix(stripped, spans, route, out var compactWeaponStatsTranslation))
        {
            return compactWeaponStatsTranslation;
        }

        if (TryTranslateBracketedDisplayNameSuffix(stripped, spans, route, out var bracketedSuffixTranslation))
        {
            return bracketedSuffixTranslation;
        }

        if (TryTranslateDisplayNameRouteText(stripped, route, out var translated))
        {
            return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translated,
                spans,
                stripped.Length);
        }

        return source!;
    }

    private static bool TryTranslateParenthesizedColoredChargeStatus(
        string source,
        string route,
        out string translated)
    {
        var changed = false;
        translated = ParenthesizedColoredChargeStatusPattern.Replace(
            source,
            match =>
            {
                var status = match.Groups["status"].Value;
                if (!EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(status, out var translatedStatus))
                {
                    return match.Value;
                }

                changed = true;
                return match.Groups["prefix"].Value + translatedStatus + match.Groups["suffix"].Value;
            });

        if (changed)
        {
            DynamicTextObservability.RecordTransform(
                route,
                "DisplayName.ColoredChargeStatusSuffix",
                source,
                translated);
        }

        return changed;
    }

    private static bool TryHandleParenthesizedColoredChargeStatusFallback(
        string source,
        string route,
        out string translated)
    {
        var changed = false;
        var matchedFallback = false;
        translated = ParenthesizedColoredChargeStatusPattern.Replace(
            source,
            match =>
            {
                var status = match.Groups["status"].Value;
                if (TryStripDirectTranslationMarkerFromChargeStatus(status, out var markedStatus))
                {
                    changed = true;
                    matchedFallback = true;
                    return match.Groups["prefix"].Value + markedStatus + match.Groups["suffix"].Value;
                }

                if (EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(status, out _))
                {
                    return match.Value;
                }

                matchedFallback = true;
                return match.Value;
            });

        if (changed)
        {
            DynamicTextObservability.RecordTransform(
                route,
                "DisplayName.ColoredChargeStatusSuffix",
                source,
                translated);
        }

        return matchedFallback;
    }

    private static bool TryStripDirectTranslationMarkerFromChargeStatus(string status, out string stripped)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(status, out stripped))
        {
            return true;
        }

        var separator = status.IndexOf('|');
        if (!status.StartsWith("{{", StringComparison.Ordinal)
            || !status.EndsWith("}}", StringComparison.Ordinal)
            || separator <= 2
            || separator >= status.Length - 2)
        {
            stripped = status;
            return false;
        }

        var inner = status.Substring(separator + 1, status.Length - separator - 3);
        if (!MessageFrameTranslator.TryStripDirectTranslationMarker(inner, out var strippedInner))
        {
            stripped = status;
            return false;
        }

        stripped = status.Substring(0, separator + 1) + strippedInner + "}}";
        return true;
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

        if (HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.HistoricSpiceGeneratedName", source, translated);
            return true;
        }

        if (TryTranslateGeneratedTitleSuffix(transformed, route, out var titleTranslated))
        {
            transformed = titleTranslated;
            changed = true;
        }

        if (TryTranslateGeneratedRandomStatueName(transformed, route, out var randomStatueTranslated))
        {
            translated = randomStatueTranslated;
            return true;
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

        if (TryTranslateLocalizedPrefixAsciiTailDisplayName(transformed, route, out var localizedPrefixTailTranslated))
        {
            translated = localizedPrefixTailTranslated;
            return true;
        }

        if (TryTranslateLiquidState(transformed, route, out var liquidStateTranslated))
        {
            translated = liquidStateTranslated;
            return true;
        }

        if (TryTranslateGeneratedCanvasTentName(transformed, route, out var canvasTentTranslated))
        {
            translated = canvasTentTranslated;
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

    private static bool TryTranslateBracketedDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = BracketedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var stateGroup = match.Groups["state"];
        var translatedBase = RestoreWholeSlice(TranslateDisplayNameFragment(baseGroup.Value, route), spans, baseGroup);
        var translatedState = RestoreWholeSlice(TranslateDisplayNameState(stateGroup.Value, route), spans, stateGroup);

        if (string.Equals(translatedBase, baseGroup.Value, StringComparison.Ordinal)
            && string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedBase + " [" + translatedState + "]";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.BracketedSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateArmorStatsDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        return TryTranslateStatDisplayNameSuffix(
            source,
            spans,
            route,
            ArmorStatsDisplayNameSuffixPattern,
            "DisplayName.ArmorStatsSuffix",
            out translated);
    }

    private static bool TryTranslateCompactWeaponStatsDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        return TryTranslateStatDisplayNameSuffix(
            source,
            spans,
            route,
            CompactWeaponStatsDisplayNameSuffixPattern,
            "DisplayName.CompactWeaponStatsSuffix",
            out translated);
    }

    private static bool TryTranslateStatDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        Regex pattern,
        string transformName,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var baseSource = baseGroup.Value;
        var translatedBase = RestoreWholeSlice(TranslateDisplayNameFragment(baseSource, route), spans, baseGroup);
        var stats = RestoreVisibleSlice(match.Groups["stats"], spans);
        var stateGroup = match.Groups["state"];

        var translatedState = stateGroup.Success
            ? RestoreWholeSlice(TranslateDisplayNameState(stateGroup.Value, route), spans, stateGroup)
            : string.Empty;

        if (string.Equals(translatedBase, baseSource, StringComparison.Ordinal)
            && (!stateGroup.Success || string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal)))
        {
            translated = source;
            return false;
        }

        translated = translatedBase + " " + stats;
        if (stateGroup.Success)
        {
            translated += " [" + translatedState + "]";
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, transformName, source, translated);
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
        var rest = TranslatePreservingColors(restSource, route);
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

    private static bool TryTranslateLocalizedPrefixAsciiTailDisplayName(string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || !JapaneseCharacterPattern.IsMatch(source))
        {
            return false;
        }

        var separatorIndex = FindLocalizedPrefixAsciiTailSeparator(source);
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 1)
        {
            return false;
        }

        var prefix = source.Substring(0, separatorIndex);
        var tail = source.Substring(separatorIndex + 1);
        if (!LooksLikeAsciiPhrase(tail))
        {
            return false;
        }

        var translatedTail = TranslateAsciiPhrase(tail);
        if (translatedTail is null)
        {
            return false;
        }

        translated = prefix + translatedTail;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LocalizedPrefixAsciiTail", source, translated);
        return true;
    }

    private static int FindLocalizedPrefixAsciiTailSeparator(string source)
    {
        for (var index = 0; index < source.Length - 1; index++)
        {
            if (source[index] != ' ')
            {
                continue;
            }

            var next = source[index + 1];
            if ((next >= 'A' && next <= 'Z') || (next >= 'a' && next <= 'z'))
            {
                return index;
            }
        }

        return -1;
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
        var editionMatch = EditionTitleSuffixPattern.Match(suffix);
        var translatedSuffix = editionMatch.Success
            ? "第" + editionMatch.Groups["number"].Value + "版"
            : Translator.Translate(suffix);
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

        if (TryTranslateGeneratedDisplayNameState(source, route, out var generatedState))
        {
            return generatedState;
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

    private static bool TryTranslateGeneratedDisplayNameState(string source, string route, out string translated)
    {
        var timedMatch = TimedDisplayNameStatePattern.Match(source);
        if (timedMatch.Success)
        {
            return GeneratedDisplayNameStateTranslated(
                source,
                route,
                timedMatch.Groups["count"].Value + "秒",
                out translated);
        }

        var cookingServingsMatch = CookingServingsDisplayNameStatePattern.Match(source);
        if (cookingServingsMatch.Success)
        {
            return GeneratedDisplayNameStateTranslated(
                source,
                route,
                "調理" + cookingServingsMatch.Groups["count"].Value + "回分",
                out translated);
        }

        var energyCellsMatch = EnergyCellsDisplayNameStatePattern.Match(source);
        if (energyCellsMatch.Success)
        {
            return GeneratedDisplayNameStateTranslated(
                source,
                route,
                "セル" + energyCellsMatch.Groups["count"].Value + "個",
                out translated);
        }

        var chapterMatch = ChapterDisplayNameStatePattern.Match(source);
        if (chapterMatch.Success)
        {
            var translatedOwner = TranslateDisplayNameStateTarget(chapterMatch.Groups["owner"].Value, route);
            return GeneratedDisplayNameStateTranslated(source, route, translatedOwner + "支部", out translated);
        }

        var displayName = TranslateDisplayNameFragment(source, route);
        if (!string.Equals(displayName, source, StringComparison.Ordinal))
        {
            return GeneratedDisplayNameStateTranslated(source, route, displayName, out translated);
        }

        translated = source;
        return false;
    }

    private static bool GeneratedDisplayNameStateTranslated(
        string source,
        string route,
        string value,
        out string translated)
    {
        translated = value;
        DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedState", source, translated);
        return true;
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
        var state = match.Groups["state"].Value;
        if (state.Length > 0)
        {
            var translatedState = TranslateDisplayNameState(state, route);
            if (string.Equals(translatedState, state, StringComparison.Ordinal))
            {
                translated = source;
                return false;
            }

            translated += "、" + translatedState;
        }

        DynamicTextObservability.RecordTransform(route, "DisplayName.QuantifiedLiquidState", source, translated);
        return true;
    }

    private static bool TryTranslateLiquidState(string source, string route, out string translated)
    {
        var match = LiquidStatePattern.Match(source);
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

        var state = match.Groups["state"].Value;
        var translatedState = TranslateDisplayNameState(state, route);
        if (string.Equals(translatedState, state, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedLiquid + "、" + translatedState;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LiquidState", source, translated);
        return true;
    }

    private static bool TryTranslateGeneratedCanvasTentName(string source, string route, out string translated)
    {
        var match = GeneratedCanvasTentPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if (!TryTranslateGeneratedCanvasTentComponents(match.Groups["body"].Value, out var translatedCreature, out var translatedMaterial))
        {
            translated = source;
            return false;
        }

        var translatedTent = TranslateDisplayNameExactOrLowerAscii("tent", GeneratedCanvasTentComponentContext);
        if (translatedTent is null)
        {
            translated = source;
            return false;
        }

        translated = translatedCreature + "の" + translatedMaterial + "の" + translatedTent;
        DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedCanvasTent", source, translated);
        return true;
    }

    private static bool TryTranslateGeneratedCanvasTentComponents(
        string body,
        out string translatedCreature,
        out string translatedMaterial)
    {
        for (var splitIndex = body.LastIndexOf(' '); splitIndex > 0; splitIndex = body.LastIndexOf(' ', splitIndex - 1))
        {
            var creature = body.Substring(0, splitIndex);
            var material = body.Substring(splitIndex + 1);
            var creatureTranslation = TranslateDisplayNameComponentPhrase(creature, GeneratedCanvasTentComponentContext);
            var materialTranslation = TranslateDisplayNameComponentPhrase(material, GeneratedCanvasTentComponentContext);
            if (creatureTranslation is null || materialTranslation is null)
            {
                continue;
            }

            translatedCreature = creatureTranslation;
            translatedMaterial = materialTranslation;
            return true;
        }

        translatedCreature = string.Empty;
        translatedMaterial = string.Empty;
        return false;
    }

    private static bool TryTranslateGeneratedRandomStatueName(string source, string route, out string translated)
    {
        var match = GeneratedRandomStatuePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if (!TryTranslateRandomStatueMaterialPhrase(match.Groups["material"].Value, out var modifierPrefix, out var translatedMaterial))
        {
            translated = source;
            return false;
        }

        var translatedStatue = TranslateDisplayNameExactOrLowerAscii("statue", GeneratedRandomStatueComponentContext);
        if (translatedStatue is null)
        {
            translated = source;
            return false;
        }

        var subject = StringHelpers.StripLeadingEnglishArticle(
            match.Groups["subject"].Value,
            includeCapitalizedDefiniteArticle: true);
        var translatedSubject = TranslateDisplayNameFragment(subject, route);
        translated = modifierPrefix + translatedSubject + "の" + translatedMaterial + "の" + translatedStatue;
        DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedRandomStatue", source, translated);
        return true;
    }

    private static bool TryTranslateRandomStatueMaterialPhrase(
        string source,
        out string modifierPrefix,
        out string translatedMaterial)
    {
        modifierPrefix = string.Empty;
        translatedMaterial = string.Empty;

        var parts = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var material = parts[parts.Length - 1];
        var materialTranslation = TranslateDisplayNameComponentPhrase(material, GeneratedRandomStatueComponentContext);
        if (materialTranslation is null)
        {
            return false;
        }

        if (parts.Length == 1)
        {
            translatedMaterial = materialTranslation;
            return true;
        }

        var modifier = string.Join(" ", parts, 0, parts.Length - 1);
        var modifierTranslation = TranslateDisplayNameComponentPhrase(modifier, GeneratedRandomStatueComponentContext);
        if (modifierTranslation is null)
        {
            return false;
        }

        modifierPrefix = modifierTranslation;
        translatedMaterial = materialTranslation;
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
        var translatedTemplate = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            templateKey,
            DisplayNameStateTemplateContext,
            DisplayNameStateTemplateDictionaryFile);
        if (translatedTemplate is null)
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

    private static string RestoreVisibleSlice(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var sliceSpans = ColorCodePreserver.SliceSpans(spans, group.Index, group.Length);
        RemoveUnmatchedTrailingSliceClosers(sliceSpans);

        return ColorAwareTranslationComposer.Restore(
            group.Value,
            sliceSpans);
    }

    private static void RemoveUnmatchedTrailingSliceClosers(List<ColorSpan> spans)
    {
        var balance = 0;
        for (var index = 0; index < spans.Count; index++)
        {
            var token = spans[index].Token;
            if (IsExplicitClosingBoundaryToken(token))
            {
                balance--;
            }
            else if (ColorCodePreserver.IsOpeningBoundaryToken(token))
            {
                balance++;
            }
        }

        for (var index = spans.Count - 1; index >= 0 && balance < 0; index--)
        {
            if (!IsExplicitClosingBoundaryToken(spans[index].Token))
            {
                continue;
            }

            spans.RemoveAt(index);
            balance++;
        }
    }

    private static bool IsExplicitClosingBoundaryToken(string token)
    {
        return string.Equals(token, "}}", StringComparison.Ordinal)
            || string.Equals(token, "</color>", StringComparison.OrdinalIgnoreCase);
    }

    private static string RestoreWholeSlice(string translated, IReadOnlyList<ColorSpan> spans, Group group)
    {
        return ColorAwareTranslationComposer.RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            group.Index,
            group.Length);
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

        scoped = TryTranslateColoredLiquidToken(source);
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
                translatedPart = TryTranslateColoredLiquidToken(parts[index]);
            }

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

    private static string? TranslateDisplayNameComponentPhrase(string source, string context)
    {
        var scoped = TranslateDisplayNameExactOrLowerAscii(source, context);
        return scoped ?? TranslateAsciiPhrase(source);
    }

    private static string? TryTranslateColoredLiquidToken(string source)
    {
        if (!LooksLikeAsciiPhrase(source) || HasAsciiSpace(source))
        {
            return null;
        }

        return ScopedDictionaryLookup.TranslateExactOrLowerAscii("{{C|" + source + "}}", LiquidPhraseDictionaryFiles);
    }

    private static bool HasAsciiSpace(string source)
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
        return TranslateDisplayNameExactOrLowerAscii(source, context: null);
    }

    private static string? TranslateDisplayNameExactOrLowerAscii(string source, string? context)
    {
        var scoped = context is null
            ? TryTranslateDisplayNameScopedExact(source)
            : ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(source, context, DisplayNameDictionaryFiles);
        if (scoped is not null)
        {
            return scoped;
        }

        if (context is null)
        {
            return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
                ? translated
                : null;
        }

        return StringHelpers.TranslateExactOrLowerAscii(source, context);
    }

    private static string? TranslateDisplayNameModifier(string source)
    {
        var bracketWrapped = source.Length >= 2
            && source[0] == '['
            && source[source.Length - 1] == ']';
        var core = bracketWrapped
            ? source.Substring(1, source.Length - 2)
            : source;

        string? direct = null;
        if (TryTranslateMarkupWrappedDisplayNameModifier(core, out var wrappedDirect))
        {
            direct = wrappedDirect;
        }

        if (direct is null)
        {
            direct = TryTranslateDisplayNameScopedExact(core);
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

    private static bool TryTranslateMarkupWrappedDisplayNameModifier(string source, out string translated)
    {
        translated = source;
        if (!source.StartsWith("{{", StringComparison.Ordinal) || !source.EndsWith("}}", StringComparison.Ordinal))
        {
            return false;
        }

        var separator = source.IndexOf('|', 2);
        if (separator <= 2)
        {
            return false;
        }

        var tag = source.Substring(2, separator - 2);
        var visible = source.Substring(separator + 1, source.Length - separator - 3);
        if (visible.Length == 0)
        {
            return false;
        }

        var direct = TryTranslateDisplayNameScopedExact(visible);
        if (direct is null)
        {
            return false;
        }

        translated = ColorAwareTranslationComposer.HasColorMarkup(direct)
            ? direct
            : "{{" + tag + "|" + direct + "}}";
        return true;
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
