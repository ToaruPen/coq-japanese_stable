using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
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

    [ThreadStatic]
    private static string? pendingDirectMarkerPassThroughText;

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

        MethodInfo? showAsyncMethod = AccessTools.Method(popupType, "ShowAsync",
            new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
        if (showAsyncMethod is null)
        {
            showAsyncMethod = AccessTools.Method(popupType, "ShowAsync", new[] { typeof(string) });
        }
        if (showAsyncMethod is null)
        {
            showAsyncMethod = AccessTools.Method(popupType, "ShowAsync");
        }

        if (showAsyncMethod is not null)
        {
            targets.Add(showAsyncMethod);
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.ShowAsync.", Context);
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

        var showKeybindAsyncMethod = AccessTools.Method(popupType, "ShowKeybindAsync",
            new[] { typeof(string), typeof(CancellationToken) });
        if (showKeybindAsyncMethod is not null)
        {
            targets.Add(showKeybindAsyncMethod);
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.ShowKeybindAsync.", Context);
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

        var showYesNoAsyncMethod = AccessTools.Method(popupType, "ShowYesNoAsync", new[] { typeof(string) });
        if (showYesNoAsyncMethod is null)
        {
            Trace.TraceWarning(
                "QudJP: {0} failed to resolve Popup.ShowYesNoAsync(string); falling back to name-only lookup.",
                Context);
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

        var showYesNoCancelAsyncMethod = AccessTools.Method(popupType, "ShowYesNoCancelAsync", new[] { typeof(string) });
        if (showYesNoCancelAsyncMethod is null)
        {
            Trace.TraceWarning(
                "QudJP: {0} failed to resolve Popup.ShowYesNoCancelAsync(string); falling back to name-only lookup.",
                Context);
            showYesNoCancelAsyncMethod = AccessTools.Method(popupType, "ShowYesNoCancelAsync");
        }
        if (showYesNoCancelAsyncMethod is not null)
        {
            targets.Add(showYesNoCancelAsyncMethod);
        }
        else
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.ShowYesNoCancelAsync.", Context);
        }

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: {0} resolved zero target methods.", Context);
        }

        return targets;
    }

    public static void Prefix(ref string __0, MethodBase? __originalMethod)
    {
        try
        {
            PopupTranslatedMessageHandoff.EnterScope();
            if (string.IsNullOrEmpty(__0))
            {
                return;
            }

            var expectsNestedDirectMarkerPassThrough = IsDirectMarkedPopupWrapperCall(__0, __originalMethod);
            __0 = PopupShowSemanticPipeline.TranslateMessage(__0, Context);
            if (expectsNestedDirectMarkerPassThrough)
            {
                pendingDirectMarkerPassThroughText = __0;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            pendingDirectMarkerPassThroughText = null;
            PopupTranslatedMessageHandoff.ExitCurrentScope();
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryConsumeDirectMarkerPassThrough(string source, ref string? passThroughText)
    {
        if (pendingDirectMarkerPassThroughText is not null)
        {
            var pending = pendingDirectMarkerPassThroughText;
            pendingDirectMarkerPassThroughText = null;
            if (string.Equals(source, pending, StringComparison.Ordinal)
                && (passThroughText is null || string.Equals(source, passThroughText, StringComparison.Ordinal)))
            {
                passThroughText = null;
                return true;
            }
        }

        if (passThroughText is null)
        {
            return false;
        }

        var shouldPassThrough = string.Equals(source, passThroughText, StringComparison.Ordinal);
        passThroughText = null;
        return shouldPassThrough;
    }

    internal static bool TryTranslateDirectMarkedOwnerPopup(
        string source,
        ref string? passThroughText,
        out string translated)
    {
        if (TryConsumeDirectMarkerPassThrough(source, ref passThroughText))
        {
            translated = source;
            return true;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            passThroughText = markedText;
            translated = markedText;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool IsDirectMarkedPopupWrapperCall(string source, MethodBase? originalMethod)
    {
        return MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _)
            && originalMethod is not null
            && string.Equals(originalMethod.Name, "ShowFail", StringComparison.Ordinal);
    }
}
