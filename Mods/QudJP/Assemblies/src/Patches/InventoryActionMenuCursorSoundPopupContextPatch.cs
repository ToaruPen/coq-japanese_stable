using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryActionMenuCursorSoundPopupContextPatch
{
    private const string Context = nameof(InventoryActionMenuCursorSoundPopupContextPatch);
    private const string TargetTypeName = "Qud.UI.PopupMessage";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", Context, TargetTypeName);
            return null;
        }

        var parameterTypes = ResolveShowPopupParameterTypes();
        if (parameterTypes is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve ShowPopup argument types.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "ShowPopup", parameterTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} method 'ShowPopup' not found on '{1}'.", Context, TargetTypeName);
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            InventoryActionMenuCursorSoundPatch.RememberPopupController(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static Type[]? ResolveShowPopupParameterTypes()
    {
        var qudMenuItemType = AccessTools.TypeByName("Qud.UI.QudMenuItem");
        var renderableType = AccessTools.TypeByName("ConsoleLib.Console.IRenderable");
        var locationType = AccessTools.TypeByName("Genkit.Location2D");
        if (qudMenuItemType is null || renderableType is null || locationType is null)
        {
            return null;
        }

        var menuItemListType = typeof(List<>).MakeGenericType(qudMenuItemType);
        var menuItemActionType = typeof(Action<>).MakeGenericType(qudMenuItemType);
        return
        [
            typeof(string),
            menuItemListType,
            menuItemActionType,
            menuItemListType,
            menuItemActionType,
            typeof(string),
            typeof(bool),
            typeof(string),
            typeof(int),
            typeof(Action),
            renderableType,
            typeof(string),
            renderableType,
            typeof(bool),
            typeof(bool),
            typeof(CancellationToken),
            typeof(bool),
            typeof(string),
            typeof(string),
            locationType,
            typeof(string),
        ];
    }
}
