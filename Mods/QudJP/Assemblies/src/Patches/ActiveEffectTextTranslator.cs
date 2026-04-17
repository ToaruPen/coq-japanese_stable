using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class ActiveEffectTextTranslator
{
    private const string QuicknessMutationSingularTemplateKey = "+{0} Quickness\n+1 rank to physical mutations";

    private const string QuicknessMutationPluralTemplateKey = "+{0} Quickness\n+{1} ranks to physical mutations";

    private static readonly Regex QuicknessMutationSingularPattern = new(
        @"^\+(?<quickness>\d+) Quickness\n\+1 rank to physical mutations$",
        RegexOptions.CultureInvariant);

    private static readonly Regex QuicknessMutationPluralPattern = new(
        @"^\+(?<quickness>\d+) Quickness\n\+(?<ranks>\d+) ranks to physical mutations$",
        RegexOptions.CultureInvariant);

    internal static bool TryTranslateText(string source, string route, string family, out string translated)
    {
        if (TryTranslateExact(source, route, family + ".Exact", out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(source, route, family + ".Template", out translated))
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

    private static bool TryTranslateExact(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(stripped, out var exact)
            && !string.Equals(exact, stripped, StringComparison.Ordinal))
        {
            translated = spans.Count == 0 ? exact : ColorAwareTranslationComposer.Restore(exact, spans);
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        if (!string.Equals(source, stripped, StringComparison.Ordinal)
            && StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exactSource)
            && !string.Equals(exactSource, source, StringComparison.Ordinal))
        {
            translated = exactSource;
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                route,
                family,
                QuicknessMutationSingularPattern,
                QuicknessMutationSingularTemplateKey,
                match => new object[] { match.Groups["quickness"].Value },
                out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
                source,
                stripped,
                spans,
                route,
                family,
                QuicknessMutationPluralPattern,
                QuicknessMutationPluralTemplateKey,
                match => new object[] { match.Groups["quickness"].Value, match.Groups["ranks"].Value },
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateTemplate(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        Regex pattern,
        string templateKey,
        Func<Match, object[]> buildArguments,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate(templateKey);
        if (string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var visible = string.Format(CultureInfo.InvariantCulture, template, buildArguments(match));
        translated = spans.Count == 0
            ? visible
            : ColorAwareTranslationComposer.RestoreRelative(visible, spans, stripped.Length);
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
