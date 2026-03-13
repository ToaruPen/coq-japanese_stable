using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace QudJP;

internal static class MessagePatternTranslator
{
    private static readonly object SyncRoot = new object();
    private static readonly ConcurrentDictionary<string, Regex> RegexCache =
        new ConcurrentDictionary<string, Regex>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> MissingPatternCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> MissingRouteCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    private static List<MessagePatternDefinition>? loadedPatterns;
    private static string? patternFileOverride;
    private static string patternLoadSummary = "MessagePatternTranslator: pattern load summary unavailable.";
    private static int loadInvocationCount;
    private const string NoContextLabel = "<no-context>";

    internal static int LoadInvocationCount => Volatile.Read(ref loadInvocationCount);

    internal static string GetPatternLoadSummaryForTests()
    {
        return patternLoadSummary;
    }

    internal static int GetMissingPatternHitCountForTests(string source)
    {
        return GetCounterValue(MissingPatternCounts, source);
    }

    internal static int GetMissingRouteHitCountForTests(string? context)
    {
        return GetCounterValue(MissingRouteCounts, NormalizeContext(context));
    }

    internal static string GetMissingPatternSummaryForTests(int maxEntries = 10)
    {
        var routeSummary = BuildRankedSummary(
            "missing pattern routes",
            MissingRouteCounts,
            maxEntries);
        var patternSummary = BuildRankedSummary(
            "missing patterns",
            MissingPatternCounts,
            maxEntries);
        return routeSummary + Environment.NewLine + patternSummary;
    }

    internal static string Translate(string? source, string? context = null)
    {
        using var _ = Translator.PushLogContext(context);

        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var (stripped, spans) = ColorCodePreserver.Strip(source);
        if (stripped.Length == 0)
        {
            return source!;
        }

        var translated = TranslateStripped(stripped);
        return ColorCodePreserver.Restore(translated, spans);
    }

    internal static void SetPatternFileForTests(string? filePath)
    {
        lock (SyncRoot)
        {
            patternFileOverride = filePath;
            loadedPatterns = null;
            RegexCache.Clear();
            MissingPatternCounts.Clear();
            MissingRouteCounts.Clear();
            patternLoadSummary = "MessagePatternTranslator: pattern load summary unavailable.";
            Interlocked.Exchange(ref loadInvocationCount, 0);
        }
    }

    internal static void ResetForTests()
    {
        SetPatternFileForTests(null);
    }

    private static string TranslateStripped(string source)
    {
        var patterns = GetLoadedPatterns();
        for (var index = 0; index < patterns.Count; index++)
        {
            var definition = patterns[index];
            var regex = GetCompiledRegex(definition.Pattern);
            var match = regex.Match(source);
            if (!match.Success)
            {
                continue;
            }

            return ApplyTemplate(definition.Template, match);
        }

        var hitCount = RecordMissingPattern(source);
        if (ShouldLogMissingHit(hitCount))
        {
            LogObservability(
                $"[QudJP] MessagePatternTranslator: no pattern for '{source}' (hit {hitCount}).{Translator.GetCurrentLogContextSuffix()}");
        }

        return source;
    }

    private static List<MessagePatternDefinition> GetLoadedPatterns()
    {
        var cached = Volatile.Read(ref loadedPatterns);
        if (cached is not null)
        {
            return cached;
        }

        lock (SyncRoot)
        {
            if (loadedPatterns is null)
            {
                loadedPatterns = LoadPatterns();
            }

            return loadedPatterns;
        }
    }

    private static List<MessagePatternDefinition> LoadPatterns()
    {
        Interlocked.Increment(ref loadInvocationCount);

        var patternFilePath = ResolvePatternFilePath();
        if (!File.Exists(patternFilePath))
        {
            throw new FileNotFoundException(
                $"QudJP: message pattern dictionary file not found: {patternFilePath}",
                patternFilePath);
        }

        using var stream = File.OpenRead(patternFilePath);
        var serializer = new DataContractJsonSerializer(typeof(MessagePatternDocument));
        var document = serializer.ReadObject(stream) as MessagePatternDocument;
        if (document?.Patterns is null)
        {
            throw new InvalidDataException($"QudJP: message pattern file has no patterns array: {patternFilePath}");
        }

        var definitions = new List<MessagePatternDefinition>(document.Patterns.Count);
        var seenPatterns = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicatePatternCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicatePatternCount = 0;
        for (var index = 0; index < document.Patterns.Count; index++)
        {
            var patternEntry = document.Patterns[index];
            var pattern = patternEntry?.Pattern;
            var template = patternEntry?.Template;
            if (pattern is null || pattern.Length == 0 || template is null)
            {
                throw new InvalidDataException(
                    $"QudJP: malformed message pattern entry at index {index} in '{patternFilePath}'.");
            }

            _ = GetCompiledRegex(pattern);
            if (seenPatterns.TryGetValue(pattern, out _))
            {
                duplicatePatternCount++;
                duplicatePatternCounts[pattern] = duplicatePatternCounts.TryGetValue(pattern, out var duplicateCount)
                    ? duplicateCount + 1
                    : 1;
            }

            seenPatterns[pattern] = index;
            definitions.Add(new MessagePatternDefinition(pattern, template));
        }

        patternLoadSummary =
            $"MessagePatternTranslator: loaded {definitions.Count} pattern(s) from '{patternFilePath}' " +
            $"({seenPatterns.Count} unique, {duplicatePatternCount} duplicate pattern(s) across {duplicatePatternCounts.Count} distinct pattern(s)).";
        LogObservability($"[QudJP] {patternLoadSummary}");
        LogDuplicatePatternSummary(duplicatePatternCounts);

        return definitions;
    }

    private static string ResolvePatternFilePath()
    {
        if (!string.IsNullOrWhiteSpace(patternFileOverride))
        {
            return Path.GetFullPath(patternFileOverride);
        }

        return LocalizationAssetResolver.GetLocalizationPath("Dictionaries/messages.ja.json");
    }

    private static Regex GetCompiledRegex(string pattern)
    {
        return RegexCache.GetOrAdd(pattern, CreateRegex);
    }

    private static Regex CreateRegex(string pattern)
    {
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private static int RecordMissingPattern(string source)
    {
        var hitCount = MissingPatternCounts.AddOrUpdate(source, 1, IncrementCounter);
        _ = MissingRouteCounts.AddOrUpdate(NormalizeContext(Translator.GetCurrentLogContext()), 1, IncrementCounter);
        return hitCount;
    }

    internal static bool ShouldLogMissingHitForTests(int hitCount)
    {
        return ShouldLogMissingHit(hitCount);
    }

    private static void LogDuplicatePatternSummary(Dictionary<string, int> duplicatePatternCounts)
    {
        if (duplicatePatternCounts.Count == 0)
        {
            return;
        }

        LogObservability(
            $"[QudJP] Warning: MessagePatternTranslator duplicate patterns: {BuildRankedCounterBody(duplicatePatternCounts, 10)}.");
    }

    private static void LogObservability(string message)
    {
        QudJPMod.LogToUnity(message);
    }

    private static bool ShouldLogMissingHit(int hitCount)
    {
        return hitCount > 0 && (hitCount & (hitCount - 1)) == 0;
    }

    private static int GetCounterValue(ConcurrentDictionary<string, int> counters, string key)
    {
        return counters.TryGetValue(key, out var count) ? count : 0;
    }

    private static string BuildRankedSummary(
        string label,
        ConcurrentDictionary<string, int> counters,
        int maxEntries)
    {
        var boundedMaxEntries = maxEntries <= 0 ? 1 : maxEntries;
        var entries = counters.ToArray();
        if (entries.Length == 0)
        {
            return $"QudJP MessagePatternTranslator: {label}: none.";
        }

        Array.Sort(entries, CompareCounterEntries);
        var limit = Math.Min(boundedMaxEntries, entries.Length);
        var builder = new StringBuilder();
        builder.Append("QudJP MessagePatternTranslator: ");
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

    private static string BuildRankedCounterBody(
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

    private static int CompareCounterEntries(
        KeyValuePair<string, int> left,
        KeyValuePair<string, int> right)
    {
        var countComparison = right.Value.CompareTo(left.Value);
        return countComparison != 0
            ? countComparison
            : StringComparer.Ordinal.Compare(left.Key, right.Key);
    }

    private static int IncrementCounter(string _, int currentValue)
    {
        return currentValue < int.MaxValue ? currentValue + 1 : int.MaxValue;
    }

    private static string NormalizeContext(string? context)
    {
        if (context is null)
        {
            return NoContextLabel;
        }

        var trimmedContext = context.Trim();
        return trimmedContext.Length == 0 ? NoContextLabel : trimmedContext;
    }

    private static string ApplyTemplate(string template, Match match)
    {
        var capturedCount = match.Groups.Count - 1;
        if (capturedCount <= 0)
        {
            return template;
        }

        var placeholders = new object[capturedCount];
        for (var index = 0; index < capturedCount; index++)
        {
            placeholders[index] = match.Groups[index + 1].Value;
        }

        return string.Format(CultureInfo.InvariantCulture, template, placeholders);
    }

    private sealed class MessagePatternDefinition
    {
        internal MessagePatternDefinition(string pattern, string template)
        {
            Pattern = pattern;
            Template = template;
        }

        internal string Pattern { get; }

        internal string Template { get; }
    }

    [DataContract]
    private sealed class MessagePatternDocument
    {
        [DataMember(Name = "patterns")]
        public List<MessagePatternEntry>? Patterns { get; set; }
    }

    [DataContract]
    private sealed class MessagePatternEntry
    {
        [DataMember(Name = "pattern")]
        public string? Pattern { get; set; }

        [DataMember(Name = "template")]
        public string? Template { get; set; }
    }
}
