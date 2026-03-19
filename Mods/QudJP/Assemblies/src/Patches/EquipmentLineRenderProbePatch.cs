using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EquipmentLineRenderProbePatch
{
    private const string TargetTypeName = "Qud.UI.EquipmentLine";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var frameworkDataElementType = AccessTools.TypeByName("XRL.UI.Framework.FrameworkDataElement");
        if (frameworkDataElementType is not null)
        {
            var method = AccessTools.Method(TargetTypeName + ":setData", new[] { frameworkDataElementType });
            if (method is not null)
            {
                return method;
            }
        }

        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve EquipmentLine.setData(...). Probe patch will not apply.");
            return null;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var candidate = methods[index];
            if (string.Equals(candidate.Name, "setData", StringComparison.Ordinal)
                && candidate.ReturnType == typeof(void)
                && candidate.GetParameters().Length == 1)
            {
                return candidate;
            }
        }

        Trace.TraceError("QudJP: Failed to resolve EquipmentLine.setData(...). Probe patch will not apply.");
        return null;
    }

    public static void Postfix(object __instance, object data)
    {
        try
        {
#if HAS_TMP
            var applied = EquipmentLineFontFixer.TryApplyPrimaryFontToEquipmentLine(__instance);
            _ = TmpTextRepairer.TryRepairInvisibleTexts(__instance);
#else
            _ = __instance;
            _ = data;
            _ = data;
#endif
            _ = applied;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: EquipmentLineRenderProbePatch.Postfix failed: {0}", ex);
        }
    }
}
