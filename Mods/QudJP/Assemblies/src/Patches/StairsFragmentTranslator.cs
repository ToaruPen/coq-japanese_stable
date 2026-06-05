using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class StairsDownFragmentTranslator
{
    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        return StairsFragmentTranslator.TryTranslatePopupMessage(source, route, family, "descend", "下に降りてください。", out translated);
    }
}

internal static class StairsUpFragmentTranslator
{
    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        return StairsFragmentTranslator.TryTranslatePopupMessage(source, route, family, "ascend", "上に昇ってください。", out translated);
    }
}

internal static class StairsFragmentTranslator
{
    private static readonly Regex UseCommandPattern =
        new(
            "^Use (?<command>.+?) to (?<direction>descend|ascend)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslatePopupMessage(
        string source,
        string route,
        string family,
        string expectedDirection,
        string translatedAction,
        out string translated)
    {
        if (string.IsNullOrEmpty(source)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (string.Equals(expectedDirection, "descend", StringComparison.Ordinal)
            && string.Equals(
                stripped,
                "It looks like an awfully long fall. Are you sure you want to jump into the shaft?",
                StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                "ひどく長い落下になりそうだ。本当に縦穴に飛び込む？",
                spans,
                stripped.Length);
            DynamicTextObservability.RecordTransform(route, family + ".LongFallJumpPrompt", source, translated);
            return true;
        }

        var match = UseCommandPattern.Match(stripped);
        if (!match.Success || !string.Equals(match.Groups["direction"].Value, expectedDirection, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var command = ColorAwareTranslationComposer.RestoreCapture(match.Groups["command"].Value, spans, match.Groups["command"]).Trim();
        translated = command + "で" + translatedAction;
        DynamicTextObservability.RecordTransform(route, family + ".UseCommandTo" + expectedDirection, source, translated);
        return true;
    }
}
