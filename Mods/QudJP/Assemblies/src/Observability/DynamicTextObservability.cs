using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace QudJP;

internal static class DynamicTextObservability
{
    private const string ProbeVersion = "v1";
    private const int MaxRouteFamilies = 1024;
    private const string OverflowKey = "__overflow__";
    private const int MaxValueLength = 200;

    private static readonly ConcurrentDictionary<string, int> RouteFamilyCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static void ResetForTests()
    {
        RouteFamilyCounts.Clear();
    }

    internal static int GetRouteFamilyHitCountForTests(string route, string family)
    {
        return ObservabilityHelpers.GetCounterValue(RouteFamilyCounts, BuildCounterKey(route, family));
    }

    internal static void RecordTransform(
        string? route,
        string family,
        string? source,
        string? translated,
        bool logWhenUnchanged = false)
    {
        var sourceValue = source ?? string.Empty;
        var translatedValue = translated ?? string.Empty;
        var changed = !string.Equals(sourceValue, translatedValue, StringComparison.Ordinal);
        if (!changed && !logWhenUnchanged)
        {
            return;
        }

        var normalizedRoute = ObservabilityHelpers.ExtractPrimaryContext(route);
        var structuredRoute = route ?? ObservabilityHelpers.NoContextLabel;
        var counterKey = BuildCounterKey(normalizedRoute, family);
        var hitCount = AddOrUpdateCapped(RouteFamilyCounts, counterKey, MaxRouteFamilies);
        if (!ObservabilityHelpers.ShouldLogMissingHit(hitCount))
        {
            return;
        }

        QudJPMod.LogToUnity(
            "[QudJP] DynamicTextProbe/" + ProbeVersion +
            ": route='" + normalizedRoute +
            "' family='" + family +
            "' hit=" + hitCount.ToString(CultureInfo.InvariantCulture) +
            " changed=" + (changed ? "true" : "false") +
            " source='" + ObservabilityHelpers.SanitizeForLog(sourceValue, MaxValueLength) +
            "' translated='" + ObservabilityHelpers.SanitizeForLog(translatedValue, MaxValueLength) +
            "'." + Translator.GetCurrentLogContextSuffix()
            + ObservabilityHelpers.BuildHelperStructuredSuffix(structuredRoute, family, sourceValue));
    }

    private static string BuildCounterKey(string route, string family)
    {
        return route + ObservabilityHelpers.ContextSeparator + family;
    }

    private static int AddOrUpdateCapped(ConcurrentDictionary<string, int> counters, string key, int maxKeys)
    {
        if (counters.ContainsKey(key) || counters.Count < maxKeys)
        {
            return counters.AddOrUpdate(key, 1, ObservabilityHelpers.IncrementCounter);
        }

        return counters.AddOrUpdate(OverflowKey, 1, ObservabilityHelpers.IncrementCounter);
    }
}
