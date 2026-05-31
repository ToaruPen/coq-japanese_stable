using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudMenuBottomContextTranslationPatch
{
    private const string TargetTypeName = "Qud.UI.QudMenuBottomContext";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        if (targetType is null)
        {
            Trace.TraceError($"QudJP: QudMenuBottomContextTranslationPatch target type '{TargetTypeName}' not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "RefreshButtons");
        if (method is null)
        {
            Trace.TraceError($"QudJP: QudMenuBottomContextTranslationPatch method 'RefreshButtons' not found on '{TargetTypeName}'.");
        }

        return method;
    }

    public static void Prefix(object __instance)
    {
        try
        {
            LogProbe(__instance, "prefix");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QudMenuBottomContextTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix(object __instance)
    {
        try
        {
            ApplyButtonDisplayTranslations(__instance);
            LogProbe(__instance, "postfix");
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QudMenuBottomContextTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static void NormalizeItemTexts(object? contextInstance)
    {
        // QudMenuBottomContext.RefreshButtons runs every frame. Do not mutate
        // QudMenuItem data here; display translation belongs to
        // SelectableTextMenuItemTranslationPatch so tutorial/control IDs remain stable.
        _ = contextInstance;
    }

    internal static void ApplyButtonDisplayTranslations(object? contextInstance, string? popupIdOverride = null)
    {
        var buttons = ReflectionUtils.GetPropertyOrFieldValue(contextInstance, "buttons") as IEnumerable;
        if (buttons is null)
        {
            return;
        }

        foreach (var button in buttons)
        {
            if (button is null)
            {
                continue;
            }

            var selected = ReflectionUtils.GetPropertyOrFieldValue(button, "selected") is true;
            SelectableTextMenuItemTranslationPatch.ApplyDisplayTranslationIfChanged(
                button,
                selected,
                popupIdOverride);
        }
    }

    private static void LogProbe(object? contextInstance, string phase)
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled)
        {
            return;
        }

        if (QudMenuBottomContextObservability.TryBuildState(contextInstance, phase, out var logLine)
            && !string.IsNullOrEmpty(logLine))
        {
            RuntimeDiagnostics.LogVerboseProbe(() => logLine!);
        }
    }
}
