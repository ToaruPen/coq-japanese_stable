using System;
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
        new Regex(@"\{\{[A-Za-z0-9#]+\||\}\}|&&|\^\^|&[A-Za-z]|\^[A-Za-z]|</?color(?:=[^>]+)?>|=[A-Za-z0-9_.]+=", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
                normalized.FinalText));
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
}
