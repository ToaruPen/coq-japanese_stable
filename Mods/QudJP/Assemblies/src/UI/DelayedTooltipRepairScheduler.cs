#if HAS_TMP
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;
#endif

namespace QudJP;

internal static class DelayedTooltipRepairScheduler
{
#if HAS_TMP
    private static readonly ConcurrentDictionary<int, byte> Scheduled = new();
    private static RepairHost? host;

    internal static void ScheduleRepair(object? triggerInstance)
    {
        if (triggerInstance is not Component component
            || !TooltipTextRepairer.ShouldScheduleRepair(triggerInstance))
        {
            return;
        }

        var triggerId = component.GetInstanceID();
        if (!Scheduled.TryAdd(triggerId, 0))
        {
            return;
        }

        var runner = EnsureHost();
        if (runner == null)
        {
            Scheduled.TryRemove(triggerId, out _);
            return;
        }

        try
        {
            runner.StartCoroutine(RunRepair(component, triggerId));
        }
        catch
        {
            Scheduled.TryRemove(triggerId, out _);
            throw;
        }
    }

    private static RepairHost? EnsureHost()
    {
        if (host != null)
        {
            return host;
        }

        var gameObject = new GameObject("QudJP.DelayedTooltipRepairHost");
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        host = gameObject.AddComponent<RepairHost>();
        _ = host.Touch();
        return host;
    }

    private static IEnumerator RunRepair(Component trigger, int triggerId)
    {
        try
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            _ = TooltipTextRepairer.TryRepairTooltip(trigger, restoreCanvasRendererVisibility: true);

            yield return null;
            _ = TooltipTextRepairer.TryRepairTooltip(trigger, restoreCanvasRendererVisibility: true);
        }
        finally
        {
            Scheduled.TryRemove(triggerId, out _);
        }
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
