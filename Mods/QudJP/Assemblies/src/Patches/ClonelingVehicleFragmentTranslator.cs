using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class ClonelingVehicleFragmentTranslator
{
    private static readonly Regex OneDramPattern =
        new Regex("^You do not have 1 dram of (?<liquid>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutOfFuelPattern =
        new Regex("^Your onboard systems are out of (?<liquid>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        return TryTranslate(
            source,
            route,
            family,
            OneDramPattern,
            static (match, spans) => string.Concat(
                RestoreVisible(match.Groups["liquid"], spans),
                "を1ドラム持っていない。"),
            out translated);
    }

    internal static bool TryTranslateQueuedMessage(string source, string route, string family, out string translated)
    {
        return TryTranslate(
            source,
            route,
            family,
            OutOfFuelPattern,
            static (match, spans) => string.Concat(
                "搭載システムの",
                RestoreVisible(match.Groups["liquid"], spans),
                "が切れている。"),
            out translated);
    }

    private static bool TryTranslate(
        string source,
        string route,
        string family,
        Regex pattern,
        Func<Match, IReadOnlyList<ColorSpan>, string> build,
        out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = build(match, spans);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static string RestoreVisible(Group group, IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
