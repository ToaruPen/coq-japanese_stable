using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class ActiveEffectTextTranslator
{
    private const string GeneratedTemplateDictionaryFile = "Scoped/world-effects-generated-templates.ja.json";

    private const string QuicknessMutationSingularTemplateKey = "+{0} Quickness\n+1 rank to physical mutations";

    private const string QuicknessMutationPluralTemplateKey = "+{0} Quickness\n+{1} ranks to physical mutations";

    private const string LongbladeDefensiveTemplateKey = "+{0} DV while wielding a long blade in the primary hand.";

    private const string LongbladeAggressiveTemplateKey = "+{0} to your penetration roll and -{1} to hit while wielding a long blade in the primary hand.";

    private const string LongbladeDuelingTemplateKey = "+{0} to hit while wielding a long blade in the primary hand.";

    private static readonly IReadOnlyDictionary<string, string> GeneratedTemplateContexts =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dominated ({0} turns remaining)"] = "XRL.World.Effects.Dominated.GetDescription",
            ["time-dilated ({{C|{0}}} Quickness)"] = "XRL.World.Effects.ITimeDilated.GetDescription",
            ["time-dilated ({{C|-{0}}} Quickness)"] = "XRL.World.Effects.ITimeDilated.GetDescription",
            ["Acts semi-randomly.\n-{0} DV\n-{0} MA"] = "XRL.World.Effects.Confused.GetDetails",
            ["Acts semi-randomly.\n-{0} DV\n-{0} MA\n-{1} to all mental attributes"] = "XRL.World.Effects.Confused.GetDetails",
            ["lying on {0}"] = "XRL.World.Effects.Prone.GetDescription",
            ["engulfed by {0}"] = "XRL.World.Effects.Engulfed.DisplayName",
            ["enclosed in {0}"] = "XRL.World.Effects.Enclosed.DisplayName",
            ["sitting on {0}"] = "XRL.World.Effects.Sitting.DisplayName",
            ["piloting {0}"] = "XRL.World.Effects.Piloting.DisplayName",
            ["marked by {0}"] = "XRL.World.Effects.RifleMark.GetDescription",
            ["cleaved ({{C|-{0} AV}})"] = "XRL.World.Effects.ShatterArmor.GetDescription",
            ["psionically cleaved (-{0} MA)"] = "XRL.World.Effects.ShatterMentalArmor.GetDescription",
            [QuicknessMutationSingularTemplateKey] = "XRL.World.Effects.AdrenalControl2Boosted.GetDetails",
            [QuicknessMutationPluralTemplateKey] = "XRL.World.Effects.AdrenalControl2Boosted.GetDetails",
            [LongbladeDefensiveTemplateKey] = "XRL.World.Effects.LongbladeStance_Defensive.GetDetails",
            [LongbladeAggressiveTemplateKey] = "XRL.World.Effects.LongbladeStance_Aggressive.GetDetails",
            [LongbladeDuelingTemplateKey] = "XRL.World.Effects.LongbladeStance_Dueling.GetDetails",
        };

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
        @"^(?<shift>[+-]\d+) [Mm]ove [Ss]peed\.?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex StatShiftLinePattern = new(
        @"^(?<shift>[+-]\d+) (?<stat>Strength|Agility|Toughness|Intelligence|Willpower|Ego|AV|DV|MA)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ResistanceShiftLinePattern = new(
        @"^(?<shift>[+-]\d+) (?<stat>heat resistance|cold resistance|electric resistance)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex AllMentalAttributesPattern = new(
        @"^(?<shift>[+-]\d+) to all mental attributes$",
        RegexOptions.CultureInvariant);

    private static readonly Regex DominatedRemainingPattern = new(
        @"^dominated \((?<turns>\d+) turns? remaining\)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex TimeDilatedSignedPattern = new(
        @"^time-dilated \((?<penalty>-?\d+) Quickness\)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ConfusionDetailsPattern = new(
        @"^Acts semi-randomly\.\n\s*-(?<level>\d+) DV\n\s*-\k<level> MA$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ConfusionDetailsWithMentalPenaltyPattern = new(
        @"^Acts semi-randomly\.\n\s*-(?<level>\d+) DV\n\s*-\k<level> MA\n\s*-(?<mental>\d+) to all mental attributes$",
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

    private static readonly Regex LongbladeDefensivePattern = new(
        @"^\+(?<dv>\d+) DV while wielding a long blade in the primary hand\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LongbladeAggressivePattern = new(
        @"^\+(?<penetration>\d+) to your penetration roll and -(?<hit>\d+) to hit while wielding a long blade in the primary hand\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LongbladeDuelingPattern = new(
        @"^\+(?<hit>\d+) to hit while wielding a long blade in the primary hand\.$",
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
        if (!string.Equals(source, stripped, StringComparison.Ordinal)
            && StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var exactSource)
            && !string.Equals(exactSource, source, StringComparison.Ordinal))
        {
            translated = exactSource;
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(stripped, out var exact)
            && !string.Equals(exact, stripped, StringComparison.Ordinal))
        {
            translated = RestoreExactTranslation(exact, spans, stripped.Length);
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

        if (TryTranslateKnownTemplate(
                source,
                stripped,
                spans,
                route,
                family,
                LongbladeDefensivePattern,
                LongbladeDefensiveTemplateKey,
                match => new object[] { match.Groups["dv"].Value },
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
                LongbladeAggressivePattern,
                LongbladeAggressiveTemplateKey,
                match => new object[] { match.Groups["penetration"].Value, match.Groups["hit"].Value },
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
                LongbladeDuelingPattern,
                LongbladeDuelingTemplateKey,
                match => new object[] { match.Groups["hit"].Value },
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
                TimeDilatedSignedPattern,
                "time-dilated ({{C|{0}}} Quickness)",
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
                TimeDilatedSignedPattern,
                "time-dilated ({{C|-{0}}} Quickness)",
                match => new object[] { match.Groups["penalty"].Value.TrimStart('-') },
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
                ConfusionDetailsWithMentalPenaltyPattern,
                "Acts semi-randomly.\n-{0} DV\n-{0} MA\n-{1} to all mental attributes",
                match => new object[] { match.Groups["level"].Value, match.Groups["mental"].Value },
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
                ConfusionDetailsPattern,
                "Acts semi-randomly.\n-{0} DV\n-{0} MA",
                match => new object[] { match.Groups["level"].Value },
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
                    TranslateGeneratedTargetCapture),
            },
            out translated);
    }

    private static string TranslateGeneratedTargetCapture(string source)
    {
        var translated = StringHelpers.TranslateExactOrLowerAscii(source);
        if (translated is not null)
        {
            return translated;
        }

        var withoutArticle = StringHelpers.StripLeadingEnglishArticle(
            source,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);
        if (string.Equals(withoutArticle, source, StringComparison.Ordinal))
        {
            return source;
        }

        translated = StringHelpers.TranslateExactOrLowerAscii(withoutArticle);
        if (translated is not null)
        {
            return translated;
        }

        return withoutArticle;
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

        var template = TranslateGeneratedTemplate(templateKey);
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

    private static string TranslateGeneratedTemplate(string templateKey)
    {
        if (!GeneratedTemplateContexts.TryGetValue(templateKey, out var context))
        {
            return Translator.Translate(templateKey);
        }

        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            templateKey,
            context,
            GeneratedTemplateDictionaryFile);
        if (scoped is not null)
        {
            return scoped;
        }

        return Translator.Translate(templateKey);
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

        var template = TranslateGeneratedTemplate(templateKey);
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

        var statShiftMatch = StatShiftLinePattern.Match(visible);
        if (statShiftMatch.Success)
        {
            return TranslateEffectStat(statShiftMatch.Groups["stat"].Value) + statShiftMatch.Groups["shift"].Value + "。";
        }

        var resistanceShiftMatch = ResistanceShiftLinePattern.Match(visible);
        if (resistanceShiftMatch.Success)
        {
            return TranslateEffectStat(resistanceShiftMatch.Groups["stat"].Value) + resistanceShiftMatch.Groups["shift"].Value + "。";
        }

        var allMentalAttributesMatch = AllMentalAttributesPattern.Match(visible);
        if (allMentalAttributesMatch.Success)
        {
            return string.Format(CultureInfo.InvariantCulture, "全精神属性に {0}", allMentalAttributesMatch.Groups["shift"].Value);
        }

        var runtimeObservedLine = visible switch
        {
            "Must spend a turn to stand up." => "立ち上がるには1ターンを費やす必要がある。",
            "Must spend a turn to stand up before moving." => "移動する前に立ち上がるには1ターンを費やす必要がある。",
            "Slightly improves natural healing rate." => "自然治癒速度がわずかに向上する。",
            "Improves natural healing rate." => "自然治癒速度が向上する。",
            "Aids in examining and disassembling artifacts." => "遺物の調査と分解に役立つ。",
            "Distracts from examining and disassembling artifacts." => "遺物の調査と分解を妨げる。",
            "Inflicts ongoing damage." => "継続ダメージを与える。",
            "Temperature does not passively return to ambient temperature" => "温度が自然に周囲温度へ戻らない。",
            "Patting or rolling firefighting actions are 25% as effective" => "叩く・転がる消火行動の効果が25%になる。",
            "Removes liquid coatings" => "液体の被覆を取り除く。",
            _ => null,
        };
        if (runtimeObservedLine is not null)
        {
            return runtimeObservedLine;
        }

        if (StatusLineTranslationHelpers.TryTranslateGeneratedActiveEffectPart(visible, out var generatedEffectName))
        {
            return generatedEffectName;
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

    private static string TranslateEffectStat(string stat)
    {
        return stat switch
        {
            "Strength" => "筋力",
            "Agility" => "敏捷",
            "Toughness" => "頑健",
            "Intelligence" => "知力",
            "Willpower" => "意志力",
            "Ego" => "自我",
            "heat resistance" => "熱耐性",
            "cold resistance" => "冷気耐性",
            "electric resistance" => "電気耐性",
            _ => stat,
        };
    }

    private static string TranslateCoveredInLiquidMatch(Match coveredMatch)
    {
        var amount = coveredMatch.Groups["amount"].Value;
        var liquid = coveredMatch.Groups["liquid"].Value;
        var translatedLiquid = TranslateLiquidPhrase(liquid);
        if (translatedLiquid is null)
        {
            translatedLiquid = StringHelpers.TranslateExactOrLowerAsciiFallback(liquid);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0}を{1}ドラム浴びている。", translatedLiquid, amount);
    }

    private static string? TranslateLiquidPhrase(string source)
    {
        return LiquidVolumeFragmentTranslator.TranslateLiquidPhrase(source);
    }
}
