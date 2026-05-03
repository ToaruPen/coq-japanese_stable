using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class FinalOutputObservability
{
    internal const string Family = "final_output";
    internal const string DetailAlreadyLocalized = "AlreadyLocalized";
    internal const string DetailDirectMarker = "DirectMarker";
    internal const string DetailSkipped = "Skipped";
    internal const string DirectMarkerStatusAbsent = "absent";
    internal const string DirectMarkerStatusPresent = "present";
    internal const string MarkupStatusFinalOnly = "final_only";
    internal const string MarkupStatusMatched = "matched";
    internal const string MarkupStatusMismatch = "mismatch";
    internal const string MarkupStatusNoMarkup = "no_markup";
    internal const string MarkupStatusSourceOnly = "source_only";
    internal const string MarkupSemanticStatusClean = "clean";
    internal const string MarkupSemanticStatusDrift = "drift";
    internal const string MarkupSpanStatusSpanMismatch = "span_mismatch";
    internal const string MarkupSpanStatusTokenMismatch = "token_mismatch";
    internal const string NotEvaluatedStatus = "not_evaluated";
    internal const string PhaseBeforeSink = "before_sink";
    internal const string TranslationStatusAlreadyLocalized = "already_localized";
    internal const string TranslationStatusDirectMarker = "direct_marker";
    internal const string TranslationStatusSkipped = "skipped";
    internal const string TranslationStatusSinkUnclaimed = "sink_unclaimed";

    private const string ProbeVersion = "v1";
    private const int MaxObservedEntries = 4096;
    private const int MaxValueLength = 200;
    private const string OverflowKey = "__overflow__";

    private static readonly ConcurrentDictionary<string, int> HitCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    private static readonly object HitCountsSync = new object();
    private static readonly Regex MarkupTokenPattern =
        new Regex(@"\{\{[^|}]+\||\}\}|&&|\^\^|&[A-Za-z]|\^[A-Za-z]|<color=[^>]+>|</color>|=[A-Za-z0-9_.]+=", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    internal static void ResetForTests()
    {
        lock (HitCountsSync)
        {
            HitCounts.Clear();
        }
    }

    internal static int GetHitCountForTests(FinalOutputObservation observation)
    {
        var normalized = Normalize(observation);
        lock (HitCountsSync)
        {
            return ObservabilityHelpers.GetCounterValue(HitCounts, BuildCounterKey(normalized));
        }
    }

    internal static string ComputeMarkupStatusForTests(string? sourceText, string? finalText)
    {
        return ComputeMarkupStatus(sourceText, finalText);
    }

    internal static string ComputeMarkupSpanStatusForTests(string? sourceText, string? finalText)
    {
        return ComputeMarkupSpanStatus(sourceText, finalText);
    }

    internal static string BuildMarkupSpanSignatureForTests(string? text)
    {
        return BuildMarkupSpanSignature(text);
    }

    internal static void RecordSinkUnclaimed(
        string sink,
        string route,
        string detail,
        string? sourceText,
        string? strippedText)
    {
        var sourceValue = sourceText ?? string.Empty;
        Record(
            new FinalOutputObservation(
                sink,
                route,
                detail,
                PhaseBeforeSink,
                TranslationStatusSinkUnclaimed,
                ComputeMarkupStatus(sourceValue, sourceValue),
                DirectMarkerStatusAbsent,
                sourceValue,
                strippedText,
                string.Empty,
                sourceValue));
    }

    internal static void RecordDirectMarker(
        string sink,
        string route,
        string detail,
        string? sourceText,
        string finalText)
    {
        var finalValue = finalText ?? string.Empty;
        _ = MessageFrameTranslator.TryStripDirectTranslationMarker(sourceText, out var sourceValue);
        var (stripped, _) = ColorAwareTranslationComposer.Strip(finalValue);
        Record(
            new FinalOutputObservation(
                sink,
                route,
                detail,
                PhaseBeforeSink,
                TranslationStatusDirectMarker,
                ComputeMarkupStatus(sourceValue, finalValue),
                DirectMarkerStatusPresent,
                sourceValue,
                stripped,
                finalValue,
                finalValue));
    }

    internal static void RecordAlreadyLocalized(
        string sink,
        string route,
        string? sourceText,
        string? strippedText)
    {
        RecordPassthroughDecision(
            sink,
            route,
            DetailAlreadyLocalized,
            TranslationStatusAlreadyLocalized,
            sourceText,
            strippedText);
    }

    internal static void RecordSkipped(
        string sink,
        string route,
        string detail,
        string? sourceText,
        string? strippedText)
    {
        RecordPassthroughDecision(
            sink,
            route,
            detail,
            TranslationStatusSkipped,
            sourceText,
            strippedText);
    }

    internal static void Record(FinalOutputObservation observation)
    {
        var normalized = Normalize(observation);
        var hitCount = AddOrUpdateCapped(HitCounts, BuildCounterKey(normalized), MaxObservedEntries);
        if (!ObservabilityHelpers.ShouldLogMissingHit(hitCount))
        {
            return;
        }

        var sourceMarkupSpans = BuildMarkupSpanSignature(normalized.SourceText);
        var finalMarkupSpans = BuildMarkupSpanSignature(normalized.FinalText);
        var markupSpanStatus = ComputeMarkupSpanStatus(
            normalized.SourceText,
            normalized.FinalText,
            sourceMarkupSpans,
            finalMarkupSpans);
        var semanticDiagnostics = MarkupSemanticDiagnostics.Analyze(normalized.FinalText);
        var sourceVisibleSha256 = ObservabilityHelpers.ComputeSha256Hex(BuildVisibleText(normalized.SourceText));
        var finalVisibleSha256 = ObservabilityHelpers.ComputeSha256Hex(BuildVisibleText(normalized.FinalText));

        QudJPMod.LogToUnity(
            "[QudJP] FinalOutputProbe/" + ProbeVersion +
            ": sink='" + SanitizeQuotedValue(normalized.Sink) +
            "' route='" + SanitizeQuotedValue(normalized.Route) +
            "' detail='" + SanitizeQuotedValue(normalized.Detail) +
            "' phase='" + SanitizeQuotedValue(normalized.Phase) +
            "' translation_status='" + SanitizeQuotedValue(normalized.TranslationStatus) +
            "' markup_status='" + SanitizeQuotedValue(normalized.MarkupStatus) +
            "' direct_marker_status='" + SanitizeQuotedValue(normalized.DirectMarkerStatus) +
            "' hit=" + hitCount.ToString(CultureInfo.InvariantCulture) +
            " source='" + SanitizeQuotedValue(normalized.SourceText) +
            "' stripped='" + SanitizeQuotedValue(normalized.StrippedText) +
            "' translated='" + SanitizeQuotedValue(normalized.TranslatedText) +
            "' final='" + SanitizeQuotedValue(normalized.FinalText) + "'"
            + ObservabilityHelpers.BuildFinalOutputStructuredSuffix(
                normalized.Route,
                normalized.Sink,
                normalized.Detail,
                normalized.Phase,
                normalized.TranslationStatus,
                normalized.MarkupStatus,
                normalized.DirectMarkerStatus,
                normalized.SourceText,
                normalized.StrippedText,
                normalized.TranslatedText,
                normalized.FinalText,
                sourceMarkupSpans,
                finalMarkupSpans,
                markupSpanStatus,
                semanticDiagnostics.Status,
                semanticDiagnostics.Flags,
                sourceVisibleSha256,
                finalVisibleSha256));
    }

    private static string SanitizeQuotedValue(string value)
    {
        return ObservabilityHelpers.SanitizeForLog(value, MaxValueLength).Replace("'", "\\'");
    }

    private static void RecordPassthroughDecision(
        string sink,
        string route,
        string detail,
        string translationStatus,
        string? sourceText,
        string? strippedText)
    {
        var sourceValue = sourceText ?? string.Empty;
        Record(
            new FinalOutputObservation(
                sink,
                route,
                detail,
                PhaseBeforeSink,
                translationStatus,
                ComputeMarkupStatus(sourceValue, sourceValue),
                DirectMarkerStatusAbsent,
                sourceValue,
                strippedText,
                string.Empty,
                sourceValue));
    }

    private static string ComputeMarkupStatus(string? sourceText, string? finalText)
    {
        var sourceMatches = MarkupTokenPattern.Matches(sourceText ?? string.Empty);
        var finalMatches = MarkupTokenPattern.Matches(finalText ?? string.Empty);
        if (sourceMatches.Count == 0 && finalMatches.Count == 0)
        {
            return MarkupStatusNoMarkup;
        }

        if (sourceMatches.Count == 0)
        {
            return MarkupStatusFinalOnly;
        }

        if (finalMatches.Count == 0)
        {
            return MarkupStatusSourceOnly;
        }

        if (sourceMatches.Count != finalMatches.Count)
        {
            return MarkupStatusMismatch;
        }

        for (var index = 0; index < sourceMatches.Count; index++)
        {
            if (!string.Equals(sourceMatches[index].Value, finalMatches[index].Value, StringComparison.Ordinal))
            {
                return MarkupStatusMismatch;
            }
        }

        return MarkupStatusMatched;
    }

    private static string ComputeMarkupSpanStatus(string? sourceText, string? finalText)
    {
        return ComputeMarkupSpanStatus(
            sourceText,
            finalText,
            BuildMarkupSpanSignature(sourceText),
            BuildMarkupSpanSignature(finalText));
    }

    private static string ComputeMarkupSpanStatus(
        string? sourceText,
        string? finalText,
        string sourceMarkupSpans,
        string finalMarkupSpans)
    {
        var tokenStatus = ComputeMarkupStatus(sourceText, finalText);
        if (string.Equals(tokenStatus, MarkupStatusNoMarkup, StringComparison.Ordinal)
            || string.Equals(tokenStatus, MarkupStatusSourceOnly, StringComparison.Ordinal)
            || string.Equals(tokenStatus, MarkupStatusFinalOnly, StringComparison.Ordinal))
        {
            return tokenStatus;
        }

        if (!string.Equals(tokenStatus, MarkupStatusMatched, StringComparison.Ordinal))
        {
            return MarkupSpanStatusTokenMismatch;
        }

        return string.Equals(sourceMarkupSpans, finalMarkupSpans, StringComparison.Ordinal)
            ? MarkupStatusMatched
            : MarkupSpanStatusSpanMismatch;
    }

    private static string BuildMarkupSpanSignature(string? text)
    {
        var value = text ?? string.Empty;
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var spans = new List<string>();
        var stack = new Stack<MarkupScope>();
        var visibleIndex = 0;
        var index = 0;
        while (index < value.Length)
        {
            if (TryReadQudOpen(value, index, out var qudShader, out var qudLength))
            {
                stack.Push(new MarkupScope("qud", qudShader, visibleIndex));
                index += qudLength;
                continue;
            }

            if (StartsWithOrdinal(value, index, "}}"))
            {
                if (TryPopScope(stack, "qud", out var scope))
                {
                    spans.Add(FormatSpan(scope, visibleIndex));
                }
                else
                {
                    spans.Add("qud-close@" + visibleIndex.ToString(CultureInfo.InvariantCulture));
                }

                index += 2;
                continue;
            }

            if (TryReadTmpColorOpen(value, index, out var tmpColor, out var tmpOpenLength))
            {
                stack.Push(new MarkupScope("tmp", tmpColor, visibleIndex));
                index += tmpOpenLength;
                continue;
            }

            if (StartsWithOrdinal(value, index, "</color>"))
            {
                if (TryPopScope(stack, "tmp", out var scope))
                {
                    spans.Add(FormatSpan(scope, visibleIndex));
                }
                else
                {
                    spans.Add("tmp-close@" + visibleIndex.ToString(CultureInfo.InvariantCulture));
                }

                index += "</color>".Length;
                continue;
            }

            if (StartsWithOrdinal(value, index, "&&"))
            {
                spans.Add("escape:&@" + visibleIndex.ToString(CultureInfo.InvariantCulture));
                visibleIndex++;
                index += 2;
                continue;
            }

            if (StartsWithOrdinal(value, index, "^^"))
            {
                spans.Add("escape:^@" + visibleIndex.ToString(CultureInfo.InvariantCulture));
                visibleIndex++;
                index += 2;
                continue;
            }

            if ((value[index] == '&' || value[index] == '^') && index + 1 < value.Length && IsAsciiLetter(value[index + 1]))
            {
                spans.Add((value[index] == '&' ? "fg:" : "bg:")
                    + value[index + 1]
                    + "@"
                    + visibleIndex.ToString(CultureInfo.InvariantCulture));
                index += 2;
                continue;
            }

            if (TryReadPlaceholder(value, index, out var placeholder, out var placeholderLength))
            {
                spans.Add("placeholder:" + placeholder + "@" + visibleIndex.ToString(CultureInfo.InvariantCulture));
                index += placeholderLength;
                continue;
            }

            visibleIndex++;
            index++;
        }

        while (stack.Count > 0)
        {
            var scope = stack.Pop();
            spans.Add(FormatSpan(scope, visibleIndex));
        }

        return string.Join("|", spans);
    }

    private static string BuildVisibleText(string text)
    {
        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            if (TryReadQudOpen(text, index, out _, out var qudLength))
            {
                index += qudLength;
                continue;
            }

            if (StartsWithOrdinal(text, index, "}}"))
            {
                index += 2;
                continue;
            }

            if (TryReadTmpColorOpen(text, index, out _, out var tmpOpenLength))
            {
                index += tmpOpenLength;
                continue;
            }

            if (StartsWithOrdinal(text, index, "</color>"))
            {
                index += "</color>".Length;
                continue;
            }

            if (StartsWithOrdinal(text, index, "&&"))
            {
                builder.Append('&');
                index += 2;
                continue;
            }

            if (StartsWithOrdinal(text, index, "^^"))
            {
                builder.Append('^');
                index += 2;
                continue;
            }

            if ((text[index] == '&' || text[index] == '^') && index + 1 < text.Length && IsAsciiLetter(text[index + 1]))
            {
                index += 2;
                continue;
            }

            if (TryReadPlaceholder(text, index, out _, out var placeholderLength))
            {
                index += placeholderLength;
                continue;
            }

            builder.Append(text[index]);
            index++;
        }

        return builder.ToString();
    }

    private static bool TryReadQudOpen(string value, int index, out string shader, out int length)
    {
        shader = string.Empty;
        length = 0;
        if (!StartsWithOrdinal(value, index, "{{"))
        {
            return false;
        }

        var separatorIndex = -1;
        for (var scanIndex = index + 2; scanIndex < value.Length; scanIndex++)
        {
            if (scanIndex + 1 < value.Length && value[scanIndex] == '}' && value[scanIndex + 1] == '}')
            {
                return false;
            }

            if (value[scanIndex] == '|')
            {
                separatorIndex = scanIndex;
                break;
            }
        }

        if (separatorIndex < 0)
        {
            return false;
        }

        var candidate = value.Substring(index + 2, separatorIndex - index - 2);
        if (candidate.Length == 0)
        {
            return false;
        }

        shader = candidate;
        length = separatorIndex - index + 1;
        return true;
    }

    private static bool TryReadTmpColorOpen(string value, int index, out string color, out int length)
    {
        color = string.Empty;
        length = 0;
        if (!StartsWithOrdinal(value, index, "<color="))
        {
            return false;
        }

        var endIndex = value.IndexOf('>', index + "<color=".Length);
        if (endIndex < 0)
        {
            return false;
        }

        color = value.Substring(index + "<color=".Length, endIndex - index - "<color=".Length);
        length = endIndex - index + 1;
        return true;
    }

    private static bool TryReadPlaceholder(string value, int index, out string placeholder, out int length)
    {
        placeholder = string.Empty;
        length = 0;
        if (value[index] != '=')
        {
            return false;
        }

        var endIndex = value.IndexOf('=', index + 1);
        if (endIndex < 0)
        {
            return false;
        }

        var candidate = value.Substring(index + 1, endIndex - index - 1);
        if (candidate.Length == 0)
        {
            return false;
        }

        for (var candidateIndex = 0; candidateIndex < candidate.Length; candidateIndex++)
        {
            var character = candidate[candidateIndex];
            if (!IsAsciiLetterOrDigit(character) && character != '_' && character != '.')
            {
                return false;
            }
        }

        placeholder = candidate;
        length = endIndex - index + 1;
        return true;
    }

    private static bool TryPopScope(Stack<MarkupScope> stack, string kind, out MarkupScope scope)
    {
        if (stack.Count > 0 && string.Equals(stack.Peek().Kind, kind, StringComparison.Ordinal))
        {
            scope = stack.Pop();
            return true;
        }

        scope = default;
        return false;
    }

    private static string FormatSpan(MarkupScope scope, int endVisibleIndex)
    {
        return scope.Kind
            + ":"
            + scope.Value
            + "@"
            + scope.StartVisibleIndex.ToString(CultureInfo.InvariantCulture)
            + "-"
            + endVisibleIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static bool StartsWithOrdinal(string value, int index, string prefix)
    {
        return index <= value.Length - prefix.Length
            && string.CompareOrdinal(value, index, prefix, 0, prefix.Length) == 0;
    }

    private static bool IsAsciiLetter(char character)
    {
        return (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return IsAsciiLetter(character) || (character >= '0' && character <= '9');
    }

    private static NormalizedObservation Normalize(FinalOutputObservation observation)
    {
        return new NormalizedObservation(
            ObservabilityHelpers.NormalizeContext(observation.Sink),
            ObservabilityHelpers.ExtractPrimaryContext(observation.Route),
            ObservabilityHelpers.NormalizeContext(observation.Detail),
            ObservabilityHelpers.NormalizeContext(observation.Phase),
            ObservabilityHelpers.NormalizeContext(observation.TranslationStatus),
            ObservabilityHelpers.NormalizeContext(observation.MarkupStatus),
            ObservabilityHelpers.NormalizeContext(observation.DirectMarkerStatus),
            observation.SourceText ?? string.Empty,
            observation.StrippedText ?? string.Empty,
            observation.TranslatedText ?? string.Empty,
            observation.FinalText ?? string.Empty);
    }

    private static string BuildCounterKey(NormalizedObservation observation)
    {
        var builder = new StringBuilder();
        AppendCounterKeyPart(builder, observation.Sink);
        AppendCounterKeyPart(builder, observation.Route);
        AppendCounterKeyPart(builder, observation.Detail);
        AppendCounterKeyPart(builder, observation.Phase);
        AppendCounterKeyPart(builder, observation.TranslationStatus);
        AppendCounterKeyPart(builder, observation.MarkupStatus);
        AppendCounterKeyPart(builder, observation.DirectMarkerStatus);
        AppendCounterKeyPart(builder, observation.SourceText);
        AppendCounterKeyPart(builder, observation.StrippedText);
        AppendCounterKeyPart(builder, observation.TranslatedText);
        AppendCounterKeyPart(builder, observation.FinalText);
        return builder.ToString();
    }

    private static void AppendCounterKeyPart(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
    }

    private static int AddOrUpdateCapped(ConcurrentDictionary<string, int> counters, string key, int maxKeys)
    {
        lock (HitCountsSync)
        {
            if (counters.ContainsKey(key) || counters.Count < maxKeys)
            {
                return counters.AddOrUpdate(key, 1, ObservabilityHelpers.IncrementCounter);
            }

            return counters.AddOrUpdate(OverflowKey, 1, ObservabilityHelpers.IncrementCounter);
        }
    }

    private readonly struct NormalizedObservation
    {
        internal NormalizedObservation(
            string sink,
            string route,
            string detail,
            string phase,
            string translationStatus,
            string markupStatus,
            string directMarkerStatus,
            string sourceText,
            string strippedText,
            string translatedText,
            string finalText)
        {
            Sink = sink;
            Route = route;
            Detail = detail;
            Phase = phase;
            TranslationStatus = translationStatus;
            MarkupStatus = markupStatus;
            DirectMarkerStatus = directMarkerStatus;
            SourceText = sourceText;
            StrippedText = strippedText;
            TranslatedText = translatedText;
            FinalText = finalText;
        }

        internal string Sink { get; }

        internal string Route { get; }

        internal string Detail { get; }

        internal string Phase { get; }

        internal string TranslationStatus { get; }

        internal string MarkupStatus { get; }

        internal string DirectMarkerStatus { get; }

        internal string SourceText { get; }

        internal string StrippedText { get; }

        internal string TranslatedText { get; }

        internal string FinalText { get; }
    }

    private readonly struct MarkupScope
    {
        internal MarkupScope(string kind, string value, int startVisibleIndex)
        {
            Kind = kind;
            Value = value;
            StartVisibleIndex = startVisibleIndex;
        }

        internal string Kind { get; }

        internal string Value { get; }

        internal int StartVisibleIndex { get; }
    }

}
