using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DescriptionInventoryActionProbePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (inventoryActionEventType is not null)
        {
            var method = AccessTools.Method("XRL.World.Parts.Description:HandleEvent", new[] { inventoryActionEventType });
            if (method is not null)
            {
                return method;
            }
        }

        var targetType = AccessTools.TypeByName("XRL.World.Parts.Description");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve Description.HandleEvent(InventoryActionEvent). Probe patch will not apply.");
            return null;
        }

        var methods = AccessTools.GetDeclaredMethods(targetType);
        for (var index = 0; index < methods.Count; index++)
        {
            var candidate = methods[index];
            var parameters = candidate.GetParameters();
            if (!string.Equals(candidate.Name, "HandleEvent", StringComparison.Ordinal)
                || candidate.ReturnType != typeof(bool)
                || parameters.Length != 1)
            {
                continue;
            }

            if (string.Equals(parameters[0].ParameterType.FullName, "XRL.World.InventoryActionEvent", StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        Trace.TraceError("QudJP: Failed to resolve Description.HandleEvent(InventoryActionEvent). Probe patch will not apply.");
        return null;
    }

    public static void Prefix(object __instance, object E)
    {
        try
        {
            if (InventoryActionObservability.TryBuildDescriptionHandleEventState(__instance, E, out var logLine)
                && logLine is not null
                && logLine.Length > 0)
            {
                LogProbe(logLine);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: DescriptionInventoryActionProbePatch.Prefix failed: {0}", ex);
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
                    "QudJP: DescriptionInventoryActionProbePatch.LogProbe could not find UnityEngine.Debug in UnityEngine.CoreModule. Trying UnityEngine assembly name.");
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
            Trace.TraceWarning("QudJP: DescriptionInventoryActionProbePatch.LogProbe fell back to trace. {0}", ex.Message);
        }

        Trace.TraceInformation(message);
    }
}
