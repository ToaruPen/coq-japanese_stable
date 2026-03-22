using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupTranslationPatch
{
    private const string TargetTypeName = "XRL.UI.Popup";
    private static readonly Regex AttackPromptWithArticlePattern =
        new Regex("^Do you really want to attack the (?<target>.+)\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AttackPromptPattern =
        new Regex("^Do you really want to attack (?<target>.+)\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RefusesToSpeakPattern =
        new Regex("^(?:The )?(?<target>.+) refuses to speak to you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HostilityPromptPattern =
        new Regex("^You sense only hostility from (?:the )?(?<target>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DeleteSavePromptPattern =
        new Regex("^Are you sure you want to delete the save game for (?<target>.+)\\?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DeleteTitlePattern =
        new Regex("^Delete (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HotkeyLabelPattern =
        new Regex("^\\[(?<hotkey>[^\\]]+)\\]\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlainHotkeyLabelPattern =
        new Regex("^(?<hotkey>Enter|Esc|Tab|Space|space)\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StyledHotkeyLabelPattern =
        new Regex("^(?<prefix>.+?)(?<labelOpen>\\{\\{[^|]+\\|)(?<label>[^{}]+)(?<labelClose>\\}\\})$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex NumberedConversationChoicePattern =
        new Regex("^\\[(?<index>\\d+)\\]\\s+(?<text>.+?)(?:\\s+\\[[^\\]]+\\])?\\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex AbandonPromptPattern =
        new Regex(
            "^If you quit without saving, you will lose all your progress\\s+and your character will be lost\\. Are you sure you want\\s+to QUIT and LOSE YOUR PROGRESS\\?\\s+Type 'ABANDON' to confirm\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    // ShowBlock parameter count for game version 2.0.4
    private const int ShowBlockParameterCount = 8;

    // ShowOptionList parameter count for game version 2.0.4
    private const int ShowOptionListParameterCount = 19;

    private const int ShowConversationParameterCount = 7;

    // ShowOptionList argument indices (game version 2.0.4)
    private const int ShowOptionListIntroIndex = 4;
    private const int ShowOptionListSpacingTextIndex = 9;
    private const int ShowOptionListButtonsIndex = 14;

    private const int ShowConversationTitleIndex = 0;
    private const int ShowConversationIntroIndex = 2;
    private const int ShowConversationOptionsIndex = 3;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var showBlock = FindMethod(methodName: "ShowBlock", parameterCount: ShowBlockParameterCount);
        if (showBlock is not null)
        {
            yield return showBlock;
        }

        var showOptionList = FindMethod(methodName: "ShowOptionList", parameterCount: ShowOptionListParameterCount);
        if (showOptionList is not null)
        {
            yield return showOptionList;
        }

        var showConversation = FindMethod(methodName: "ShowConversation", parameterCount: ShowConversationParameterCount);
        if (showConversation is not null)
        {
            yield return showConversation;
        }
    }

    public static void Prefix(MethodBase __originalMethod, object[] __args)
    {
        try
        {
            if (__originalMethod is null || __args is null)
            {
                Trace.TraceError("QudJP: PopupTranslationPatch.Prefix received null originalMethod or args.");
                return;
            }

            if (__originalMethod.Name == "ShowBlock")
            {
                TranslateShowBlockArgs(__args);
                return;
            }

            if (__originalMethod.Name == "ShowOptionList")
            {
                TranslateShowOptionListArgs(__args);
                return;
            }

            if (__originalMethod.Name == "ShowConversation")
            {
                TranslateShowConversationArgs(__args);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PopupTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    private static void TranslateShowBlockArgs(object[] args)
    {
        TranslateStringArg(args, index: 0);
        TranslateStringArg(args, index: 1);
    }

    private static void TranslateShowOptionListArgs(object[] args)
    {
        TranslateStringArg(args, index: 0);
        TranslateStringListArg(args, index: 1);
        TranslateStringArg(args, index: ShowOptionListIntroIndex);
        TranslateStringArg(args, index: ShowOptionListSpacingTextIndex);

        if (args.Length > ShowOptionListButtonsIndex)
        {
            TranslatePopupMenuItemTextCollection(args[ShowOptionListButtonsIndex]);
        }
    }

    private static void TranslateShowConversationArgs(object[] args)
    {
        TranslateStringArg(args, index: ShowConversationTitleIndex);
        TranslateStringArg(args, index: ShowConversationIntroIndex);
        TranslateStringListArg(args, index: ShowConversationOptionsIndex);
    }

    private static void TranslateStringArg(object[] args, int index)
    {
        if (index < 0 || index >= args.Length)
        {
            return;
        }

        if (args[index] is string text)
        {
            args[index] = TranslatePopupText(text);
        }
    }

    private static void TranslateStringListArg(object[] args, int index)
    {
        if (index < 0 || index >= args.Length)
        {
            return;
        }

        if (args[index] is null || args[index] is string || args[index] is not IEnumerable enumerable)
        {
            return;
        }

        var translated = new List<string>();
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                translated.Add(string.Empty);
                continue;
            }

            if (item is not string text)
            {
                return;
            }

            translated.Add(TranslatePopupText(text));
        }

        args[index] = translated;
    }

    internal static bool TryTranslatePopupTemplate(string source, out string translated)
    {
        if (TryTranslateAttackPrompt(source, out var candidate))
        {
            translated = candidate;
            return true;
        }

        if (TryTranslateRefusesToSpeak(source, out candidate))
        {
            translated = candidate;
            return true;
        }

        if (TryTranslateHostilityPrompt(source, out candidate))
        {
            translated = candidate;
            return true;
        }

        if (TryTranslateDeleteSavePrompt(source, out candidate))
        {
            translated = candidate;
            return true;
        }

        if (TryTranslateDeleteTitle(source, out candidate))
        {
            translated = candidate;
            return true;
        }

        if (TryTranslateHotkeyLabel(source, out candidate))
        {
            translated = candidate;
            return true;
        }

        if (TryTranslateNumberedConversationChoice(source, out candidate))
        {
            translated = candidate;
            return true;
        }

        if (DeathWrapperFamilyTranslator.TryTranslatePopup(source, out candidate))
        {
            translated = candidate;
            return true;
        }

        if (TryTranslateAbandonPrompt(source, out candidate))
        {
            translated = candidate;
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslatePopupText(string source)
    {
        if (IsAlreadyLocalizedPopupText(source))
        {
            return source;
        }

        var popupMenuItemText = TranslatePopupMenuItemText(source);
        if (!string.Equals(popupMenuItemText, source, StringComparison.Ordinal))
        {
            return popupMenuItemText;
        }

        if (TryTranslatePopupTemplate(source, out var translated))
        {
            return translated;
        }

        var exact = TranslateLabelWithCaseFallback(source);
        if (exact is not null)
        {
            return exact;
        }

        var patternTranslated = MessagePatternTranslator.Translate(source, nameof(PopupTranslationPatch));
        if (!string.Equals(patternTranslated, source, StringComparison.Ordinal))
        {
            return patternTranslated;
        }

        return UITextSkinTranslationPatch.TranslatePreservingColors(source, nameof(PopupTranslationPatch));
    }

    internal static string TranslatePopupMenuItemText(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        if (IsAlreadyLocalizedPopupText(source))
        {
            return source;
        }

        if (TryTranslateStyledHotkeyLabel(source, out var translated))
        {
            return translated;
        }

        return source;
    }

    internal static bool IsAlreadyLocalizedPopupText(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.Length == 0)
        {
            return true;
        }

        if (UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
                stripped,
                nameof(PopupTranslationPatch)))
        {
            return true;
        }

        var numberedChoice = NumberedConversationChoicePattern.Match(stripped);
        if (numberedChoice.Success)
        {
            return UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
                numberedChoice.Groups["text"].Value.TrimEnd(),
                nameof(PopupTranslationPatch));
        }

        var hotkeyMatch = HotkeyLabelPattern.Match(stripped);
        if (hotkeyMatch.Success && !int.TryParse(hotkeyMatch.Groups["hotkey"].Value, out _))
        {
            return UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
                hotkeyMatch.Groups["label"].Value,
                nameof(PopupTranslationPatch));
        }

        var plainHotkeyMatch = PlainHotkeyLabelPattern.Match(stripped);
        if (!plainHotkeyMatch.Success)
        {
            return false;
        }

        return UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
            plainHotkeyMatch.Groups["label"].Value,
            nameof(PopupTranslationPatch));
    }

    private static bool TryTranslateAttackPrompt(string source, out string translated)
    {
        var matchedWithArticle = true;
        var translatedTemplate = Translator.Translate("Do you really want to attack the {0}?");
        var match = AttackPromptWithArticlePattern.Match(source);
        if (!match.Success)
        {
            matchedWithArticle = false;
            translatedTemplate = Translator.Translate("Do you really want to attack {0}?");
            match = AttackPromptPattern.Match(source);
        }

        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if ((!matchedWithArticle && string.Equals(translatedTemplate, "Do you really want to attack {0}?", StringComparison.Ordinal))
            || (matchedWithArticle && string.Equals(translatedTemplate, "Do you really want to attack the {0}?", StringComparison.Ordinal)))
        {
            translated = source;
            return false;
        }

        var target = TranslatePopupEntityReference(match.Groups["target"].Value);

        translated = translatedTemplate.Replace("{0}", target);
        DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "AttackPrompt", source, translated);
        return true;
    }

    private static bool TryTranslateHostilityPrompt(string source, out string translated)
    {
        var match = HostilityPromptPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedTemplate = Translator.Translate("You sense only hostility from {0}.");
        if (string.Equals(translatedTemplate, "You sense only hostility from {0}.", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var target = TranslatePopupEntityReference(match.Groups["target"].Value);

        translated = translatedTemplate.Replace("{0}", target);
        DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "HostilityPrompt", source, translated);
        return true;
    }

    private static bool TryTranslateRefusesToSpeak(string source, out string translated)
    {
        var match = RefusesToSpeakPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedTemplate = Translator.Translate("The {0} refuses to speak to you.");
        if (string.Equals(translatedTemplate, "The {0} refuses to speak to you.", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var target = TranslatePopupEntityReference(match.Groups["target"].Value);

        translated = translatedTemplate.Replace("{0}", target);
        DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "RefusesToSpeak", source, translated);
        return true;
    }

    private static bool TryTranslateDeleteSavePrompt(string source, out string translated)
    {
        var match = DeleteSavePromptPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedTemplate = Translator.Translate("Are you sure you want to delete the save game for {0}?");
        if (string.Equals(translatedTemplate, "Are you sure you want to delete the save game for {0}?", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedTemplate.Replace("{0}", match.Groups["target"].Value);
        DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "DeleteSavePrompt", source, translated);
        return true;
    }

    private static bool TryTranslateNumberedConversationChoice(string source, out string translated)
    {
        var match = NumberedConversationChoicePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var index = match.Groups["index"].Value;
        var text = match.Groups["text"].Value.TrimEnd();
        if (!UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(text, nameof(PopupTranslationPatch)))
        {
            text = Translator.Translate(text);
        }

        translated = $"[{index}] {text}";
        DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "ConversationChoice", source, translated);
        return true;
    }

    private static bool TryTranslateAbandonPrompt(string source, out string translated)
    {
        if (!AbandonPromptPattern.IsMatch(source))
        {
            translated = source;
            return false;
        }

        var canonicalKey = "If you quit without saving, you will lose all your progress and your character will be lost. Are you sure you want to QUIT and LOSE YOUR PROGRESS?\n\nType 'ABANDON' to confirm.";
        translated = Translator.Translate(canonicalKey);
        var changed = !string.Equals(translated, canonicalKey, StringComparison.Ordinal);
        if (changed)
        {
            DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "AbandonPrompt", source, translated);
        }

        return changed;
    }

    private static bool TryTranslateHotkeyLabel(string source, out string translated)
    {
        var match = HotkeyLabelPattern.Match(source);
        if (!match.Success || int.TryParse(match.Groups["hotkey"].Value, out _))
        {
            translated = source;
            return false;
        }

        var label = match.Groups["label"].Value;
        var translatedLabel = TranslateLabelWithCaseFallback(label);
        if (translatedLabel is null)
        {
            translated = source;
            return false;
        }

        translated = $"[{match.Groups["hotkey"].Value}] {translatedLabel}";
        DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "HotkeyLabel", source, translated);
        return true;
    }

    private static bool TryTranslateStyledHotkeyLabel(string source, out string translated)
    {
        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (!TryTranslateHotkeyLabel(stripped, out var translatedVisible))
        {
            translated = source;
            return false;
        }

        var sourceMatch = StyledHotkeyLabelPattern.Match(source);
        var visibleMatch = HotkeyLabelPattern.Match(translatedVisible);
        if (!sourceMatch.Success || !visibleMatch.Success)
        {
            translated = source;
            return false;
        }

        translated = sourceMatch.Groups["prefix"].Value
            + sourceMatch.Groups["labelOpen"].Value
            + visibleMatch.Groups["label"].Value
            + sourceMatch.Groups["labelClose"].Value;
        DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "HotkeyLabelMarkup", source, translated);
        return true;
    }

    private static string TranslatePopupEntityReference(string source)
    {
        return DeathWrapperFamilyTranslator.TranslateEntityReference(source, nameof(PopupTranslationPatch));
    }

    private static bool TryTranslateDeleteTitle(string source, out string translated)
    {
        var match = DeleteTitlePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedTemplate = Translator.Translate("Delete {0}");
        if (string.Equals(translatedTemplate, "Delete {0}", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedTemplate.Replace("{0}", match.Groups["target"].Value);
        DynamicTextObservability.RecordTransform(nameof(PopupTranslationPatch), "DeleteTitle", source, translated);
        return true;
    }

    private static MethodBase? FindMethod(string methodName, int parameterCount)
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: PopupTranslationPatch target type '{TargetTypeName}' not found.");
            return null;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var method = methods[index];
            if (method.Name == methodName && method.GetParameters().Length == parameterCount)
            {
                return method;
            }
        }

        Trace.TraceError(
            $"QudJP: PopupTranslationPatch method '{methodName}' with {parameterCount} params not found on '{TargetTypeName}'.");
        return null;
    }

    private static string? TranslateLabelWithCaseFallback(string source)
    {
        return StringHelpers.TranslateExactOrLowerAscii(source);
    }

#pragma warning disable S1144
    private static string TranslatePopupTemplateValue(string source)
    {
        var direct = TranslateLabelWithCaseFallback(source);
        if (direct is not null)
        {
            return direct;
        }

        return TranslatePopupEntityReference(source);
    }
#pragma warning restore S1144
    private static void TranslatePopupMenuItemTextCollection(object? maybeCollection)
    {
        if (maybeCollection is null || maybeCollection is string || maybeCollection is not IList list)
        {
            return;
        }

        for (var index = 0; index < list.Count; index++)
        {
            var item = list[index];
            if (item is null)
            {
                continue;
            }

            var field = AccessTools.Field(item.GetType(), "text");
            if (field is null || field.FieldType != typeof(string))
            {
                continue;
            }

            var current = field.GetValue(item) as string;
            var translated = TranslatePopupText(current ?? string.Empty);
            if (string.Equals(current, translated, StringComparison.Ordinal))
            {
                continue;
            }

            field.SetValue(item, translated);
            list[index] = item;
        }
    }
}
