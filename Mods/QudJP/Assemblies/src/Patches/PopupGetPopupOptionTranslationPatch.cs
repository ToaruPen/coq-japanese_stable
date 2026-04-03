using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupGetPopupOptionTranslationPatch
{
    private const string Context = nameof(PopupGetPopupOptionTranslationPatch);
    private const string TargetTypeName = "XRL.UI.Popup";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: {Context} target type '{TargetTypeName}' not found.");
            return null;
        }

        var renderableType = AccessTools.TypeByName("ConsoleLib.Console.IRenderable");
        MethodInfo? method = null;
        if (renderableType is not null)
        {
            method = AccessTools.Method(
                targetType,
                "GetPopupOption",
                new[]
                {
                    typeof(int),
                    typeof(IReadOnlyList<string>),
                    typeof(IReadOnlyList<char>),
                    typeof(IReadOnlyList<>).MakeGenericType(renderableType),
                });
        }

        if (method is null)
        {
            Trace.TraceError($"QudJP: {Context} expected 'GetPopupOption' signature not found on '{TargetTypeName}'.");
            return null;
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            if (__result is null)
            {
                return;
            }

            var textField = AccessTools.Field(__result.GetType(), "text");
            if (textField is null || textField.FieldType != typeof(string))
            {
                return;
            }

            var current = textField.GetValue(__result) as string;
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            var translated = PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(current!, Context);
            if (!string.Equals(translated, current, StringComparison.Ordinal))
            {
                textField.SetValue(__result, translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
