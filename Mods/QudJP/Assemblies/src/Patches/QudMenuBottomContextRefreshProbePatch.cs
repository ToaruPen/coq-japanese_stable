using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudMenuBottomContextRefreshProbePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method("Qud.UI.QudMenuBottomContext:RefreshButtons");
        if (method is not null)
        {
            return method;
        }

        var targetType = AccessTools.TypeByName("Qud.UI.QudMenuBottomContext");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: Failed to resolve QudMenuBottomContext.RefreshButtons(). Probe patch will not apply.");
            return null;
        }

        var fallback = AccessTools.Method(targetType, "RefreshButtons");
        if (fallback is null)
        {
            Trace.TraceError("QudJP: Failed to resolve QudMenuBottomContext.RefreshButtons(). Probe patch will not apply.");
        }

        return fallback;
    }

    public static void Prefix(object __instance)
    {
        try
        {
            if (QudMenuBottomContextObservability.TryBuildState(__instance, "RefreshButtonsBefore/v1", out var logLine)
                && !string.IsNullOrEmpty(logLine))
            {
                LogProbe(logLine!);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QudMenuBottomContextRefreshProbePatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(object __instance)
    {
        try
        {
            if (QudMenuBottomContextObservability.TryBuildState(__instance, "RefreshButtonsAfter/v1", out var logLine)
                && !string.IsNullOrEmpty(logLine))
            {
                LogProbe(logLine!);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QudMenuBottomContextRefreshProbePatch.Postfix failed: {0}", ex);
        }
    }

    private static void LogProbe(string message)
    {
        try
        {
            var debugType = Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule", throwOnError: false);
            if (debugType is null)
            {
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
            Trace.TraceWarning("QudJP: QudMenuBottomContextRefreshProbePatch.LogProbe fallback: {0}", ex.Message);
            Trace.TraceInformation(message);
        }
    }
}
