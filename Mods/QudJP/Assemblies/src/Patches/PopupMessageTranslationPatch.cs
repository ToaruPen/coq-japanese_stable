using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupMessageTranslationPatch
{
    private const string Context = nameof(PopupMessageTranslationPatch);
    private const string TargetTypeName = "Qud.UI.PopupMessage";

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

    public static void Prefix(ref string __0, object? __1, object? __3, ref string? __5, ref string? __11)
    {
        try
        {
            __0 = TranslatePopupText(__0, "PopupMessage.Message")!;
            TranslateItemTextCollection(__1, "PopupMessage.ButtonText");
            TranslateItemTextCollection(__3, "PopupMessage.ItemText");
            __5 = TranslatePopupText(__5, "PopupMessage.Title");
            __11 = TranslatePopupText(__11, "PopupMessage.ContextTitle");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PopupMessageTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    private static void TranslateItemTextCollection(object? maybeList, string family)
    {
        if (maybeList is null || maybeList is string || maybeList is not IList list)
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

    private static string? TranslatePopupText(string? source, string family)
    {
        _ = family;
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        return PopupTranslationPatch.TranslatePopupTextForProducerRoute(source!, Context);
    }
}
