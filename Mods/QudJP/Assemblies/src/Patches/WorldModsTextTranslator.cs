using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class WorldModsTextTranslator
{
    private const string WorldModsDictionaryFile = "world-mods.ja.json";

    private static readonly Regex MasterworkPattern = new Regex(
        "^Masterwork: This weapon scores critical hits (?<value>.+) of the time instead of 5%\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PaintedPattern = new Regex(
        "^Painted: This item is painted with a scene from the life of the ancient (?<subject>.+):$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex JewelEncrustedPattern = new Regex(
        "^Jewel-Encrusted: This item is much more valuable than usual and grants the wearer (?<amount>[+-]\\d+) reputation with water barons\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ImprovedMutationPattern = new Regex(
        "^Grants you (?<mutation>.+) at level (?<level>\\d+)\\. If you already have (?<repeatMutation>.+), its level is increased by (?<repeatLevel>\\d+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisguiseAppearancePattern = new Regex(
        "^Disguise: This item makes its wearer appear to be (?<appearance>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisguiseReputationPattern = new Regex(
        "^\\+(?<amount>\\d+) reputation with (?<faction>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FactionSlayerPattern = new Regex(
        "^(?<chance>\\d+)% chance to behead (?<target>.+) on hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BlinkEscapePattern = new Regex(
        "^Whenever you're about to take avoidable damage, there's (?:a|an) (?<chance>\\d+)% chance you blink away instead\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FatecallerPattern = new Regex(
        "^(?<chance>\\d+)% of the time, the Fates have their way\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GlassArmorPattern = new Regex(
        "^Reflects (?<chance>\\d+)% damage back at your attackers, rounded up\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GlazedPattern = new Regex(
        "^(?<chance>\\d+)% chance to dismember on hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RefractivePattern = new Regex(
        "^Refractive: This item has (?:a|an) (?<chance>\\d+)% chance to refract light-based attacks\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LiquidCooledPattern = new Regex(
        "^Liquid-cooled: This weapon's rate of fire is increased by (?<bonus>\\d+), but it requires (?<liquid>.+?) to function\\. When fired, there's a one in (?<chance>\\d+) chance that 1 dram is consumed\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TransmuteSmallPattern = new Regex(
        "^Small chance to transmute an enemy into (?<term>.+) on hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TransmutePercentPattern = new Regex(
        "^(?<chance>\\d+)% chance to transmute an enemy into (?<term>.+) on hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslate(string source, string route, string family, out string translated)
    {
        if (TryTranslateScopedExact(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateTemplated(source, route, family, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateScopedExact(string source, string route, string family, out string translated)
    {
        var direct = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, WorldModsDictionaryFile);
        if (!string.IsNullOrEmpty(direct) && !string.Equals(direct, source, StringComparison.Ordinal))
        {
            translated = direct!;
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (string.Equals(stripped, source, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var strippedTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAscii(stripped, WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(strippedTranslation) || string.Equals(strippedTranslation, stripped, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.Restore(strippedTranslation, spans);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    private static bool TryTranslateTemplated(string source, string route, string family, out string translated)
    {
        if (TryTranslateMasterworkTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            PaintedPattern,
            "Painted: This item is painted with a scene from the life of the ancient {0}:",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "subject", TranslatePaintedSubject) },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            JewelEncrustedPattern,
            "Jewel-Encrusted: This item is much more valuable than usual and grants the wearer {0} reputation with water barons.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "amount") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            ImprovedMutationPattern,
            "Grants you {0} at level {1}. If you already have {0}, its level is increased by {1}.",
            (match, spans) => new[]
            {
                GetTranslatedCapture(match, spans, "mutation"),
                GetTranslatedCapture(match, spans, "level"),
            },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            DisguiseAppearancePattern,
            "Disguise: This item makes its wearer appear to be {0}.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "appearance", TranslateDisguiseAppearance) },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            DisguiseReputationPattern,
            "+{0} reputation with {1}",
            (match, spans) => new[]
            {
                GetTranslatedCapture(match, spans, "amount"),
                GetTranslatedCapture(match, spans, "faction"),
            },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            FactionSlayerPattern,
            "{0}% chance to behead {1} on hit.",
            (match, spans) => new[]
            {
                GetTranslatedCapture(match, spans, "chance"),
                GetTranslatedCapture(match, spans, "target"),
            },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            BlinkEscapePattern,
            "Whenever you're about to take avoidable damage, there's {0}% chance you blink away instead.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            FatecallerPattern,
            "{0}% of the time, the Fates have their way.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            GlassArmorPattern,
            "Reflects {0}% damage back at your attackers, rounded up.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            GlazedPattern,
            "{0}% chance to dismember on hit.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            RefractivePattern,
            "Refractive: This item has {0}% chance to refract light-based attacks.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            LiquidCooledPattern,
            "Liquid-cooled: This weapon's rate of fire is increased by {0}, but it requires {1} to function. When fired, there's a one in {2} chance that 1 dram is consumed.",
            (match, spans) => new[]
            {
                GetTranslatedCapture(match, spans, "bonus"),
                GetTranslatedCapture(match, spans, "liquid", TranslateLiquidRequirement),
                GetTranslatedCapture(match, spans, "chance"),
            },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            TransmuteSmallPattern,
            "Small chance to transmute an enemy into {0} on hit.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "term") },
            out translated))
        {
            return true;
        }

        return TryTranslateTemplate(
            source,
            route,
            family,
            TransmutePercentPattern,
            "{0}% chance to transmute an enemy into {1} on hit.",
            (match, spans) => new[]
            {
                GetTranslatedCapture(match, spans, "chance"),
                GetTranslatedCapture(match, spans, "term"),
            },
            out translated);
    }

    private static bool TryTranslateMasterworkTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = MasterworkPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate("Masterwork: This weapon scores critical hits {0} of the time instead of 5%.");
        if (string.Equals(template, "Masterwork: This weapon scores critical hits {0} of the time instead of 5%.", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var visible = template.Replace("{0}", match.Groups["value"].Value);
        translated = ColorAwareTranslationComposer.Restore(visible, spans);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool TryTranslateTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string templateKey,
        Func<Match, IReadOnlyList<ColorSpan>, string[]> buildArguments,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = ScopedDictionaryLookup.TranslateExactOrLowerAscii(templateKey, WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var visible = string.Format(CultureInfo.InvariantCulture, template, buildArguments(match, spans));
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, stripped.Length, visible.Length);
            translated = ColorAwareTranslationComposer.Restore(visible, boundarySpans);
        }
        else
        {
            translated = visible;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static string GetTranslatedCapture(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string groupName,
        Func<string, string>? translate = null)
    {
        var group = match.Groups[groupName];
        var value = translate is null ? TranslateTemplateCapture(group.Value) : translate(group.Value);
        return spans.Count > 0
            ? ColorAwareTranslationComposer.RestoreCapture(value, spans, group)
            : value;
    }

    private static string TranslatePaintedSubject(string source)
    {
        var translated = TranslateTemplateCapture(source);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return translated;
        }

        var separator = source.IndexOf(' ');
        if (separator <= 0)
        {
            return source;
        }

        var head = source.Substring(0, separator);
        var headTranslation = TranslateTemplateCapture(head);
        if (string.Equals(headTranslation, head, StringComparison.Ordinal))
        {
            return source;
        }

#pragma warning disable CA1845
        return headTranslation + source.Substring(separator);
#pragma warning restore CA1845
    }

    private static string TranslateDisguiseAppearance(string source)
    {
        var strippedArticle = StringHelpers.StripLeadingEnglishArticle(source);
        var translated = TranslateTemplateCapture(strippedArticle);
        return string.Equals(translated, strippedArticle, StringComparison.Ordinal)
            ? strippedArticle
            : translated;
    }

    private static string TranslateLiquidRequirement(string source)
    {
        var normalized = WhitespacePattern.Replace(source.Trim(), " ");
        const string purePrefix = "pure ";
        if (normalized.StartsWith(purePrefix, StringComparison.Ordinal))
        {
            var liquid = normalized.Substring(purePrefix.Length);
            return "純粋な" + TranslateTemplateCapture(liquid);
        }

        return TranslateTemplateCapture(normalized);
    }

    private static string TranslateTemplateCapture(string source)
    {
        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, WorldModsDictionaryFile);
        if (scoped is not null)
        {
            return scoped;
        }

        return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
            ? translated
            : source;
    }

    private static readonly Regex WhitespacePattern = new Regex(
        "\\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
