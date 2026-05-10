#if HAS_TMP && QUDJP_DEV_BUILD
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;
#endif

namespace QudJP;

internal static class DelayedBookLineGeometryProbeScheduler
{
#if HAS_TMP && QUDJP_DEV_BUILD
    private static readonly ConcurrentDictionary<int, PendingSnapshot> PendingSnapshots = new();
    private static readonly ConcurrentDictionary<int, byte> Scheduled = new();

    private static ProbeHost? host;

    internal static void ScheduleSnapshot(object? lineInstance, string source, string rendered)
    {
        if (lineInstance is not Component component)
        {
            return;
        }

        var lineId = component.GetInstanceID();
        PendingSnapshots[lineId] = new PendingSnapshot(source, rendered);
        if (!Scheduled.TryAdd(lineId, 0))
        {
            return;
        }

        var runner = EnsureHost();
        if (runner is null)
        {
            Scheduled.TryRemove(lineId, out _);
            PendingSnapshots.TryRemove(lineId, out _);
            return;
        }

        runner.StartCoroutine(RunSnapshot(component, lineId));
    }

    private static ProbeHost? EnsureHost()
    {
        if (host is not null)
        {
            return host;
        }

        var gameObject = new GameObject("QudJP.DelayedBookLineGeometryProbeHost");
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        host = gameObject.AddComponent<ProbeHost>();
        _ = host.Touch();
        return host;
    }

    private static IEnumerator RunSnapshot(Component component, int lineId)
    {
        try
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            if (component is null || !PendingSnapshots.TryGetValue(lineId, out var snapshot))
            {
                yield break;
            }

            if (BookLineGeometryObservability.TryBuildSnapshot(
                component,
                snapshot.Source,
                snapshot.Rendered,
                out var logLine,
                phase: "post-layout"))
            {
                RuntimeDiagnostics.LogVerboseProbe(() => logLine!);
            }
        }
        finally
        {
            Scheduled.TryRemove(lineId, out _);
            PendingSnapshots.TryRemove(lineId, out _);
        }
    }

    private sealed class PendingSnapshot
    {
        internal PendingSnapshot(string source, string rendered)
        {
            Source = source;
            Rendered = rendered;
        }

        internal string Source { get; }

        internal string Rendered { get; }
    }

    private sealed class ProbeHost : MonoBehaviour
    {
        internal int Touch()
        {
            return GetInstanceID();
        }
    }
#else
    internal static void ScheduleSnapshot(object? lineInstance, string source, string rendered)
    {
        _ = lineInstance;
        _ = source;
        _ = rendered;
    }
#endif
}
