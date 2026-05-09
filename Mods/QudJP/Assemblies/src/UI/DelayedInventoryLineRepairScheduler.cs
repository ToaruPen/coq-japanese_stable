#if HAS_TMP
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class DelayedInventoryLineRepairScheduler
{
#if HAS_TMP
    private const int MaxAttemptsPerLine = 2;
#if QUDJP_DEV_BUILD
    private const int MaxEvidenceLogs = 48;
    private const string VisibleRepairScanProbeName = "InventoryLineVisibleRepairScan/v1";
#endif

    private static readonly ConcurrentDictionary<int, int> AttemptCounts = new();
    private static readonly ConcurrentDictionary<int, string> LastScheduledTextByLine = new();
    private static readonly ConcurrentDictionary<int, byte> Scheduled = new();
    private static int visibleProbeScanScheduled;
#if QUDJP_DEV_BUILD
    private static int evidenceLogCount;
#endif

    private static RepairHost? host;

    internal static void ScheduleRepair(object? lineInstance)
    {
        ScheduleRepair(lineInstance, resetAttempts: false);
    }

    internal static void ScheduleRepair(object? lineInstance, bool resetAttempts)
    {
        if (lineInstance is not Component component)
        {
            return;
        }

        var lineId = component.GetInstanceID();
        if (resetAttempts)
        {
            AttemptCounts.TryRemove(lineId, out _);
        }

        var attempts = AttemptCounts.TryGetValue(lineId, out var existing) ? existing : 0;
        if (attempts >= MaxAttemptsPerLine || !Scheduled.TryAdd(lineId, 0))
        {
            return;
        }

        var runner = EnsureHost();
        if (runner is null)
        {
            Scheduled.TryRemove(lineId, out _);
            return;
        }

        _ = AttemptCounts.AddOrUpdate(lineId, 1, static (_, current) => current + 1);
        runner.StartCoroutine(RunRepair(component, lineId));
    }

    internal static void ScheduleRepairForCurrentText(object? lineInstance, string? currentText)
    {
        var textKey = currentText ?? string.Empty;
        if (textKey.Length == 0 || lineInstance is not Component component)
        {
            return;
        }

        var lineId = component.GetInstanceID();
        if (LastScheduledTextByLine.TryGetValue(lineId, out var previousText)
            && !string.Equals(previousText, textKey, System.StringComparison.Ordinal))
        {
            AttemptCounts.TryRemove(lineId, out _);
        }

        LastScheduledTextByLine[lineId] = textKey;
        ScheduleRepair(component);
    }

    internal static void LogVisibleInventoryProbeSnapshots(object? inventoryScreenInstance)
    {
        if (ReflectionUtils.GetPropertyOrFieldValue(inventoryScreenInstance, "inventoryController") is not Component inventoryController)
        {
            LogVisibleRepairScanProbe(inventoryScreenInstance, componentCount: 0, candidateCount: 0, controllerFound: false);
            return;
        }

        var allComponents = inventoryController.GetComponentsInChildren<Component>(includeInactive: true);
        var candidateCount = 0;
        for (var index = 0; index < allComponents.Length; index++)
        {
            var component = allComponents[index];
            if (component is null
                || component.gameObject is null
                || !component.gameObject.activeInHierarchy)
            {
                continue;
            }

            var type = component.GetType();
            if (!string.Equals(type.FullName, "Qud.UI.InventoryLine", System.StringComparison.Ordinal))
            {
                continue;
            }

            candidateCount++;
            InventoryLineTmpLifecycleObservability.LogOriginalTmpLifecycle(
                component,
                "visible-scan-candidate",
                forceMesh: false);
        }

        LogVisibleRepairScanProbe(
            inventoryScreenInstance,
            allComponents.Length,
            candidateCount,
            controllerFound: true);
    }

    internal static void ScheduleVisibleInventoryProbeSnapshotsAfterDelay(object? inventoryScreenInstance)
    {
        if (Interlocked.Exchange(ref visibleProbeScanScheduled, 1) == 1)
        {
            return;
        }

        var runner = EnsureHost();
        if (runner is null)
        {
            Interlocked.Exchange(ref visibleProbeScanScheduled, 0);
            return;
        }

        runner.StartCoroutine(RunVisibleInventoryProbeScan(inventoryScreenInstance));
    }

    private static RepairHost? EnsureHost()
    {
        if (host is not null)
        {
            return host;
        }

        var gameObject = new GameObject("QudJP.DelayedInventoryLineRepairHost");
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        host = gameObject.AddComponent<RepairHost>();
        _ = host.Touch();
        return host;
    }

    private static IEnumerator RunRepair(Component component, int lineId)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;

        try
        {
            if (component is null)
            {
                yield break;
            }

            InventoryLineTmpLifecycleObservability.LogOriginalTmpLifecycle(
                component,
                "repair-before-replacement",
                forceMesh: false);
            var replaced = TextShellReplacementRenderer.TryRenderReplacementTexts(component, out var replacementLogLine);

            yield return null;

            if (replaced > 0)
            {
                _ = TmpTextRepairer.TryRepairInvisibleTexts(component);
                if (RuntimeDiagnostics.VerboseProbesEnabled)
                {
                    RuntimeDiagnostics.RunVerboseProbe(() =>
                    {
                        LogInventoryReplacementEvidence(replacementLogLine);
                        LogVerboseRepairProbeSnapshots(component);
                    });
                }
            }

        }
        finally
        {
            Scheduled.TryRemove(lineId, out _);
        }
    }

    private static IEnumerator RunVisibleInventoryProbeScan(object? inventoryScreenInstance)
    {
        try
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            LogVisibleInventoryProbeSnapshots(inventoryScreenInstance);
        }
        finally
        {
            Interlocked.Exchange(ref visibleProbeScanScheduled, 0);
        }
    }

    private static void LogVisibleRepairScanProbe(
        object? inventoryScreenInstance,
        int componentCount,
        int candidateCount,
        bool controllerFound)
    {
#if QUDJP_DEV_BUILD
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] " + VisibleRepairScanProbeName + ": "
            + $"screen='{inventoryScreenInstance?.GetType().FullName ?? "<null>"}' "
            + $"controllerFound={controllerFound.ToString()} "
            + $"components={componentCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
            + $"candidates={candidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
#else
        _ = inventoryScreenInstance;
        _ = componentCount;
        _ = candidateCount;
        _ = controllerFound;
#endif
    }

    private static void LogVerboseRepairProbeSnapshots(Component component)
    {
#if QUDJP_DEV_BUILD
        if (TextShellReplacementRenderer.TryBuildReplacementState(
            component,
            "InventoryLineReplacementStateNextFrame/v1",
            out var stateLogLine))
        {
            LogInventoryReplacementEvidence(stateLogLine);
        }

        if (ScreenHierarchyObservability.TryBuildLineItemSnapshot(
            component,
            "InventoryLineItemProbe/v1",
            out var itemLogLine))
        {
            LogInventoryReplacementEvidence(itemLogLine);
        }
#else
        _ = component;
#endif
    }

    private static void LogInventoryReplacementEvidence(string? logLine)
    {
#if QUDJP_DEV_BUILD
        if (string.IsNullOrEmpty(logLine))
        {
            return;
        }

        if (Interlocked.Increment(ref evidenceLogCount) > MaxEvidenceLogs)
        {
            return;
        }

        RuntimeDiagnostics.LogVerboseProbe(() => logLine!);
#else
        _ = logLine;
#endif
    }

    private sealed class RepairHost : MonoBehaviour
    {
        internal int Touch()
        {
            return GetInstanceID();
        }
    }
#endif
}
