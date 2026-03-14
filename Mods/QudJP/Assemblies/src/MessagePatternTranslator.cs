using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
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
    private const int MaxLogSourceLength = 200;
    internal const int MaxUniquePatterns = 10_000;
    internal const int MaxUniqueRoutes = 1_000;
    internal const string OverflowKey = "__overflow__";

    internal static int LoadInvocationCount => Volatile.Read(ref loadInvocationCount);

    internal static string GetPatternLoadSummaryForTests()
    {
        return patternLoadSummary;
    }

    internal static int GetMissingPatternHitCountForTests(string source)
    {
        return ObservabilityHelpers.GetCounterValue(MissingPatternCounts, source);
    }

    internal static int GetMissingRouteHitCountForTests(string? context)
    {
        return ObservabilityHelpers.GetCounterValue(MissingRouteCounts, ObservabilityHelpers.NormalizeContext(context));
    }

    internal static string GetMissingPatternSummaryForTests(int maxEntries = 10)
    {
        var routeSummary = ObservabilityHelpers.BuildRankedSummary(
            "QudJP MessagePatternTranslator",
            "missing pattern routes",
            MissingRouteCounts,
            maxEntries);
        var patternSummary = ObservabilityHelpers.BuildRankedSummary(
            "QudJP MessagePatternTranslator",
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
        if (ObservabilityHelpers.ShouldLogMissingHit(hitCount))
        {
            var sanitizedSource = SanitizeForLog(source);
            LogObservability(
                $"[QudJP] MessagePatternTranslator: no pattern for '{sanitizedSource}' (hit {hitCount}).{Translator.GetCurrentLogContextSuffix()}");
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
        var hitCount = AddOrUpdateCapped(MissingPatternCounts, source, MaxUniquePatterns);
        _ = AddOrUpdateCapped(
            MissingRouteCounts,
            ObservabilityHelpers.NormalizeContext(Translator.GetCurrentLogContext()),
            MaxUniqueRoutes);
        return hitCount;
    }

    private static int AddOrUpdateCapped(ConcurrentDictionary<string, int> counters, string key, int maxKeys)
    {
        if (counters.ContainsKey(key) || counters.Count < maxKeys)
        {
            return counters.AddOrUpdate(key, 1, ObservabilityHelpers.IncrementCounter);
        }

        return counters.AddOrUpdate(OverflowKey, 1, ObservabilityHelpers.IncrementCounter);
    }

    internal static bool ShouldLogMissingHitForTests(int hitCount)
    {
        return ObservabilityHelpers.ShouldLogMissingHit(hitCount);
    }

    private static void LogDuplicatePatternSummary(Dictionary<string, int> duplicatePatternCounts)
    {
        if (duplicatePatternCounts.Count == 0)
        {
            return;
        }

        LogObservability(
            $"[QudJP] Warning: MessagePatternTranslator duplicate patterns: {ObservabilityHelpers.BuildRankedCounterBody(duplicatePatternCounts, 10)}.");
    }

    private static void LogObservability(string message)
    {
        QudJPMod.LogToUnity(message);
    }

    private static string SanitizeForLog(string source)
    {
#if NET48
        var sanitized = source.Length > MaxLogSourceLength
            ? source.Substring(0, MaxLogSourceLength) + "..."
            : source;
#else
        var sanitized = source.Length > MaxLogSourceLength
            ? string.Concat(source.AsSpan(0, MaxLogSourceLength), "...")
            : source;
#endif

        var builder = new System.Text.StringBuilder(sanitized.Length);
        for (var index = 0; index < sanitized.Length; index++)
        {
            var character = sanitized[index];
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
                builder.Append(((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
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
