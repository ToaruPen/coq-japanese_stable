using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupShowColorPickerTranslationPatch
{
    private const string Context = nameof(PopupShowColorPickerTranslationPatch);
    private const string TargetTypeName = "XRL.UI.Popup";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var popupType = AccessTools.TypeByName(TargetTypeName);
        if (popupType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            yield break;
        }

        var method = AccessTools.Method(
            popupType,
            "ShowColorPicker",
            new[]
            {
                typeof(string),
                typeof(int),
                typeof(string),
                typeof(int),
                typeof(bool),
                typeof(bool),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(string),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ShowColorPicker(...) signature not found.", Context);
            yield break;
        }

        yield return method;
    }

    public static void Prefix(ref string __0, ref string? __2)
    {
        try
        {
            var translatedTitle = Translate(__0);
            if (translatedTitle is not null)
            {
                __0 = translatedTitle;
            }

            __2 = Translate(__2);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static string? Translate(string? source)
    {
        return string.IsNullOrEmpty(source)
            ? source
            : PopupTranslationPatch.TranslatePopupTextForProducerRoute(source!, Context);
    }
}
