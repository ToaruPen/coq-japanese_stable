using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class TempleDedicationPlaqueInscriptionTranslator
{
    private static readonly Regex DedicationPattern = new(
        "^This temple was built in (?<date>.+?) by (?<guild>.+?), who detached from their egregore (?<egregore>.+?) in the (?<era>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        var match = DedicationPattern.Match(stripped);
        if (!match.Success)
        {
            translated = original;
            return false;
        }

        var egregore = match.Groups["egregore"].Value;
        if (!HistorySpiceComponentLookup.TryTranslateTitlePhrase(egregore, out var translatedEgregore))
        {
            translatedEgregore = egregore;
        }

        var era = match.Groups["era"].Value;
        if (!HistorySpiceComponentLookup.TryTranslateEraName(era, out var translatedEra))
        {
            translatedEra = era;
        }

        var translatedCore = "この寺院は"
            + match.Groups["date"].Value
            + "に"
            + match.Groups["guild"].Value
            + "によって建てられた。彼らは"
            + translatedEra
            + "に、エグレゴア「"
            + translatedEgregore
            + "」から分離した。";
        translated = Restore(translatedCore, spans, stripped.Length, original);
        return true;
    }

    private static string Restore(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int strippedLength,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            strippedLength,
            source);
    }
}
