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

        var qudMenuItemType = AccessTools.TypeByName("Qud.UI.QudMenuItem");
        var renderableType = AccessTools.TypeByName("ConsoleLib.Console.IRenderable");
        var locationType = AccessTools.TypeByName("Genkit.Location2D");
        if (qudMenuItemType is null || renderableType is null || locationType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Popup argument types.", Context);
            return targets;
        }

        var menuItemListType = typeof(List<>).MakeGenericType(qudMenuItemType);
        AddTarget(
            targets,
            popupType,
            "WaitNewPopupMessage",
            [
                typeof(string),
                menuItemListType,
                typeof(Action<>).MakeGenericType(qudMenuItemType),
                menuItemListType,
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(string),
                renderableType,
                renderableType,
                typeof(bool),
                typeof(bool),
                locationType,
                typeof(string),
                typeof(bool),
            ]);
        AddTarget(
            targets,
            popupType,
            "NewPopupMessageAsync",
            [
                typeof(string),
                menuItemListType,
                menuItemListType,
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(string),
                renderableType,
                renderableType,
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(System.Threading.CancellationToken),
                typeof(bool),
                typeof(string),
                typeof(string),
                locationType,
                typeof(string),
            ]);
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

    private static void AddTarget(ICollection<MethodBase> targets, Type popupType, string methodName, Type[] parameterTypes)
    {
        var method = AccessTools.Method(popupType, methodName, parameterTypes);
        if (method is null)
        {
            Trace.TraceWarning("QudJP: {0} failed to resolve Popup.{1}.", Context, methodName);
            return;
        }

        targets.Add(method);
    }
}
