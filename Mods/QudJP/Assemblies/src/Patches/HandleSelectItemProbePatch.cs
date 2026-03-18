using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HandleSelectItemProbePatch
{
    private const string TargetTypeName = "Qud.UI.InventoryAndEquipmentStatusScreen";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var frameworkDataElementType = AccessTools.TypeByName("XRL.UI.Framework.FrameworkDataElement");
        if (frameworkDataElementType is not null)
        {
            var method = AccessTools.Method(TargetTypeName + ":HandleSelectItem", new[] { frameworkDataElementType });
            if (method is not null)
            {
                return method;
            }
        }

        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve InventoryAndEquipmentStatusScreen.HandleSelectItem(...). HandleSelectItemProbePatch will not apply.");
            return null;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var candidate = methods[index];
            if (string.Equals(candidate.Name, "HandleSelectItem", StringComparison.Ordinal)
                && candidate.ReturnType == typeof(void)
                && candidate.GetParameters().Length == 1)
            {
                return candidate;
            }
        }

        Trace.TraceError("QudJP: Failed to resolve InventoryAndEquipmentStatusScreen.HandleSelectItem(...). HandleSelectItemProbePatch will not apply.");
        return null;
    }

    public static void Postfix(object __instance)
    {
        try
        {
            _ = __instance;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: HandleSelectItemProbePatch.Postfix failed: {0}", ex);
        }
    }
}
