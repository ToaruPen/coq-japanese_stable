#if HAS_TMP
using System.Collections;
using UnityEngine;
#endif

namespace QudJP;

internal static class DelayedSceneProbeScheduler
{
#if HAS_TMP
    private static ProbeHost? host;
    private static bool compareSceneScheduled;

    internal static void ScheduleCompareSceneProbe(object? screenInstance)
    {
        if (compareSceneScheduled)
        {
            return;
        }

        _ = screenInstance;

        var runner = EnsureHost();
        if (runner is null)
        {
            return;
        }

        SceneTextObservability.ResetBuckets();
        compareSceneScheduled = true;
        runner.StartCoroutine(RunCompareSceneProbe());
    }

    private static ProbeHost? EnsureHost()
    {
        if (host is not null)
        {
            return host;
        }

        var gameObject = new GameObject("QudJP.DelayedSceneProbeHost");
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        host = gameObject.AddComponent<ProbeHost>();
        _ = host.Touch();
        return host;
    }

    private static IEnumerator RunCompareSceneProbe()
    {
        try
        {
            for (var attempt = 0; attempt < 1; attempt++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();

                _ = ComparePopupTextFixer.RepairActiveComparePopup();
                _ = ComparePopupTextFixer.RepairAnyActivePopup();
            }
        }
        finally
        {
            compareSceneScheduled = false;
        }
    }

    private sealed class ProbeHost : MonoBehaviour
    {
        internal int Touch()
        {
            return GetInstanceID();
        }
    }
#endif
}
