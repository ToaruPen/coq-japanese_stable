using System;
#if HAS_TMP
using UnityEngine;
#endif

namespace QudJP;

internal static class CompareProbeRunner
{
    internal static void Run(object screenInstance)
    {
#if HAS_TMP
#if QUDJP_DEV_BUILD
        var verboseProbesEnabled = RuntimeDiagnostics.VerboseProbesEnabled;

        if (verboseProbesEnabled
            && TmpTextRepairer.TryBuildTextShellLeafProbe(screenInstance, "HandleSelectItemLeafReachBefore/v1", out var beforeLeafLog)
            && beforeLeafLog is not null
            && beforeLeafLog.Length > 0)
        {
            LogProbe(beforeLeafLog);
        }

        var repaired = TmpTextRepairer.TryRepairInvisibleTexts(screenInstance);
        if (verboseProbesEnabled && repaired > 0)
        {
            LogProbe(TmpTextRepairer.BuildRepairLog("HandleSelectItemRepair/v1", repaired));
        }

        if (verboseProbesEnabled
            && TmpTextRepairer.TryBuildTextShellLeafProbe(screenInstance, "HandleSelectItemLeafReachAfter/v1", out var afterLeafLog)
            && afterLeafLog is not null
            && afterLeafLog.Length > 0)
        {
            LogProbe(afterLeafLog);
        }

        if (verboseProbesEnabled
            && UiChildTextObservability.TryBuildSnapshot(screenInstance, "HandleSelectItemProbe/v1", out var logLine)
            && logLine is not null
            && logLine.Length > 0)
        {
            LogProbe(logLine);
        }

        if (verboseProbesEnabled
            && ComparePopupTextFixer.TryRepairActiveComparePopup(out var comparePopupRepairLog)
            && comparePopupRepairLog is not null
            && comparePopupRepairLog.Length > 0)
        {
            LogProbe(comparePopupRepairLog);
        }
        else if (!verboseProbesEnabled)
        {
            _ = ComparePopupTextFixer.RepairActiveComparePopup();
        }

        if (verboseProbesEnabled
            && ScreenHierarchyObservability.TryBuildNeighborhoodSnapshot(screenInstance, "CompareHierarchyProbe/v1", out var hierarchyLogLine)
            && hierarchyLogLine is not null
            && hierarchyLogLine.Length > 0)
        {
            LogProbe(hierarchyLogLine);
        }

        if (verboseProbesEnabled)
        {
            var focusedBranchLogs = ScreenHierarchyObservability.BuildFocusedBranchSnapshots(screenInstance, "CompareBranchProbe/v1");
            for (var index = 0; index < focusedBranchLogs.Length; index++)
            {
                LogProbe(focusedBranchLogs[index]);
            }
        }

        if (verboseProbesEnabled
            && SceneTextObservability.TryBuildCompareSceneSnapshot("CompareSceneProbe/v1", out var sceneLogLine)
            && sceneLogLine is not null
            && sceneLogLine.Length > 0)
        {
            LogProbe(sceneLogLine);
        }
#else
        _ = TmpTextRepairer.TryRepairInvisibleTexts(screenInstance);
        _ = ComparePopupTextFixer.RepairActiveComparePopup();
#endif
        DelayedSceneProbeScheduler.ScheduleCompareSceneProbe(screenInstance);
#else
        _ = screenInstance;
#endif
    }

    internal static void RunFromTrigger(object triggerInstance)
    {
#if HAS_TMP
        var screen = ResolveInventoryScreen(triggerInstance);
        if (screen is not null)
        {
            Run(screen);
            return;
        }

        if (triggerInstance is Component component)
        {
            RuntimeDiagnostics.LogVerboseProbe(() =>
                "[QudJP] CompareProbeRunner: failed to resolve InventoryAndEquipmentStatusScreen from trigger='"
                + component.GetType().FullName + "' object='" + component.gameObject.name + "'");
        }
#endif
    }

#if HAS_TMP
    private const string InventoryScreenTypeName = "Qud.UI.InventoryAndEquipmentStatusScreen";

    private static object? ResolveInventoryScreen(object triggerInstance)
    {
        if (string.Equals(triggerInstance.GetType().FullName, InventoryScreenTypeName, StringComparison.Ordinal))
        {
            return triggerInstance;
        }

        if (triggerInstance is not Component component)
        {
            return null;
        }

        var current = component.transform;
        while (current is not null)
        {
            var components = current.GetComponents<Component>();
            for (var index = 0; index < components.Length; index++)
            {
                var currentComponent = components[index];
                if (currentComponent is null)
                {
                    continue;
                }

                if (string.Equals(currentComponent.GetType().FullName, InventoryScreenTypeName, StringComparison.Ordinal))
                {
                    return currentComponent;
                }
            }

            current = current.parent;
        }

        var root = component.transform.root;
        if (root is null)
        {
            return null;
        }

        var descendants = root.GetComponentsInChildren<Component>(includeInactive: true);
        for (var index = 0; index < descendants.Length; index++)
        {
            var currentComponent = descendants[index];
            if (currentComponent is null)
            {
                continue;
            }

            if (string.Equals(currentComponent.GetType().FullName, InventoryScreenTypeName, StringComparison.Ordinal))
            {
                return currentComponent;
            }
        }

        return null;
    }
#endif

    internal static void LogProbe(string message)
    {
        RuntimeDiagnostics.LogVerboseProbe(() => message);
    }
}
