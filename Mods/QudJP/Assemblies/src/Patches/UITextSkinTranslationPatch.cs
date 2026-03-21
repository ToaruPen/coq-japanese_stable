using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class UITextSkinTranslationPatch
{
    private static readonly Regex CompactStatBadgePattern =
        new Regex("^[A-Z]{2,3}:\\s*\\d+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex VersionBuildPattern =
        new Regex("^\\d.*build", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ShortcutPrefixedLabelPattern =
        new Regex("^\\[[^\\]]+\\]\\s+.+$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HotkeySuffixedLabelPattern =
        new Regex("^.+\\n\\[[A-Z]\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DirectRouteControlTokenPattern =
        new Regex("^\\[(?:R|V|Delete|Esc|Space|| |■)\\](?:\\[-?\\d+\\])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DirectRoutePointTokenPattern =
        new Regex("^\\[-?\\d+pts\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DirectRoutePseudoGraphicPattern =
        new Regex("^>\\{\\{K\\|", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HpLinePattern =
        new Regex("^HP:\\s*(?<current>\\d+)\\s*/\\s*(?<max>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LevelExpLinePattern =
        new Regex("^LVL:\\s*(?<level>\\d+)\\s+Exp:\\s*(?<current>\\d+)\\s*/\\s*(?<next>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StrengthBonusCapPattern =
        new Regex("^Strength Bonus Cap:\\s*(?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WeaponClassPattern =
        new Regex("^Weapon Class:\\s*(?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnglishWordPattern =
        new Regex("[A-Za-z]{2,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AllowedLocalizedEnglishTokenPattern =
        new Regex("^(Caves|Qud|of|Mod|HP|AV|DV|XP|SP|MA|STR|AGI|TOU|INT|WIL|EGO|DEX|BURST)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PointsRemainingPattern =
        new Regex("^Points Remaining:\\s*\\d+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StatHelpTextPattern =
        new Regex("^Your [A-Za-z]+ score determines", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CharGenBulletBlockPattern =
        new Regex("(^|\\n)ù ", RegexOptions.CultureInvariant | RegexOptions.Compiled);
#pragma warning disable S1144, CA1823
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
    private static readonly ConcurrentDictionary<string, string?> ContextResolveCache =
        new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);
    private const int MaxContextCacheEntries = 2048;
    private static readonly string[] CharGenStackHints =
    {
        "CharacterCreation",
        "CharacterBuilds",
        "EmbarkBuilder",
        "EmbarkModule",
        "GenotypeModule",
        "MutationsModule",
        "CallingModule",
        "CyberneticsModule",
    };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("XRL.UI.UITextSkin:SetText", new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve UITextSkin.SetText(string). Patch will not apply.");
        }

        return method;
    }

    public static void Prefix(ref string text)
    {
        try
        {
            text = TranslatePreservingColors(text, nameof(UITextSkinTranslationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: UITextSkinTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    internal static string TranslatePreservingColors(string? source, string? context = null)
    {
        return TranslatePreservingColors(source, context, Array.Empty<string>());
    }

    internal static string TranslatePreservingColors(string? source, string? context, params string?[] contextDetails)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var effectiveContext = ResolveObservabilityContext(context, stripped);
        var detailedContext = ObservabilityHelpers.ComposeContext(effectiveContext, contextDetails);
        using var _ = Translator.PushLogContext(detailedContext);
        using var __ = Translator.PushMissingKeyLoggingSuppression(
            IsAlreadyLocalizedDirectRouteText(stripped, effectiveContext)
            || ShouldSuppressMissingKeyLogging(stripped, effectiveContext));

        if (stripped.Length == 0)
        {
            return source!;
        }

        if (IsIgnoredDirectRouteToken(stripped, effectiveContext))
        {
            return source!;
        }

        if (ShouldSkipTranslation(stripped, effectiveContext))
        {
            return source!;
        }

        var primaryRoute = ObservabilityHelpers.ExtractPrimaryContext(effectiveContext);

        if (TryTranslateDedicatedRouteText(stripped, primaryRoute, out var dedicatedRouteTranslation))
        {
            return ColorAwareTranslationComposer.Restore(dedicatedRouteTranslation, spans);
        }

        if (IsPopupTemplateContext(effectiveContext)
            && PopupTranslationPatch.TryTranslatePopupTemplate(stripped, out var popupTranslation))
        {
            return ColorAwareTranslationComposer.Restore(popupTranslation, spans);
        }

        if (IsUITextSinkTemplateContext(effectiveContext)
            && TryTranslateUITextSinkTemplate(stripped, primaryRoute, out var sinkTranslation))
        {
            return ColorAwareTranslationComposer.Restore(sinkTranslation, spans);
        }

        if (TryTranslateTrimmedLookup(stripped, primaryRoute, out var trimmedTranslation))
        {
            return ColorAwareTranslationComposer.Restore(trimmedTranslation, spans);
        }

        var translated = Translator.Translate(stripped);
        return ColorAwareTranslationComposer.Restore(translated, spans);
    }

    internal static bool ShouldSkipTranslationForTests(string source)
    {
        return ShouldSkipTranslation(source, nameof(UITextSkinTranslationPatch));
    }

    internal static bool ShouldSkipTranslationForTests(string source, string? context)
    {
        return ShouldSkipTranslation(source, context);
    }

    internal static bool IsAlreadyLocalizedDirectRouteTextForContext(string source, string? context)
    {
        return IsAlreadyLocalizedDirectRouteText(source, context);
    }

    internal static bool IsAlreadyLocalizedDisplayNameText(string source, string? context)
    {
        _ = context;
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

    internal static string? ResolveObservabilityContextForTests(string? context, params string[] stackTypeNames)
    {
        return ResolveObservabilityContext(context, stackTypeNames);
    }

    internal static string? ResolveObservabilityContextForTests(string? context, string source, params string[] stackTypeNames)
    {
        return ResolveObservabilityContext(context, stackTypeNames, source);
    }

    internal static void TranslateStringField(object? instance, string fieldName, string? context = null)
    {
        if (instance is null || string.IsNullOrEmpty(fieldName))
        {
            return;
        }

        var field = AccessTools.Field(instance.GetType(), fieldName);
        if (field is null || field.FieldType != typeof(string))
        {
            return;
        }

        var current = field.GetValue(instance) as string;
        var translated = TranslatePreservingColors(
            current,
            context,
            $"itemType={instance.GetType().Name}",
            $"field={fieldName}");
        if (!string.Equals(current, translated, StringComparison.Ordinal))
        {
            field.SetValue(instance, translated);
        }
    }

    internal static void TranslateStringFieldsInCollection(object? maybeCollection, string? context = null, params string[] fieldNames)
    {
        if (maybeCollection is null || maybeCollection is string || fieldNames is null || fieldNames.Length == 0)
        {
            return;
        }

        if (maybeCollection is not IEnumerable enumerable)
        {
            return;
        }

        foreach (var item in enumerable)
        {
            if (item is null)
            {
                continue;
            }

            for (var index = 0; index < fieldNames.Length; index++)
            {
                TranslateStringField(item, fieldNames[index], context);
            }
        }
    }

    private static bool ShouldSkipTranslation(string source, string? context)
    {
        if (IsWhitespaceOnly(source))
        {
            return true;
        }

        if (!string.Equals(context, nameof(UITextSkinTranslationPatch), StringComparison.Ordinal))
        {
            return false;
        }

        return IsBracketedControlLabel(source)
            || IsShortcutPrefixedLabel(source)
            || IsVersionBuildString(source)
            || IsCompactStatBadge(source)
            || IsUiPseudoGraphic(source)
            || string.Equals(source, "quit", StringComparison.Ordinal)
            || IsAlreadyLocalizedUITextSinkText(source, context);
    }

    private static bool IsBracketedControlLabel(string source)
    {
        return source.Length >= 3
            && source[0] == '['
            && source[source.Length - 1] == ']';
    }

    private static bool IsShortcutPrefixedLabel(string source)
    {
        return ShortcutPrefixedLabelPattern.IsMatch(source);
    }

    private static bool IsVersionBuildString(string source)
    {
        return VersionBuildPattern.IsMatch(source);
    }

    private static bool IsCompactStatBadge(string source)
    {
        return CompactStatBadgePattern.IsMatch(source);
    }

    private static bool IsAlreadyLocalizedUITextSinkText(string source, string? context)
    {
        if (!string.Equals(context, nameof(UITextSkinTranslationPatch), StringComparison.Ordinal))
        {
            return false;
        }

        return JapaneseCharacterPattern.IsMatch(source)
            || HotkeySuffixedLabelPattern.IsMatch(source);
    }

    private static bool IsAlreadyLocalizedDirectRouteText(string source, string? context)
    {
        if (!IsDirectRouteAlreadyLocalizedContext(context))
        {
            return false;
        }

        if (!JapaneseCharacterPattern.IsMatch(source))
        {
            return false;
        }

        var matches = EnglishWordPattern.Matches(source);
        for (var index = 0; index < matches.Count; index++)
        {
            if (!AllowedLocalizedEnglishTokenPattern.IsMatch(matches[index].Value))
            {
                return false;
            }
        }

        return !IsStrictDirectRouteContext(context) || !HasDirectRouteDynamicMarkers(source);
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

    private static bool ShouldSuppressMissingKeyLogging(string source, string? context)
    {
        var primaryContext = ObservabilityHelpers.ExtractPrimaryContext(context);
        return string.Equals(primaryContext, nameof(CharacterStatusScreenTranslationPatch), StringComparison.Ordinal)
            && CharacterStatusScreenTextTranslator.ShouldSuppressMissingKeyLogging(source);
    }

    private static bool IsDirectRouteAlreadyLocalizedContext(string? context)
    {
        var primaryContext = ObservabilityHelpers.ExtractPrimaryContext(context);
        return string.Equals(primaryContext, nameof(MainMenuLocalizationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(CharGenLocalizationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(CharacterStatusScreenTranslationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(OptionsLocalizationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(ConversationDisplayTextPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(GetDisplayNamePatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(GetDisplayNameProcessPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(FactionsStatusScreenTranslationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(InventoryLocalizationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(PopupTranslationPatch), StringComparison.Ordinal);
    }

    private static bool IsStrictDirectRouteContext(string? context)
    {
        var primaryContext = ObservabilityHelpers.ExtractPrimaryContext(context);
        return string.Equals(primaryContext, nameof(GetDisplayNamePatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(GetDisplayNameProcessPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(FactionsStatusScreenTranslationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(InventoryLocalizationPatch), StringComparison.Ordinal);
    }

    private static bool IsIgnoredDirectRouteToken(string source, string? context)
    {
        var primaryContext = ObservabilityHelpers.ExtractPrimaryContext(context);
        if (!string.Equals(primaryContext, nameof(CharGenLocalizationPatch), StringComparison.Ordinal)
            && !string.Equals(primaryContext, nameof(MainMenuLocalizationPatch), StringComparison.Ordinal))
        {
            return false;
        }

        return DirectRouteControlTokenPattern.IsMatch(source)
            || DirectRoutePointTokenPattern.IsMatch(source)
            || DirectRoutePseudoGraphicPattern.IsMatch(source);
    }

#pragma warning disable S1144
    private static bool IsPopupTemplateContext(string? context)
    {
        return string.Equals(ObservabilityHelpers.ExtractPrimaryContext(context), nameof(PopupTranslationPatch), StringComparison.Ordinal);
    }

    private static bool TryTranslateDedicatedRouteText(string source, string route, out string translated)
    {
        if (string.Equals(route, nameof(FactionsStatusScreenTranslationPatch), StringComparison.Ordinal))
        {
            if (FactionsStatusScreenTranslationPatch.IsAlreadyLocalizedFactionText(source))
            {
                translated = source;
                return true;
            }

            if (FactionsStatusScreenTranslationPatch.TryTranslateFactionText(source, route, out translated))
            {
                return true;
            }
        }

        if (string.Equals(route, nameof(CharacterStatusScreenTranslationPatch), StringComparison.Ordinal)
            && CharacterStatusScreenTextTranslator.TryTranslateUiText(source, route, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateDisplayNameRouteText(string source, string route, out string translated)
    {
        _ = route;
        translated = source;
        return false;
    }

    private static bool IsUITextSinkTemplateContext(string? context)
    {
        return string.Equals(ObservabilityHelpers.ExtractPrimaryContext(context), nameof(UITextSkinTranslationPatch), StringComparison.Ordinal);
    }

    private static bool TryTranslateUITextSinkTemplate(string source, string route, out string translated)
    {
        if (SkillsAndPowersStatusScreenTranslationPatch.TryTranslateText(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateExactUITextSinkLookup(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateCompareStatusLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateStatusLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateLevelExpLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateHpLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateCommaSeparatedAsciiSequence(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateSpaceSeparatedAsciiSequence(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateCommandBar(source, route, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateExactUITextSinkLookup(string source, string route, out string translated)
    {
        if (Translator.TryGetTranslation(source, out var direct)
            && !string.Equals(direct, source, StringComparison.Ordinal))
        {
            translated = direct;
            DynamicTextObservability.RecordTransform(route, "UITextSink.ExactLookup", source, translated);
            return true;
        }

        var lower = LowerAscii(source);
        if (!string.Equals(lower, source, StringComparison.Ordinal)
            && Translator.TryGetTranslation(lower, out var lowered)
            && !string.Equals(lowered, lower, StringComparison.Ordinal))
        {
            translated = lowered;
            DynamicTextObservability.RecordTransform(route, "UITextSink.ExactLookup", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCompareStatusLine(string source, string route, out string translated)
    {
        var strengthMatch = StrengthBonusCapPattern.Match(source);
        if (strengthMatch.Success)
        {
            var translatedPrefix = TranslateAsciiTokenWithCaseFallback("Strength Bonus Cap:");
            if (translatedPrefix is not null)
            {
                var translatedValue = TranslateAsciiTokenWithCaseFallback(strengthMatch.Groups["value"].Value);
                if (translatedValue is null)
                {
                    translatedValue = strengthMatch.Groups["value"].Value;
                }

                translated = translatedPrefix + " " + translatedValue;
                DynamicTextObservability.RecordTransform(route, "UITextSink.CompareStatus", source, translated);
                return true;
            }
        }

        var weaponClassMatch = WeaponClassPattern.Match(source);
        if (weaponClassMatch.Success)
        {
            var translatedPrefix = TranslateAsciiTokenWithCaseFallback("Weapon Class:");
            if (translatedPrefix is not null)
            {
                var translatedValue = TranslateAsciiTokenWithCaseFallback(weaponClassMatch.Groups["value"].Value);
                if (translatedValue is null)
                {
                    translatedValue = weaponClassMatch.Groups["value"].Value;
                }

                translated = translatedPrefix + " " + translatedValue;
                DynamicTextObservability.RecordTransform(route, "UITextSink.CompareStatus", source, translated);
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateStatusLine(string source, string route, out string translated)
    {
        const string activeEffectsPrefix = "ACTIVE EFFECTS:";
        if (!source.StartsWith(activeEffectsPrefix, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var status = Translator.Translate(activeEffectsPrefix);
        if (string.Equals(status, activeEffectsPrefix, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var tail = source.Substring(activeEffectsPrefix.Length).Trim();
        if (tail.Length == 0)
        {
            translated = status;
            DynamicTextObservability.RecordTransform(route, "UITextSink.StatusLine", source, translated);
            return true;
        }

        var parts = tail.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
        var translatedParts = new string[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            var translatedPart = TranslateAsciiTokenWithCaseFallback(parts[index]);
            if (translatedPart is null)
            {
                translated = source;
                return false;
            }

            translatedParts[index] = translatedPart;
        }

        translated = status + " " + string.Join("、", translatedParts);
        DynamicTextObservability.RecordTransform(route, "UITextSink.StatusLine", source, translated);
        return true;
    }

    private static bool TryTranslateLevelExpLine(string source, string route, out string translated)
    {
        var match = LevelExpLinePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var levelLabel = Translator.Translate("LVL");
        var experienceLabel = Translator.Translate("Exp");
        if (string.Equals(levelLabel, "LVL", StringComparison.Ordinal)
            || string.Equals(experienceLabel, "Exp", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated =
            $"{levelLabel}: {match.Groups["level"].Value} {experienceLabel}: {match.Groups["current"].Value} / {match.Groups["next"].Value}";
        DynamicTextObservability.RecordTransform(route, "UITextSink.LevelExp", source, translated);
        return true;
    }

    private static bool TryTranslateHpLine(string source, string route, out string translated)
    {
        var match = HpLinePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var hpLabel = Translator.Translate("HP");
        if (string.Equals(hpLabel, "HP", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = $"{hpLabel}: {match.Groups["current"].Value} / {match.Groups["max"].Value}";
        DynamicTextObservability.RecordTransform(route, "UITextSink.HpLine", source, translated);
        return true;
    }

    private static bool TryTranslateCommaSeparatedAsciiSequence(string source, string route, out string translated)
    {
        translated = source;
        if (source.IndexOf(", ", StringComparison.Ordinal) < 0)
        {
            return false;
        }

        var parts = source.Split(new[] { ", " }, StringSplitOptions.None);
        return TryTranslateAsciiSequence(parts, "、", route, "UITextSink.CommaSequence", source, out translated);
    }

    private static bool TryTranslateSpaceSeparatedAsciiSequence(string source, string route, out string translated)
    {
        translated = source;
        if (source.IndexOf(' ') < 0)
        {
            return false;
        }

        var parts = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        return TryTranslateAsciiSequence(parts, " ", route, "UITextSink.SpaceSequence", source, out translated);
    }

    private static bool TryTranslateAsciiSequence(string[] parts, string separator, string route, string family, string source, out string translated)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < parts.Length; index++)
        {
            var translatedPart = TranslateAsciiTokenWithCaseFallback(parts[index]);
            if (translatedPart is null)
            {
                translated = string.Join(separator, parts);
                return false;
            }

            if (index > 0)
            {
                builder.Append(separator);
            }

            builder.Append(translatedPart);
        }

        translated = builder.ToString();
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
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
        DynamicTextObservability.RecordTransform(route, "UITextSink.CommandBar", source, translated);
        return true;
    }

    private static bool TryTranslateCommandBarSegment(string source, out string translated)
    {
        var direct = TranslateAsciiTokenWithCaseFallback(source);
        if (direct is not null)
        {
            translated = direct;
            return true;
        }

        if (LooksLikeCommandHotkeyToken(source))
        {
            translated = source;
            return true;
        }

        var parenthesizedHotkeyMatch = Regex.Match(source, "^\\((?<hotkey>[^)]+)\\)\\s+(?<label>.+)$", RegexOptions.CultureInvariant);
        if (parenthesizedHotkeyMatch.Success)
        {
            var translatedLabel = TranslateAsciiTokenWithCaseFallback(parenthesizedHotkeyMatch.Groups["label"].Value);
            if (translatedLabel is not null)
            {
                translated = $"({parenthesizedHotkeyMatch.Groups["hotkey"].Value}) {translatedLabel}";
                return true;
            }
        }

        var hotkeyPrefixMatch = Regex.Match(source, "^(?<hotkey>\\S+)\\s+(?<label>.+)$", RegexOptions.CultureInvariant);
        if (hotkeyPrefixMatch.Success)
        {
            var translatedLabel = TranslateAsciiTokenWithCaseFallback(hotkeyPrefixMatch.Groups["label"].Value);
            if (translatedLabel is not null)
            {
                translated = $"{hotkeyPrefixMatch.Groups["hotkey"].Value} {translatedLabel}";
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool LooksLikeCommandHotkeyToken(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9')
                || character == '('
                || character == ')')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static string? TranslateAsciiTokenWithCaseFallback(string source)
    {
        var direct = Translator.Translate(source);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        var lower = LowerAscii(source);
        if (!string.Equals(lower, source, StringComparison.Ordinal))
        {
            var lowered = Translator.Translate(lower);
            if (!string.Equals(lowered, lower, StringComparison.Ordinal))
            {
                return lowered;
            }
        }

        return null;
    }

    private static string LowerAscii(string source)
    {
        var buffer = source.ToCharArray();
        var changed = false;
        for (var index = 0; index < buffer.Length; index++)
        {
            var character = buffer[index];
            if (character < 'A' || character > 'Z')
            {
                continue;
            }

            buffer[index] = (char)(character + ('a' - 'A'));
            changed = true;
        }

        return changed ? new string(buffer) : source;
    }
#pragma warning restore S1144

    private static bool TryTranslateTrimmedLookup(string source, string route, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();
        if (trimmed.Length == 0 || trimmed.Length == source.Length)
        {
            return false;
        }

        var trimmedTranslation = Translator.Translate(trimmed);
        if (string.Equals(trimmedTranslation, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
#pragma warning disable CA1845 // net48 target keeps simple string APIs here
        translated =
            source.Substring(0, leadingLength) +
            trimmedTranslation +
            source.Substring(source.Length - trailingLength, trailingLength);
#pragma warning restore CA1845
        DynamicTextObservability.RecordTransform(route, "TrimmedLookup", source, translated);
        return true;
    }

    private static bool HasDirectRouteDynamicMarkers(string source)
    {
        if (LooksLikeLocalizedBracketLabel(source))
        {
            return false;
        }

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (char.IsDigit(character)
                || char.IsControl(character)
                || character == '['
                || character == ']'
                || character == ':'
                || character == '	')
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeLocalizedBracketLabel(string source)
    {
        if (!source.StartsWith("[", StringComparison.Ordinal) || !source.EndsWith("]", StringComparison.Ordinal))
        {
            return false;
        }

        var hasJapanese = false;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (char.IsDigit(character)
                || (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z'))
            {
                return false;
            }

            if ((character >= '\u3040' && character <= '\u30ff')
                || (character >= '\u3400' && character <= '\u4dbf')
                || (character >= '\u4e00' && character <= '\u9fff')
                || (character >= '\uf900' && character <= '\ufaff'))
            {
                hasJapanese = true;
            }
        }

        return hasJapanese;
    }

    private static bool IsWhitespaceOnly(string source)
    {
        return source.Trim().Length == 0;
    }

    private static bool IsUiPseudoGraphic(string source)
    {
        var trimmed = source.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        var hasGraphicMarker = false;
        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];
            if (char.IsDigit(character))
            {
                return false;
            }

            if (character == '■' || character == '.' || character == '>' || character == '<')
            {
                hasGraphicMarker = true;
            }
        }

        return hasGraphicMarker && !EnglishWordPattern.IsMatch(trimmed) && !JapaneseCharacterPattern.IsMatch(trimmed);
    }

    private static string? ResolveObservabilityContext(string? context, string? source)
    {
        if (!string.Equals(context, nameof(UITextSkinTranslationPatch), StringComparison.Ordinal))
        {
            return context;
        }

        var cacheKey = source ?? string.Empty;
        if (ContextResolveCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var resolved = ResolveObservabilityContextFromStack(context, source);
        if (ContextResolveCache.Count < MaxContextCacheEntries)
        {
            ContextResolveCache.TryAdd(cacheKey, resolved);
        }

        return resolved;
    }

    private static string? ResolveObservabilityContext(string? context, string[] stackTypeNames)
    {
        if (!string.Equals(context, nameof(UITextSkinTranslationPatch), StringComparison.Ordinal))
        {
            return context;
        }

        if (ContainsAnyHint(stackTypeNames, CharGenStackHints))
        {
            return nameof(CharGenLocalizationPatch);
        }

        if (ContainsHint(stackTypeNames, "Qud.UI.CharacterStatusScreen")
            || ContainsHint(stackTypeNames, "Qud.UI.CharacterMutationLine")
            || ContainsHint(stackTypeNames, "Qud.UI.CharacterAttributeLine"))
        {
            return nameof(CharacterStatusScreenTranslationPatch);
        }

        if (ContainsHint(stackTypeNames, "Qud.UI.FactionsStatusScreen")
            || ContainsHint(stackTypeNames, "Qud.UI.FactionsLine"))
        {
            return nameof(FactionsStatusScreenTranslationPatch);
        }

        if (ContainsHint(stackTypeNames, "Qud.UI.MainMenu"))
        {
            return nameof(MainMenuLocalizationPatch);
        }

        if (ContainsHint(stackTypeNames, "Qud.UI.OptionsScreen"))
        {
            return nameof(OptionsLocalizationPatch);
        }

        if (ContainsHint(stackTypeNames, "Qud.UI.Popup") || ContainsHint(stackTypeNames, "XRL.UI.Popup"))
        {
            return nameof(PopupTranslationPatch);
        }

        return context;
    }

    private static string? ResolveObservabilityContext(string? context, string[] stackTypeNames, string source)
    {
        var resolvedContext = ResolveObservabilityContext(context, stackTypeNames);
        if (!string.Equals(resolvedContext, nameof(UITextSkinTranslationPatch), StringComparison.Ordinal))
        {
            return resolvedContext;
        }

        return LooksLikeCharGenSinkText(source, stackTypeNames)
            ? nameof(CharGenLocalizationPatch)
            : resolvedContext;
    }

    private static string? ResolveObservabilityContextFromStack(string? context, string? source)
    {
        var stackTrace = new StackTrace();
        var frames = stackTrace.GetFrames();
        if (frames is null || frames.Length == 0)
        {
            Trace.TraceWarning(
                "QudJP: ResolveObservabilityContext: no stack frames available for context '{0}'.",
                context);
            return context;
        }

        var typeNames = new string[frames.Length];
        var count = 0;
        for (var index = 0; index < frames.Length; index++)
        {
            var typeName = frames[index].GetMethod()?.DeclaringType?.FullName;
            if (string.IsNullOrEmpty(typeName))
            {
                continue;
            }

            typeNames[count] = typeName!;
            count++;
        }

        if (count == 0)
        {
            Trace.TraceWarning(
                "QudJP: ResolveObservabilityContext: all {0} stack frames had null/empty declaring types for context '{1}'.",
                frames.Length,
                context);
            return context;
        }

        var trimmedTypeNames = new string[count];
        Array.Copy(typeNames, trimmedTypeNames, count);
        return source is null
            ? ResolveObservabilityContext(context, trimmedTypeNames)
            : ResolveObservabilityContext(context, trimmedTypeNames, source);
    }

    private static bool ContainsAnyHint(string[] stackTypeNames, string[] hints)
    {
        for (var hintIndex = 0; hintIndex < hints.Length; hintIndex++)
        {
            if (ContainsHint(stackTypeNames, hints[hintIndex]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeCharGenSinkText(string? source, string[] stackTypeNames)
    {
        if (source is not null
            && (PointsRemainingPattern.IsMatch(source)
                || StatHelpTextPattern.IsMatch(source)
                || CharGenBulletBlockPattern.IsMatch(source)))
        {
            return true;
        }

        return ContainsAnyHint(stackTypeNames, CharGenStackHints);
    }

    private static bool ContainsHint(string[] stackTypeNames, string hint)
    {
        for (var index = 0; index < stackTypeNames.Length; index++)
        {
            if (StringHelpers.ContainsOrdinalIgnoreCase(stackTypeNames[index], hint))
            {
                return true;
            }
        }

        return false;
    }

}
#pragma warning restore S1144, CA1823
