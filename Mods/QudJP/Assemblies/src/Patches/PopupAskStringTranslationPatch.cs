using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupAskStringTranslationPatch
{
    private const string Context = nameof(PopupAskStringTranslationPatch);
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
                "AskString",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool?),
                }),
            "AskString");

        AddTarget(
            targets,
            AccessTools.Method(
                popupType,
                "AskStringAsync",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(int),
                    typeof(string),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool?),
                    typeof(bool),
                    typeof(string),
                }),
            "AskStringAsync");

        if (targets.Count == 0)
        {
            Trace.TraceError($"QudJP: {Context} resolved zero target methods.");
        }

        return targets;
    }

    public static void Prefix(ref string __0)
    {
        try
        {
            if (string.IsNullOrEmpty(__0))
            {
                return;
            }

            __0 = PopupTranslationPatch.TranslatePopupTextForProducerRoute(__0, Context);
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
