using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryActionMenuCursorSoundPlayClickPatch
{
    private const string Context = nameof(InventoryActionMenuCursorSoundPlayClickPatch);
    private const string GenericControllerTargetTypeName = "Qud.UI.QudBaseMenuController`2";
    private const string MenuItemDataTypeName = "Qud.UI.QudMenuItem";
    private const string MenuItemControlTypeName = "Qud.UI.SelectableTextMenuItem";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var genericControllerType = AccessTools.TypeByName(GenericControllerTargetTypeName);
        var menuItemDataType = AccessTools.TypeByName(MenuItemDataTypeName);
        var menuItemControlType = AccessTools.TypeByName(MenuItemControlTypeName);
        if (genericControllerType is null || menuItemDataType is null || menuItemControlType is null)
        {
            Trace.TraceError(
                "QudJP: {0} target types not found. controller='{1}', data='{2}', control='{3}'.",
                Context,
                GenericControllerTargetTypeName,
                MenuItemDataTypeName,
                MenuItemControlTypeName);
            return null;
        }

        var targetType = genericControllerType.MakeGenericType(menuItemDataType, menuItemControlType);
        var method = AccessTools.Method(targetType, "PlayClick", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} method 'PlayClick()' not found on '{1}'.", Context, targetType.FullName);
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            InventoryActionMenuCursorSoundPatch.PlayCursorSoundForInventoryActionMenuController(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
