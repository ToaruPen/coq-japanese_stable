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

            if (PopupPickOptionTranslationPatch.ShouldPreservePopupOptionMenuData)
            {
                return;
            }

            PopupTextFieldTranslator.TryTranslateTextField(__result, TranslatePopupMenuItemText);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static string TranslatePopupMenuItemText(string source)
    {
        return PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(source, Context);
    }
}
