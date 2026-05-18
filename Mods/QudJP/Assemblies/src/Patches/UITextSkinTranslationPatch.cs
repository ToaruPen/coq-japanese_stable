using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using QudJP;
#if HAS_TMP
using TMPro;
using UnityEngine;
#endif

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
    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnglishWordPattern =
        new Regex("[A-Za-z]{2,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AllowedLocalizedEnglishTokenPattern =
        new Regex("^(Caves|Qud|of|Mod|HP|AV|DV|XP|SP|MA|STR|AGI|TOU|INT|WIL|EGO|DEX|BURST|Tab|Esc|Enter|Space|Delete)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PointsRemainingPattern =
        new Regex("^Points Remaining:\\s*\\d+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StatHelpTextPattern =
        new Regex("^Your [A-Za-z]+ score determines", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CharGenBulletBlockPattern =
        new Regex("(^|\\n)ù ", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ActiveEffectsVisibleTitlePattern =
        new Regex("^発動中の効果 - (?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] DisplayNameWithClauseMarkers =
    {
        "beamsplitter",
        "filters",
        "suspensors",
        "cleats",
        "piping",
        "electromagnetic shielding",
        "gearbox",
        "co-processor",
        "quantum reverb",
        "terrifying visage",
        "serene visage",
    };
#pragma warning disable S1144, CA1823
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

    public static void Postfix(object? __instance, string text)
    {
#if HAS_TMP
        try
        {
            RepairActiveEffectsTitleText(__instance, text);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: UITextSkinTranslationPatch.Postfix failed: {0}", ex);
        }
#else
        _ = __instance;
        _ = text;
#endif
    }

    internal static bool ShouldRepairActiveEffectsTitleForTests(string? text)
    {
        return ShouldRepairActiveEffectsTitle(text);
    }

    internal static string BuildActiveEffectsTitleRtfForTests(string text)
    {
        return BuildActiveEffectsTitleRtf(text);
    }

#if HAS_TMP
    private static void RepairActiveEffectsTitleText(object? instance, string? text)
    {
        var component = instance as Component;
        if (!ShouldRepairActiveEffectsTitle(text) || component == null || string.IsNullOrEmpty(text))
        {
            return;
        }

        var tmp = component.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            return;
        }

        var richText = BuildActiveEffectsTitleRtf(text!);
        if (string.IsNullOrEmpty(richText))
        {
            return;
        }

        tmp.text = richText;
        SetStringFieldIfPresent(component, "formattedText", richText);
        SetStringFieldIfPresent(component, "lasttext", text!);
        tmp.richText = true;
        FontManager.ForcePrimaryFont(tmp);
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (tmp.maxVisibleCharacters <= 0)
        {
            tmp.maxVisibleCharacters = int.MaxValue;
        }

        if (tmp.maxVisibleLines <= 0)
        {
            tmp.maxVisibleLines = int.MaxValue;
        }

        if (tmp.pageToDisplay <= 0)
        {
            tmp.pageToDisplay = 1;
        }

        tmp.havePropertiesChanged = true;
        tmp.SetAllDirty();
        tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
#if QUDJP_DEV_BUILD
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] ActiveEffectsTitleTextRepair/v1: text='"
            + ColorAwareTranslationComposer.GetVisibleText(text!)
            + "' tmpText='"
            + tmp.text
            + "' chars="
            + tmp.textInfo.characterCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " pageCount="
            + tmp.textInfo.pageCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " font='"
            + (tmp.font == null ? string.Empty : tmp.font.name)
            + "'");
#endif
    }

    private static void SetStringFieldIfPresent(object instance, string fieldName, string value)
    {
#pragma warning disable S3011
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011
        if (field?.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
        }
    }
#endif

    private static bool ShouldRepairActiveEffectsTitle(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(text);
        return visible.StartsWith("発動中の効果 - ", StringComparison.Ordinal);
    }

    private static string BuildActiveEffectsTitleRtf(string text)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(text);
        var match = ActiveEffectsVisibleTitlePattern.Match(visible);
        if (!match.Success)
        {
            return string.Empty;
        }

        return "<color=#CFC041FF>発動中の効果</color><color=#40A4B9FF> - "
            + EscapeTmpRichText(match.Groups["name"].Value)
            + "</color>";
    }

    private static string EscapeTmpRichText(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            var sanitizedMarkedText = MessageFrameTranslator.StripAllDirectTranslationMarkers(markedText);
            FinalOutputObservability.RecordDirectMarker(
                nameof(UITextSkinTranslationPatch),
                context ?? string.Empty,
                FinalOutputObservability.DetailDirectMarker,
                source,
                sanitizedMarkedText);
            return sanitizedMarkedText;
        }

        var sanitizedSource = MessageFrameTranslator.StripAllDirectTranslationMarkers(source);
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(sanitizedSource);
        var effectiveContext = context;

        if (stripped.Length == 0)
        {
            return sanitizedSource;
        }

        if (IsIgnoredDirectRouteToken(stripped, effectiveContext))
        {
            FinalOutputObservability.RecordSkipped(
                nameof(UITextSkinTranslationPatch),
                effectiveContext ?? string.Empty,
                "IgnoredDirectRouteToken",
                sanitizedSource,
                stripped);
            return sanitizedSource;
        }

        if (TryTranslateDirectUiActionToken(stripped, effectiveContext, out var directActionTranslation))
        {
            var translated = ColorAwareTranslationComposer.Restore(directActionTranslation, spans);
            DynamicTextObservability.RecordTransform(
                nameof(UITextSkinTranslationPatch),
                "DirectUiActionToken",
                sanitizedSource,
                translated);
            return translated;
        }

        if (TryTranslatePickTargetUiText(sanitizedSource, stripped, effectiveContext, out var pickTargetTranslation))
        {
            DynamicTextObservability.RecordTransform(
                nameof(PickTargetWindowTextTranslator),
                "PickTarget.UiText",
                sanitizedSource,
                pickTargetTranslation);
            return pickTargetTranslation;
        }

        if (TryTranslateDisplayNameWithClauseUiText(source!, stripped, effectiveContext, out var displayNameTranslation))
        {
            DynamicTextObservability.RecordTransform(
                nameof(UITextSkinTranslationPatch),
                "DisplayName.WithClauseUiText",
                source!,
                displayNameTranslation);
            return displayNameTranslation;
        }

        var alreadyLocalized = IsAlreadyLocalizedDirectRouteText(stripped, effectiveContext);
        var shouldSkipTranslation = ShouldSkipTranslation(stripped, effectiveContext);
        if (alreadyLocalized)
        {
            FinalOutputObservability.RecordAlreadyLocalized(
                nameof(UITextSkinTranslationPatch),
                effectiveContext ?? string.Empty,
                sanitizedSource,
                stripped);
        }
        else if (shouldSkipTranslation)
        {
            FinalOutputObservability.RecordSkipped(
                nameof(UITextSkinTranslationPatch),
                effectiveContext ?? string.Empty,
                FinalOutputObservability.DetailSkipped,
                sanitizedSource,
                stripped);
        }
        else
        {
            SinkObservation.LogUnclaimed(
                nameof(UITextSkinTranslationPatch),
                effectiveContext ?? string.Empty,
                SinkObservation.ObservationOnlyDetail,
                sanitizedSource,
                stripped);
        }

        return sanitizedSource;
    }

    private static bool TryTranslateDirectUiActionToken(string source, string? context, out string translated)
    {
        translated = source;
        if (!string.Equals(context, nameof(UITextSkinTranslationPatch), StringComparison.Ordinal))
        {
            return false;
        }

        switch (source)
        {
            case "navigate":
            case "Navigate":
                translated = "移動";
                return true;
            case "select":
            case "Select":
                translated = "選択";
                return true;
            default:
                return false;
        }
    }

    private static bool TryTranslatePickTargetUiText(string source, string stripped, string? context, out string translated)
    {
        translated = source;
        if (!string.Equals(context, nameof(PickTargetWindowTextTranslator), StringComparison.Ordinal)
            && (!string.Equals(context, nameof(UITextSkinTranslationPatch), StringComparison.Ordinal)
                || !LooksLikePickTargetCommandBar(stripped)))
        {
            return false;
        }

        return PickTargetWindowTextTranslator.TryTranslateUiText(source, nameof(PickTargetWindowTextTranslator), out translated);
    }

    private static bool TryTranslateDisplayNameWithClauseUiText(
        string source,
        string stripped,
        string? context,
        out string translated)
    {
        translated = source;
        if (!string.Equals(context, nameof(UITextSkinTranslationPatch), StringComparison.Ordinal)
            || !LooksLikeKnownDisplayNameWithClause(stripped))
        {
            return false;
        }

        var displayNameTranslation = source;
        if (!DisplayNameSemanticPipeline.TryTranslateResult(
                ref displayNameTranslation,
                ObservabilityHelpers.ComposeContext(
                    nameof(GetDisplayNameProcessPatch),
                    "UITextSkin.WithClauseUiText")))
        {
            return false;
        }

        translated = displayNameTranslation;
        return true;
    }

    private static bool LooksLikeKnownDisplayNameWithClause(string source)
    {
        for (var index = 0; index < DisplayNameWithClauseMarkers.Length; index++)
        {
            if (StringHelpers.ContainsOrdinalIgnoreCase(source, " with " + DisplayNameWithClauseMarkers[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikePickTargetCommandBar(string source)
    {
        return source.Contains(" | ")
            && source.Contains("-select")
            && (source.Contains("Fire Missile Weapon")
                || source.Contains(" lock (")
                || source.Contains(" unlock ("));
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
        return GetDisplayNameRouteTranslator.IsAlreadyLocalizedDisplayNameText(source);
    }

    internal static bool IsAlreadyLocalizedDisplayNameStateText(string source)
    {
        return GetDisplayNameRouteTranslator.IsAlreadyLocalizedDisplayNameStateText(source);
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
        if (current is null)
        {
            return;
        }

        var translated = TranslatePreservingColors(current, context ?? nameof(UITextSkinTranslationPatch));
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

        if (string.Equals(context, nameof(FactionsLineTranslationPatch), StringComparison.Ordinal))
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

    private static bool IsDirectRouteAlreadyLocalizedContext(string? context)
    {
        var primaryContext = ObservabilityHelpers.ExtractPrimaryContext(context);
        return string.Equals(primaryContext, nameof(MainMenuLocalizationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(CharGenLocalizationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(CharacterStatusScreenTranslationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(OptionsLocalizationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(PickTargetWindowTextTranslator), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(ConversationDisplayTextPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(GetDisplayNamePatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(GetDisplayNameProcessPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(FactionsStatusScreenTranslationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(InventoryLocalizationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(PopupTranslationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(PopupMessageTranslationPatch), StringComparison.Ordinal)
            || string.Equals(primaryContext, nameof(QudMenuBottomContextTranslationPatch), StringComparison.Ordinal);
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
    internal static bool LooksLikeCommandHotkeyToken(string source)
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

    internal static string? TranslateAsciiTokenWithCaseFallback(string source)
    {
        return StringHelpers.TranslateExactOrLowerAscii(source);
    }
#pragma warning restore S1144

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

        if (ContainsHint(stackTypeNames, "Qud.UI.FactionsLine"))
        {
            return nameof(FactionsLineTranslationPatch);
        }

        if (ContainsHint(stackTypeNames, "Qud.UI.FactionsStatusScreen"))
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

        if (ContainsHint(stackTypeNames, "XRL.UI.PickTargetWindow"))
        {
            return nameof(PickTargetWindowTextTranslator);
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
