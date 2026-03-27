using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
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
            return markedText;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        var effectiveContext = context;

        if (stripped.Length == 0)
        {
            return source!;
        }

        if (IsIgnoredDirectRouteToken(stripped, effectiveContext))
        {
            return source!;
        }

        if (!IsAlreadyLocalizedDirectRouteText(stripped, effectiveContext)
            && !ShouldSkipTranslation(stripped, effectiveContext))
        {
            SinkObservation.LogUnclaimed(
                nameof(UITextSkinTranslationPatch),
                effectiveContext ?? string.Empty,
                SinkObservation.ObservationOnlyDetail,
                source!,
                stripped);
        }

        return source!;
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
