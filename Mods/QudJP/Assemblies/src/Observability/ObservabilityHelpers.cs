using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace QudJP;

internal static class ObservabilityHelpers
{
    internal const string ContextSeparator = " > ";
    internal const string NoContextLabel = "<no-context>";
    private const string MissingStructuredValue = "<missing>";
    private const int HelperPayloadExcerptLimit = 512;

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

    internal static string SanitizeForLog(string value, int maxLength)
    {
        var boundedValue = maxLength > 0 && value.Length > maxLength
            ? TrimForLog(value, maxLength) + "..."
            : value;
        return EscapeControlCharacters(boundedValue);
    }

    internal static string BuildHelperStructuredSuffix(string route, string family, string payload)
    {
        var sanitizedPayload = EscapeControlCharacters(payload);
        var escapedRoute = EscapeStructuredValue(route);
        var escapedFamily = EscapeStructuredValue(family);
        if (sanitizedPayload.Length <= HelperPayloadExcerptLimit)
        {
            return "; route=" + escapedRoute
                + "; family=" + escapedFamily
                + "; template_id=" + MissingStructuredValue
                + "; payload_mode=full"
                + "; payload_excerpt=" + EscapeStructuredValue(sanitizedPayload)
                + "; payload_sha256=" + MissingStructuredValue;
        }

        return "; route=" + escapedRoute
            + "; family=" + escapedFamily
            + "; template_id=" + MissingStructuredValue
            + "; payload_mode=prefix_hash"
            + "; payload_excerpt=" + EscapeStructuredValue(TrimForLog(sanitizedPayload, HelperPayloadExcerptLimit))
            + "; payload_sha256=" + ComputeSha256Hex(sanitizedPayload);
    }

    internal static string EscapeStructuredValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '\\' or ';' or '=')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string EscapeControlCharacters(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
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

    private static string ComputeSha256Hex(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
#if NET48
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
#else
        var hash = SHA256.HashData(bytes);
#endif
        var builder = new StringBuilder(hash.Length * 2);
        for (var index = 0; index < hash.Length; index++)
        {
            builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string TrimForLog(string value, int maxLength)
    {
#if NET48
        return value.Substring(0, maxLength);
#else
        return new string(value.AsSpan(0, maxLength));
#endif
    }
}
