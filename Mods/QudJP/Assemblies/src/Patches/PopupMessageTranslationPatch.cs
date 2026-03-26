using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupMessageTranslationPatch
{
    private const string Context = nameof(PopupMessageTranslationPatch);
    private const string TargetTypeName = "Qud.UI.PopupMessage";
    private static readonly Regex BracketedHotkeyLabelPattern =
        new Regex("^\\[(?<hotkey>[^\\]]+)\\]\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlainHotkeyLabelPattern =
        new Regex("^(?<hotkey>Enter|Esc|Tab|Space|space|[A-Z0-9+\\-]+)\\s+(?<label>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // ShowPopup parameter indices for game version 2.0.4
    private const int MessageIndex = 0;
    private const int ButtonsIndex = 1;
    private const int ItemsIndex = 3;
    private const int TitleIndex = 5;
    private const int ContextTitleIndex = 11;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: PopupMessageTranslationPatch target type '{TargetTypeName}' not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "ShowPopup");
        if (method is null)
        {
            Trace.TraceError($"QudJP: PopupMessageTranslationPatch method 'ShowPopup' not found on '{TargetTypeName}'.");
        }

        return method;
    }

    public static void Prefix(object[] __args)
    {
        try
        {
            if (__args is null)
            {
                Trace.TraceError("QudJP: PopupMessageTranslationPatch.Prefix received null args.");
                return;
            }

            TranslateStringArg(__args, MessageIndex, "PopupMessage.Message");
            TranslateItemTextCollection(__args, ButtonsIndex, "PopupMessage.ButtonText");
            TranslateItemTextCollection(__args, ItemsIndex, "PopupMessage.ItemText");
            TranslateStringArg(__args, TitleIndex, "PopupMessage.Title");
            TranslateStringArg(__args, ContextTitleIndex, "PopupMessage.ContextTitle");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PopupMessageTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    private static void TranslateStringArg(object[] args, int index, string family)
    {
        if (index < 0 || index >= args.Length || args[index] is not string text)
        {
            return;
        }

        args[index] = TranslatePopupText(text, family);
    }

    private static void TranslateItemTextCollection(object[] args, int index, string family)
    {
        if (index < 0 || index >= args.Length || args[index] is null || args[index] is string || args[index] is not IList list)
        {
            return;
        }

        for (var itemIndex = 0; itemIndex < list.Count; itemIndex++)
        {
            var item = list[itemIndex];
            if (item is null)
            {
                continue;
            }

            var textField = AccessTools.Field(item.GetType(), "text");
            if (textField is null || textField.FieldType != typeof(string))
            {
                continue;
            }

            var current = textField.GetValue(item) as string;
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            var translated = TranslatePopupText(current!, family);
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                textField.SetValue(item, translated);
                list[itemIndex] = item;
            }
        }
    }

    private static string TranslatePopupText(string source, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            return markedText;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateVisibleText);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(Context, family, source, translated);
        }

        return translated;
    }

    private static string TranslateVisibleText(string visibleText)
    {
        var translated = Translator.Translate(visibleText);
        if (!string.Equals(translated, visibleText, StringComparison.Ordinal))
        {
            return translated;
        }

        var bracketedHotkey = BracketedHotkeyLabelPattern.Match(visibleText);
        if (bracketedHotkey.Success && !int.TryParse(bracketedHotkey.Groups["hotkey"].Value, out _))
        {
            return ReplaceLabel(visibleText, bracketedHotkey.Groups["label"]);
        }

        var plainHotkey = PlainHotkeyLabelPattern.Match(visibleText);
        if (plainHotkey.Success)
        {
            return ReplaceLabel(visibleText, plainHotkey.Groups["label"]);
        }

        return visibleText;
    }

    private static string ReplaceLabel(string source, Group labelGroup)
    {
        var label = labelGroup.Value;
        var translatedLabel = Translator.Translate(label);
        if (string.Equals(translatedLabel, label, StringComparison.Ordinal))
        {
            return source;
        }

        return string.Concat(source.Remove(labelGroup.Index), translatedLabel);
    }
}
