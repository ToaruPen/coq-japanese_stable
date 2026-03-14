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
    private static readonly string[] CharGenStackHints =
    {
        "CharacterCreation",
        "Embark",
        "Genotype",
        "Mutation",
        "Calling",
        "Cybernetics",
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
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var (stripped, spans) = ColorCodePreserver.Strip(source);
        var effectiveContext = ResolveObservabilityContext(context, stripped);
        using var _ = Translator.PushLogContext(effectiveContext);

        if (stripped.Length == 0)
        {
            return source!;
        }

        if (ShouldSkipTranslation(stripped, effectiveContext))
        {
            return source!;
        }

        var translated = Translator.Translate(stripped);
        return ColorCodePreserver.Restore(translated, spans);
    }

    internal static bool ShouldSkipTranslationForTests(string source)
    {
        return ShouldSkipTranslation(source, nameof(UITextSkinTranslationPatch));
    }

    internal static bool ShouldSkipTranslationForTests(string source, string? context)
    {
        return ShouldSkipTranslation(source, context);
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
        var translated = TranslatePreservingColors(current, context);
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
        return IsBracketedControlLabel(source)
            || IsShortcutPrefixedLabel(source)
            || IsVersionBuildString(source)
            || IsCompactStatBadge(source)
            || IsWhitespaceOnly(source)
            || IsUiPseudoGraphic(source)
            || string.Equals(source, "quit", StringComparison.Ordinal)
            || IsAlreadyLocalizedDirectRouteText(source, context)
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
        if (!string.Equals(context, nameof(MainMenuLocalizationPatch), StringComparison.Ordinal)
            && !string.Equals(context, nameof(CharGenLocalizationPatch), StringComparison.Ordinal))
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

        return true;
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

        var stackTrace = new StackTrace();
        var frames = stackTrace.GetFrames();
        if (frames is null || frames.Length == 0)
        {
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
            return context;
        }

        var trimmedTypeNames = new string[count];
        Array.Copy(typeNames, trimmedTypeNames, count);
        return source is null
            ? ResolveObservabilityContext(context, trimmedTypeNames)
            : ResolveObservabilityContext(context, trimmedTypeNames, source);
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
