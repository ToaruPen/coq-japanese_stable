using System;
using System.Collections.Concurrent;

namespace QudJP;

internal static class SinkObservation
{
    internal const string ObservationOnlyDetail = "ObservationOnly";
    private const string StructuredFamily = "sink_observe";
    private const string ProbeVersion = "v1";
    private const int MaxObservedEntries = 4096;
    private const int MaxValueLength = 200;
    private const string OverflowKey = "__overflow__";

    [ThreadStatic]
    private static int suppressionDepth;

    private static readonly ConcurrentDictionary<string, int> HitCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static void ResetForTests()
    {
        HitCounts.Clear();
        suppressionDepth = 0;
    }

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
        if (suppressionDepth > 0)
        {
            return;
        }

        var normalizedSink = ObservabilityHelpers.NormalizeContext(sink);
        var normalizedRoute = ObservabilityHelpers.ExtractPrimaryContext(route);
        var normalizedDetail = ObservabilityHelpers.NormalizeContext(detail);
        var sourceValue = source ?? string.Empty;
        var strippedValue = stripped ?? string.Empty;

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

        QudJPMod.LogToUnity(
            "[QudJP] SinkObserve/" + ProbeVersion +
            ": sink='" + ObservabilityHelpers.SanitizeForLog(normalizedSink, MaxValueLength) +
            "' route='" + ObservabilityHelpers.SanitizeForLog(normalizedRoute, MaxValueLength) +
            "' detail='" + ObservabilityHelpers.SanitizeForLog(normalizedDetail, MaxValueLength) +
            "' source='" + ObservabilityHelpers.SanitizeForLog(sourceValue, MaxValueLength) +
            "' stripped='" + ObservabilityHelpers.SanitizeForLog(strippedValue, MaxValueLength) + "'"
            + ObservabilityHelpers.BuildHelperStructuredSuffix(normalizedRoute, StructuredFamily, sourceValue));
    }

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
