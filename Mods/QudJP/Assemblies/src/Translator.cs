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
    private static readonly ConcurrentDictionary<string, byte> MissingKeyLog =
        new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

    private static Dictionary<string, string>? loadedTranslations;
    private static string? dictionaryDirectoryOverride;
    private static int loadInvocationCount;

    [ThreadStatic]
    private static Stack<string>? logContextStack;

    public static string Translate(string key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        return TranslationCache.GetOrAdd(key, TranslateCore);
    }

    internal static void SetDictionaryDirectoryForTests(string? directoryPath)
    {
        lock (SyncRoot)
        {
            dictionaryDirectoryOverride = directoryPath;
            loadedTranslations = null;
            TranslationCache.Clear();
            MissingKeyLog.Clear();
            Interlocked.Exchange(ref loadInvocationCount, 0);
        }
    }

    internal static void ResetForTests()
    {
        SetDictionaryDirectoryForTests(null);
    }

    internal static int LoadInvocationCount => Volatile.Read(ref loadInvocationCount);

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

    private static string TranslateCore(string key)
    {
        var translations = GetLoadedTranslations();
        if (translations.TryGetValue(key, out var translated))
        {
            return translated;
        }

        if (MissingKeyLog.TryAdd(key, 0))
        {
            Trace.TraceInformation($"QudJP Translator: missing key '{key}'.{GetCurrentLogContextSuffix()}");
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

        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            LoadDictionaryFile(files[fileIndex], translations);
        }

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

    private static void LoadDictionaryFile(string filePath, Dictionary<string, string> translations)
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

            translations[entry.Key!] = entry.Text;
        }
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

    private static string? GetCurrentLogContext()
    {
        return logContextStack is { Count: > 0 } ? logContextStack.Peek() : null;
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
}
