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

    internal static void ExitCurrentScope(bool retainPendingEntries = false)
    {
        var scopeId = GetCurrentScopeId();
        if (scopeId != 0)
        {
            ExitScope(scopeId, retainPendingEntries);
        }
    }

    internal static void ExitScope(int scopeId, bool retainPendingEntries = false)
    {
        if (retainPendingEntries)
        {
            DetachPendingEntriesForScope(scopeId);
        }
        else
        {
            RemovePendingEntriesForScope(scopeId);
        }

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

        var key = CreateKey(source);
        var entries = pendingEntries;
        if (entries is null)
        {
            return false;
        }

        var scopeId = GetCurrentScopeId();
        if (scopeId == 0)
        {
            Entry? detachedMatch = null;
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                var entry = entries[index];
                if (entry.ScopeId != 0
                    || !string.Equals(entry.Visible, key.Visible, StringComparison.Ordinal)
                    || !string.Equals(entry.ColorSignature, key.ColorSignature, StringComparison.Ordinal))
                {
                    continue;
                }

                detachedMatch = entry;
                break;
            }

            RemoveDetachedEntries();
            if (detachedMatch is null)
            {
                return false;
            }

            translated = detachedMatch.Translated;
            return true;
        }

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

    private static void DetachPendingEntriesForScope(int scopeId)
    {
        var entries = pendingEntries;
        if (entries is null)
        {
            return;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].ScopeId == scopeId)
            {
                entries[index].Detach();
            }
        }
    }

    private static void RemoveDetachedEntries()
    {
        var entries = pendingEntries;
        if (entries is null)
        {
            return;
        }

        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index].ScopeId == 0)
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
            return family.Length > 0 && !string.Equals(family, "y", StringComparison.Ordinal);
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

        internal int ScopeId { get; private set; }

        internal string Visible { get; }

        internal string ColorSignature { get; }

        internal string Translated { get; }

        internal void Detach()
        {
            ScopeId = 0;
        }
    }
}
