using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
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
    private static readonly ConcurrentDictionary<string, CachedPatternFile> PatternFileCache =
        new ConcurrentDictionary<string, CachedPatternFile>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> MissingPatternCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> MissingRouteCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    private static List<MessagePatternDefinition>? loadedPatterns;
    private static string? loadedPatternFilePath;
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
    private static readonly Regex LevelUpPopupMarkupPattern = new Regex(
        "^(?<prefix>&[A-Za-z])?You have gained a level! You are now level (?<level>.+?)!\\r?\\n(?<lines>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LevelUpStatLineMarkupPattern = new Regex(
        "^You gain (?<amount>.+?) (?<kind>hitpoints?|Skill Points?|Mutation Points?|Attribute Points?|to each attribute)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DirectionQualifiedCapturePattern = new Regex(
        "^(?<target>.+?) to the (?<direction>north|south|east|west|northeast|northwest|southeast|southwest)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        return TranslateCore(source, context, logMissingPattern: true);
    }

    internal static string TranslateIfPatternMatches(string? source, string? context = null)
    {
        return TranslateCore(source, context, logMissingPattern: false);
    }

    private static string TranslateCore(string? source, string? context, bool logMissingPattern)
    {
        using var _ = Translator.PushLogContext(context);

        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        if (TryTranslateLevelUpPopupPreservingMarkup(source!, out var levelUpPopup))
        {
            DynamicTextObservability.RecordTransform(
                nameof(MessagePatternTranslator),
                "level-up-popup",
                source!,
                levelUpPopup);
            return levelUpPopup;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.Length == 0)
        {
            return source!;
        }

        return TranslateStripped(stripped, spans, logMissingPattern);
    }

    private sealed class CachedPatternFile
    {
        public CachedPatternFile(List<MessagePatternDefinition> patterns, string summary)
        {
            Patterns = patterns;
            Summary = summary;
        }

        public List<MessagePatternDefinition> Patterns { get; }
        public string Summary { get; }
    }

    internal static void SetPatternFileForTests(string? filePath)
    {
        lock (SyncRoot)
        {
            patternFileOverride = filePath;
            loadedPatterns = null;
            loadedPatternFilePath = null;
            MissingPatternCounts.Clear();
            MissingRouteCounts.Clear();
            patternLoadSummary = "MessagePatternTranslator: pattern load summary unavailable.";
            Interlocked.Exchange(ref loadInvocationCount, 0);
        }
    }

    // Required after a test rewrites the contents of a previously selected temp pattern file.
    internal static void InvalidatePatternFileCacheForTests(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var canonicalPath = GetCanonicalPath(filePath!);
        lock (SyncRoot)
        {
            PatternFileCache.TryRemove(canonicalPath, out _);
            if (string.Equals(loadedPatternFilePath, canonicalPath, StringComparison.Ordinal))
            {
                loadedPatterns = null;
                loadedPatternFilePath = null;
                patternLoadSummary = "MessagePatternTranslator: pattern load summary unavailable.";
            }
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
                using var timing = RuntimeStartupTiming.Measure("message_pattern.load_leaf_dictionary");
                try
                {
                    var file = JsonAssetLoader.LoadFromFile<LeafDictionaryFile>(leafPath);
                    if (file.Entries != null)
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
                    Trace.TraceError(
                        "QudJP: failed to load leaf dictionary '{0}': {1}",
                        leafPath,
                        ex);
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

        // Backstop for callers that pass un-stripped colored input straight to
        // MessagePatternTranslator (e.g. unit tests, MessageLogProducer paths). The primary
        // production interception lives in DescriptionTextTranslator, which strips colors
        // BEFORE MessagePatternTranslator is reached for shrine descriptions.
        var currentLogContext = Translator.GetCurrentLogContext();
        var shrineRoute = string.IsNullOrEmpty(currentLogContext)
            ? nameof(MessagePatternTranslator)
            : currentLogContext!;

        if (SultanShrineWrapperTranslator.TryTranslateMessage(source, spans, shrineRoute, out var shrineTranslated))
        {
            return shrineTranslated;
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
            RuntimeDiagnostics.RunVerboseProbe(() =>
            {
                var hitCount = RecordMissingPattern(source);
                if (ObservabilityHelpers.ShouldLogMissingHit(hitCount))
                {
                    var sanitizedSource = SanitizeForLog(source);
                    RuntimeDiagnostics.LogVerboseProbe(() =>
                        $"[QudJP] MessagePatternTranslator: no pattern for '{sanitizedSource}' (hit {hitCount}).{Translator.GetCurrentLogContextSuffix()}{Translator.BuildTranslatorStructuredSuffix(Translator.ExtractCurrentRoute(), "message_pattern", sanitizedSource)}");
                }
            });
        }

        return spans is null || spans.Count == 0
            ? source
            : ColorAwareTranslationComposer.Restore(source, spans);
    }

    private static List<MessagePatternDefinition> GetLoadedPatterns()
    {
        var patternFilePath = ResolvePatternFilePath();
        var cached = Volatile.Read(ref loadedPatterns);
        if (cached is not null
            && string.Equals(Volatile.Read(ref loadedPatternFilePath), patternFilePath, StringComparison.Ordinal))
        {
            return cached;
        }

        lock (SyncRoot)
        {
            if (loadedPatterns is null
                || !string.Equals(loadedPatternFilePath, patternFilePath, StringComparison.Ordinal))
            {
                loadedPatterns = LoadPatterns(patternFilePath);
                loadedPatternFilePath = patternFilePath;
            }

            return loadedPatterns;
        }
    }

    private static List<MessagePatternDefinition> LoadPatterns(string patternFilePath)
    {
        var cached = PatternFileCache.GetOrAdd(patternFilePath, LoadPatternFile);
        patternLoadSummary = cached.Summary;
        return cached.Patterns;
    }

    private static CachedPatternFile LoadPatternFile(string patternFilePath)
    {
        using var timing = RuntimeStartupTiming.Measure("message_pattern.load_patterns");
        Interlocked.Increment(ref loadInvocationCount);

        if (!File.Exists(patternFilePath))
        {
            throw new FileNotFoundException(
                $"QudJP: message pattern dictionary file not found: {patternFilePath}",
                patternFilePath);
        }

        var document = JsonAssetLoader.LoadFromFile<MessagePatternDocument>(patternFilePath);
        if (document.Patterns is null)
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

        var summary =
            $"MessagePatternTranslator: loaded {definitions.Count} pattern(s) from '{patternFilePath}' " +
            $"({seenPatterns.Count} unique, {duplicatePatternCount} duplicate pattern(s) across {duplicatePatternCounts.Count} distinct pattern(s)).";
        LogObservability($"[QudJP] {summary}");
        LogDuplicatePatternSummary(duplicatePatternCounts);

        return new CachedPatternFile(definitions, summary);
    }

    private static string ResolvePatternFilePath()
    {
        if (!string.IsNullOrWhiteSpace(patternFileOverride))
        {
            return GetCanonicalPath(patternFileOverride!);
        }

        return GetCanonicalPath(LocalizationAssetResolver.GetLocalizationPath("Dictionaries/messages.ja.json")!);
    }

    private static string GetCanonicalPath(string path)
    {
        return Path.GetFullPath(path);
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
        RuntimeDiagnostics.LogImportant(message);
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

        if (HasParsedPlaceholderTemplate(template))
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
        if (spans is not null
            && spans.Count > 0
            && strippedSourceLength is not null
            && HasInteriorBoundarySpans(spans, strippedSourceLength.Value)
            && TryApplySegmentedColorAwareTemplate(template, match, strippedSourceLength.Value, spans, out var segmented))
        {
            return BalanceQudBoundaryMarkup(segmented);
        }

        var builder = new StringBuilder(template.Length);
        var firstCaptureGroupIndex = -1;
        var lastCaptureGroupIndex = -1;
        if (strippedSourceLength is not null)
        {
            var firstCaptureStart = strippedSourceLength.Value;
            var lastCaptureEnd = 0;
            for (var groupIndex = 1; groupIndex < match.Groups.Count; groupIndex++)
            {
                var group = match.Groups[groupIndex];
                if (!group.Success || group.Length == 0)
                {
                    continue;
                }

                if (firstCaptureGroupIndex < 0 || group.Index < firstCaptureStart)
                {
                    firstCaptureGroupIndex = groupIndex;
                    firstCaptureStart = group.Index;
                }

                var groupEnd = group.Index + group.Length;
                if (groupEnd >= lastCaptureEnd)
                {
                    lastCaptureGroupIndex = groupIndex;
                    lastCaptureEnd = groupEnd;
                }
            }
        }

        var translatedFirstCaptureStart = -1;
        var translatedLastCaptureEnd = -1;
        var lastCaptureConsumesAdjacentClosingBoundary = false;
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
            var translateAdverbCapture = token.Length > 1 && token[0] == 'a';
            var translateDisplayNameCapture = token.Length > 1 && token[0] == 'd';
            if (translateCapture || translateAdverbCapture || translateDisplayNameCapture)
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
            if (translateAdverbCapture)
            {
                value = TranslateAdverbTemplateCapture(value);
            }
            else if (translateDisplayNameCapture)
            {
                value = TranslateDisplayNameTemplateCapture(value);
            }
            else if (translateCapture)
            {
                value = TranslateTemplateCapture(value);
            }

            if (spans is not null && spans.Count > 0)
            {
                value = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(value, spans, group);
            }

            if (captureIndex + 1 == firstCaptureGroupIndex && translatedFirstCaptureStart < 0)
            {
                translatedFirstCaptureStart = builder.Length;
            }

            builder.Append(value);
            if (captureIndex + 1 == lastCaptureGroupIndex)
            {
                translatedLastCaptureEnd = builder.Length;
                lastCaptureConsumesAdjacentClosingBoundary =
                    spans is not null
                    && spans.Count > 0
                    && ColorCodePreserver.HasAdjacentCaptureWrapper(spans, group.Index, group.Length);
            }

            index = closeIndex;
        }

        var translated = builder.ToString();
        if (spans is null || spans.Count == 0 || strippedSourceLength is null)
        {
            return translated;
        }

        if (HasInteriorBoundarySpans(spans, strippedSourceLength.Value)
            && TryRestoreWholeLineBoundaryWrappers(translated, spans, strippedSourceLength.Value, out var wholeLineRestored))
        {
            return BalanceQudBoundaryMarkup(wholeLineRestored);
        }

        if (translatedFirstCaptureStart < 0
            || translatedLastCaptureEnd < 0
            || translatedFirstCaptureStart > translatedLastCaptureEnd)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, strippedSourceLength.Value, translated.Length);
            return BalanceQudBoundaryMarkup(ColorAwareTranslationComposer.Restore(translated, boundarySpans));
        }

        return BalanceQudBoundaryMarkup(
            ColorAwareTranslationComposer.RestoreMatchBoundaries(
                translated,
                spans,
                match,
                strippedSourceLength.Value,
                translatedFirstCaptureStart,
                translatedLastCaptureEnd,
                lastCaptureConsumesAdjacentClosingBoundary));
    }

    private static bool TryApplySegmentedColorAwareTemplate(
        string template,
        Match match,
        int strippedSourceLength,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var parts = ParseTemplateParts(template, match.Groups.Count - 1);
        var referencedGroups = new HashSet<int>();
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            if (!part.IsCapture)
            {
                continue;
            }

            var groupIndex = part.CaptureIndex + 1;
            var group = match.Groups[groupIndex];
            if (!group.Success || !referencedGroups.Add(groupIndex))
            {
                translated = string.Empty;
                return false;
            }
        }

        for (var groupIndex = 1; groupIndex < match.Groups.Count; groupIndex++)
        {
            var group = match.Groups[groupIndex];
            if (group.Success
                && group.Length > 0
                && !referencedGroups.Contains(groupIndex))
            {
                translated = string.Empty;
                return false;
            }
        }

        var builder = new StringBuilder(template.Length);
        var nextSourceStart = 0;
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            if (!part.IsCapture)
            {
                var nextCaptureStart = GetNextReferencedCaptureStart(parts, index + 1, match);
                var sourceEnd = nextCaptureStart.HasValue
                    ? nextCaptureStart.Value
                    : strippedSourceLength;
                var sourceLength = sourceEnd - nextSourceStart;
                if (sourceLength < 0)
                {
                    translated = string.Empty;
                    return false;
                }

                builder.Append(RestoreLiteralSegment(part.Literal, spans, nextSourceStart, sourceLength));
                nextSourceStart = sourceEnd;
                continue;
            }

            var group = match.Groups[part.CaptureIndex + 1];
            if (group.Index != nextSourceStart)
            {
                translated = string.Empty;
                return false;
            }

            var value = group.Value;
            if (part.TranslateAdverbCapture)
            {
                value = TranslateAdverbTemplateCapture(value);
            }
            else if (part.TranslateDisplayNameCapture)
            {
                value = TranslateDisplayNameTemplateCapture(value);
            }
            else if (part.TranslateCapture)
            {
                value = TranslateTemplateCapture(value);
            }
            builder.Append(ColorAwareTranslationComposer.MarkupAwareRestoreCapture(value, spans, group));
            nextSourceStart = group.Index + group.Length;
        }

        if (nextSourceStart != strippedSourceLength)
        {
            translated = string.Empty;
            return false;
        }

        translated = builder.ToString();
        return true;
    }

    private static bool TryRestoreWholeLineBoundaryWrappers(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int strippedSourceLength,
        out string restored)
    {
        var wholeLinePairs = ColorAwareTranslationComposer.SliceWholeBoundaryPairs(spans, sourceStart: 0, strippedSourceLength);
        var wholeLineSpans = ColorAwareTranslationComposer.ProjectWholeBoundaryPairsRelative(wholeLinePairs, strippedSourceLength);
        if (wholeLineSpans.Count == 0)
        {
            restored = string.Empty;
            return false;
        }

        restored = ColorAwareTranslationComposer.RestoreRelative(translated, wholeLineSpans, strippedSourceLength);
        return true;
    }

    private static string RestoreLiteralSegment(
        string literal,
        IReadOnlyList<ColorSpan> spans,
        int startIndex,
        int sourceLength)
    {
        if (literal.Length == 0 || sourceLength <= 0)
        {
            return literal;
        }

        var closingQuoteIndex = literal.IndexOf('」');
        if (closingQuoteIndex >= 0 && closingQuoteIndex + 1 < literal.Length)
        {
            var quotedLiteral = literal.Substring(0, closingQuoteIndex + 1);
            var trailingLiteral = literal.Substring(closingQuoteIndex + 1);
            return ColorAwareTranslationComposer.RestoreSlice(quotedLiteral, spans, startIndex, sourceLength)
                + trailingLiteral;
        }

        return ColorAwareTranslationComposer.RestoreSlice(literal, spans, startIndex, sourceLength);
    }

    private static List<TemplatePart> ParseTemplateParts(string template, int captureCount)
    {
        var parts = new List<TemplatePart>();
        var literal = new StringBuilder();
        for (var index = 0; index < template.Length; index++)
        {
            var character = template[index];
            if (character == '{' && index + 1 < template.Length && template[index + 1] == '{')
            {
                literal.Append('{');
                index++;
                continue;
            }

            if (character == '}' && index + 1 < template.Length && template[index + 1] == '}')
            {
                literal.Append('}');
                index++;
                continue;
            }

            if (character != '{')
            {
                literal.Append(character);
                continue;
            }

            var closeIndex = template.IndexOf('}', index + 1);
            if (closeIndex < 0)
            {
                throw new FormatException($"QudJP: malformed message pattern template '{template}'.");
            }

            if (literal.Length > 0)
            {
                parts.Add(TemplatePart.CreateLiteral(literal.ToString()));
                literal.Clear();
            }

            var token = template.Substring(index + 1, closeIndex - index - 1);
            var translateCapture = token.Length > 1 && token[0] == 't';
            var translateAdverbCapture = token.Length > 1 && token[0] == 'a';
            var translateDisplayNameCapture = token.Length > 1 && token[0] == 'd';
            if (translateCapture || translateAdverbCapture || translateDisplayNameCapture)
            {
                token = token.Substring(1);
            }

            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var captureIndex))
            {
                throw new FormatException($"QudJP: unsupported placeholder '{{{token}}}' in message pattern template '{template}'.");
            }

            if (captureIndex < 0 || captureIndex >= captureCount)
            {
                throw new FormatException($"QudJP: placeholder '{{{token}}}' exceeds capture count in message pattern template '{template}'.");
            }

            parts.Add(TemplatePart.CreateCapture(
                captureIndex,
                translateCapture,
                translateAdverbCapture,
                translateDisplayNameCapture));
            index = closeIndex;
        }

        if (literal.Length > 0)
        {
            parts.Add(TemplatePart.CreateLiteral(literal.ToString()));
        }

        return parts;
    }

    private static int? GetNextReferencedCaptureStart(List<TemplatePart> parts, int startIndex, Match match)
    {
        for (var index = startIndex; index < parts.Count; index++)
        {
            var part = parts[index];
            if (part.IsCapture)
            {
                return match.Groups[part.CaptureIndex + 1].Index;
            }
        }

        return null;
    }

    private static bool HasInteriorBoundarySpans(IReadOnlyList<ColorSpan> spans, int strippedSourceLength)
    {
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index > 0
                && span.Index < strippedSourceLength
                && ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                return true;
            }
        }

        return false;
    }

    private static string BalanceQudBoundaryMarkup(string source)
    {
        if (source.IndexOf("{{", StringComparison.Ordinal) < 0)
        {
            return source;
        }

        var openCount = 0;
        for (var index = 0; index < source.Length - 1; index++)
        {
            if (source[index] == '{'
                && source[index + 1] == '{'
                && TryReadQudOpeningToken(source, index, out var nextIndex))
            {
                openCount++;
                index = nextIndex - 1;
                continue;
            }

            if (source[index] == '}' && source[index + 1] == '}')
            {
                if (openCount > 0)
                {
                    openCount--;
                }

                index++;
            }
        }

        if (openCount <= 0)
        {
            return source;
        }

        var builder = new StringBuilder(source.Length + (openCount * 2));
        builder.Append(source);
        for (var index = 0; index < openCount; index++)
        {
            builder.Append("}}");
        }

        return builder.ToString();
    }

    private static bool TryReadQudOpeningToken(string source, int startIndex, out int nextIndex)
    {
        nextIndex = startIndex;
        for (var index = startIndex + 2; index < source.Length; index++)
        {
            if (source[index] == '|')
            {
                nextIndex = index + 1;
                return true;
            }

            if (index < source.Length - 1 && source[index] == '}' && source[index + 1] == '}')
            {
                return false;
            }
        }

        return false;
    }

    private static string TranslateTemplateCapture(string source)
    {
        if (string.Equals(source, "You", StringComparison.Ordinal)
            || string.Equals(source, "you", StringComparison.Ordinal))
        {
            return "あなた";
        }

        if (TryTranslateTinkeringBitInventoryLines(source, out var tinkeringBitInventory))
        {
            return tinkeringBitInventory;
        }

        if (string.Equals(source, "yourself", StringComparison.OrdinalIgnoreCase))
        {
            return "自分自身";
        }

        if (string.Equals(source, "Quenched", StringComparison.Ordinal))
        {
            return "潤っている";
        }

        if (CirculatoryLossTermTranslator.TryTranslateTermPhrase(source, out var circulatoryLossTerm))
        {
            return circulatoryLossTerm;
        }

        if (DirectionPhraseTranslator.TryTranslateNounStem(source, out var direction))
        {
            return direction;
        }

        if (TryTranslateDirectionQualifiedCapture(source, out var directionQualified))
        {
            return directionQualified;
        }

        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        if (ActivatedAbilityNameTranslator.TryTranslateVisibleName(source, out var activatedAbilityCapture))
        {
            return activatedAbilityCapture;
        }

        var historySpiceComponent = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source);
        if (historySpiceComponent is not null)
        {
            return historySpiceComponent;
        }

        if (TryTranslatePossessiveCapture(source, out var possessiveCapture))
        {
            return possessiveCapture;
        }

        if (TryTranslatePossessivePronounCapture(source, out var possessivePronounCapture))
        {
            return possessivePronounCapture;
        }

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

        if (HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(source, out var historicGeneratedCapture))
        {
            return historicGeneratedCapture;
        }

        var articleStripped = TranslateArticleStrippedTemplateCapture(source);
        if (articleStripped is not null)
        {
            return articleStripped;
        }

        return source;
    }

    private static bool HasParsedPlaceholderTemplate(string template)
    {
        return template.Contains("{t")
            || template.Contains("{a")
            || template.Contains("{d");
    }

    private static string TranslateDisplayNameTemplateCapture(string source)
    {
        try
        {
            return DisplayNameCaptureTranslator.TranslatePreservingColors(source, nameof(MessagePatternTranslator));
        }
        catch (DirectoryNotFoundException)
        {
            return source;
        }
    }

    private static bool TryTranslateDirectionQualifiedCapture(string source, out string translated)
    {
        var match = DirectionQualifiedCapturePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if (!DirectionPhraseTranslator.TryTranslateNounStem(match.Groups["direction"].Value, out var direction))
        {
            translated = source;
            return false;
        }

        translated = TranslateTemplateCapture(match.Groups["target"].Value) + "（" + direction + "）";
        return true;
    }

    private static string TranslateAdverbTemplateCapture(string source)
    {
        if (DirectionPhraseTranslator.TryTranslateAdverbPhrase(source, out var direction))
        {
            return direction;
        }

        return TranslateTemplateCapture(source);
    }

    private static bool TryTranslateTinkeringBitInventoryLines(string source, out string translated)
    {
        var newline = source.Contains("\r\n") ? "\r\n" : "\n";
        var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            if (!TinkeringBitDescriptionTranslator.TryTranslateInventoryLine(lines[index], out var line))
            {
                continue;
            }

            lines[index] = line;
            changed = true;
        }

        translated = changed ? string.Join(newline, lines) : source;
        return changed;
    }

    private static bool TryTranslateLevelUpPopupPreservingMarkup(string source, out string translated)
    {
        var match = LevelUpPopupMarkupPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var newline = match.Groups["lines"].Value.Contains("\r\n") ? "\r\n" : "\n";
        var lines = match.Groups["lines"].Value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryTranslateLevelUpStatLinePreservingMarkup(lines[index], out var line))
            {
                translated = source;
                return false;
            }

            lines[index] = line;
        }

        var prefix = match.Groups["prefix"].Success
            ? match.Groups["prefix"].Value
            : string.Empty;
        translated = prefix
            + "レベルが上がった！現在レベル"
            + match.Groups["level"].Value
            + "！"
            + newline
            + string.Join(newline, lines);
        return true;
    }

    private static bool TryTranslateLevelUpStatLinePreservingMarkup(string source, out string translated)
    {
        var match = LevelUpStatLineMarkupPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var amount = match.Groups["amount"].Value;
        var kind = match.Groups["kind"].Value;
        translated = kind switch
        {
            "hitpoint" or "hitpoints" => "ヒットポイントを" + amount + "得た",
            "Skill Point" or "Skill Points" => "スキルポイントを" + amount + "得た",
            "Mutation Point" or "Mutation Points" => "変異ポイントを" + amount + "得た",
            "Attribute Point" or "Attribute Points" => "能力値ポイントを" + amount + "得た",
            "to each attribute" => "各能力値が" + amount + "上昇した",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static bool TryTranslatePossessiveCapture(string source, out string translated)
    {
        var index = source.IndexOf("'s ", StringComparison.Ordinal);
        if (index <= 0)
        {
            translated = string.Empty;
            return false;
        }

        var owner = TranslateTemplateCapture(source.Substring(0, index));
        var owned = TranslateTemplateCapture(source.Substring(index + 3));
        translated = owner + "の" + owned;
        return true;
    }

    private static bool TryTranslatePossessivePronounCapture(string source, out string translated)
    {
        if (string.Equals(source, "your", StringComparison.OrdinalIgnoreCase))
        {
            translated = "あなたの";
            return true;
        }

        if (string.Equals(source, "its", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "his", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "her", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "their", StringComparison.OrdinalIgnoreCase))
        {
            translated = "その";
            return true;
        }

        if (TryStripPossessivePronounPrefix(source, out var prefix, out var rest))
        {
            translated = prefix + TranslateTemplateCapture(rest);
            return true;
        }

        translated = string.Empty;
        return false;
    }

    private static bool TryStripPossessivePronounPrefix(string source, out string translatedPrefix, out string rest)
    {
        translatedPrefix = string.Empty;
        rest = string.Empty;

        if (source.StartsWith("your ", StringComparison.Ordinal)
            || source.StartsWith("Your ", StringComparison.Ordinal))
        {
            translatedPrefix = "あなたの";
            rest = source.Substring(5);
            return rest.Length > 0;
        }

        if (source.StartsWith("its ", StringComparison.Ordinal)
            || source.StartsWith("Its ", StringComparison.Ordinal))
        {
            translatedPrefix = "その";
            rest = source.Substring(4);
            return rest.Length > 0;
        }

        if (source.StartsWith("his ", StringComparison.Ordinal)
            || source.StartsWith("His ", StringComparison.Ordinal)
            || source.StartsWith("her ", StringComparison.Ordinal)
            || source.StartsWith("Her ", StringComparison.Ordinal))
        {
            translatedPrefix = "その";
            rest = source.Substring(4);
            return rest.Length > 0;
        }

        if (source.StartsWith("their ", StringComparison.Ordinal)
            || source.StartsWith("Their ", StringComparison.Ordinal))
        {
            translatedPrefix = "その";
            rest = source.Substring(6);
            return rest.Length > 0;
        }

        return false;
    }

    private static string? TranslateArticleStrippedTemplateCapture(string source)
    {
        var strippedArticle = StringHelpers.StripLeadingEnglishArticle(
            source,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);
        if (string.Equals(strippedArticle, source, StringComparison.Ordinal))
        {
            return null;
        }

        var direct = Translator.Translate(strippedArticle);
        if (!string.Equals(direct, strippedArticle, StringComparison.Ordinal))
        {
            return direct;
        }

        var lower = LowerAscii(strippedArticle);
        if (!string.Equals(lower, strippedArticle, StringComparison.Ordinal))
        {
            var lowered = Translator.Translate(lower);
            if (!string.Equals(lowered, lower, StringComparison.Ordinal))
            {
                return lowered;
            }
        }

        return strippedArticle;
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

    private readonly struct TemplatePart
    {
        private TemplatePart(
            string literal,
            int captureIndex,
            bool translateCapture,
            bool translateAdverbCapture,
            bool translateDisplayNameCapture)
        {
            Literal = literal;
            CaptureIndex = captureIndex;
            TranslateCapture = translateCapture;
            TranslateAdverbCapture = translateAdverbCapture;
            TranslateDisplayNameCapture = translateDisplayNameCapture;
        }

        internal string Literal { get; }

        internal int CaptureIndex { get; }

        internal bool TranslateCapture { get; }

        internal bool TranslateAdverbCapture { get; }

        internal bool TranslateDisplayNameCapture { get; }

        internal bool IsCapture => CaptureIndex >= 0;

        internal static TemplatePart CreateLiteral(string literal)
        {
            return new TemplatePart(
                literal,
                captureIndex: -1,
                translateCapture: false,
                translateAdverbCapture: false,
                translateDisplayNameCapture: false);
        }

        internal static TemplatePart CreateCapture(
            int captureIndex,
            bool translateCapture,
            bool translateAdverbCapture,
            bool translateDisplayNameCapture)
        {
            return new TemplatePart(
                string.Empty,
                captureIndex,
                translateCapture,
                translateAdverbCapture,
                translateDisplayNameCapture);
        }
    }
}
