#if HAS_TMP
using System.Collections;
using System.Collections.Concurrent;
using TMPro;
using UnityEngine;
#endif

namespace QudJP;

internal static class DelayedInventoryLineRepairScheduler
{
#if HAS_TMP
    private const int MaxAttemptsPerLine = 2;

    private static readonly ConcurrentDictionary<int, int> AttemptCounts = new();
    private static readonly ConcurrentDictionary<int, string> LastScheduledTextByLine = new();
    private static readonly ConcurrentDictionary<int, byte> Scheduled = new();

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

    internal static void ScheduleVisibleInventoryRepairs()
    {
        var allComponents = Resources.FindObjectsOfTypeAll<Component>();
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

            ScheduleRepair(component, resetAttempts: true);
        }
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

            var replaced = TextShellReplacementRenderer.TryRenderReplacementTexts(component, out _);

            yield return null;

            if (replaced > 0)
            {
                _ = TmpTextRepairer.TryRepairInvisibleTexts(component);
                RuntimeDiagnostics.RunVerboseProbe(() => BuildVerboseRepairProbeSnapshots(component));
            }

        }
        finally
        {
            Scheduled.TryRemove(lineId, out _);
        }
    }

    private static void BuildVerboseRepairProbeSnapshots(Component component)
    {
#if QUDJP_DEV_BUILD
        _ = TextShellReplacementRenderer.TryBuildReplacementState(
            component,
            "InventoryLineReplacementStateNextFrame/v1",
            out _);
        _ = ScreenHierarchyObservability.TryBuildLineItemSnapshot(component, "InventoryLineItemProbe/v1", out _);
#else
        _ = component;
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
