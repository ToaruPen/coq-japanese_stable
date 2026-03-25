using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudMenuBottomContextTranslationPatch
{
    private const string TargetTypeName = "Qud.UI.QudMenuBottomContext";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: QudMenuBottomContextTranslationPatch target type '{TargetTypeName}' not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "RefreshButtons");
        if (method is null)
        {
            Trace.TraceError($"QudJP: QudMenuBottomContextTranslationPatch method 'RefreshButtons' not found on '{TargetTypeName}'.");
        }

        return method;
    }

    public static void Prefix(object __instance)
    {
        try
        {
            LogProbe(__instance, "prefix");
            NormalizeItemTexts(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QudMenuBottomContextTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(object __instance)
    {
        try
        {
            LogProbe(__instance, "postfix");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QudMenuBottomContextTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static void NormalizeItemTexts(object? contextInstance)
    {
        if (contextInstance is null)
        {
            return;
        }

        var itemsMember = AccessTools.Field(contextInstance.GetType(), "items");
        if (itemsMember?.GetValue(contextInstance) is not IList items)
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
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

            var translated = ColorAwareTranslationComposer.TranslatePreservingColors(current);

            if (string.Equals(translated, current, StringComparison.Ordinal))
            {
                continue;
            }

            textField.SetValue(item, translated);
            items[index] = item;
        }
    }

    private static void LogProbe(object? contextInstance, string phase)
    {
        if (QudMenuBottomContextObservability.TryBuildState(contextInstance, phase, out var logLine)
            && !string.IsNullOrEmpty(logLine))
        {
            QudJPMod.LogToUnity(logLine!);
        }
    }
}
