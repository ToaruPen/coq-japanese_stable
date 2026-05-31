using System;
using System.Collections.Generic;
using System.Text;

namespace QudJP.Patches;

internal static class PopupTranslatedMessageHandoff
{
    private const int MaxPendingEntries = 8;

    [ThreadStatic]
    private static List<Entry>? pendingEntries;

    [ThreadStatic]
    private static List<int>? activeScopes;

    [ThreadStatic]
    private static int nextScopeId;

    internal static void EnterScope()
    {
        EnterScope(out _);
    }

    internal static void EnterScope(out int scopeId)
    {
        scopeId = ++nextScopeId;
        if (activeScopes is null)
        {
            activeScopes = new List<int>();
        }

        activeScopes.Add(scopeId);
    }

    internal static void ExitCurrentScope()
    {
        var scopeId = GetCurrentScopeId();
        if (scopeId != 0)
        {
            ExitScope(scopeId);
        }
    }

    internal static void ExitScope(int scopeId)
    {
        RemovePendingEntriesForScope(scopeId);

        var scopes = activeScopes;
        if (scopes is null)
        {
            return;
        }

        for (var index = scopes.Count - 1; index >= 0; index--)
        {
            if (scopes[index] != scopeId)
            {
                continue;
            }

            scopes.RemoveAt(index);
            break;
        }

        if (scopes.Count == 0)
        {
            activeScopes = null;
        }
    }

    internal static void Remember(string source, string translated)
    {
        if (string.IsNullOrEmpty(source)
            || string.IsNullOrEmpty(translated)
            || string.Equals(source, translated, StringComparison.Ordinal))
        {
            return;
        }

        var scopeId = GetCurrentScopeId();
        if (scopeId == 0)
        {
            return;
        }

        var key = CreateKey(source);
        if (key.Visible.Length == 0)
        {
            return;
        }

        if (pendingEntries is null)
        {
            pendingEntries = new List<Entry>();
        }

        pendingEntries.Add(new Entry(scopeId, key.Visible, key.ColorSignature, translated));
        if (pendingEntries.Count > MaxPendingEntries)
        {
            pendingEntries.RemoveAt(0);
        }
    }

    internal static bool TryGet(string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var scopes = activeScopes;
        if (scopes is null || scopes.Count == 0)
        {
            return false;
        }

        var key = CreateKey(source);
        var entries = pendingEntries;
        if (entries is null)
        {
            return false;
        }

        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];
            if (!scopes.Contains(entry.ScopeId)
                || !string.Equals(entry.Visible, key.Visible, StringComparison.Ordinal)
                || !string.Equals(entry.ColorSignature, key.ColorSignature, StringComparison.Ordinal))
            {
                continue;
            }

            entries.RemoveAt(index);
            translated = entry.Translated;
            return true;
        }

        return false;
    }

    internal static bool TryGetFromCurrentScope(string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var scopeId = GetCurrentScopeId();
        var entries = pendingEntries;
        if (scopeId == 0 || entries is null)
        {
            return false;
        }

        var key = CreateKey(source);
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];
            if (entry.ScopeId != scopeId
                || !string.Equals(entry.Visible, key.Visible, StringComparison.Ordinal)
                || !string.Equals(entry.ColorSignature, key.ColorSignature, StringComparison.Ordinal))
            {
                continue;
            }

            entries.RemoveAt(index);
            translated = entry.Translated;
            return true;
        }

        return false;
    }

    internal static void ResetForTests()
    {
        pendingEntries = null;
        activeScopes = null;
        nextScopeId = 0;
    }

    private static int GetCurrentScopeId()
    {
        var scopes = activeScopes;
        return scopes is null || scopes.Count == 0 ? 0 : scopes[scopes.Count - 1];
    }

    private static void RemovePendingEntriesForScope(int scopeId)
    {
        var entries = pendingEntries;
        if (entries is null)
        {
            return;
        }

        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index].ScopeId == scopeId)
            {
                entries.RemoveAt(index);
            }
        }

        if (entries.Count == 0)
        {
            pendingEntries = null;
        }
    }

    private static Key CreateKey(string source)
    {
        var (visible, spans) = ColorAwareTranslationComposer.Strip(source);
        return new Key(visible.Trim(), CreateColorSignature(spans, visible.Length));
    }

    private static string CreateColorSignature(IReadOnlyList<ColorSpan> spans, int visibleLength)
    {
        if (spans.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < spans.Count; index++)
        {
            if (IsDefaultYellowNoise(spans[index], spans, index, visibleLength)
                || !TryGetOpeningColorFamily(spans[index].Token, out var family))
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

    private static bool IsDefaultYellowNoise(
        ColorSpan span,
        IReadOnlyList<ColorSpan> spans,
        int spanPosition,
        int visibleLength)
    {
        if (string.Equals(span.Token, "&y", StringComparison.Ordinal))
        {
            return span.Index == visibleLength
                || span.Index > 0 && HasNonYellowShorthandBefore(spans, spanPosition)
                || HasQudYellowAndNonYellowOpeningAtIndex(spans, span.Index)
                || span.Index == 0 && HasForegroundYellowResetAfterNonYellowShorthand(spans);
        }

        return string.Equals(span.Token, "{{y|", StringComparison.Ordinal)
            && HasForegroundYellowAtIndex(spans, span.Index)
            && HasForegroundYellowResetAfterNonYellowShorthand(spans);
    }

    private static bool HasQudYellowAndNonYellowOpeningAtIndex(IReadOnlyList<ColorSpan> spans, int index)
    {
        return HasForegroundYellowAtIndex(spans, index) && IsNonYellowOpeningAtIndex(spans, index);
    }

    private static bool HasForegroundYellowAtIndex(IReadOnlyList<ColorSpan> spans, int index)
    {
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            if (spans[spanIndex].Index == index && string.Equals(spans[spanIndex].Token, "&y", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNonYellowShorthandBefore(IReadOnlyList<ColorSpan> spans, int spanPosition)
    {
        for (var index = spanPosition - 1; index >= 0; index--)
        {
            var token = spans[index].Token;
            if (token.Length == 2 && token[0] == '&' && token[1] != 'y')
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasForegroundYellowResetAfterNonYellowShorthand(IReadOnlyList<ColorSpan> spans)
    {
        var sawNonYellowShorthand = false;
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            if (span.Token.Length == 2 && span.Token[0] == '&' && span.Token[1] != 'y')
            {
                sawNonYellowShorthand = true;
                continue;
            }

            if (sawNonYellowShorthand && string.Equals(span.Token, "&y", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNonYellowOpeningAtIndex(IReadOnlyList<ColorSpan> spans, int index)
    {
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            if (span.Index != index)
            {
                continue;
            }

            var token = span.Token;
            if (string.IsNullOrEmpty(token)
                || string.Equals(token, "}}", StringComparison.Ordinal)
                || string.Equals(token, "</color>", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "&y", StringComparison.Ordinal)
                || string.Equals(token, "^y", StringComparison.Ordinal)
                || string.Equals(token, "{{y|", StringComparison.Ordinal))
            {
                continue;
            }

            if (token.StartsWith("{{", StringComparison.Ordinal)
                || token.Length == 2 && (token[0] == '&' || token[0] == '^')
                || token.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
            return true;
        }

        if (token.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
        {
            family = token;
            return true;
        }

        return false;
    }

    private readonly struct Key
    {
        internal Key(string visible, string colorSignature)
        {
            Visible = visible;
            ColorSignature = colorSignature;
        }

        internal string Visible { get; }

        internal string ColorSignature { get; }
    }

    private sealed class Entry
    {
        internal Entry(int scopeId, string visible, string colorSignature, string translated)
        {
            ScopeId = scopeId;
            Visible = visible;
            ColorSignature = colorSignature;
            Translated = translated;
        }

        internal int ScopeId { get; }

        internal string Visible { get; }

        internal string ColorSignature { get; }

        internal string Translated { get; }
    }
}
