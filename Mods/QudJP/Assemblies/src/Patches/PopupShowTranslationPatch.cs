using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Observes Message parameter in Popup.Show / ShowYesNo / ShowYesNoCancel / ShowYesNoAsync.
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

        var showMethod = AccessTools.Method(popupType, "Show",
            new[] { typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool), AccessTools.TypeByName("Genkit.Location2D") });
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

        var showYesNoMethod = AccessTools.Method(popupType, "ShowYesNo",
            new[] { typeof(string), typeof(string), typeof(bool), AccessTools.TypeByName("XRL.UI.DialogResult") });
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

        var showYesNoCancelMethod = AccessTools.Method(popupType, "ShowYesNoCancel",
            new[] { typeof(string), typeof(string), typeof(bool), AccessTools.TypeByName("XRL.UI.DialogResult") });
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

        var showYesNoAsyncMethod = AccessTools.Method(popupType, "ShowYesNoAsync", new[] { typeof(string) });
        if (showYesNoAsyncMethod is null)
        {
            showYesNoAsyncMethod = AccessTools.Method(popupType, "ShowYesNoAsync");
        }

        if (showYesNoAsyncMethod is not null)
        {
            targets.Add(showYesNoAsyncMethod);
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.ShowYesNoAsync.", Context);
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
