using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StatisticStatShiftDisplayNameTranslationPatch
{
    internal const string Context = nameof(StatisticStatShiftDisplayNameTranslationPatch);
    internal const string Family = Context + ".AddShift";

    private static readonly Regex PossessiveSourcePattern = new(
        "^(?<owner>.+)'s (?<source>camouflage|co-processor)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> FixedTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["camouflage"] = "迷彩",
            ["co-processor"] = "コプロセッサ",
        };

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var statisticType = AccessTools.TypeByName("XRL.World.Statistic");
        var method = statisticType is null
            ? null
            : AccessTools.Method(statisticType, "AddShift", new[] { typeof(int), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Statistic.AddShift(int,string,bool) target not found.", Context);
        }

        return method;
    }

    public static void Prefix(ref string DisplayName)
    {
        try
        {
            if (!TryTranslateStatShiftDisplayName(DisplayName, out var translated)
                || string.Equals(DisplayName, translated, StringComparison.Ordinal))
            {
                return;
            }

            DynamicTextObservability.RecordTransform(Context, Family, DisplayName, translated);
            DisplayName = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateStatShiftDisplayName(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var sourceText = source!;
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(sourceText, out var markedText))
        {
            sourceText = markedText;
        }

        if (FixedTranslations.TryGetValue(sourceText, out var exactTranslation))
        {
            translated = exactTranslation;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(sourceText);
        if (FixedTranslations.TryGetValue(stripped, out var colorAwareExactTranslation))
        {
            translated = ColorAwareTranslationComposer.Restore(colorAwareExactTranslation, spans);
            return true;
        }

        var match = PossessiveSourcePattern.Match(stripped);
        if (match.Success && FixedTranslations.TryGetValue(match.Groups["source"].Value, out var sourceTranslation))
        {
            var owner = ColorAwareTranslationComposer.RestoreCapture(match.Groups["owner"].Value, spans, match.Groups["owner"]).Trim();
            var sourceName = ColorAwareTranslationComposer.RestoreCapture(sourceTranslation, spans, match.Groups["source"]).Trim();
            translated = owner + "の" + sourceName;
            return true;
        }

        translated = sourceText;
        return !string.Equals(sourceText, source, StringComparison.Ordinal);
    }
}
