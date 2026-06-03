using System;
using System.Collections.Generic;
using System.Globalization;

namespace QudJP;

internal static class DisplayNamePlaceholderTranslator
{
    internal static bool TryTranslatePlaceholderValue(
        string source,
        Func<string, string> translateDisplayNameCandidate,
        out string translated)
    {
        foreach (var candidate in EnumerateDisplayNameCandidates(source))
        {
            var translatedCandidate = translateDisplayNameCandidate(candidate);
            if (string.Equals(translatedCandidate, candidate, StringComparison.Ordinal))
            {
                continue;
            }

            var sourceWithoutDirectMarkers = MessageFrameTranslator.StripAllDirectTranslationMarkers(source);
            translated = ColorAwareTranslationComposer.HasColorMarkup(sourceWithoutDirectMarkers)
                ? ColorAwareTranslationComposer.TranslatePreservingColors(sourceWithoutDirectMarkers, _ => translatedCandidate)
                : translatedCandidate;
            return true;
        }

        translated = source;
        return false;
    }

    private static IEnumerable<string> EnumerateDisplayNameCandidates(string source)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(source).Trim();
        yield return visible;

        var title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(visible);
        if (!string.Equals(title, visible, StringComparison.Ordinal))
        {
            yield return title;
        }

        if (!TrySingularizeEnglishPluralDisplayName(visible, out var singular))
        {
            yield break;
        }

        var titleSingular = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(singular);
        if (!string.Equals(titleSingular, visible, StringComparison.Ordinal)
            && !string.Equals(titleSingular, title, StringComparison.Ordinal))
        {
            yield return titleSingular;
        }

        if (!string.Equals(singular, visible, StringComparison.Ordinal)
            && !string.Equals(singular, title, StringComparison.Ordinal)
            && !string.Equals(singular, titleSingular, StringComparison.Ordinal))
        {
            yield return singular;
        }
    }

    private static bool TrySingularizeEnglishPluralDisplayName(string source, out string singular)
    {
        if (source.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && source.Length > 3)
        {
            singular = source.Substring(0, source.Length - 3) + "y";
            return true;
        }

        if (source.Length > 1
            && char.ToLowerInvariant(source[source.Length - 1]) == 's'
            && !source.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
        {
            singular = source.Substring(0, source.Length - 1);
            return true;
        }

        singular = source;
        return false;
    }
}
