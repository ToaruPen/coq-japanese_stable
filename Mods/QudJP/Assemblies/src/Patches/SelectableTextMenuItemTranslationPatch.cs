using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SelectableTextMenuItemTranslationPatch
{
    private const string Context = nameof(SelectableTextMenuItemTranslationPatch);
    private const string TargetTypeName = "Qud.UI.SelectableTextMenuItem";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: {Context} target type '{TargetTypeName}' not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "SelectChanged", new[] { typeof(bool) });
        if (method is null)
        {
            Trace.TraceError($"QudJP: {Context} method 'SelectChanged(bool)' not found on '{TargetTypeName}'.");
        }

        return method;
    }

    public static void Postfix(object? __instance, bool newState)
    {
        try
        {
            if (__instance is null)
            {
                return;
            }

            var itemText = AccessTools.Property(__instance.GetType(), "itemText")?.GetValue(__instance) as string;
            if (itemText is null || itemText.Length == 0)
            {
                return;
            }

            var translated = TranslateMenuItemTextForDisplay(itemText, TryGetPopupIdFromParentPopup(__instance));
            if (string.Equals(translated, itemText, StringComparison.Ordinal))
            {
                return;
            }

            var item = AccessTools.Field(__instance.GetType(), "item")?.GetValue(__instance);
            var setText = item is null ? null : AccessTools.Method(item.GetType(), "SetText", new[] { typeof(string) });
            setText?.Invoke(item, new object[] { WrapForSelection(translated, newState) });
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static string TranslateMenuItemTextForDisplay(string source)
    {
        return TranslateMenuItemTextForDisplay(source, popupId: null);
    }

    internal static string TranslateMenuItemTextForDisplay(string source, string? popupId)
    {
        return StripDirectTranslationMarkers(
            PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(source, Context, popupId));
    }

    internal static string WrapForSelection(string source, bool selected)
    {
        return selected ? "{{W|" + source + "}}" : "{{c|" + source + "}}";
    }

    private static string? TryGetPopupIdFromParentPopup(object instance)
    {
        var popupMessageType = AccessTools.TypeByName("Qud.UI.PopupMessage");
        if (popupMessageType is null)
        {
            return null;
        }

        var popupMessage = TryGetParentPopupMessage(instance, popupMessageType);
        if (popupMessage is null)
        {
            return TryGetLastPopupId(popupMessageType);
        }

        return AccessTools.Field(popupMessageType, "PopupID")?.GetValue(popupMessage) as string
            ?? TryGetLastPopupId(popupMessageType);
    }

    private static object? TryGetParentPopupMessage(object instance, Type popupMessageType)
    {
        var componentType = AccessTools.TypeByName("UnityEngine.Component");
        if (componentType is null || !componentType.IsInstanceOfType(instance))
        {
            return null;
        }

        var includeInactiveMethod = AccessTools.Method(
            componentType,
            "GetComponentInParent",
            new[] { typeof(Type), typeof(bool) });
        if (includeInactiveMethod is not null)
        {
            return includeInactiveMethod.Invoke(instance, new object[] { popupMessageType, true });
        }

        var method = AccessTools.Method(componentType, "GetComponentInParent", new[] { typeof(Type) });
        return method?.Invoke(instance, new object[] { popupMessageType });
    }

    private static string? TryGetLastPopupId(Type popupMessageType)
    {
        return AccessTools.Field(popupMessageType, "lastPopupID")?.GetValue(null) as string;
    }

    private static string StripDirectTranslationMarkers(string source)
    {
        return source.IndexOf(MessageFrameTranslator.DirectTranslationMarker) < 0
            ? source
            : source.Replace(MessageFrameTranslator.DirectTranslationMarker.ToString(), string.Empty);
    }
}
