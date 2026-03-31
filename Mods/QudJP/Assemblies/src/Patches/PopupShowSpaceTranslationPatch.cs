using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupShowSpaceTranslationPatch
{
    private const string Context = nameof(PopupShowSpaceTranslationPatch);
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

        var renderableType = AccessTools.TypeByName("ConsoleLib.Console.Renderable");
        MethodInfo? method = null;
        if (renderableType is not null)
        {
            method = AccessTools.Method(
                targetType,
                "ShowSpace",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    renderableType,
                    typeof(bool),
                    typeof(bool),
                    typeof(string),
                });
        }

        if (method is null)
        {
            Trace.TraceError("QudJP: PopupShowSpaceTranslationPatch.ShowSpace() signature not found.");
            return null;
        }

        return method;
    }

    public static void Prefix(object[] __args)
    {
        try
        {
            if (__args.Length == 0)
            {
                return;
            }

            if (__args[0] is string message && !string.IsNullOrEmpty(message))
            {
                __args[0] = PopupTranslationPatch.TranslatePopupTextForProducerRoute(message, Context);
            }

            if (__args.Length > 1 && __args[1] is string title && !string.IsNullOrEmpty(title))
            {
                __args[1] = PopupTranslationPatch.TranslatePopupTextForProducerRoute(title, Context);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
