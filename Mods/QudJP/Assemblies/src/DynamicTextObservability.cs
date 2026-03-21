using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

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
            " source='" + SanitizeForLog(sourceValue) +
            "' translated='" + SanitizeForLog(translatedValue) +
            "'." + Translator.GetCurrentLogContextSuffix());
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

    private static string SanitizeForLog(string value)
    {
#if NET48
        var sanitized = value.Length > MaxValueLength
            ? value.Substring(0, MaxValueLength) + "..."
            : value;
#else
        var sanitized = value.Length > MaxValueLength
            ? string.Concat(value.AsSpan(0, MaxValueLength), "...")
            : value;
#endif

        var builder = new StringBuilder(sanitized.Length);
        for (var index = 0; index < sanitized.Length; index++)
        {
            var character = sanitized[index];
            if (character == '\n')
            {
                builder.Append("\\n");
            }
            else if (character == '\r')
            {
                builder.Append("\\r");
            }
            else if (character == '\t')
            {
                builder.Append("\\t");
            }
            else if (char.IsControl(character))
            {
                builder.Append("\\u");
                builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
