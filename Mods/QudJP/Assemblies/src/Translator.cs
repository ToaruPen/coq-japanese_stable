using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace QudJP;

public static class Translator
{
    private static readonly object SyncRoot = new object();
    private static readonly ConcurrentDictionary<string, string> TranslationCache =
        new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> MissingKeyCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> MissingRouteCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    private static Dictionary<string, string>? loadedTranslations;
    private static string? dictionaryDirectoryOverride;
    private static string dictionaryLoadSummary = "Translator: dictionary load summary unavailable.";
    private static int loadInvocationCount;

    [ThreadStatic]
    private static Stack<string>? logContextStack;

    [ThreadStatic]
    private static int suppressMissingKeyLoggingDepth;

    public static string Translate(string key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (TranslationCache.TryGetValue(key, out var cachedTranslation))
        {
            return cachedTranslation;
        }

        return TranslateCore(key);
    }

    internal static bool TryGetTranslation(string key, out string translated)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (TranslationCache.TryGetValue(key, out var cachedTranslation))
        {
            translated = cachedTranslation;
            return true;
        }

        var translations = GetLoadedTranslations();
        if (translations.TryGetValue(key, out var loadedTranslation))
        {
            translated = loadedTranslation;
            _ = TranslationCache.TryAdd(key, translated);
            return true;
        }

        translated = key;
        return false;
    }

    internal static void SetDictionaryDirectoryForTests(string? directoryPath)
    {
        lock (SyncRoot)
        {
            dictionaryDirectoryOverride = directoryPath;
            loadedTranslations = null;
            TranslationCache.Clear();
            MissingKeyCounts.Clear();
            MissingRouteCounts.Clear();
            DynamicTextObservability.ResetForTests();
            ScopedDictionaryLookup.ResetForTests();
            dictionaryLoadSummary = "Translator: dictionary load summary unavailable.";
            Interlocked.Exchange(ref loadInvocationCount, 0);
        }
    }

    internal static void ResetForTests()
    {
        SetDictionaryDirectoryForTests(null);
    }

    internal static int LoadInvocationCount => Volatile.Read(ref loadInvocationCount);

    internal static string GetDictionaryLoadSummaryForTests()
    {
        return dictionaryLoadSummary;
    }

    internal static int GetMissingKeyHitCountForTests(string key)
    {
        return ObservabilityHelpers.GetCounterValue(MissingKeyCounts, key);
    }

    internal static int GetMissingRouteHitCountForTests(string? context)
    {
        return ObservabilityHelpers.GetCounterValue(MissingRouteCounts, ObservabilityHelpers.ExtractPrimaryContext(context));
    }

    internal static string GetMissingKeySummaryForTests(int maxEntries = 10)
    {
        var routeSummary = ObservabilityHelpers.BuildRankedSummary(
            "QudJP Translator",
            "missing key routes",
            MissingRouteCounts,
            maxEntries);
        var keySummary = ObservabilityHelpers.BuildRankedSummary(
            "QudJP Translator",
            "missing keys",
            MissingKeyCounts,
            maxEntries);
        return routeSummary + Environment.NewLine + keySummary;
    }

    internal static IDisposable PushLogContext(string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return NoopScope.Instance;
        }

        logContextStack ??= new Stack<string>();
        logContextStack.Push(context!.Trim());
        return new LogContextScope();
    }

    internal static string GetCurrentLogContextSuffix()
    {
        var context = GetCurrentLogContext();
        return string.IsNullOrEmpty(context) ? string.Empty : $" (context: {context})";
    }

    internal static IDisposable PushMissingKeyLoggingSuppression(bool suppress)
    {
        if (!suppress)
        {
            return NoopScope.Instance;
        }

        suppressMissingKeyLoggingDepth++;
        return new MissingKeySuppressionScope();
    }

    private static string TranslateCore(string key)
    {
        var translations = GetLoadedTranslations();
        if (translations.TryGetValue(key, out var translated))
        {
            _ = TranslationCache.TryAdd(key, translated);
            return translated;
        }

        var hitCount = RecordMissingKey(key);
        if (ObservabilityHelpers.ShouldLogMissingHit(hitCount))
        {
            LogObservability(
                $"[QudJP] Translator: missing key '{key}' (hit {hitCount}).{GetCurrentLogContextSuffix()}");
        }

        return key;
    }

    private static Dictionary<string, string> GetLoadedTranslations()
    {
        var cached = Volatile.Read(ref loadedTranslations);
        if (cached is not null)
        {
            return cached;
        }

        lock (SyncRoot)
        {
            if (loadedTranslations is null)
            {
                loadedTranslations = LoadTranslations();
            }

            return loadedTranslations;
        }
    }

    private static Dictionary<string, string> LoadTranslations()
    {
        Interlocked.Increment(ref loadInvocationCount);

        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        var dictionaryDirectory = ResolveDictionaryDirectory();
        if (!Directory.Exists(dictionaryDirectory))
        {
            throw new DirectoryNotFoundException(
                $"QudJP Translator: dictionary directory does not exist: {dictionaryDirectory}");
        }

        var files = Directory.GetFiles(dictionaryDirectory, "*.ja.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        var keySources = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicateKeyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var rawEntryCount = 0;
        var duplicateKeyCount = 0;

        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            LoadDictionaryFile(
                files[fileIndex],
                translations,
                keySources,
                duplicateKeyCounts,
                ref rawEntryCount,
                ref duplicateKeyCount);
        }

        dictionaryLoadSummary =
            $"Translator: loaded {translations.Count} unique entries from {files.Length} file(s) " +
            $"({rawEntryCount} raw entries, {duplicateKeyCount} duplicate key override(s) across {duplicateKeyCounts.Count} distinct key(s)).";
        LogObservability($"[QudJP] {dictionaryLoadSummary}");
        LogDuplicateKeySummary(duplicateKeyCounts);

        return translations;
    }

    private static string ResolveDictionaryDirectory()
    {
        if (!string.IsNullOrWhiteSpace(dictionaryDirectoryOverride))
        {
            return Path.GetFullPath(dictionaryDirectoryOverride);
        }

        return LocalizationAssetResolver.GetLocalizationPath("Dictionaries");
    }

    internal static string GetDictionaryDirectoryPath()
    {
        return ResolveDictionaryDirectory();
    }

    private static void LoadDictionaryFile(
        string filePath,
        Dictionary<string, string> translations,
        Dictionary<string, string> keySources,
        Dictionary<string, int> duplicateKeyCounts,
        ref int rawEntryCount,
        ref int duplicateKeyCount)
    {
        using var stream = File.OpenRead(filePath);
        var serializer = new DataContractJsonSerializer(typeof(DictionaryDocument));
        var document = serializer.ReadObject(stream) as DictionaryDocument;
        if (document?.Entries is null)
        {
            throw new InvalidDataException($"Dictionary file has no entries array: {filePath}");
        }

        for (var index = 0; index < document.Entries.Count; index++)
        {
            var entry = document.Entries[index];
            if (entry is null || string.IsNullOrEmpty(entry.Key) || entry.Text is null)
            {
                Trace.TraceWarning(
                    $"QudJP: Translator skipped malformed entry at index {index} in '{filePath}'.");
                continue;
            }

            rawEntryCount++;
            if (keySources.TryGetValue(entry.Key!, out _))
            {
                duplicateKeyCount++;
                duplicateKeyCounts[entry.Key!] = duplicateKeyCounts.TryGetValue(entry.Key!, out var duplicateCount)
                    ? duplicateCount + 1
                    : 1;
            }

            keySources[entry.Key!] = filePath;
            translations[entry.Key!] = entry.Text;
        }
    }

    private static int RecordMissingKey(string key)
    {
        if (suppressMissingKeyLoggingDepth > 0)
        {
            return 0;
        }

        var hitCount = MissingKeyCounts.AddOrUpdate(key, 1, ObservabilityHelpers.IncrementCounter);
        _ = MissingRouteCounts.AddOrUpdate(
            ObservabilityHelpers.ExtractPrimaryContext(GetCurrentLogContext()),
            1,
            ObservabilityHelpers.IncrementCounter);
        return hitCount;
    }

    internal static bool ShouldLogMissingHitForTests(int hitCount)
    {
        return ObservabilityHelpers.ShouldLogMissingHit(hitCount);
    }

    private static void LogDuplicateKeySummary(Dictionary<string, int> duplicateKeyCounts)
    {
        if (duplicateKeyCounts.Count == 0)
        {
            return;
        }

        LogObservability(
            $"[QudJP] Warning: Translator duplicate key overrides: {ObservabilityHelpers.BuildRankedCounterBody(duplicateKeyCounts, 10)}.");
    }

    private static void LogObservability(string message)
    {
        QudJPMod.LogToUnity(message);
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

    internal static string? GetCurrentLogContext()
    {
        if (logContextStack is not { Count: > 0 })
        {
            return null;
        }

        if (logContextStack.Count == 1)
        {
            return logContextStack.Peek();
        }

        var frames = logContextStack.ToArray();
        Array.Reverse(frames);
        return string.Join(ObservabilityHelpers.ContextSeparator, frames);
    }

    private sealed class LogContextScope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (logContextStack is { Count: > 0 })
            {
                logContextStack.Pop();
            }
        }
    }

    private sealed class NoopScope : IDisposable
    {
        internal static readonly NoopScope Instance = new NoopScope();

        public void Dispose()
        {
        }
    }

    private sealed class MissingKeySuppressionScope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (suppressMissingKeyLoggingDepth > 0)
            {
                DecrementSuppressionDepth();
            }
        }

        private static void DecrementSuppressionDepth()
        {
            suppressMissingKeyLoggingDepth--;
        }
    }
}
