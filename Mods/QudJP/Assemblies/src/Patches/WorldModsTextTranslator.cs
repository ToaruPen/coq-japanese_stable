using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using QudJP.Patches;

namespace QudJP;

internal static class WorldModsTextTranslator
{
    private const string WorldModsDictionaryFile = "world-mods.ja.json";
    private const string AddsRepAppendDescriptionContext = "XRL.World.Parts.AddsRep.AppendDescription";
    private const string MeleeWeaponShortDescriptionContext = "XRL.World.Parts.MeleeWeapon.GetShortDescription";
    private const string MasterworkDescriptionContext = "XRL.World.Parts.ModMasterwork.GetShortDescription";
    private const string BeamsplitterDescriptionContext = "XRL.World.Parts.ModBeamsplitter.GetShortDescription";
    private const string JewelEncrustedDescriptionContext = "XRL.World.Parts.ModJewelEncrusted.GetShortDescription";
    private const string HeartstopperDescriptionContext = "XRL.World.Parts.ModHeartstopper.GetShortDescription";
    private const string LiquidCooledDescriptionContext = "XRL.World.Parts.ModLiquidCooled.GetShortDescription";
    private const string MicroserratedDescriptionContext = "XRL.World.Parts.ModMicroserrated.GetShortDescription";
    private const string NanonDescriptionContext = "XRL.World.Parts.ModNanon.GetShortDescription";
    private const string SerratedDescriptionContext = "XRL.World.Parts.ModSerrated.GetShortDescription";
    private const string SmartDescriptionContext = "XRL.World.Parts.ModSmart.GetShortDescription";
    private const string TransmuteOnHitDescriptionContext = "XRL.World.Parts.ModTransmuteOnHit.GetShortDescription";

    private static readonly Dictionary<string, string> ExactDescriptionContexts = new(StringComparer.Ordinal)
    {
        ["Projectiles fired with this weapon receive bonus penetration based on the wielder's Strength."] = "XRL.World.Parts.MissileWeapon.GetShortDescription",
        ["{{rules|Shields only grant their AV when you successfully block an attack.}}"] = "XRL.World.Parts.Shield.GetShortDescription",
        ["\n{{rules|Shields only grant their AV when you successfully block an attack.}}"] = "XRL.World.Parts.Shield.GetShortDescription",
        ["Shields only grant their AV when you successfully block an attack."] = MeleeWeaponShortDescriptionContext,
        ["When powered, discharges a clockwork beetle friend on hit. Drains cell power quickly."] = "XRL.World.Parts.ModBeetlehost.GetShortDescription",
        ["You are suddenly elsewhere!"] = "XRL.World.Parts.ModDisplacer.GetShortDescription",
        ["Your blood frenzies last twice as long."] = "XRL.World.Parts.ModImprovedBerserk.GetShortDescription",
        ["Your chance to block with shields is increased by 25%."] = "XRL.World.Parts.ModImprovedBlock.GetShortDescription",
        ["Your chance to Bludgeon is doubled."] = "XRL.World.Parts.ModImprovedBludgeon.GetShortDescription",
        ["Your Hobble cooldown is reduced by 5 rounds."] = "XRL.World.Parts.ModImprovedHobble.GetShortDescription",
    };

    private static readonly Dictionary<string, string> PrefixDescriptionContexts = new(StringComparer.Ordinal)
    {
        ["Airfoil"] = "XRL.World.Parts.ModAirfoil.GetShortDescription",
        ["Biomech"] = "XRL.World.Parts.ModBiomech.GetShortDescription",
        ["Camo"] = "XRL.World.Parts.ModCamo.GetShortDescription",
        ["Cybrid"] = "XRL.World.Parts.ModCybrid.GetShortDescription",
        ["Defib"] = "XRL.World.Parts.ModDefib.GetShortDescription",
        ["Desecrated"] = "XRL.World.Parts.ModDesecrated.GetShortDescription",
        ["Disguise"] = "XRL.World.Parts.ModDisguise.GetDescription",
        ["Drum-loaded"] = "XRL.World.Parts.ModDrumLoaded.GetShortDescription",
        ["Electromagnetic shielding"] = "XRL.World.Parts.ModHardened.GetShortDescription",
        ["Extradimensional"] = "XRL.World.Parts.ModExtradimensional.GetShortDescription",
        ["Fitted with filters"] = "XRL.World.Parts.ModFilters.GetShortDescription",
        ["Fitted with suspensors"] = "XRL.World.Parts.ModSuspensor.GetShortDescription",
        ["Flare-compensating"] = "XRL.World.Parts.ModFlareCompensating.GetShortDescription",
        ["Flexiweaved"] = "XRL.World.Parts.ModFlexiweaved.GetShortDescription",
        ["Gearbox"] = "XRL.World.Parts.ModGearbox.GetShortDescription",
        ["Gesticulating"] = "XRL.World.Parts.ModGesticulating.GetShortDescription",
        ["Gigantic"] = "XRL.World.Parts.ModGigantic.GetShortDescription",
        ["HUD"] = "XRL.World.Parts.ModHUD.GetShortDescription",
        ["Heartstopper"] = HeartstopperDescriptionContext,
        ["High-capacity"] = "XRL.World.Parts.ModHighCapacity.GetShortDescription",
        ["Homing"] = "XRL.World.Parts.ModHeatSeeking.GetShortDescription",
        ["Hypervelocity"] = "XRL.World.Parts.ModHypervelocity.GetShortDescription",
        ["Illuminated"] = "XRL.World.Parts.ModIlluminated.GetShortDescription",
        ["Induction"] = "XRL.World.Parts.ModInduction.GetShortDescription",
        ["Jacked"] = "XRL.World.Parts.ModJacked.GetShortDescription",
        ["Keen"] = "XRL.World.Parts.ModKeen.GetShortDescription",
        ["Lacquered"] = "XRL.World.Parts.ModLacquered.GetShortDescription",
        ["Lanterned"] = "XRL.World.Parts.ModLanterned.GetShortDescription",
        ["Magnetized"] = "XRL.World.Parts.ModMagnetized.GetShortDescription",
        ["Massively overloaded"] = "XRL.World.Parts.ModMassivelyOverloaded.GetShortDescription",
        ["Mercurial"] = "XRL.World.Parts.ModMercurial.GetShortDescription",
        ["Metallized"] = "XRL.World.Parts.ModMetallized.GetShortDescription",
        ["Metered"] = "XRL.World.Parts.ModMetered.GetShortDescription",
        ["Microserrated"] = MicroserratedDescriptionContext,
        ["Mighty"] = "XRL.World.Parts.ModMighty.GetShortDescription",
        ["Morphogenetic"] = "XRL.World.Parts.ModMorphogenetic.GetShortDescription",
        ["Nanochelated"] = "XRL.World.Parts.ModNanochelated.GetShortDescription",
        ["Nanon"] = NanonDescriptionContext,
        ["Nav"] = "XRL.World.Parts.ModNav.GetShortDescription",
        ["Nulling"] = "XRL.World.Parts.ModNulling.GetShortDescription",
        ["Overbuilt"] = "XRL.World.Parts.ModOverbuilt.GetShortDescription",
        ["Overloaded"] = "XRL.World.Parts.ModOverloaded.GetShortDescription",
        ["Phase-Harmonic"] = "XRL.World.Parts.ModPhaseHarmonic.GetShortDescription",
        ["Phase-conjugate"] = "XRL.World.Parts.ModPhaseConjugate.GetShortDescription",
        ["Piping"] = "XRL.World.Parts.ModPiping.GetShortDescription",
        ["Polarized"] = "XRL.World.Parts.ModPolarized.GetShortDescription",
        ["Psionic"] = "XRL.World.Parts.ModPsionic.GetShortDescription",
        ["Quantum reverb"] = "XRL.World.Parts.ModQuantumReverb.GetShortDescription",
        ["Radio-powered"] = "XRL.World.Parts.ModRadioPowered.GetShortDescription",
        ["Reinforced"] = "XRL.World.Parts.ModReinforced.GetShortDescription",
        ["Scaled"] = "XRL.World.Parts.ModScaled.GetShortDescription",
        ["Scoped"] = "XRL.World.Parts.ModScoped.GetShortDescription",
        ["Serene visage"] = "XRL.World.Parts.ModSereneVisage.GetShortDescription",
        ["Serrated"] = SerratedDescriptionContext,
        ["Sharp"] = "XRL.World.Parts.ModSharp.GetShortDescription",
        ["Sirocco"] = "XRL.World.Parts.ModSirocco.GetShortDescription",
        ["Six-Fingered"] = "XRL.World.Parts.ModSixFingered.GetShortDescription",
        ["Slender"] = "XRL.World.Parts.ModSlender.GetShortDescription",
        ["Smart"] = SmartDescriptionContext,
        ["Snail-Encrusted"] = "XRL.World.Parts.ModSnailEncrusted.GetShortDescription",
        ["Spiked"] = "XRL.World.Parts.ModSpiked.GetShortDescription",
        ["Spring-loaded"] = "XRL.World.Parts.ModSpringLoaded.GetShortDescription",
        ["Sturdy"] = "XRL.World.Parts.ModSturdy.GetShortDescription",
        ["Terrifying visage"] = "XRL.World.Parts.ModTerrifyingVisage.GetShortDescription",
        ["Two-faced"] = "XRL.World.Parts.ModTwoFaced.GetShortDescription",
        ["Urban camo"] = "XRL.World.Parts.ModUrbanCamo.GetShortDescription",
        ["Visored"] = "XRL.World.Parts.ModVisored.GetShortDescription",
        ["Weightless"] = "XRL.World.Parts.ModWeightless.GetShortDescription",
        ["Willowy"] = "XRL.World.Parts.ModWillowy.GetShortDescription",
        ["Wired"] = "XRL.World.Parts.ModWired.GetShortDescription",
        ["Wooly"] = "XRL.World.Parts.ModWooly.GetShortDescription",
    };

    private static readonly Regex JapaneseCharacterPattern = new Regex(
        "[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MasterworkPattern = new Regex(
        "^Masterwork: This weapon scores critical hits (?<value>.+) of the time instead of 5%\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PaintedPattern = new Regex(
        "^Painted: This item is painted with a scene from the life of the ancient (?<subject>.+):$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EngravedPattern = new Regex(
        "^Engraved: This item is engraved with a scene from the life of the ancient (?<subject>.+):$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DataDiskItemModificationPattern = new Regex(
        "^Adds item modification: (?<description>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GiganticDescriptionPattern = new Regex(
        "^Gigantic: (?<body>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AntiGravityPattern = new Regex(
        "^Anti-gravity: When powered, this item's weight is reduced by (?<percent>\\d+)% plus (?<force>\\d+) (?<unit>lb|lbs)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CoProcessorPattern = new Regex(
        "^[Cc]o-[Pp]rocessor: When powered, this item grants (?<bonus>bonus|[+-]\\d+) (?<attribute>.+?) and provides (?:(?<units>\\d+) units of )?compute power to the local lattice\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ActivePartStatusSuffixPattern = new Regex(
        "^(?<body>.+) \\((?<status>unpowered|nonfunctional|EMP|unfueled|fuel contaminated|switched off|warming up)\\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ComputeNodePattern = new Regex(
        "^When (?<conditions>.+), provides (?<amount>\\d+) (?<unit>unit|units) of compute power to the local lattice\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ActiveLightSourcePattern = new Regex(
        "^When (?<conditions>.+), provides (?<kind>light|night vision)(?: in radius (?<radius>\\d+))?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PartsGasChancePattern = new Regex(
        "^(?<chance>\\d+)% chance per turn to repel gases (?<location>near|within (?:(?<article>a|an)-square radius|(?<radius>\\d+) squares) of) (?<scope>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PartsGasAlwaysPattern = new Regex(
        "^Repels gases (?<location>near|within (?:(?<article>a|an)-square radius|(?<radius>\\d+) squares) of) (?<scope>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CounterweightedPattern = new Regex(
        "^Counterweighted: Adds (?<bonus>a bonus|[+-]\\d+) to hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisplacerPattern = new Regex(
        "^Displacer: When powered, this weapon randomly teleports its target (?<distance>\\d+-\\d+) tiles away on a successful hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BeamsplitterPattern = new Regex(
        "^Fitted with beamsplitter: This weapon has (?:(?:a|an) )?(?<count>\\d+)-way spread with each shot at -1 penetration roll\\.$",
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
        "^(?<amount>[+-]\\d+) reputation with (?<faction>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FactionSlayerPattern = new Regex(
        "^(?<chance>\\d+)% chance to behead (?<target>.+) on hit\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StatBonusCapPattern = new Regex(
        "^(?<stat>Strength|Agility|Toughness|Intelligence|Willpower|Ego) Bonus Cap: (?<cap>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WeaponClassPattern = new Regex(
        "^Weapon Class: (?<weaponClass>.+)$",
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
    private static readonly Regex MicroserratedChancePattern = new Regex(
        "^Microserrated: This weapon has (?<chance>\\d+)% chance to dismember opponents\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SerratedChancePattern = new Regex(
        "^Serrated: This weapon has (?<chance>\\d+)% chance to dismember opponents\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NanonChancePattern = new Regex(
        "^Nanon: (?<chance>\\d+)% chance to dismember on penetration\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RefractivePattern = new Regex(
        "^Refractive: This item has (?:a|an) (?<chance>\\d+)% chance to refract light-based attacks\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LiquidCooledPattern = new Regex(
        "^Liquid-cooled: This weapon's rate of fire is increased by (?<bonus>\\d+), but it requires (?<liquid>.+?) to function\\. When fired, there's a one in (?<chance>\\d+) chance that 1 dram is consumed\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LiquidCooledStaticPattern = new Regex(
        "^Liquid-cooled: This weapon's rate of fire is increased, but it requires (?<liquid>.+?) to function\\. When fired, there's a one in (?<chance>\\d+) chance that 1 dram is consumed\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HeartstopperChancePattern = new Regex(
        "^Heartstopper: When powered, this weapon has (?<chance>\\d+)% chance to put opponents into cardiac arrest(?: if they fail a difficulty (?<difficulty>\\d+) (?<attribute>.+?) save)?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SmartTrackingScopePattern = new Regex(
        "^Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives (?<bonus>a bonus|[+-]\\d+) to hit a target aimed at\\.$",
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

        if (TryTranslateDisguiseReputationTemplate(source, route, family, out translated))
        {
            translated = CloseDanglingRulesSpan(translated);
            return true;
        }

        if (TryTranslateScopedExact(source, route, family, out translated))
        {
            translated = CloseDanglingRulesSpan(translated);
            return true;
        }

        if (TryTranslateActivePartStatusSuffix(source, route, family, out translated))
        {
            translated = CloseDanglingRulesSpan(translated);
            return true;
        }

        if (TryTranslateTemplated(source, route, family, out translated))
        {
            translated = CloseDanglingRulesSpan(translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateScopedExact(string source, string route, string family, out string translated)
    {
        var direct = TranslateWorldModsOwnedExactOrLowerAscii(source);
        if (direct is null)
        {
            direct = TranslateWorldModsExactOrLowerAscii(source);
        }

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

        var strippedTranslation = TranslateWorldModsOwnedExactOrLowerAscii(stripped);
        if (strippedTranslation is null)
        {
            strippedTranslation = TranslateWorldModsExactOrLowerAscii(stripped);
        }

        if (string.IsNullOrEmpty(strippedTranslation) || string.Equals(strippedTranslation, stripped, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.Restore(strippedTranslation, spans);
        if (translated.StartsWith("{{rules|", StringComparison.Ordinal)
            && !translated.EndsWith("}}", StringComparison.Ordinal))
        {
            translated += "}}";
        }

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

        if (TryTranslateStrengthBonusCapTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateWeaponClassTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateOffhandAttackChanceTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateGiganticDescriptionTemplate(source, route, family, out translated))
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

        if (TryTranslateComputeNodeTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateActiveLightSourceTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslatePartsGasTemplate(source, route, family, out translated))
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

        if (TryTranslateBeamsplitterTemplate(
            source,
            route,
            family,
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
            (match, spans) => new[] { GetTranslatedHistoricSceneSubject(match, spans, "subject") },
            out translated))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            EngravedPattern,
            "Engraved: This item is engraved with a scene from the life of the ancient {0}:",
            (match, spans) => new[] { GetTranslatedHistoricSceneSubject(match, spans, "subject") },
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
            out translated,
            JewelEncrustedDescriptionContext))
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
            MicroserratedChancePattern,
            "Microserrated: This weapon has {0}% chance to dismember opponents.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated,
            MicroserratedDescriptionContext))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            SerratedChancePattern,
            "Serrated: This weapon has {0}% chance to dismember opponents.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated,
            SerratedDescriptionContext))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            NanonChancePattern,
            "Nanon: {0}% chance to dismember on penetration.",
            (match, spans) => new[] { GetTranslatedCapture(match, spans, "chance") },
            out translated,
            NanonDescriptionContext))
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
                GetTranslatedLiquidRequirement(match, spans, "liquid"),
                GetTranslatedCapture(match, spans, "chance"),
            },
            out translated,
            LiquidCooledDescriptionContext))
        {
            return true;
        }

        if (TryTranslateTemplate(
            source,
            route,
            family,
            LiquidCooledStaticPattern,
            "Liquid-cooled: This weapon's rate of fire is increased, but it requires {0} to function. When fired, there's a one in {1} chance that 1 dram is consumed.",
            (match, spans) => new[]
            {
                GetTranslatedLiquidRequirement(match, spans, "liquid"),
                GetTranslatedCapture(match, spans, "chance"),
            },
            out translated,
            LiquidCooledDescriptionContext))
        {
            return true;
        }

        if (TryTranslateHeartstopperTemplate(source, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateSmartTrackingScopeTemplate(source, route, family, out translated))
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
            out translated,
            TransmuteOnHitDescriptionContext))
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
            out translated,
            TransmuteOnHitDescriptionContext);
    }

    private static bool TryTranslateHeartstopperTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = HeartstopperChancePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var hasSave = match.Groups["difficulty"].Success;
        var templateKey = hasSave
            ? "Heartstopper: When powered, this weapon has {0}% chance to put opponents into cardiac arrest if they fail a difficulty {1} {2} save."
            : "Heartstopper: When powered, this weapon has {0}% chance to put opponents into cardiac arrest.";
        var template = TranslateWorldModsExactOrLowerAscii(
            templateKey,
            HeartstopperDescriptionContext);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var args = hasSave
            ? new[]
            {
                GetTranslatedCapture(match, spans, "chance"),
                GetTranslatedCapture(match, spans, "difficulty"),
                GetTranslatedCapture(match, spans, "attribute"),
            }
            : new[] { GetTranslatedCapture(match, spans, "chance") };

        return TryFormatTemplate(source, stripped, spans, route, family, match, template!, args, out translated);
    }

    private static bool TryTranslateSmartTrackingScopeTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = SmartTrackingScopePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var hasNumericBonus = !string.Equals(match.Groups["bonus"].Value, "a bonus", StringComparison.Ordinal);
        var templateKey = hasNumericBonus
            ? "Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives {0} to hit a target aimed at."
            : "Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives a bonus to hit a target aimed at.";
        var template = TranslateWorldModsExactOrLowerAscii(
            templateKey,
            SmartDescriptionContext);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var args = hasNumericBonus
            ? new[] { GetTranslatedCapture(match, spans, "bonus") }
            : Array.Empty<string>();

        return TryFormatTemplate(source, stripped, spans, route, family, match, template!, args, out translated);
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

        const string templateKey = "Masterwork: This weapon scores critical hits {0} of the time instead of 5%.";
        var template = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            templateKey,
            MasterworkDescriptionContext,
            WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        return TryFormatTemplate(
            source,
            stripped,
            spans,
            route,
            family,
            match,
            template!,
            new[] { GetTranslatedCapture(match, contentSpans, "value") },
            out translated);
    }

    private static bool TryTranslateBeamsplitterTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = BeamsplitterPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        const string templateKey = "Fitted with beamsplitter: This weapon has a {0}-way spread with each shot at -1 penetration roll.";
        var template = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            templateKey,
            BeamsplitterDescriptionContext,
            WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            template = ScopedDictionaryLookup.TranslateExactOrLowerAscii(templateKey, WorldModsDictionaryFile);
        }

        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        return TryFormatTemplate(
            source,
            stripped,
            spans,
            route,
            family,
            match,
            template!,
            new[] { match.Groups["count"].Success ? GetTranslatedCapture(match, contentSpans, "count") : "3" },
            out translated);
    }

    private static bool TryTranslateOffhandAttackChanceTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = OffhandAttackChancePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        const string templateKey = "Offhand Attack Chance: {0}%";
        var template = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            templateKey,
            MeleeWeaponShortDescriptionContext,
            WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            template = ScopedDictionaryLookup.TranslateExactOrLowerAscii(templateKey, WorldModsDictionaryFile);
        }

        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        return TryFormatTemplate(
            source,
            stripped,
            spans,
            route,
            family,
            match,
            template!,
            new[] { GetTranslatedCapture(match, contentSpans, "chance") },
            out translated);
    }

    private static bool TryTranslateStrengthBonusCapTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = StatBonusCapPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var bonusCap = BuildBonusCapLabel();
        if (string.IsNullOrEmpty(bonusCap))
        {
            translated = source;
            return false;
        }

        var template = "{0}" + bonusCap + " {1}";
        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        return TryFormatTemplate(
            source,
            stripped,
            spans,
            route,
            family,
            match,
            template!,
            new[]
            {
                GetTranslatedCapture(match, contentSpans, "stat"),
                GetTranslatedCapture(match, contentSpans, "cap", TranslateBonusCapValue),
            },
            out translated);
    }

    private static bool TryTranslateWeaponClassTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = WeaponClassPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var weaponClass = match.Groups["weaponClass"].Value;
        var translatedWeaponClass = TranslateTemplateCapture(weaponClass);
        if (string.Equals(translatedWeaponClass, weaponClass, StringComparison.Ordinal)
            && !JapaneseCharacterPattern.IsMatch(weaponClass))
        {
            translated = source;
            return false;
        }

        var template = BuildWeaponClassTemplate();
        if (string.IsNullOrEmpty(template))
        {
            translated = source;
            return false;
        }

        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        return TryFormatTemplate(
            source,
            stripped,
            spans,
            route,
            family,
            match,
            template!,
            new[] { contentSpans.Count > 0 ? ColorAwareTranslationComposer.RestoreCapture(translatedWeaponClass, contentSpans, match.Groups["weaponClass"]) : translatedWeaponClass },
            out translated);
    }

    private static string? BuildBonusCapLabel()
    {
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii("Bonus Cap:", out var bonusCap))
        {
            return null;
        }

        return bonusCap;
    }

    private static string? BuildWeaponClassTemplate()
    {
        return StringHelpers.TryGetTranslationExactOrLowerAscii("Weapon Class:", out var label)
            ? label + " {0}"
            : null;
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

        var visible = string.Format(CultureInfo.InvariantCulture, template!, args);
        translated = CloseDanglingRulesSpan(RestoreTemplateBoundarySpans(stripped, spans, match, visible));

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool TryTranslateActivePartStatusSuffix(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ActivePartStatusSuffixPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var body = match.Groups["body"].Value;
        var status = TranslateActivePartStatus(match.Groups["status"].Value);
        var translatedBody = TranslateActivePartStatusBody(body, route, family);

        var visible = translatedBody + "（" + status + "）";
        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        translated = RestoreTemplateBoundarySpans(stripped, contentSpans, match, visible);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static string TranslateActivePartStatusBody(string source, string route, string family)
    {
        if (TryTranslate(source, route, family, out var translated))
        {
            return translated;
        }

        translated = Patches.DescriptionTextTranslator.TranslateShortDescription(source, route);
        return string.Equals(source, translated, StringComparison.Ordinal) ? source : translated;
    }

    private static bool TryTranslateComputeNodeTemplate(string source, string route, string family, out string translated)
    {
        return TryTranslateTemplate(
            source,
            route,
            family,
            ComputeNodePattern,
            "When {0}, provides {1} {2} of compute power to the local lattice.",
            (match, spans) => new[]
            {
                GetTranslatedCapture(match, spans, "conditions", TranslateActivePartConditionList),
                GetTranslatedCapture(match, spans, "amount"),
                GetTranslatedCapture(match, spans, "unit", TranslateComputeUnit),
            },
            out translated);
    }

    private static bool TryTranslateActiveLightSourceTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ActiveLightSourcePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = match.Groups["radius"].Success
            ? "When {0}, provides {1} in radius {2}."
            : "When {0}, provides {1}.";
        var template = ScopedDictionaryLookup.TranslateExactOrLowerAscii(templateKey, WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var args = match.Groups["radius"].Success
            ? new[]
            {
                GetTranslatedCapture(match, spans, "conditions", TranslateActivePartConditionList),
                GetTranslatedCapture(match, spans, "kind", TranslateLightKind),
                GetTranslatedCapture(match, spans, "radius"),
            }
            : new[]
            {
                GetTranslatedCapture(match, spans, "conditions", TranslateActivePartConditionList),
                GetTranslatedCapture(match, spans, "kind", TranslateLightKind),
            };

        var formatted = TryFormatTemplate(source, stripped, spans, route, family, match, template!, args, out translated);
        translated = RestoreRulesOpeningIfNeeded(source, translated, formatted);
        return formatted;
    }

    private static bool TryTranslatePartsGasTemplate(string source, string route, string family, out string translated)
    {
        if (TryTranslateTemplate(
            source,
            route,
            family,
            PartsGasChancePattern,
            "{0}% chance per turn to repel gases {1} {2}.",
            (match, spans) => new[]
            {
                GetTranslatedCapture(match, spans, "chance"),
                TranslateGasLocation(match),
                GetTranslatedCapture(match, spans, "scope", TranslateActivePartScope),
            },
            out translated))
        {
            return true;
        }

        return TryTranslateTemplate(
            source,
            route,
            family,
            PartsGasAlwaysPattern,
            "Repels gases {0} {1}.",
            (match, spans) => new[]
            {
                TranslateGasLocation(match),
                GetTranslatedCapture(match, spans, "scope", TranslateActivePartScope),
            },
            out translated);
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

        var formatted = TryFormatTemplate(source, stripped, spans, route, family, match, template!, args, out translated);
        translated = RestoreRulesOpeningIfNeeded(source, translated, formatted);
        return formatted;
    }

    private static bool TryTranslateGiganticDescriptionTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = GiganticDescriptionPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var body = match.Groups["body"].Value;
        var rawSentences = body.Split(new[] { ". " }, StringSplitOptions.None);
        if (rawSentences.Length == 0)
        {
            translated = source;
            return false;
        }

        var translatedSentences = new List<string>();
        for (var index = 0; index < rawSentences.Length; index++)
        {
            if (!TryTranslateGiganticSentence(rawSentences[index], out var translatedSentence))
            {
                translated = source;
                return false;
            }

            translatedSentences.Add(translatedSentence);
        }

        var visible = "巨大: " + string.Join(string.Empty, translatedSentences);
        translated = RestoreTemplateBoundarySpans(stripped, spans, match, visible);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length);

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool TryTranslateGiganticSentence(string source, out string translated)
    {
        if (!TryExtractGiganticSubject(source, out var subject, out var clauseText))
        {
            translated = source;
            return false;
        }

        var clauses = SplitGiganticClauses(clauseText);
        if (clauses.Count == 0)
        {
            translated = source;
            return false;
        }

        var translatedClauses = new List<string>();
        for (var index = 0; index < clauses.Count; index++)
        {
            if (!TryTranslateGiganticClause(clauses[index], out var translatedClause))
            {
                translated = source;
                return false;
            }

            translatedClauses.Add(translatedClause);
        }

        translated = subject + JoinGiganticClauses(translatedClauses) + "。";
        return true;
    }

    private static string JoinGiganticClauses(List<string> clauses)
    {
        for (var index = 0; index < clauses.Count - 1; index++)
        {
            if (clauses[index].EndsWith("になる", StringComparison.Ordinal))
            {
                clauses[index] = clauses[index].Substring(0, clauses[index].Length - "になる".Length) + "になり";
            }
            else if (clauses[index].EndsWith("くなる", StringComparison.Ordinal))
            {
                clauses[index] = clauses[index].Substring(0, clauses[index].Length - "くなる".Length) + "くなり";
            }
        }

        return string.Join("、", clauses);
    }

    private static bool TryExtractGiganticSubject(string source, out string subject, out string clauseText)
    {
        foreach (var candidate in GiganticSubjectPrefixes)
        {
            if (!source.StartsWith(candidate.Source, StringComparison.Ordinal))
            {
                continue;
            }

            subject = candidate.Translated;
            clauseText = source.Substring(candidate.Source.Length);
            return true;
        }

        subject = string.Empty;
        clauseText = string.Empty;
        return false;
    }

    private static List<string> SplitGiganticClauses(string source)
    {
        var normalized = source
            .Replace(", and ", "|")
            .Replace(", ", "|")
            .Replace(" and ", "|");
        var rawParts = normalized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>();
        for (var index = 0; index < rawParts.Length; index++)
        {
            var part = rawParts[index].Trim();
            if (part.Length > 0)
            {
                parts.Add(part);
            }
        }

        return parts;
    }

    private static bool TryTranslateGiganticClause(string source, out string translated)
    {
        translated = source switch
        {
            "holds twice as much liquid" or "hold twice as much liquid" => "液体容量が2倍になる",
            "has twice the energy capacity" or "have twice the energy capacity" => "エネルギー容量が2倍になる",
            "has twice as large a radius of effect" or "have twice as large a radius of effect" => "効果半径が2倍になる",
            "contains double the tonic dosage" or "contain double the tonic dosage" => "トニック用量が2倍になる",
            "is much more valuable" or "are much more valuable" => "価値が大幅に高くなる",
            "is much heavier than usual" or "are much heavier than usual" => "通常より大幅に重くなる",
            "has +3 damage" or "have +3 damage" => "ダメージ+3",
            "is twice as effective when you Slam with it" or "are twice as effective when you Slam with them" => "スラム時の効果が2倍になる",
            "cleaves for -3 AV" or "cleave for -3 AV" => "装甲切断でAV-3を与える",
            "can only be equipped by gigantic creatures" => "巨大な生物しか装備できない",
            "must be wielded two-handed by non-gigantic creatures" => "巨大でない生物が扱うには二手持ちが必要",
            "must be wielded four-handed by non-gigantic creatures" => "巨大でない生物が扱うには四手持ちが必要",
            "digs twice as fast" or "dig twice as fast" => "掘削速度が2倍になる",
            _ => source,
        };

        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateDisguiseReputationTemplate(string source, string route, string family, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = DisguiseReputationPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        const string templateKey = "+{0} reputation with {1}";
        var template = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            templateKey,
            AddsRepAppendDescriptionContext,
            WorldModsDictionaryFile);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var amount = match.Groups["amount"].Value;
        var amountArgument = int.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAmount)
            ? (object)parsedAmount
            : amount;
        var faction = GetTranslatedWholeCaptureBoundary(match, spans, "faction", TranslateArticleStrippedTemplateCapture);
        var factionOpening = Regex.Match(faction, "^\\{\\{[^|{}]+\\|");
        if (factionOpening.Success && source.StartsWith(factionOpening.Value, StringComparison.Ordinal))
        {
            faction = TranslateArticleStrippedTemplateCapture(match.Groups["faction"].Value);
        }

        var args = new[]
        {
            amountArgument,
            faction,
        };

        var formatted = TryFormatTemplate(source, stripped, spans, route, family, match, template!, args, out translated);
        translated = RestoreRulesOpeningIfNeeded(source, translated, formatted);
        return formatted;
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
        out string translated,
        string? templateContext = null)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = templateContext is null
            ? TranslateWorldModsExactOrLowerAscii(templateKey)
            : TranslateWorldModsExactOrLowerAscii(templateKey, templateContext);
        if (string.IsNullOrEmpty(template) || string.Equals(template, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        return TryFormatTemplate(source, stripped, spans, route, family, match, template!, buildArguments(match, contentSpans), out translated);
    }

    private static string? TranslateWorldModsOwnedExactOrLowerAscii(string source)
    {
        if (!ExactDescriptionContexts.TryGetValue(source, out var context)
            && !TryGetPrefixDescriptionContext(source, out context))
        {
            return null;
        }

        return TranslateWorldModsExactOrLowerAscii(source, context);
    }

    private static bool TryGetPrefixDescriptionContext(string source, out string context)
    {
        context = string.Empty;
        var separator = source.IndexOf(':');
        if (separator <= 0)
        {
            return false;
        }

        var prefix = source.Substring(0, separator);
        return PrefixDescriptionContexts.TryGetValue(prefix, out context!);
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
        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        translated = RestoreTemplateBoundarySpans(stripped, contentSpans, match, visible);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length);

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static string? TranslateWorldModsExactOrLowerAscii(
        string source,
        string? context = null)
    {
        var direct = context is null
            ? ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, WorldModsDictionaryFile)
            : ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(source, context, WorldModsDictionaryFile);
        if (!string.IsNullOrEmpty(direct) && !string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        return null;
    }

    private static string RestoreRulesOpeningIfNeeded(string source, string translated, bool translatedSuccessfully)
    {
        return translatedSuccessfully
            && source.StartsWith("{{rules|", StringComparison.Ordinal)
            && !translated.StartsWith("{{rules|", StringComparison.Ordinal)
            ? "{{rules|" + translated + "}}"
            : translated;
    }

    private static string RestoreTemplateBoundarySpans(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Match match,
        string visible)
    {
        if (spans.Count > 0)
        {
            var boundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(spans, match, stripped.Length, visible.Length);
            return ColorAwareTranslationComposer.Restore(visible, boundarySpans);
        }

        return visible;
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

    private static string GetTranslatedLiquidRequirement(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string groupName)
    {
        var group = match.Groups[groupName];
        var source = spans.Count > 0
            ? ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group)
            : group.Value;
        return TranslateLiquidRequirement(source);
    }

    private static string GetTranslatedWholeCaptureBoundary(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string groupName,
        Func<string, string> translate)
    {
        var group = match.Groups[groupName];
        var value = translate(group.Value);
        if (spans.Count == 0)
        {
            return value;
        }

        var restored = RestoreTrueWholeBoundary(value, spans, group.Index, group.Length);
        return HasUnbalancedQudSpan(restored) ? value : restored;
    }

    private static string GetTranslatedHistoricSceneSubject(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var translatedVisible = TranslatePaintedSubject(group.Value);
        if (spans.Count == 0)
        {
            return translatedVisible;
        }

        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group);
        if (!ColorAwareTranslationComposer.HasColorMarkup(restored))
        {
            return translatedVisible;
        }

        var translated = TranslatePaintedSubjectPreservingMarkup(restored, group.Value, translatedVisible);
        return HasUnbalancedQudSpan(translated) ? translatedVisible : translated;
    }

    private static string TranslatePaintedSubjectPreservingMarkup(string restored, string visible, string translatedVisible)
    {
        if (string.Equals(translatedVisible, visible, StringComparison.Ordinal))
        {
            return restored;
        }

        var separator = visible.IndexOf(' ');
        if (separator > 0)
        {
            var head = visible.Substring(0, separator);
            var headTranslation = TranslateTemplateCapture(head);
            if (!string.Equals(headTranslation, head, StringComparison.Ordinal)
                && string.Equals(translatedVisible, headTranslation + visible.Substring(separator), StringComparison.Ordinal)
                && restored.StartsWith(head, StringComparison.Ordinal))
            {
                return headTranslation + restored.Substring(head.Length);
            }
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(restored, TranslatePaintedSubject);
    }

    private static string RestoreTrueWholeBoundary(
        string value,
        IReadOnlyList<ColorSpan> spans,
        int startIndex,
        int length)
    {
        if (length == 0)
        {
            return value;
        }

        var endIndex = startIndex + length;
        var stack = new List<ColorSpan>();
        var restoreSpans = new List<ColorSpan>();
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index < startIndex || span.Index > endIndex)
            {
                continue;
            }

            if (ColorCodePreserver.IsClosingBoundaryToken(span.Token) && stack.Count > 0)
            {
                var opening = stack[stack.Count - 1];
                if (BoundaryTokensMatch(opening.Token, span.Token))
                {
                    stack.RemoveAt(stack.Count - 1);
                    if (opening.Index == startIndex && span.Index == endIndex)
                    {
                        restoreSpans.Add(new ColorSpan(0, opening.Token));
                        restoreSpans.Add(new ColorSpan(value.Length, span.Token));
                    }

                    continue;
                }
            }

            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                stack.Add(span);
            }
        }

        return ColorAwareTranslationComposer.Restore(value, restoreSpans);
    }

    private static bool HasUnbalancedQudSpan(string value)
    {
        var openingCount = CountMatches(value, "\\{\\{[^|{}]+\\|");
        var closingCount = CountMatches(value, "\\}\\}");
        return openingCount != closingCount;
    }

    private static int CountMatches(string value, string pattern)
    {
        var count = 0;
        var match = Regex.Match(value, pattern);
        while (match.Success)
        {
            count++;
            match = match.NextMatch();
        }

        return count;
    }

    private static string CloseDanglingRulesSpan(string value)
    {
        return value.StartsWith("{{rules|", StringComparison.Ordinal) && !value.EndsWith("}}", StringComparison.Ordinal)
            ? value + "}}"
            : value;
    }

    private static bool BoundaryTokensMatch(string openingToken, string closingToken)
    {
        if (openingToken.StartsWith("{{", StringComparison.Ordinal)
            && openingToken.EndsWith("|", StringComparison.Ordinal))
        {
            return string.Equals(closingToken, "}}", StringComparison.Ordinal);
        }

        if (openingToken.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(closingToken, "</color>", StringComparison.OrdinalIgnoreCase);
        }

        if (openingToken.Length == 2
            && closingToken.Length == 2
            && (openingToken[0] == '&' || openingToken[0] == '^'))
        {
            return openingToken[0] == closingToken[0];
        }

        return false;
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
        return TranslateArticleStrippedTemplateCapture(source);
    }

    private static string TranslateArticleStrippedTemplateCapture(string source)
    {
        var originalTranslated = TranslateTemplateCapture(source);
        if (!string.Equals(originalTranslated, source, StringComparison.Ordinal))
        {
            return originalTranslated;
        }

        var strippedArticle = StringHelpers.StripLeadingEnglishArticle(source);
        if (string.Equals(strippedArticle, source, StringComparison.Ordinal))
        {
            return source;
        }

        var translated = TranslateTemplateCapture(strippedArticle);
        if (!string.Equals(translated, strippedArticle, StringComparison.Ordinal))
        {
            return translated;
        }

        return JapaneseCharacterPattern.IsMatch(strippedArticle) ? strippedArticle : source;
    }

    private static string TranslateLiquidRequirement(string source)
    {
        var normalized = WhitespacePattern.Replace(source.Trim(), " ");
        const string purePrefix = "pure ";
        if (normalized.StartsWith(purePrefix, StringComparison.Ordinal))
        {
            var liquid = normalized.Substring(purePrefix.Length);
            return "純粋な" + TranslateLiquidCapture(liquid);
        }

        return TranslateLiquidCapture(normalized);
    }

    private static string TranslateLiquidCapture(string source)
    {
        var translated = LiquidVolumeFragmentTranslator.TranslateLiquidPhrasePreservingColors(source);
        return translated is not null ? translated : source;
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

    private static string TranslateComputeUnit(string source)
    {
        return string.Equals(source, "unit", StringComparison.Ordinal)
            || string.Equals(source, "units", StringComparison.Ordinal)
            ? "ユニット"
            : TranslateTemplateCapture(source);
    }

    private static string TranslateLightKind(string source)
    {
        return source switch
        {
            "light" => "光",
            "night vision" => "暗視",
            _ => TranslateTemplateCapture(source),
        };
    }

    private static string TranslateActivePartStatus(string source)
    {
        return source switch
        {
            "EMP" => "EMP",
            "unpowered" => "無電力",
            "unfueled" => "燃料切れ",
            "fuel contaminated" => "燃料汚染",
            "switched off" => "オフ",
            "warming up" => "ウォームアップ中",
            "nonfunctional" => "機能停止",
            _ => TranslateTemplateCapture(source),
        };
    }

    private static string TranslateActivePartConditionList(string source)
    {
        var normalized = WhitespacePattern.Replace(source.Trim(), " ");
        normalized = normalized
            .Replace("、", "|")
            .Replace("，", "|")
            .Replace(" and ", "|")
            .Replace(" or ", "|")
            .Replace("または", "|")
            .Replace("と", "|");

        var rawParts = normalized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>();
        for (var index = 0; index < rawParts.Length; index++)
        {
            var part = rawParts[index].Trim();
            if (part.Length == 0)
            {
                continue;
            }

            var translatedPart = part switch
            {
                "equipped" => "装備",
                "implanted" => "埋め込み",
                "powered" => "通電",
                "in use" => "使用",
                "operational" => "稼働",
                _ => TranslateTemplateCapture(part),
            };
            parts.Add(NormalizeActivePartCondition(translatedPart));
        }

        if (parts.Count == 0)
        {
            return TranslateTemplateCapture(source);
        }

        if (parts.Count == 1)
        {
            return parts[0] + "中";
        }

        return string.Join("・", parts) + "中";
    }

    private static string NormalizeActivePartCondition(string source)
    {
        return source switch
        {
            "装備中" => "装備",
            "埋め込み中" => "埋め込み",
            "給電" => "通電",
            "給電中" => "通電",
            "通電中" => "通電",
            "使用中" => "使用",
            "稼働中" => "稼働",
            _ => source,
        };
    }

    private static string TranslateGasLocation(Match match)
    {
        var location = match.Groups["location"].Value;
        if (string.Equals(location, "near", StringComparison.Ordinal))
        {
            return "近くの";
        }

        if (match.Groups["article"].Success)
        {
            return "半径1マス以内の";
        }

        if (match.Groups["radius"].Success)
        {
            return "半径" + match.Groups["radius"].Value + "マス以内の";
        }

        return TranslateTemplateCapture(location);
    }

    private static string TranslateActivePartScope(string source)
    {
        var normalized = WhitespacePattern.Replace(source.Trim(), " ");
        if (normalized.StartsWith("its ", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(4);
        }

        normalized = normalized
            .Replace("、", "|")
            .Replace("，", "|")
            .Replace(" or ", "|")
            .Replace("または", "|");

        var rawParts = normalized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>();
        for (var index = 0; index < rawParts.Length; index++)
        {
            var part = rawParts[index].Trim();
            if (part.Length == 0)
            {
                continue;
            }

            parts.Add(part switch
            {
                "user" => "使用者",
                "wielder" => "使用者",
                "wearer" => "着用者",
                "carrier" => "携帯者",
                "contents" => "内容物",
                "vicinity" => "周辺",
                "immediate vicinity" => "直近",
                "nearby vicinity" => "近傍",
                "operating area" => "作動範囲",
                _ => TranslateTemplateCapture(part),
            });
        }

        if (parts.Count == 0)
        {
            return TranslateTemplateCapture(source);
        }

        return string.Join("または", parts);
    }

    private static string TranslateTemplateCapture(string source)
    {
        if (TryTranslateStatContractLabel(source, out var statLabel))
        {
            return statLabel;
        }

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

    private static string TranslateBonusCapValue(string source)
    {
        return string.Equals(source.Trim(), "no limit", StringComparison.OrdinalIgnoreCase)
            ? "なし"
            : TranslateTemplateCapture(source);
    }

    private static bool TryTranslateStatContractLabel(string source, out string translated)
    {
        translated = source.Trim() switch
        {
            "Strength" or "STR" => "筋力",
            "Agility" or "AGI" => "敏捷",
            "Toughness" or "TOU" => "頑健",
            "Intelligence" or "INT" => "知力",
            "Willpower" or "WIL" => "意志力",
            "Ego" or "EGO" => "自我",
            _ => source,
        };
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static readonly Regex WhitespacePattern = new Regex(
        "\\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly (string Source, string Translated)[] GiganticSubjectPrefixes =
    {
        ("This item ", "この品は"),
        ("These items ", "これらの品は"),
        ("This weapon ", "この武器は"),
        ("These weapons ", "これらの武器は"),
        ("It ", "これは"),
        ("They ", "これらは"),
    };
}
