using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace QudJP;

internal static class ObservabilityHelpers
{
    internal const string ContextSeparator = " > ";
    internal const string NoContextLabel = "<no-context>";

    internal static string ComposeContext(string? primaryContext, params string?[] details)
    {
        var normalizedPrimary = NormalizeContext(primaryContext);
        if (details is null || details.Length == 0)
        {
            return normalizedPrimary;
        }

        var builder = new StringBuilder(normalizedPrimary);
        for (var index = 0; index < details.Length; index++)
        {
            var detail = details[index]?.Trim();
            if (string.IsNullOrEmpty(detail))
            {
                continue;
            }

            builder.Append(ContextSeparator);
            builder.Append(detail);
        }

        return builder.ToString();
    }

    internal static string ExtractPrimaryContext(string? context)
    {
        var normalized = NormalizeContext(context);
        var separatorIndex = normalized.IndexOf(ContextSeparator, StringComparison.Ordinal);
        return separatorIndex < 0 ? normalized : normalized.Substring(0, separatorIndex);
    }

    internal static bool ShouldLogMissingHit(int hitCount)
    {
        return hitCount > 0 && (hitCount & (hitCount - 1)) == 0;
    }

    internal static int GetCounterValue(ConcurrentDictionary<string, int> counters, string key)
    {
        return counters.TryGetValue(key, out var count) ? count : 0;
    }

    internal static string BuildRankedSummary(
        string prefix,
        string label,
        ConcurrentDictionary<string, int> counters,
        int maxEntries)
    {
        var boundedMaxEntries = maxEntries <= 0 ? 1 : maxEntries;
        var entries = counters.ToArray();
        if (entries.Length == 0)
        {
            return $"{prefix}: {label}: none.";
        }

        Array.Sort(entries, CompareCounterEntries);
        var limit = Math.Min(boundedMaxEntries, entries.Length);
        var builder = new StringBuilder();
        builder.Append(prefix);
        builder.Append(": ");
        builder.Append(label);
        builder.Append(": ");
        for (var index = 0; index < limit; index++)
        {
            if (index > 0)
            {
                builder.Append("; ");
            }

            builder.Append(entries[index].Key);
            builder.Append('=');
            builder.Append(entries[index].Value);
        }

        builder.Append('.');
        return builder.ToString();
    }

    internal static string BuildRankedCounterBody(
        IDictionary<string, int> counters,
        int maxEntries)
    {
        var boundedMaxEntries = maxEntries <= 0 ? 1 : maxEntries;
        var entries = new KeyValuePair<string, int>[counters.Count];
        counters.CopyTo(entries, 0);
        Array.Sort(entries, CompareCounterEntries);

        var limit = Math.Min(boundedMaxEntries, entries.Length);
        var builder = new StringBuilder();
        for (var index = 0; index < limit; index++)
        {
            if (index > 0)
            {
                builder.Append("; ");
            }

            builder.Append(entries[index].Key);
            builder.Append('=');
            builder.Append(entries[index].Value);
        }

        return builder.ToString();
    }

    internal static int CompareCounterEntries(
        KeyValuePair<string, int> left,
        KeyValuePair<string, int> right)
    {
        var countComparison = right.Value.CompareTo(left.Value);
        return countComparison != 0
            ? countComparison
            : StringComparer.Ordinal.Compare(left.Key, right.Key);
    }

    internal static int IncrementCounter(string _, int currentValue)
    {
        return currentValue < int.MaxValue ? currentValue + 1 : int.MaxValue;
    }

    internal static string NormalizeContext(string? context)
    {
        if (context is null)
        {
            return NoContextLabel;
        }

        var trimmedContext = context.Trim();
        return trimmedContext.Length == 0 ? NoContextLabel : trimmedContext;
    }
}
