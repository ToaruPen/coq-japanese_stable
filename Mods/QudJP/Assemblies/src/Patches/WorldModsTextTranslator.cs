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
    private static readonly Regex DataDiskItemModificationPattern = new Regex(
        "^Adds item modification: (?<description>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AntiGravityPattern = new Regex(
        "^Anti-gravity: When powered, this item's weight is reduced by (?<percent>\\d+)% plus (?<force>\\d+) (?<unit>lb|lbs)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CoProcessorPattern = new Regex(
        "^[Cc]o-[Pp]rocessor: When powered, this item grants (?<bonus>bonus|[+-]\\d+) (?<attribute>.+?) and provides (?:(?<units>\\d+) units of )?compute power to the local lattice\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CounterweightedPattern = new Regex(
        "^Counterweighted: Adds (?<bonus>a bonus|[+-]\\d+) to hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisplacerPattern = new Regex(
        "^Displacer: When powered, this weapon randomly teleports its target (?<distance>\\d+-\\d+) tiles away on a successful hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ElectrifiedPattern = new Regex(
        "^Electrified: When powered, this weapon deals(?: an additional (?<damage>\\d+(?:-\\d+)?)| additional)?\\s+electrical damage on hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FlamingPattern = new Regex(
        "^Flaming: When powered, this weapon deals(?: an additional (?<damage>\\d+(?:-\\d+)?)| additional)?\\s+heat damage on hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FreezingPattern = new Regex(
        "^Freezing: When powered, this weapon deals(?: an additional (?<damage>\\d+(?:-\\d+)?)| additional)?\\s+cold damage on hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FeatheredPattern = new Regex(
        "^Feathered: This item grants the wearer (?<amount>[+-]?\\d+) reputation with birds\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex JewelEncrustedPattern = new Regex(
        "^Jewel-Encrusted: This item is much more valuable than usual and grants the wearer (?<amount>[+-]\\d+) reputation with water barons\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ScaledPattern = new Regex(
        "^Scaled: This item grants the wearer (?<amount>[+-]?\\d+) reputation with unshelled reptiles\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SnailEncrustedPattern = new Regex(
        "^Snail-Encrusted: This item is crawling with tiny snails and grants the wearer (?<amount>[+-]?\\d+) reputation with mollusks\\.$",
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
    private static readonly Regex OffhandAttackChancePattern = new Regex(
        "^Offhand Attack Chance: (?<chance>\\d+)%$",
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
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var withoutMarker))
        {
            if (TryTranslate(withoutMarker, route, family, out var innerTranslated))
            {
                translated = MessageFrameTranslator.MarkDirectTranslation(innerTranslated);
                return true;
            }

            translated = source;
            return false;
        }

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
        if (TryTranslateTemplate(
            source,
            route,
            family,
            DataDiskItemModificationPattern,
            "Adds item modification: {0}",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "description", TranslateNestedWorldModDescription) },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            OffhandAttackChancePattern,
            "Offhand Attack Chance: {0}%",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            AntiGravityPattern,
            "Anti-gravity: When powered, this item's weight is reduced by {0}% plus {1} {2}.",
            (match, spans) => new[]
            {
                GetTranslatedCapture(match, spans, "percent"),
                GetTranslatedCapture(match, spans, "force"),
                GetTranslatedCapture(match, spans, "unit"),
            },
            out translated))
        {
            return true;
        }

        if (TryTranslateCoProcessorTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateCounterweightedTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            DisplacerPattern,
            "Displacer: When powered, this weapon randomly teleports its target {0} tiles away on a successful hit.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "distance") },
            out translated))
        {
            return true;
        }

        if (TryTranslateElementalDamageTemplate(
            source,
            route,
            family,
            ElectrifiedPattern,
            "Electrified: When powered, this weapon deals additional electrical damage on hit.",
            "Electrified: When powered, this weapon deals an additional {0} electrical damage on hit.",
            out translated))
        {
            return true;
        }

        if (TryTranslateElementalDamageTemplate(
            source,
            route,
            family,
            FlamingPattern,
            "Flaming: When powered, this weapon deals additional heat damage on hit.",
            "Flaming: When powered, this weapon deals an additional {0} heat damage on hit.",
            out translated))
        {
            return true;
        }

        if (TryTranslateElementalDamageTemplate(
            source,
            route,
            family,
            FreezingPattern,
            "Freezing: When powered, this weapon deals additional cold damage on hit.",
            "Freezing: When powered, this weapon deals an additional {0} cold damage on hit.",
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            FeatheredPattern,
            "Feathered: This item grants the wearer {0} reputation with birds.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "amount") },
            out translated))
        {
            return true;
        }

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
            ScaledPattern,
            "Scaled: This item grants the wearer {0} reputation with unshelled reptiles.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "amount") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            SnailEncrustedPattern,
            "Snail-Encrusted: This item is crawling with tiny snails and grants the wearer {0} reputation with mollusks.",
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

    private static bool TryTranslateCoProcessorTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = CoProcessorPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = match.Groups["units"].Success
            ? "Co-Processor: When powered, this item grants {0} {1} and provides {2} units of compute power to the local lattice."
            : "Co-Processor: When powered, this item grants {0} {1} and provides compute power to the local lattice.";
        var template = ScopedDictionaryLookup.TranslateExactOrLowerAscii(templateKey, WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var args = match.Groups["units"].Success
            ? new[]
            {
                GetTranslatedCapture(match, spans, "bonus", TranslateCoProcessorBonus),
                GetTranslatedCapture(match, spans, "attribute"),
                GetTranslatedCapture(match, spans, "units"),
            }
            : new[]
            {
                GetTranslatedCapture(match, spans, "bonus", TranslateCoProcessorBonus),
                GetTranslatedCapture(match, spans, "attribute"),
            };

        return TryFormatTemplate(source, stripped, spans, route, family, match, template!, args, out translated);
    }

    private static bool TryTranslateCounterweightedTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = CounterweightedPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = string.Equals(match.Groups["bonus"].Value, "a bonus", StringComparison.Ordinal)
            ? "Counterweighted: Adds a bonus to hit."
            : "Counterweighted: Adds {0} to hit.";
        var template = ScopedDictionaryLookup.TranslateExactOrLowerAscii(templateKey, WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var args = string.Equals(match.Groups["bonus"].Value, "a bonus", StringComparison.Ordinal)
            ? Array.Empty<string>()
            : new[] { GetTranslatedCapture(match, spans, "bonus") };

        return TryFormatTemplate(source, stripped, spans, route, family, match, template!, args, out translated);
    }

    private static bool TryTranslateElementalDamageTemplate(
        string source,
        string route,
        string family,
        Regex pattern,
        string withoutRangeTemplateKey,
        string withRangeTemplateKey,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var hasDamageRange = match.Groups["damage"].Success;
        var templateKey = hasDamageRange ? withRangeTemplateKey : withoutRangeTemplateKey;
        var template = ScopedDictionaryLookup.TranslateExactOrLowerAscii(templateKey, WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var args = hasDamageRange
            ? new[] { GetTranslatedCapture(match, spans, "damage") }
            : Array.Empty<string>();

        return TryFormatTemplate(source, stripped, spans, route, family, match, template!, args, out translated);
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

        return TryFormatTemplate(source, stripped, spans, route, family, match, template!, buildArguments(match, spans), out translated);
    }

    private static bool TryFormatTemplate(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        Match match,
        string template,
        object[] args,
        out string translated)
    {
        var visible = string.Format(CultureInfo.InvariantCulture, template, args);
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

        return headTranslation + source.Substring(separator);
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

    private static string TranslateNestedWorldModDescription(string source)
    {
        return TryTranslate(source, "WorldModsNestedTemplate", "Description.WorldMods.Nested", out var translated)
            ? translated
            : TranslateTemplateCapture(source);
    }

    private static string TranslateCoProcessorBonus(string source)
    {
        return string.Equals(source, "bonus", StringComparison.Ordinal)
            ? "ボーナス"
            : TranslateTemplateCapture(source);
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
