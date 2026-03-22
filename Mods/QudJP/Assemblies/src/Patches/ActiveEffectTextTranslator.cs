using System;

namespace QudJP.Patches;

internal static class ActiveEffectTextTranslator
{
    internal static bool TryTranslateText(string source, string route, string family, out string translated)
    {
        if (TryTranslateExactPreservingColors(source, route, family + ".Exact", out translated))
        {
            return true;
        }

        if (TryTranslateLines(source, route, family + ".Lines", out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateExactPreservingColors(string source, string route, string family, out string translated)
    {
        translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TranslateExactOrLowerAsciiFallback(visible));
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateLines(string source, string route, string family, out string translated)
    {
        var lines = source.Split(new[] { '\n' }, StringSplitOptions.None);
        if (lines.Length < 2)
        {
            translated = source;
            return false;
        }

        var translatedLines = new string[lines.Length];
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var translatedLine = ColorAwareTranslationComposer.TranslatePreservingColors(
                line,
                static visible => StringHelpers.TranslateExactOrLowerAsciiFallback(visible));
            changed |= !string.Equals(line, translatedLine, StringComparison.Ordinal);
            translatedLines[index] = translatedLine;
        }

        if (!changed)
        {
            translated = source;
            return false;
        }

        translated = string.Join("\n", translatedLines);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }
}
