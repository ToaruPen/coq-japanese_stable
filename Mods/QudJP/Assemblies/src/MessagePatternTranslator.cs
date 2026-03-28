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
using QudJP.Patches;

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
    private static Dictionary<string, string>? leafDictionary;
    private static string? patternFileOverride;
    private static string? leafFileOverride;
    private static string patternLoadSummary = "MessagePatternTranslator: pattern load summary unavailable.";
    private static int loadInvocationCount;
    private const int MaxLogSourceLength = 200;
    private const string DefaultLeafFileName = "ui-messagelog-leaf.ja.json";
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.Length == 0)
        {
            return source!;
        }

        return TranslateStripped(stripped, spans, logMissingPattern: true);
    }

    internal static bool TryTranslateWithoutLogging(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.Length == 0)
        {
            translated = source!;
            return false;
        }

        translated = TranslateStripped(stripped, spans, logMissingPattern: false);
        return !string.Equals(translated, source, StringComparison.Ordinal);
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
        leafFileOverride = null;
        leafDictionary = null;
    }

    internal static void SetLeafFileForTests(string? path)
    {
        leafFileOverride = path;
        leafDictionary = null;
    }

    private static bool TryGetLeafTranslation(string source, out string translation)
    {
        var dict = GetLoadedLeafDictionary();
        if (dict.TryGetValue(source, out var value)
            && !string.Equals(value, source, StringComparison.Ordinal))
        {
            translation = value;
            return true;
        }
        translation = source;
        return false;
    }

    private static Dictionary<string, string> GetLoadedLeafDictionary()
    {
        var cached = leafDictionary;
        if (cached != null)
        {
            return cached;
        }

        lock (SyncRoot)
        {
            cached = leafDictionary;
            if (cached != null)
            {
                return cached;
            }

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            var leafPath = leafFileOverride ?? ResolveLeafFilePath();
            if (leafPath != null && File.Exists(leafPath))
            {
                try
                {
                    var json = File.ReadAllText(leafPath, Encoding.UTF8);
                    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                    var serializer = new DataContractJsonSerializer(typeof(LeafDictionaryFile));
                    if (serializer.ReadObject(ms) is LeafDictionaryFile file && file.Entries != null)
                    {
                        foreach (var entry in file.Entries)
                        {
                            if (entry.Key is { Length: > 0 } key && entry.Value is { Length: > 0 } value)
                            {
                                dict[key] = value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("QudJP: failed to load leaf dictionary: {0}", ex.Message);
                }
            }
            leafDictionary = dict;
            return dict;
        }
    }

    private static string? ResolveLeafFilePath()
    {
        if (leafFileOverride != null) return leafFileOverride;
        return LocalizationAssetResolver.GetLocalizationPath("Dictionaries/" + DefaultLeafFileName);
    }

    private static string TranslateStripped(
        string source,
        IReadOnlyList<ColorSpan>? spans = null,
        bool logMissingPattern = true)
    {
        if (DeathWrapperFamilyTranslator.TryTranslateMessage(source, spans, out var deathTranslated))
        {
            return deathTranslated;
        }

        if (TryGetLeafTranslation(source, out var exactTranslation))
        {
            DynamicTextObservability.RecordTransform(
                nameof(MessagePatternTranslator),
                "leaf-dictionary",
                source,
                exactTranslation);
            return spans is null || spans.Count == 0
                ? exactTranslation
                : ColorAwareTranslationComposer.Restore(exactTranslation, spans);
        }

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

            var translated = ApplyTemplate(definition.Template, match, source, spans);
            DynamicTextObservability.RecordTransform(
                nameof(MessagePatternTranslator),
                definition.Pattern,
                source,
                translated);
            return translated;
        }

        if (logMissingPattern)
        {
            var hitCount = RecordMissingPattern(source);
            if (ObservabilityHelpers.ShouldLogMissingHit(hitCount))
            {
                var sanitizedSource = SanitizeForLog(source);
                LogObservability(
                    $"[QudJP] MessagePatternTranslator: no pattern for '{sanitizedSource}' (hit {hitCount}).{Translator.GetCurrentLogContextSuffix()}");
            }
        }

        return spans is null || spans.Count == 0
            ? source
            : ColorAwareTranslationComposer.Restore(source, spans);
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

    // NOTE: The ContainsKey/Count check and subsequent AddOrUpdate are not atomic.
    // Under contention, the dictionary may slightly exceed maxKeys before new keys
    // are routed to the overflow bucket. This is acceptable for observability counters
    // where approximate caps are sufficient and lock-free throughput is preferred.
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

    private static string ApplyTemplate(string template, Match match, string source, IReadOnlyList<ColorSpan>? spans)
    {
        var capturedCount = match.Groups.Count - 1;
        if (capturedCount <= 0)
        {
            return spans is null || spans.Count == 0
                ? template
                : ColorAwareTranslationComposer.Restore(template, spans);
        }

        if (spans is not null && spans.Count > 0)
        {
            return ApplyTemplateWithColorAwareCaptures(template, match, source, spans);
        }

        if (template.Contains("{t"))
        {
            return ApplyTemplateWithTranslatedCaptures(template, match);
        }

        var placeholders = new object[capturedCount];
        for (var index = 0; index < capturedCount; index++)
        {
            placeholders[index] = match.Groups[index + 1].Value;
        }

        return string.Format(CultureInfo.InvariantCulture, template, placeholders);
    }

    private static string ApplyTemplateWithTranslatedCaptures(string template, Match match)
    {
        return ApplyTemplateWithParsedPlaceholders(template, match, strippedSourceLength: null, spans: null);
    }

    private static string ApplyTemplateWithColorAwareCaptures(
        string template,
        Match match,
        string strippedSource,
        IReadOnlyList<ColorSpan> spans)
    {
        return ApplyTemplateWithParsedPlaceholders(template, match, strippedSource.Length, spans);
    }

    private static string ApplyTemplateWithParsedPlaceholders(
        string template,
        Match match,
        int? strippedSourceLength,
        IReadOnlyList<ColorSpan>? spans)
    {
        var builder = new StringBuilder(template.Length);
        for (var index = 0; index < template.Length; index++)
        {
            var character = template[index];
            if (character == '{' && index + 1 < template.Length && template[index + 1] == '{')
            {
                builder.Append('{');
                index++;
                continue;
            }

            if (character == '}' && index + 1 < template.Length && template[index + 1] == '}')
            {
                builder.Append('}');
                index++;
                continue;
            }

            if (character != '{')
            {
                builder.Append(character);
                continue;
            }

            var closeIndex = template.IndexOf('}', index + 1);
            if (closeIndex < 0)
            {
                throw new FormatException($"QudJP: malformed message pattern template '{template}'.");
            }

            var token = template.Substring(index + 1, closeIndex - index - 1);
            var translateCapture = token.Length > 1 && token[0] == 't';
            if (translateCapture)
            {
                token = token.Substring(1);
            }

            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var captureIndex))
            {
                throw new FormatException($"QudJP: unsupported placeholder '{{{token}}}' in message pattern template '{template}'.");
            }

            if (captureIndex < 0 || captureIndex >= match.Groups.Count - 1)
            {
                throw new FormatException($"QudJP: placeholder '{{{token}}}' exceeds capture count in message pattern template '{template}'.");
            }

            var group = match.Groups[captureIndex + 1];
            var value = group.Value;
            if (translateCapture)
            {
                value = TranslateTemplateCapture(value);
            }

            if (spans is not null && spans.Count > 0)
            {
                value = ColorAwareTranslationComposer.RestoreCapture(value, spans, group);
            }

            builder.Append(value);
            index = closeIndex;
        }

        var translated = builder.ToString();
        if (spans is null || spans.Count == 0 || strippedSourceLength is null)
        {
            return translated;
        }

        var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, strippedSourceLength.Value, translated.Length);
        return ColorAwareTranslationComposer.Restore(translated, boundarySpans);
    }

    private static string TranslateTemplateCapture(string source)
    {
        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var direct = Translator.Translate(source);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        var lower = LowerAscii(source);
        if (!string.Equals(lower, source, StringComparison.Ordinal))
        {
            var lowered = Translator.Translate(lower);
            if (!string.Equals(lowered, lower, StringComparison.Ordinal))
            {
                return lowered;
            }
        }

        return source;
    }

    private static string LowerAscii(string source)
    {
        var buffer = source.ToCharArray();
        var changed = false;
        for (var index = 0; index < buffer.Length; index++)
        {
            var character = buffer[index];
            if (character < 'A' || character > 'Z')
            {
                continue;
            }

            buffer[index] = (char)(character + ('a' - 'A'));
            changed = true;
        }

        return changed ? new string(buffer) : source;
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

    [DataContract]
    private sealed class LeafDictionaryFile
    {
        [DataMember(Name = "entries")]
        public List<LeafEntry>? Entries { get; set; }
    }

    [DataContract]
    private sealed class LeafEntry
    {
        [DataMember(Name = "key")]
        public string? Key { get; set; }

        [DataMember(Name = "text")]
        public string? Value { get; set; }
    }
}
