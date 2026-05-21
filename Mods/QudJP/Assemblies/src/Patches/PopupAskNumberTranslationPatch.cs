using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupAskNumberTranslationPatch
{
    private const string Context = nameof(PopupAskNumberTranslationPatch);
    private const string TargetTypeName = "XRL.UI.Popup";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var popupType = AccessTools.TypeByName(TargetTypeName);
        if (popupType is null)
        {
            Trace.TraceError($"QudJP: {Context} target type '{TargetTypeName}' not found.");
            return targets;
        }

        AddTarget(
            targets,
            AccessTools.Method(
                popupType,
                "AskNumber",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                }),
            "AskNumber");

        AddTarget(
            targets,
            AccessTools.Method(
                popupType,
                "AskNumberAsync",
                new[]
                {
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(string),
                    typeof(bool),
                }),
            "AskNumberAsync");

        if (targets.Count == 0)
        {
            Trace.TraceError($"QudJP: {Context} resolved zero target methods.");
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

            if (TradeScreenUiTranslationPatch.TryTranslateTradeSomePrompt(message, out var tradeSomeTranslated))
            {
                __args[0] = tradeSomeTranslated;
                return;
            }

            if (SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage(
                    message,
                    Context,
                    "Popup.AskNumber",
                    out var ownerTranslated))
            {
                __args[0] = ownerTranslated;
                return;
            }

            __args[0] = PopupTranslationPatch.TranslatePopupTextForProducerRoute(message, Context);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static void AddTarget(List<MethodBase> targets, MethodInfo? method, string methodName)
    {
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceWarning("QudJP: {0} failed to resolve Popup.{1}.", Context, methodName);
    }
}
