using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class InventoryLineActiveTextRefreshPatch
{
    private const string TargetTypeName = "Qud.UI.InventoryLine";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName(TargetTypeName);
        var method = targetType is null ? null : AccessTools.Method(targetType, "LateUpdate");
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve InventoryLine.LateUpdate(). Active text refresh patch will not apply.");
        }

        return method;
    }

    public static void Postfix(object __instance)
    {
        try
        {
#if HAS_TMP
            var isActiveItemLine = InventoryLineFontFixer.IsActiveItemLine(__instance);
            var hasActiveReplacement = isActiveItemLine
                && InventoryLineFontFixer.HasActiveReplacementForCurrentItemText(__instance);
            var shouldRefreshActiveItemLine = isActiveItemLine && !hasActiveReplacement;
            var refreshSucceeded = false;
#if QUDJP_DEV_BUILD
            var scheduledRepair = false;
#endif
            string? currentText = null;

            if (!isActiveItemLine || hasActiveReplacement)
            {
                InventoryLineFontFixer.ForgetSuccessfulRefreshForLine(__instance);
            }

            if (shouldRefreshActiveItemLine)
            {
                var preRefreshKey = InventoryLineFontFixer.GetActiveItemLineRefreshKey(__instance);
                if (string.IsNullOrEmpty(preRefreshKey))
                {
                    InventoryLineFontFixer.ForgetSuccessfulRefreshForLine(__instance);
                }
                else if (InventoryLineFontFixer.HasHealthySuccessfulRefreshForCurrentKey(__instance, preRefreshKey))
                {
                    refreshSucceeded = true;
                }
                else
                {
                    refreshSucceeded = InventoryLineFontFixer.TryRefreshActiveItemLine(__instance);
                    if (refreshSucceeded)
                    {
                        InventoryLineFontFixer.RecordSuccessfulRefreshForCurrentKey(
                            __instance,
                            InventoryLineFontFixer.GetActiveItemLineRefreshKey(__instance));
                    }
                }
            }

            if (shouldRefreshActiveItemLine && !refreshSucceeded)
            {
                currentText = InventoryLineFontFixer.GetActiveItemLineText(__instance);
                InventoryLineFontFixer.ForgetSuccessfulRefreshForLine(__instance);
                DelayedInventoryLineRepairScheduler.ScheduleRepairForCurrentText(
                    __instance,
                    currentText);
#if QUDJP_DEV_BUILD
                scheduledRepair = true;
#endif
            }

#if QUDJP_DEV_BUILD
            if (RuntimeDiagnostics.VerboseProbesEnabled)
            {
                InventoryLineTmpLifecycleObservability.LogActiveRefreshDecision(
                    __instance,
                    isActiveItemLine,
                    hasActiveReplacement,
                    refreshSucceeded,
                    scheduledRepair,
                    currentText);
            }
#endif
#else
            _ = __instance;
#endif
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: InventoryLineActiveTextRefreshPatch.Postfix failed: {0}", ex);
        }
    }
}
