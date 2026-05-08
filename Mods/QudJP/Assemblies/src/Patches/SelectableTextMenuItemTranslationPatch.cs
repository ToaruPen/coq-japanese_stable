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
            if (string.IsNullOrEmpty(itemText))
            {
                return;
            }

            var translated = TranslateMenuItemTextForDisplay(itemText!);
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
        return PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(source, Context);
    }

    internal static string WrapForSelection(string source, bool selected)
    {
        return selected ? "{{W|" + source + "}}" : "{{c|" + source + "}}";
    }
}
