using System;
using System.Collections.Generic;
using System.Text;

namespace QudJP;

internal static class LocalizedSearchTextBuilder
{
    internal static string Build(IEnumerable<string?> sourceFragments, IEnumerable<string?> localizedFragments)
    {
        var builder = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AppendFragments(builder, seen, sourceFragments, lowerCase: true);
        AppendFragments(builder, seen, localizedFragments, lowerCase: false);

        return builder.ToString();
    }

    internal static string Build(string? sourceFragment, IEnumerable<string?> localizedFragments)
    {
        return Build(new[] { sourceFragment }, localizedFragments);
    }

    private static void AppendFragments(
        StringBuilder builder,
        HashSet<string> seen,
        IEnumerable<string?> fragments,
        bool lowerCase)
    {
        foreach (var fragment in fragments)
        {
            if (string.IsNullOrWhiteSpace(fragment))
            {
                continue;
            }

            var normalized = NormalizeFragment(fragment!, lowerCase);
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(normalized);
        }
    }

    private static string NormalizeFragment(string fragment, bool lowerCase)
    {
        var trimmed = fragment.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

#pragma warning disable CA1308
        return lowerCase
            ? trimmed.ToLowerInvariant()
            : trimmed;
#pragma warning restore CA1308
    }
}
