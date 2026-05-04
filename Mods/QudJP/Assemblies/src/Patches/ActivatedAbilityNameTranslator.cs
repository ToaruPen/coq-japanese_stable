using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class ActivatedAbilityNameTranslator
{
    private static readonly Regex ReleaseGasPattern =
        new Regex("^Release (?<gas>.+ Gas)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static string TranslatePreservingColors(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => TryTranslateVisibleName(visible, out var visibleTranslated)
                ? visibleTranslated
                : visible);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
    }

    internal static bool TryTranslateVisibleName(string source, out string translated)
    {
        var releaseGasMatch = ReleaseGasPattern.Match(source);
        if (releaseGasMatch.Success
            && TryTranslateReleaseGasName(releaseGasMatch.Groups["gas"].Value, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateReleaseGasName(string gasName, out string translated)
    {
        var generationName = gasName + " Generation";
        var translatedGenerationName = ChargenStructuredTextTranslator.Translate(generationName);
        if (string.Equals(translatedGenerationName, generationName, StringComparison.Ordinal))
        {
            translated = "Release " + gasName;
            return false;
        }

        translated = ToReleaseName(translatedGenerationName);
        return true;
    }

    private static string ToReleaseName(string translatedGenerationName)
    {
        const string generationSuffix = "生成";
        if (translatedGenerationName.EndsWith(generationSuffix, StringComparison.Ordinal))
        {
            return translatedGenerationName.Substring(0, translatedGenerationName.Length - generationSuffix.Length) + "放出";
        }

        return translatedGenerationName + "放出";
    }
}
