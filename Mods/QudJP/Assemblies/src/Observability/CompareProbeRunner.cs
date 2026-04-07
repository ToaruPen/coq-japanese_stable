using System;
using System.Diagnostics;
using System.Reflection;
#if HAS_TMP
using UnityEngine;
#endif

namespace QudJP;

internal static class CompareProbeRunner
{
    internal static void Run(object screenInstance)
    {
#if HAS_TMP
        if (TmpTextRepairer.TryBuildTextShellLeafProbe(screenInstance, "HandleSelectItemLeafReachBefore/v1", out var beforeLeafLog)
            && beforeLeafLog is not null
            && beforeLeafLog.Length > 0)
        {
            LogProbe(beforeLeafLog);
        }

        var repaired = TmpTextRepairer.TryRepairInvisibleTexts(screenInstance);
        if (repaired > 0)
        {
            LogProbe(TmpTextRepairer.BuildRepairLog("HandleSelectItemRepair/v1", repaired));
        }

        if (TmpTextRepairer.TryBuildTextShellLeafProbe(screenInstance, "HandleSelectItemLeafReachAfter/v1", out var afterLeafLog)
            && afterLeafLog is not null
            && afterLeafLog.Length > 0)
        {
            LogProbe(afterLeafLog);
        }

        if (UiChildTextObservability.TryBuildSnapshot(screenInstance, "HandleSelectItemProbe/v1", out var logLine)
            && logLine is not null
            && logLine.Length > 0)
        {
            LogProbe(logLine);
        }

        if (ComparePopupTextFixer.TryRepairActiveComparePopup(out var comparePopupRepairLog)
            && comparePopupRepairLog is not null
            && comparePopupRepairLog.Length > 0)
        {
            LogProbe(comparePopupRepairLog);
        }

        if (ScreenHierarchyObservability.TryBuildNeighborhoodSnapshot(screenInstance, "CompareHierarchyProbe/v1", out var hierarchyLogLine)
            && hierarchyLogLine is not null
            && hierarchyLogLine.Length > 0)
        {
            LogProbe(hierarchyLogLine);
        }

        var focusedBranchLogs = ScreenHierarchyObservability.BuildFocusedBranchSnapshots(screenInstance, "CompareBranchProbe/v1");
        for (var index = 0; index < focusedBranchLogs.Length; index++)
        {
            LogProbe(focusedBranchLogs[index]);
        }

        if (SceneTextObservability.TryBuildCompareSceneSnapshot("CompareSceneProbe/v1", out var sceneLogLine)
            && sceneLogLine is not null
            && sceneLogLine.Length > 0)
        {
            LogProbe(sceneLogLine);
        }

        DelayedSceneProbeScheduler.ScheduleCompareSceneProbe(screenInstance);
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
            LogProbe("[QudJP] CompareProbeRunner: failed to resolve InventoryAndEquipmentStatusScreen from trigger='"
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
        try
        {
            var debugType = Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule", throwOnError: false);
            if (debugType is null)
            {
                Trace.TraceWarning(
                    "QudJP: CompareProbeRunner.LogProbe could not find UnityEngine.Debug in UnityEngine.CoreModule. Trying UnityEngine assembly name.");
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
            Trace.TraceWarning("QudJP: CompareProbeRunner.LogProbe fell back to trace. {0}", ex.Message);
        }

        Trace.TraceInformation(message);
    }
}
