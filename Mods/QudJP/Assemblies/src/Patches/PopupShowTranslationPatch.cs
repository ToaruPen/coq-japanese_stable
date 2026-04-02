using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Observes Message parameter in Popup.Show family methods.
/// Producer-owned translations may arrive pre-marked; sink-side patch only strips the marker.
/// </summary>
[HarmonyPatch]
public static class PopupShowTranslationPatch
{
    private const string Context = nameof(PopupShowTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var popupType = AccessTools.TypeByName("XRL.UI.Popup");
        if (popupType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Popup type.", Context);
            return targets;
        }

        var location2DType = AccessTools.TypeByName("Genkit.Location2D");
        var dialogResultType = AccessTools.TypeByName("XRL.UI.DialogResult");

        MethodInfo? showMethod = null;
        if (location2DType is not null)
        {
            showMethod = AccessTools.Method(popupType, "Show",
                new[] { typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool), location2DType });
        }
        if (showMethod is null)
        {
            showMethod = AccessTools.Method(popupType, "Show");
        }

        if (showMethod is not null)
        {
            targets.Add(showMethod);
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.Show.", Context);
        }

        var showFailMethod = AccessTools.Method(popupType, "ShowFail",
            new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool) });
        if (showFailMethod is null)
        {
            showFailMethod = AccessTools.Method(popupType, "ShowFail");
        }

        if (showFailMethod is not null)
        {
            targets.Add(showFailMethod);
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.ShowFail.", Context);
        }

        MethodInfo? showYesNoMethod = null;
        if (dialogResultType is not null)
        {
            showYesNoMethod = AccessTools.Method(popupType, "ShowYesNo",
                new[] { typeof(string), typeof(string), typeof(bool), dialogResultType });
        }
        if (showYesNoMethod is null)
        {
            showYesNoMethod = AccessTools.Method(popupType, "ShowYesNo");
        }

        if (showYesNoMethod is not null)
        {
            targets.Add(showYesNoMethod);
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.ShowYesNo.", Context);
        }

        MethodInfo? showYesNoCancelMethod = null;
        if (dialogResultType is not null)
        {
            showYesNoCancelMethod = AccessTools.Method(popupType, "ShowYesNoCancel",
                new[] { typeof(string), typeof(string), typeof(bool), dialogResultType });
        }
        if (showYesNoCancelMethod is null)
        {
            showYesNoCancelMethod = AccessTools.Method(popupType, "ShowYesNoCancel");
        }

        if (showYesNoCancelMethod is not null)
        {
            targets.Add(showYesNoCancelMethod);
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.ShowYesNoCancel.", Context);
        }

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: {0} resolved zero target methods.", Context);
        }

        return targets;
    }

    public static void Prefix(object[] __args)
    {
        try
        {
            if (__args.Length == 0 || __args[0] is not string message || string.IsNullOrEmpty(message))
            {
                return;
            }

            __args[0] = PopupTranslationPatch.TranslatePopupTextForProducerRoute(message, Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }
}
