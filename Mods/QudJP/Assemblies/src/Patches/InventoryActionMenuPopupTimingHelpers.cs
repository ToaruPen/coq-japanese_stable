using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class InventoryActionMenuPopupTimingHelpers
{
    private const string TargetTypeName = "Qud.UI.PopupMessage";

    internal static MethodBase? ResolvePopupMessageMethod(string context, string methodName)
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type '{1}' not found.", context, TargetTypeName);
            return null;
        }

        var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.{1} target not found.", context, methodName);
        }

        return method;
    }

    internal static string? GetPopupId(object? instance)
    {
        if (instance is null)
        {
            return null;
        }

        return AccessTools.Field(instance.GetType(), "PopupID")?.GetValue(instance) as string;
    }

    internal static int? GetHideNextFrame(object? instance)
    {
        if (instance is null)
        {
            return null;
        }

        return AccessTools.Field(instance.GetType(), "HideNextFrame")?.GetValue(instance) as int?;
    }
}
