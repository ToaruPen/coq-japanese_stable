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

    private static readonly Regex CoveredInLiquidPattern = new(
        @"^Covered in (?<amount>\d+) drams? of (?<liquid>.+)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex MoveSpeedPattern = new(
        @"^(?<shift>[+-]\d+) move speed\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex DominatedRemainingPattern = new(
        @"^dominated \((?<turns>\d+) turns? remaining\)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex TimeDilatedPattern = new(
        @"^time-dilated \(-(?<penalty>\d+) Quickness\)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LyingOnPattern = new(
        @"^lying on (?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex EngulfedByPattern = new(
        @"^engulfed by (?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex EnclosedInPattern = new(
        @"^enclosed in (?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex SittingOnPattern = new(
        @"^sitting on (?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PilotingPattern = new(
        @"^piloting (?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex MarkedByPattern = new(
        @"^marked by (?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex CleavedArmorPattern = new(
        @"^cleaved \(-(?<penalty>\d+) AV\)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PsionicallyCleavedPattern = new(
        @"^psionically cleaved \(-(?<penalty>\d+) MA\)$",
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

        if (TryTranslateGeneratedLine(source, route, family + ".GeneratedLine", out translated))
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
            translated = RestoreExactTranslation(exact, spans, stripped.Length);
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

    private static string RestoreExactTranslation(string exact, IReadOnlyList<ColorSpan> spans, int sourceLength)
    {
        if (spans.Count == 0)
        {
            return exact;
        }

        return ColorAwareTranslationComposer.HasColorMarkup(exact)
            ? ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                exact,
                spans,
                sourceLength)
            : ColorAwareTranslationComposer.Restore(exact, spans);
    }

    private static bool TryTranslateTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateGeneratedDescriptionTemplate(source, stripped, spans, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateKnownTemplate(
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

        if (TryTranslateKnownTemplate(
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

    private static bool TryTranslateGeneratedDescriptionTemplate(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        if (TryTranslateSimpleGeneratedTemplate(
                source,
                stripped,
                spans,
                route,
                family,
                DominatedRemainingPattern,
                "dominated ({0} turns remaining)",
                match => new object[] { match.Groups["turns"].Value },
                out translated))
        {
            return true;
        }

        if (TryTranslateSimpleGeneratedTemplate(
                source,
                stripped,
                spans,
                route,
                family,
                TimeDilatedPattern,
                "time-dilated ({{C|-{0}}} Quickness)",
                match => new object[] { match.Groups["penalty"].Value },
                out translated))
        {
            return true;
        }

        if (TryTranslateSingleCaptureGeneratedTemplate(source, stripped, spans, route, family, LyingOnPattern, "lying on {0}", out translated)
            || TryTranslateSingleCaptureGeneratedTemplate(source, stripped, spans, route, family, EngulfedByPattern, "engulfed by {0}", out translated)
            || TryTranslateSingleCaptureGeneratedTemplate(source, stripped, spans, route, family, EnclosedInPattern, "enclosed in {0}", out translated)
            || TryTranslateSingleCaptureGeneratedTemplate(source, stripped, spans, route, family, SittingOnPattern, "sitting on {0}", out translated)
            || TryTranslateSingleCaptureGeneratedTemplate(source, stripped, spans, route, family, PilotingPattern, "piloting {0}", out translated)
            || TryTranslateSingleCaptureGeneratedTemplate(source, stripped, spans, route, family, MarkedByPattern, "marked by {0}", out translated))
        {
            return true;
        }

        if (TryTranslateSimpleGeneratedTemplate(
                source,
                stripped,
                spans,
                route,
                family,
                CleavedArmorPattern,
                "cleaved ({{C|-{0} AV}})",
                match => new object[] { match.Groups["penalty"].Value },
                out translated))
        {
            return true;
        }

        if (TryTranslateSimpleGeneratedTemplate(
                source,
                stripped,
                spans,
                route,
                family,
                PsionicallyCleavedPattern,
                "psionically cleaved (-{0} MA)",
                match => new object[] { match.Groups["penalty"].Value },
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateSingleCaptureGeneratedTemplate(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        Regex pattern,
        string templateKey,
        out string translated)
    {
        return TryTranslateSimpleGeneratedTemplate(
            source,
            stripped,
            spans,
            route,
            family,
            pattern,
            templateKey,
            static match => new object[]
            {
                ColorAwareTranslationComposer.TranslatePreservingColors(
                    match.Groups["target"].Value,
                    StringHelpers.TranslateExactOrLowerAsciiFallback),
            },
            out translated);
    }

    private static bool TryTranslateSimpleGeneratedTemplate(
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

        var visible = ReplacePlaceholders(template, buildArguments(match));
        translated = spans.Count == 0
            ? visible
            : ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                visible,
                spans,
                stripped.Length);
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + ".GeneratedDescription", source, translated);
        return true;
    }

    private static bool TryTranslateKnownTemplate(
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

        var visible = ReplacePlaceholders(template, buildArguments(match));
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

    private static string ReplacePlaceholders(string template, IReadOnlyList<object> arguments)
    {
        var result = template;
        for (var index = 0; index < arguments.Count; index++)
        {
            var value = Convert.ToString(arguments[index], CultureInfo.InvariantCulture)!;
            result = result.Replace("{" + index.ToString(CultureInfo.InvariantCulture) + "}", value);
        }

        return result;
    }

    private static bool TryTranslateGeneratedLine(string source, string route, string family, out string translated)
    {
        if (StringHelpers.ContainsOrdinal(source, "\n"))
        {
            translated = source;
            return false;
        }

        if (TryTranslateCoveredInLiquidLine(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        translated = ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateEffectLineFallback);
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateCoveredInLiquidLine(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var coveredMatch = CoveredInLiquidPattern.Match(stripped);
        if (!coveredMatch.Success)
        {
            translated = source;
            return false;
        }

        var visible = TranslateCoveredInLiquidMatch(coveredMatch);
        translated = spans.Count == 0
            ? visible
            : ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
                visible,
                spans,
                stripped);
        return !string.Equals(source, translated, StringComparison.Ordinal);
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
                TranslateEffectLineFallback);
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

    private static string TranslateEffectLineFallback(string visible)
    {
        var coveredMatch = CoveredInLiquidPattern.Match(visible);
        if (coveredMatch.Success)
        {
            return TranslateCoveredInLiquidMatch(coveredMatch);
        }

        var moveSpeedMatch = MoveSpeedPattern.Match(visible);
        if (moveSpeedMatch.Success)
        {
            return string.Format(CultureInfo.InvariantCulture, "移動速度 {0}。", moveSpeedMatch.Groups["shift"].Value);
        }

        if (string.Equals(visible, "Moving at full speed.", StringComparison.Ordinal))
        {
            return "通常速度で移動している。";
        }

        var translated = StringHelpers.TranslateExactOrLowerAscii(visible);
        if (translated is not null)
        {
            return translated;
        }

        return visible;
    }

    private static string TranslateCoveredInLiquidMatch(Match coveredMatch)
    {
        var amount = coveredMatch.Groups["amount"].Value;
        var liquid = coveredMatch.Groups["liquid"].Value;
        var translatedLiquid = StringHelpers.TranslateExactOrLowerAsciiFallback(liquid);
        return string.Format(CultureInfo.InvariantCulture, "{0}を{1}ドラム浴びている。", translatedLiquid, amount);
    }
}
