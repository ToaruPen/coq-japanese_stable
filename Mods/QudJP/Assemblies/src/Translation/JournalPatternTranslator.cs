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

/// <summary>
/// Pattern translator scoped to journal route patterns loaded from journal-patterns.ja.json.
/// </summary>
internal static class JournalPatternTranslator
{
    private static readonly object SyncRoot = new object();
    private static readonly ConcurrentDictionary<string, Regex> RegexCache =
        new ConcurrentDictionary<string, Regex>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> MissingPatternCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> MissingRouteCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    private static List<JournalPatternDefinition>? loadedPatterns;
    private static string[]? patternFileOverrides;
    private static string patternLoadSummary = "JournalPatternTranslator: pattern load summary unavailable.";
    private static readonly Regex VillageTemplateCapturePattern =
        new Regex("^(?:the|The) villagers of (?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingArticlePattern =
        new Regex("^(?:a|an|the|A|An|The)\\s+(?<rest>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static readonly string[] DefaultPatternAssetPaths =
    {
        "Dictionaries/journal-patterns.ja.json",
        "Dictionaries/annals-patterns.ja.json",
    };
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

        return TranslateStripped(stripped, spans);
    }

    internal static void SetPatternFilesForTests(params string[]? filePaths)
    {
        lock (SyncRoot)
        {
            // Null OR empty array resets to defaults; non-empty array overrides.
            patternFileOverrides = (filePaths is null || filePaths.Length == 0) ? null : filePaths;
            loadedPatterns = null;
            RegexCache.Clear();
            MissingPatternCounts.Clear();
            MissingRouteCounts.Clear();
            patternLoadSummary = "JournalPatternTranslator: pattern load summary unavailable.";
            Interlocked.Exchange(ref loadInvocationCount, 0);
        }
    }

    internal static void SetPatternFileForTests(string? filePath)
    {
        SetPatternFilesForTests(filePath is null ? null : new[] { filePath });
    }

    internal static void ResetForTests()
    {
        SetPatternFilesForTests((string[]?)null);
    }

    private static string TranslateStripped(string source, IReadOnlyList<ColorSpan>? spans = null)
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

            var translated = ApplyTemplate(definition.Template, match, source, spans);
            DynamicTextObservability.RecordTransform(
                nameof(JournalPatternTranslator),
                definition.Pattern,
                source,
                translated);
            return translated;
        }

        var hitCount = RecordMissingPattern(source);
        if (ObservabilityHelpers.ShouldLogMissingHit(hitCount))
        {
            var sanitizedSource = SanitizeForLog(source);
            LogObservability(
                $"[QudJP] JournalPatternTranslator: no pattern for '{sanitizedSource}' (hit {hitCount}).{Translator.GetCurrentLogContextSuffix()}");
        }

        return spans is null || spans.Count == 0
            ? source
            : ColorAwareTranslationComposer.Restore(source, spans);
    }

    private static List<JournalPatternDefinition> GetLoadedPatterns()
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

    private static List<JournalPatternDefinition> LoadPatterns()
    {
        Interlocked.Increment(ref loadInvocationCount);

        var paths = ResolvePatternFilePaths();
        var allDefinitions = new List<JournalPatternDefinition>();
        var summaries = new List<string>(paths.Count);
        var totalDuplicates = 0;
        var distinctDuplicates = new Dictionary<string, int>(StringComparer.Ordinal);
        var seenPatternsAcrossFiles = new HashSet<string>(StringComparer.Ordinal);

        for (var fileIndex = 0; fileIndex < paths.Count; fileIndex++)
        {
            var patternFilePath = paths[fileIndex];
            if (!File.Exists(patternFilePath))
            {
                // For test overrides, any missing file is always a hard fail.
                // In production (patternFileOverrides is null):
                //   - fileIndex == 0 is the primary file (journal-patterns.ja.json) and must exist.
                //   - Later defaults (e.g. annals-patterns.ja.json) may be absent during
                //     incremental rollout; log and skip them.
                if (patternFileOverrides is not null)
                {
                    throw new FileNotFoundException(
                        $"QudJP: journal pattern dictionary file not found: {patternFilePath}",
                        patternFilePath);
                }

                if (fileIndex == 0)
                {
                    throw new FileNotFoundException(
                        $"QudJP: primary journal pattern dictionary file not found: {patternFilePath}",
                        patternFilePath);
                }

                LogObservability($"[QudJP] JournalPatternTranslator: default pattern file not present, skipping: {patternFilePath}");
                continue;
            }

            JournalPatternDocument? document;
            try
            {
                using var stream = File.OpenRead(patternFilePath);
                var serializer = new DataContractJsonSerializer(typeof(JournalPatternDocument));
                document = serializer.ReadObject(stream) as JournalPatternDocument;
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                throw new InvalidDataException(
                    $"QudJP: malformed JSON in pattern file '{patternFilePath}': {ex.Message}", ex);
            }

            if (document?.Patterns is null)
            {
                throw new InvalidDataException(
                    $"QudJP: journal pattern file has no patterns array: {patternFilePath}");
            }

            var fileDuplicateCount = 0;
            for (var index = 0; index < document.Patterns.Count; index++)
            {
                var patternEntry = document.Patterns[index];
                var pattern = patternEntry?.Pattern;
                var template = patternEntry?.Template;
                if (pattern is null || template is null
                    || string.IsNullOrWhiteSpace(pattern)
                    || string.IsNullOrWhiteSpace(template))
                {
                    throw new InvalidDataException(
                        $"QudJP: malformed journal pattern entry at index {index} in '{patternFilePath}'.");
                }

                _ = GetCompiledRegex(pattern);
                if (seenPatternsAcrossFiles.Contains(pattern))
                {
                    fileDuplicateCount++;
                    totalDuplicates++;
                    distinctDuplicates[pattern] = distinctDuplicates.TryGetValue(pattern, out var dc) ? dc + 1 : 1;
                    // First-match-wins: skip adding the duplicate; the earlier definition prevails.
                    continue;
                }

                seenPatternsAcrossFiles.Add(pattern);
                allDefinitions.Add(new JournalPatternDefinition(pattern, template));
            }

            summaries.Add($"{document.Patterns.Count} pattern(s) from '{patternFilePath}' ({fileDuplicateCount} duplicate(s) shadowed)");
        }

        patternLoadSummary =
            $"JournalPatternTranslator: loaded {allDefinitions.Count} unique pattern(s) across {paths.Count} file(s); " +
            $"{totalDuplicates} duplicate(s) across {distinctDuplicates.Count} distinct pattern(s) shadowed by earlier files. " +
            string.Join("; ", summaries);
        LogObservability($"[QudJP] {patternLoadSummary}");
        LogDuplicatePatternSummary(distinctDuplicates);

        return allDefinitions;
    }

    // Must be called while holding SyncRoot (only call site is LoadPatterns).
    private static IReadOnlyList<string> ResolvePatternFilePaths()
    {
        var overrides = patternFileOverrides;
        if (overrides is { Length: > 0 })
        {
            var resolved = new string[overrides.Length];
            for (var i = 0; i < overrides.Length; i++)
            {
                resolved[i] = Path.GetFullPath(overrides[i]);
            }
            return resolved;
        }

        var defaults = new string[DefaultPatternAssetPaths.Length];
        for (var i = 0; i < DefaultPatternAssetPaths.Length; i++)
        {
            defaults[i] = LocalizationAssetResolver.GetLocalizationPath(DefaultPatternAssetPaths[i]);
        }
        return defaults;
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
            $"[QudJP] Warning: JournalPatternTranslator duplicate patterns: {ObservabilityHelpers.BuildRankedCounterBody(duplicatePatternCounts, 10)}.");
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
        if (spans is not null
            && spans.Count > 0
            && strippedSourceLength is not null
            && HasInteriorBoundarySpans(spans, strippedSourceLength.Value)
            && TryApplySegmentedColorAwareTemplate(template, match, strippedSourceLength.Value, spans, out var segmented))
        {
            return segmented;
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
                throw new FormatException($"QudJP: malformed journal pattern template '{template}'.");
            }

            var token = template.Substring(index + 1, closeIndex - index - 1);
            var translateCapture = token.Length > 1 && token[0] == 't';
            if (translateCapture)
            {
                token = token.Substring(1);
            }

            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var captureIndex))
            {
                throw new FormatException($"QudJP: unsupported placeholder '{{{token}}}' in journal pattern template '{template}'.");
            }

            if (captureIndex < 0 || captureIndex >= match.Groups.Count - 1)
            {
                throw new FormatException($"QudJP: placeholder '{{{token}}}' exceeds capture count in journal pattern template '{template}'.");
            }

            var group = match.Groups[captureIndex + 1];
            var value = group.Value;
            if (translateCapture)
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

        if (translatedFirstCaptureStart < 0
            || translatedLastCaptureEnd < 0
            || translatedFirstCaptureStart > translatedLastCaptureEnd)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, strippedSourceLength.Value, translated.Length);
            return ColorAwareTranslationComposer.Restore(translated, boundarySpans);
        }

        return ColorAwareTranslationComposer.RestoreMatchBoundaries(
            translated,
            spans,
            match,
            strippedSourceLength.Value,
            translatedFirstCaptureStart,
            translatedLastCaptureEnd,
            lastCaptureConsumesAdjacentClosingBoundary);
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

                builder.Append(ColorAwareTranslationComposer.RestoreSlice(part.Literal, spans, nextSourceStart, sourceLength));
                nextSourceStart = sourceEnd;
                continue;
            }

            var group = match.Groups[part.CaptureIndex + 1];
            if (group.Index != nextSourceStart)
            {
                translated = string.Empty;
                return false;
            }

            var value = part.TranslateCapture ? TranslateTemplateCapture(group.Value) : group.Value;
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
                throw new FormatException($"QudJP: malformed journal pattern template '{template}'.");
            }

            if (literal.Length > 0)
            {
                parts.Add(TemplatePart.CreateLiteral(literal.ToString()));
                literal.Clear();
            }

            var token = template.Substring(index + 1, closeIndex - index - 1);
            var translateCapture = token.Length > 1 && token[0] == 't';
            if (translateCapture)
            {
                token = token.Substring(1);
            }

            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var captureIndex))
            {
                throw new FormatException($"QudJP: unsupported placeholder '{{{token}}}' in journal pattern template '{template}'.");
            }

            if (captureIndex < 0 || captureIndex >= captureCount)
            {
                throw new FormatException($"QudJP: placeholder '{{{token}}}' exceeds capture count in journal pattern template '{template}'.");
            }

            parts.Add(TemplatePart.CreateCapture(captureIndex, translateCapture));
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

        if (TryTranslateVillageTemplateCapture(source, out var villageCapture))
        {
            return villageCapture;
        }

        if (TryTranslateArticlelessTemplateCapture(source, out var articlelessCapture))
        {
            return articlelessCapture;
        }

        if (HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(source, out var historicGeneratedCapture))
        {
            return historicGeneratedCapture;
        }

        return source;
    }

    private static bool TryTranslateVillageTemplateCapture(string source, out string translated)
    {
        var match = VillageTemplateCapturePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var name = TranslateTemplateCapture(match.Groups["name"].Value);
        const string templateKey = "The villagers of {0}";
        var template = Translator.Translate(templateKey);
        if (string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return true;
        }

        translated = template.Replace("{0}", name);
        return true;
    }

    private static bool TryTranslateArticlelessTemplateCapture(string source, out string translated)
    {
        var match = LeadingArticlePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var rest = match.Groups["rest"].Value;
        if (JapaneseCharacterPattern.IsMatch(rest))
        {
            translated = rest;
            return true;
        }

        translated = source;
        return false;
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

    private sealed class JournalPatternDefinition
    {
        internal JournalPatternDefinition(string pattern, string template)
        {
            Pattern = pattern;
            Template = template;
        }

        internal string Pattern { get; }

        internal string Template { get; }
    }

    [DataContract]
    private sealed class JournalPatternDocument
    {
        [DataMember(Name = "patterns")]
        public List<JournalPatternEntry>? Patterns { get; set; }
    }

    [DataContract]
    private sealed class JournalPatternEntry
    {
        [DataMember(Name = "pattern")]
        public string? Pattern { get; set; }

        [DataMember(Name = "template")]
        public string? Template { get; set; }
    }

    private readonly struct TemplatePart
    {
        private TemplatePart(string literal, int captureIndex, bool translateCapture)
        {
            Literal = literal;
            CaptureIndex = captureIndex;
            TranslateCapture = translateCapture;
        }

        internal string Literal { get; }

        internal int CaptureIndex { get; }

        internal bool TranslateCapture { get; }

        internal bool IsCapture => CaptureIndex >= 0;

        internal static TemplatePart CreateLiteral(string literal)
        {
            return new TemplatePart(literal, captureIndex: -1, translateCapture: false);
        }

        internal static TemplatePart CreateCapture(int captureIndex, bool translateCapture)
        {
            return new TemplatePart(string.Empty, captureIndex, translateCapture);
        }
    }
}
