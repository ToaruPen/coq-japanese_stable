using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PopupInternalMessageHandoffPatch
{
    private const string Context = nameof(PopupInternalMessageHandoffPatch);

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

        AddTarget(targets, popupType, "WaitNewPopupMessage");
        AddTarget(targets, popupType, "NewPopupMessageAsync");
        return targets;
    }

    public static void Prefix(ref string __0)
    {
        try
        {
            if (PopupTranslatedMessageHandoff.TryGetFromCurrentScope(__0, out var handedOff))
            {
                __0 = handedOff;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    private static void AddTarget(ICollection<MethodBase> targets, Type popupType, string methodName)
    {
        var method = AccessTools.Method(popupType, methodName);
        if (method is null)
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.{1}.", Context, methodName);
            return;
        }

        targets.Add(method);
    }
}
