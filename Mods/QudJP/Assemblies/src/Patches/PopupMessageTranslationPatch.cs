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

    public static void Prefix(ref string __0, object? __1, object? __3, ref string? __5, ref string? __11, string? __20)
    {
        try
        {
            __0 = TranslatePopupText(__0, "PopupMessage.Message")!;
            if (!ShouldPreserveMenuItemData(__20))
            {
                TranslateItemTextCollection(__1, "PopupMessage.ButtonText");
                TranslateItemTextCollection(__3, "PopupMessage.ItemText");
            }

            __5 = TranslatePopupText(__5, "PopupMessage.Title");
            __11 = TranslatePopupText(__11, "PopupMessage.ContextTitle");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PopupMessageTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    internal static bool ShouldPreserveMenuItemData(string? popupId)
    {
        return popupId is not null
            && popupId.StartsWith("InventoryActionMenu:", StringComparison.Ordinal);
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
            if (PopupTextFieldTranslator.TryTranslateTextField(item, text => TranslatePopupText(text, family)!))
            {
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

        if (PopupTranslatedMessageHandoff.TryGet(source!, out var handedOff))
        {
            return handedOff;
        }

        return PopupTranslationPatch.TranslatePopupTextForProducerRoute(source!, Context);
    }
}
