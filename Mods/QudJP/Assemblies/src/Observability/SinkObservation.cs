using System;
#if QUDJP_DEV_BUILD
using System.Collections.Concurrent;
#endif

namespace QudJP;

internal static class SinkObservation
{
    internal const string ObservationOnlyDetail = "ObservationOnly";
#if QUDJP_DEV_BUILD
    private const string StructuredFamily = "sink_observe";
    private const string ProbeVersion = "v1";
    private const int MaxObservedEntries = 4096;
    private const int MaxValueLength = 200;
    private const string OverflowKey = "__overflow__";
#endif

    [ThreadStatic]
    private static int suppressionDepth;

#if QUDJP_DEV_BUILD
    private static readonly ConcurrentDictionary<string, int> HitCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
#endif

    internal static void ResetForTests()
    {
#if QUDJP_DEV_BUILD
        HitCounts.Clear();
#endif
        suppressionDepth = 0;
    }

#if QUDJP_DEV_BUILD
    internal static int GetHitCountForTests(
        string sink,
        string route,
        string detail,
        string source,
        string stripped)
    {
        return ObservabilityHelpers.GetCounterValue(
            HitCounts,
            BuildCounterKey(
                ObservabilityHelpers.NormalizeContext(sink),
                ObservabilityHelpers.ExtractPrimaryContext(route),
                ObservabilityHelpers.NormalizeContext(detail),
                source,
                stripped));
    }
#endif

    internal static IDisposable PushSuppression(bool suppress)
    {
        if (!suppress)
        {
            return NoopScope.Instance;
        }

        suppressionDepth++;
        return SuppressionScope.Instance;
    }

    internal static void LogUnclaimed(
        string sink,
        string route,
        string detail,
        string source,
        string stripped)
    {
#if QUDJP_DEV_BUILD
        if (!RuntimeDiagnostics.VerboseProbesEnabled)
        {
            return;
        }

        if (suppressionDepth > 0)
        {
            return;
        }

        var normalizedSink = ObservabilityHelpers.NormalizeContext(sink);
        var normalizedRoute = ObservabilityHelpers.ExtractPrimaryContext(route);
        var normalizedDetail = ObservabilityHelpers.NormalizeContext(detail);
        var sourceValue = source ?? string.Empty;
        var strippedValue = stripped ?? string.Empty;

        FinalOutputObservability.RecordSinkUnclaimed(
            normalizedSink,
            normalizedRoute,
            normalizedDetail,
            sourceValue,
            strippedValue);

        var hitCount = AddOrUpdateCapped(
            HitCounts,
            BuildCounterKey(
                normalizedSink,
                normalizedRoute,
                normalizedDetail,
                sourceValue,
                strippedValue),
            MaxObservedEntries);
        if (!ObservabilityHelpers.ShouldLogMissingHit(hitCount))
        {
            return;
        }

        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] SinkObserve/" + ProbeVersion +
            ": sink='" + ObservabilityHelpers.SanitizeForLog(normalizedSink, MaxValueLength) +
            "' route='" + ObservabilityHelpers.SanitizeForLog(normalizedRoute, MaxValueLength) +
            "' detail='" + ObservabilityHelpers.SanitizeForLog(normalizedDetail, MaxValueLength) +
            "' source='" + ObservabilityHelpers.SanitizeForLog(sourceValue, MaxValueLength) +
            "' stripped='" + ObservabilityHelpers.SanitizeForLog(strippedValue, MaxValueLength) + "'"
            + ObservabilityHelpers.BuildHelperStructuredSuffix(normalizedRoute, StructuredFamily, sourceValue));
#else
        _ = sink;
        _ = route;
        _ = detail;
        _ = source;
        _ = stripped;
#endif
    }

#if QUDJP_DEV_BUILD
    private static string BuildCounterKey(
        string sink,
        string route,
        string detail,
        string source,
        string stripped)
    {
        return sink
            + ObservabilityHelpers.ContextSeparator
            + route
            + ObservabilityHelpers.ContextSeparator
            + detail
            + ObservabilityHelpers.ContextSeparator
            + source
            + ObservabilityHelpers.ContextSeparator
            + stripped;
    }

    private static int AddOrUpdateCapped(ConcurrentDictionary<string, int> counters, string key, int maxKeys)
    {
        if (counters.ContainsKey(key) || counters.Count < maxKeys)
        {
            return counters.AddOrUpdate(key, 1, ObservabilityHelpers.IncrementCounter);
        }

        return counters.AddOrUpdate(OverflowKey, 1, ObservabilityHelpers.IncrementCounter);
    }
#endif

    private sealed class SuppressionScope : IDisposable
    {
        internal static readonly SuppressionScope Instance = new SuppressionScope();

        private SuppressionScope()
        {
        }

        public void Dispose()
        {
            Release();
        }

        private static void Release()
        {
            if (suppressionDepth > 0)
            {
                suppressionDepth--;
            }
        }
    }

    private sealed class NoopScope : IDisposable
    {
        internal static readonly NoopScope Instance = new NoopScope();

        private NoopScope()
        {
        }

        public void Dispose()
        {
        }
    }
}
