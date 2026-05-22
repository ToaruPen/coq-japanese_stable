using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace QudJP;

internal sealed class ColorShapeCapture
{
    internal string Route { get; init; } = string.Empty;

    internal string Producer { get; init; } = string.Empty;

    internal string SourceText { get; init; } = string.Empty;

    internal string SourceVisibleText { get; init; } = string.Empty;

    internal string FinalText { get; init; } = string.Empty;

    internal string FinalVisibleText { get; init; } = string.Empty;

    internal string SourceColorSpans { get; init; } = string.Empty;

    internal string FinalColorSpans { get; init; } = string.Empty;

    internal string SourceVisibleSha256 { get; init; } = string.Empty;

    internal string FinalVisibleSha256 { get; init; } = string.Empty;

    internal string MarkupSemanticStatus { get; init; } = string.Empty;

    internal string MarkupSemanticFlags { get; init; } = string.Empty;
}

internal static class ColorShapeCaptureObservability
{
    private const string ProbeVersion = "v1";
    private const int MaxRouteProducers = 2048;
    private const int MaxValueLength = 200;
    private const string OverflowKey = "__overflow__";

    private static readonly ConcurrentDictionary<string, int> RouteProducerCounts =
        new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    internal static void ResetForTests()
    {
        RouteProducerCounts.Clear();
    }

    internal static int GetRouteProducerHitCountForTests(string route, string producer)
    {
        return ObservabilityHelpers.GetCounterValue(RouteProducerCounts, BuildCounterKey(route, producer));
    }

    internal static void Record(string? route, string producer, string? source, string? final)
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled)
        {
            return;
        }

        var sourceValue = source ?? string.Empty;
        var finalValue = final ?? string.Empty;
        var normalizedRoute = ObservabilityHelpers.ExtractPrimaryContext(route);
        var structuredRoute = route ?? ObservabilityHelpers.NoContextLabel;
        var normalizedProducer = string.IsNullOrWhiteSpace(producer)
            ? ObservabilityHelpers.NoContextLabel
            : producer.Trim();

        var counterKey = BuildCounterKey(normalizedRoute, normalizedProducer);
        var hitCount = AddOrUpdateCapped(RouteProducerCounts, counterKey, MaxRouteProducers);
        if (!ObservabilityHelpers.ShouldLogMissingHit(hitCount))
        {
            return;
        }

        RuntimeDiagnostics.LogVerboseProbe(() =>
        {
            var capture = Capture(normalizedRoute, normalizedProducer, sourceValue, finalValue);

            return "[QudJP] ColorShapeProbe/" + ProbeVersion
                + ": route='" + SanitizeQuotedValue(capture.Route)
                + "' producer='" + SanitizeQuotedValue(capture.Producer)
                + "' hit=" + hitCount.ToString(CultureInfo.InvariantCulture)
                + " source='" + SanitizeQuotedValue(capture.SourceText)
                + "' source_visible='" + SanitizeQuotedValue(capture.SourceVisibleText)
                + "' final='" + SanitizeQuotedValue(capture.FinalText)
                + "' final_visible='" + SanitizeQuotedValue(capture.FinalVisibleText) + "'"
                + ObservabilityHelpers.BuildHelperStructuredSuffix(
                    structuredRoute,
                    capture.Producer,
                    capture.SourceText)
                + "; producer=" + ObservabilityHelpers.EscapeStructuredValue(capture.Producer)
                + "; source_text_sample=" + EscapeStructuredSample(capture.SourceText)
                + "; source_visible_text_sample=" + EscapeStructuredSample(capture.SourceVisibleText)
                + "; final_text_sample=" + EscapeStructuredSample(capture.FinalText)
                + "; final_visible_text_sample=" + EscapeStructuredSample(capture.FinalVisibleText)
                + "; source_color_spans=" + ObservabilityHelpers.EscapeStructuredValue(capture.SourceColorSpans)
                + "; final_color_spans=" + ObservabilityHelpers.EscapeStructuredValue(capture.FinalColorSpans)
                + "; source_visible_sha256=" + capture.SourceVisibleSha256
                + "; final_visible_sha256=" + capture.FinalVisibleSha256
                + "; markup_semantic_status=" + capture.MarkupSemanticStatus
                + "; markup_semantic_flags=" + capture.MarkupSemanticFlags;
        });
    }

    internal static ColorShapeCapture Capture(string? route, string producer, string? source, string? final)
    {
        var sourceValue = source ?? string.Empty;
        var finalValue = final ?? string.Empty;
        var normalizedRoute = ObservabilityHelpers.ExtractPrimaryContext(route);
        var normalizedProducer = string.IsNullOrWhiteSpace(producer)
            ? ObservabilityHelpers.NoContextLabel
            : producer.Trim();
        var (sourceVisible, sourceSpans) = ColorAwareTranslationComposer.Strip(sourceValue);
        var (finalVisible, finalSpans) = ColorAwareTranslationComposer.Strip(finalValue);
        var semanticDiagnostics = MarkupSemanticDiagnostics.Analyze(finalValue);

        return new ColorShapeCapture
        {
            Route = normalizedRoute,
            Producer = normalizedProducer,
            SourceText = sourceValue,
            SourceVisibleText = sourceVisible,
            FinalText = finalValue,
            FinalVisibleText = finalVisible,
            SourceColorSpans = BuildSpanSignature(sourceSpans),
            FinalColorSpans = BuildSpanSignature(finalSpans),
            SourceVisibleSha256 = ObservabilityHelpers.ComputeSha256Hex(sourceVisible),
            FinalVisibleSha256 = ObservabilityHelpers.ComputeSha256Hex(finalVisible),
            MarkupSemanticStatus = semanticDiagnostics.Status,
            MarkupSemanticFlags = semanticDiagnostics.Flags,
        };
    }

    private static string BuildCounterKey(string route, string producer)
    {
        return route + ObservabilityHelpers.ContextSeparator + producer;
    }

    private static int AddOrUpdateCapped(ConcurrentDictionary<string, int> counters, string key, int maxKeys)
    {
        if (counters.ContainsKey(key) || counters.Count < maxKeys)
        {
            return counters.AddOrUpdate(key, 1, ObservabilityHelpers.IncrementCounter);
        }

        return counters.AddOrUpdate(OverflowKey, 1, ObservabilityHelpers.IncrementCounter);
    }

    private static string BuildSpanSignature(IReadOnlyList<ColorSpan> spans)
    {
        if (spans.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < spans.Count; index++)
        {
            if (index > 0)
            {
                builder.Append('|');
            }

            var span = spans[index];
            builder.Append(span.Index.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(span.Token);
        }

        return builder.ToString();
    }

    private static string SanitizeQuotedValue(string value)
    {
        return ObservabilityHelpers.SanitizeForLog(value, MaxValueLength).Replace("'", "\\'");
    }

    private static string EscapeStructuredSample(string value)
    {
        return ObservabilityHelpers.EscapeStructuredValue(ObservabilityHelpers.SanitizeForLog(value, MaxValueLength));
    }
}
