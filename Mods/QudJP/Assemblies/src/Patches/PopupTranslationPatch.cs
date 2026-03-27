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
    private static readonly Regex HotkeyLabelPattern =
        new Regex("^\\[(?<hotkey>[^\\]]+)\\]\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlainHotkeyLabelPattern =
        new Regex("^(?<hotkey>Enter|Esc|Tab|Space|space)\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NumberedConversationChoicePattern =
        new Regex("^\\[(?<index>\\d+)\\]\\s+(?<text>.+?)(?:\\s+\\[[^\\]]+\\])?\\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

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
        var anyChanged = false;
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

            var result = TranslatePopupText(text);
            if (!anyChanged && !string.Equals(text, result, StringComparison.Ordinal))
            {
                anyChanged = true;
            }

            translated.Add(result);
        }

        if (anyChanged)
        {
            args[index] = translated;
        }
    }

    internal static string TranslatePopupTextForRoute(string source, string route)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (!IsAlreadyLocalizedPopupTextCore(stripped))
        {
            SinkObservation.LogUnclaimed(
                nameof(PopupTranslationPatch),
                route,
                SinkObservation.ObservationOnlyDetail,
                source,
                stripped);
        }

        return source;
    }

    internal static string TranslatePopupMenuItemText(string source)
    {
        return TranslatePopupMenuItemTextForRoute(source, nameof(PopupTranslationPatch));
    }

    private static string TranslatePopupText(string source)
    {
        return TranslatePopupTextForRoute(source, nameof(PopupTranslationPatch));
    }

    internal static string TranslatePopupMenuItemTextForRoute(string source, string route)
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
        if (!IsAlreadyLocalizedPopupTextCore(stripped))
        {
            SinkObservation.LogUnclaimed(
                nameof(PopupTranslationPatch),
                route,
                SinkObservation.ObservationOnlyDetail,
                source,
                stripped);
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
        return IsAlreadyLocalizedPopupTextCore(stripped);
    }

    private static bool IsAlreadyLocalizedPopupTextCore(string stripped)
    {
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
