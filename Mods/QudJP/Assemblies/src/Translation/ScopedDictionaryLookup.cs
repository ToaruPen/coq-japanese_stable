using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;

namespace QudJP;

internal static class ScopedDictionaryLookup
{
    private static readonly ConcurrentDictionary<string, DictionaryStore> Cache =
        new ConcurrentDictionary<string, DictionaryStore>(StringComparer.OrdinalIgnoreCase);

    internal static string? TranslateExactOrLowerAscii(string source, params string[] dictionaryFileNames)
    {
        if (string.IsNullOrEmpty(source) || dictionaryFileNames.Length == 0)
        {
            return null;
        }

        if (TryGetTranslation(source, null, dictionaryFileNames, out var translated))
        {
            return translated;
        }

        var lowerAscii = StringHelpers.LowerAscii(source);
        if (string.Equals(lowerAscii, source, StringComparison.Ordinal))
        {
            return null;
        }

        return TryGetTranslation(lowerAscii, null, dictionaryFileNames, out translated)
            ? translated
            : null;
    }

    internal static string? TranslateExactOrLowerAsciiForContext(string source, string? context, params string[] dictionaryFileNames)
    {
        if (string.IsNullOrEmpty(source) || dictionaryFileNames.Length == 0)
        {
            return null;
        }

        if (TryGetTranslation(source, context, dictionaryFileNames, out var translated))
        {
            return translated;
        }

        var lowerAscii = StringHelpers.LowerAscii(source);
        if (string.Equals(lowerAscii, source, StringComparison.Ordinal))
        {
            return null;
        }

        return TryGetTranslation(lowerAscii, context, dictionaryFileNames, out translated)
            ? translated
            : null;
    }

    internal static string? TranslateExactOrLowerAsciiForContextOnly(string source, string context, params string[] dictionaryFileNames)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrWhiteSpace(context) || dictionaryFileNames.Length == 0)
        {
            return null;
        }

        if (TryGetContextualTranslation(source, context, dictionaryFileNames, out var translated))
        {
            return translated;
        }

        var lowerAscii = StringHelpers.LowerAscii(source);
        if (string.Equals(lowerAscii, source, StringComparison.Ordinal))
        {
            return null;
        }

        return TryGetContextualTranslation(lowerAscii, context, dictionaryFileNames, out translated)
            ? translated
            : null;
    }

    internal static void ResetForTests()
    {
        Cache.Clear();
    }

    private static bool TryGetTranslation(string source, string? context, IReadOnlyList<string> dictionaryFileNames, out string translated)
    {
        for (var index = 0; index < dictionaryFileNames.Count; index++)
        {
            if (LoadDictionary(dictionaryFileNames[index]).TryGetValue(source, context, out var loadedTranslation))
            {
                translated = loadedTranslation;
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool TryGetContextualTranslation(
        string source,
        string context,
        IReadOnlyList<string> dictionaryFileNames,
        out string translated)
    {
        for (var index = 0; index < dictionaryFileNames.Count; index++)
        {
            if (LoadDictionary(dictionaryFileNames[index]).TryGetContextValue(source, context, out var loadedTranslation))
            {
                translated = loadedTranslation;
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static DictionaryStore LoadDictionary(string dictionaryFileName)
    {
        var path = Path.Combine(Translator.GetDictionaryDirectoryPath(), dictionaryFileName);
        return Cache.GetOrAdd(path, static dictionaryPath => ReadDictionary(dictionaryPath));
    }

    private static DictionaryStore ReadDictionary(string path)
    {
        var unscopedEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        var contextualEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicateKeyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return new DictionaryStore(unscopedEntries, contextualEntries);
        }

        var document = JsonAssetLoader.LoadFromFile<DictionaryDocument>(path);
        if (document.Entries is null)
        {
            throw new InvalidDataException($"Dictionary file has no entries array: {path}");
        }

        for (var index = 0; index < document.Entries.Count; index++)
        {
            var entry = document.Entries[index];
            if (entry is null || string.IsNullOrEmpty(entry.Key) || entry.Text is null)
            {
                continue;
            }

            var key = entry.Key!;
            var text = entry.Text!;
            var context = string.IsNullOrWhiteSpace(entry.Context) ? null : entry.Context!.Trim();
            var effectiveKey = context is null ? key : BuildContextKey(context, key);
            var duplicateLabel = context is null ? key : context + "|" + key;
            var entries = context is null ? unscopedEntries : contextualEntries;
            if (entries.ContainsKey(effectiveKey))
            {
                var duplicateCount = duplicateKeyCounts.TryGetValue(duplicateLabel, out var currentDuplicateCount)
                    ? currentDuplicateCount + 1
                    : 1;
                duplicateKeyCounts[duplicateLabel] = duplicateCount;
                if (duplicateCount == 1)
                {
                    Trace.TraceWarning(
                        "QudJP: ScopedDictionaryLookup duplicate key '{0}' in '{1}'.",
                        duplicateLabel,
                        path);
                }
            }

            entries[effectiveKey] = text;
        }

        if (duplicateKeyCounts.Count > 0)
        {
            Trace.TraceWarning(
                "QudJP: ScopedDictionaryLookup duplicate key overrides in '{0}': {1}.",
                path,
                ObservabilityHelpers.BuildRankedCounterBody(duplicateKeyCounts, 10));
        }

        return new DictionaryStore(unscopedEntries, contextualEntries);
    }

    private static string BuildContextKey(string context, string key)
    {
        return context + "\u001f" + key;
    }

    [DataContract]
    private sealed class DictionaryDocument
    {
        [DataMember(Name = "entries")]
        public List<DictionaryEntry>? Entries { get; set; }
    }

    [DataContract]
    private sealed class DictionaryEntry
    {
        [DataMember(Name = "key")]
        public string? Key { get; set; }

        [DataMember(Name = "context")]
        public string? Context { get; set; }

        [DataMember(Name = "text")]
        public string? Text { get; set; }
    }

    private sealed class DictionaryStore
    {
        private readonly IReadOnlyDictionary<string, string> unscopedEntries;
        private readonly IReadOnlyDictionary<string, string> contextualEntries;

        internal DictionaryStore(
            IReadOnlyDictionary<string, string> unscopedEntries,
            IReadOnlyDictionary<string, string> contextualEntries)
        {
            this.unscopedEntries = unscopedEntries;
            this.contextualEntries = contextualEntries;
        }

        internal bool TryGetValue(string source, string? context, out string translated)
        {
            if (!string.IsNullOrWhiteSpace(context)
                && contextualEntries.TryGetValue(BuildContextKey(context!.Trim(), source), out var contextualTranslation))
            {
                translated = contextualTranslation;
                return true;
            }

            if (unscopedEntries.TryGetValue(source, out var unscopedTranslation))
            {
                translated = unscopedTranslation;
                return true;
            }

            translated = source;
            return false;
        }

        internal bool TryGetContextValue(string source, string context, out string translated)
        {
            if (contextualEntries.TryGetValue(BuildContextKey(context.Trim(), source), out var contextualTranslation))
            {
                translated = contextualTranslation;
                return true;
            }

            translated = source;
            return false;
        }
    }
}
