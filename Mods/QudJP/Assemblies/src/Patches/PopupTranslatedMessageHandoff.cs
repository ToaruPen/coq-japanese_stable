using System;
using System.Collections.Generic;
using System.Text;

namespace QudJP.Patches;

internal static class PopupTranslatedMessageHandoff
{
    private static readonly object Sync = new();

    private static Entry? pending;

    internal static void Remember(string source, string translated)
    {
        if (string.IsNullOrEmpty(source)
            || string.IsNullOrEmpty(translated)
            || string.Equals(source, translated, StringComparison.Ordinal))
        {
            return;
        }

        var key = CreateKey(source);
        if (key.Visible.Length == 0)
        {
            return;
        }

        lock (Sync)
        {
            pending = new Entry(key.Visible, key.ColorSignature, translated);
        }
    }

    internal static bool TryGet(string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var key = CreateKey(source);
        lock (Sync)
        {
            var entry = pending;
            pending = null;
            if (entry is null
                || !string.Equals(entry.Visible, key.Visible, StringComparison.Ordinal)
                || !string.Equals(entry.ColorSignature, key.ColorSignature, StringComparison.Ordinal))
            {
                return false;
            }

            translated = entry.Translated;
            return true;
        }
    }

    internal static void ResetForTests()
    {
        lock (Sync)
        {
            pending = null;
        }
    }

    private static Key CreateKey(string source)
    {
        var (visible, spans) = ColorAwareTranslationComposer.Strip(source);
        return new Key(visible.Trim(), CreateColorSignature(spans));
    }

    private static string CreateColorSignature(IReadOnlyList<ColorSpan> spans)
    {
        if (spans.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < spans.Count; index++)
        {
            if (!TryGetOpeningColorFamily(spans[index].Token, out var family))
            {
                continue;
            }

            builder.Append(spans[index].Index);
            builder.Append(':');
            builder.Append(family);
            builder.Append(';');
        }

        return builder.ToString();
    }

    private static bool TryGetOpeningColorFamily(string token, out string family)
    {
        family = string.Empty;
        if (string.IsNullOrEmpty(token)
            || string.Equals(token, "}}", StringComparison.Ordinal)
            || string.Equals(token, "</color>", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (token.StartsWith("{{", StringComparison.Ordinal) && token.EndsWith("|", StringComparison.Ordinal))
        {
            family = token.Substring(2, token.Length - 3);
            family = string.Equals(family, "rules", StringComparison.Ordinal) ? "C" : family;
            return family.Length > 0;
        }

        if (token.Length == 2 && (token[0] == '&' || token[0] == '^'))
        {
            family = token[1].ToString();
            return !string.Equals(family, "y", StringComparison.Ordinal);
        }

        if (token.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
        {
            family = token;
            return true;
        }

        return false;
    }

    private readonly record struct Key(string Visible, string ColorSignature);

    private sealed record Entry(string Visible, string ColorSignature, string Translated);
}
