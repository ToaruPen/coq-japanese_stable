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
            var applied = EquipmentLineFontFixer.TryApplyPrimaryFontToEquipmentLine(__instance);
            var repaired = TmpTextRepairer.TryRepairInvisibleTexts(__instance);
            if (EquipmentLineObservability.TryBuildState(__instance, data, applied, out var logLine)
                && logLine is not null
                && logLine.Length > 0)
            {
                LogProbe(logLine);
            }

            if (repaired > 0)
            {
                LogProbe(TmpTextRepairer.BuildRepairLog("EquipmentLineRepair/v1", repaired));
            }

            if (UiChildTextObservability.TryBuildSnapshot(__instance, "EquipmentLineChildren/v1", out var childLogLine)
                && childLogLine is not null
                && childLogLine.Length > 0)
            {
                LogProbe(childLogLine);
            }

            if (EquipmentLineObservability.TryBuildCompactChildSummary(__instance, out var compactChildLogLine)
                && compactChildLogLine is not null
                && compactChildLogLine.Length > 0)
            {
                LogProbe(compactChildLogLine);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: EquipmentLineRenderProbePatch.Postfix failed: {0}", ex);
        }
    }

    private static void LogProbe(string message)
    {
        try
        {
            var debugType = Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule", throwOnError: false);
            if (debugType is null)
            {
                Trace.TraceWarning(
                    "QudJP: EquipmentLineRenderProbePatch.LogProbe could not find UnityEngine.Debug in UnityEngine.CoreModule. Trying UnityEngine assembly name.");
                debugType = Type.GetType("UnityEngine.Debug, UnityEngine", throwOnError: false);
            }

            var logMethod = debugType?.GetMethod(
                "Log",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(object) },
                modifiers: null);
            logMethod?.Invoke(null, new object[] { message });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: EquipmentLineRenderProbePatch.LogProbe fell back to trace. {0}", ex.Message);
        }

        Trace.TraceInformation(message);
    }
}
