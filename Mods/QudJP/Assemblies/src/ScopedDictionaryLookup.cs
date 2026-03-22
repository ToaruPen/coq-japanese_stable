using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace QudJP;

internal static class ScopedDictionaryLookup
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache =
        new ConcurrentDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    internal static string? TranslateExactOrLowerAscii(string source, params string[] dictionaryFileNames)
    {
        if (string.IsNullOrEmpty(source) || dictionaryFileNames.Length == 0)
        {
            return null;
        }

        if (TryGetTranslation(source, dictionaryFileNames, out var translated))
        {
            return translated;
        }

        var lowerAscii = StringHelpers.LowerAscii(source);
        if (string.Equals(lowerAscii, source, StringComparison.Ordinal))
        {
            return null;
        }

        return TryGetTranslation(lowerAscii, dictionaryFileNames, out translated)
            ? translated
            : null;
    }

    internal static void ResetForTests()
    {
        Cache.Clear();
    }

    private static bool TryGetTranslation(string source, IReadOnlyList<string> dictionaryFileNames, out string translated)
    {
        for (var index = 0; index < dictionaryFileNames.Count; index++)
        {
            if (LoadDictionary(dictionaryFileNames[index]).TryGetValue(source, out var loadedTranslation))
            {
                translated = loadedTranslation;
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static IReadOnlyDictionary<string, string> LoadDictionary(string dictionaryFileName)
    {
        var path = Path.Combine(Translator.GetDictionaryDirectoryPath(), dictionaryFileName);
        return Cache.GetOrAdd(path, static dictionaryPath => ReadDictionary(dictionaryPath));
    }

    private static IReadOnlyDictionary<string, string> ReadDictionary(string path)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return entries;
        }

        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(DictionaryDocument));
        var document = serializer.ReadObject(stream) as DictionaryDocument;
        if (document?.Entries is null)
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
            entries[key] = text;
        }

        return entries;
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

        [DataMember(Name = "text")]
        public string? Text { get; set; }
    }
}
