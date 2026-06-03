using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using QudJP;

namespace QudJP.Patches;

internal static class GetDisplayNameRouteTranslator
{
    private const string DisplayNameAdjectiveContext = "GetDisplayName.Adjective";
    private const string DisplayNameStateTemplateContext = "GetDisplayName.StateTemplate";
    private const string DisplayNameTitleContext = "GetDisplayName.Title";
    private const string DisplayNameStateTemplateDictionaryFile = "Scoped/ui-displayname-state-templates.ja.json";

    private static readonly string[] DisplayNameLegacyAliasDictionaryFiles =
    {
        "displayname-legacy-aliases.json",
    };
    private static readonly string[] DisplayNameDictionaryFiles =
    {
        "ui-displayname-adjectives.ja.json",
        "ui-displayname-atomic.ja.json",
    };
    private static readonly string[] LiquidPhraseDictionaryFiles =
    {
        "ui-liquid-adjectives.ja.json",
        "ui-liquids.ja.json",
        "ui-displayname-adjectives.ja.json",
    };
    private static readonly string[] LiquidColorCodes =
    {
        "r", "R", "g", "G", "b", "B", "c", "C", "y", "Y", "w", "W", "K",
    };
    private static readonly object LocalizedBlueprintDisplayNameMarkupLock = new();
    private static readonly HashSet<string> SpacedDisplayNameModifierKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "counterweighted",
            "displacer",
            "electrified",
            "flaming",
            "freezing",
            "masterwork",
            "scoped",
        };
    private static readonly Regex BracketedDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+\\[(?<state>.+)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WholeBracketedDisplayNameStatePattern =
        new Regex("^\\[(?<state>.+)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MarkedUpBracketedStateSuffixPattern =
        new Regex(
            "^(?<base>.+?)\\s+(?<open>\\{\\{[^{}|]+\\|\\[)(?<state>.+)(?<close>\\]\\}\\})(?:\\s+(?<parenOpen>\\{\\{[^{}|]+\\|\\()(?<parenState>.+)(?<parenClose>\\)\\}\\}))?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MarkedUpBracketedStateSuffixSequencePattern =
        new Regex(
            "^(?<base>.+?)(?<suffixes>(?:\\s+\\{\\{[^{}|]+\\|\\[[^\\]\\r\\n]+\\]\\}\\})+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MarkedUpBracketedStateSuffixTokenPattern =
        new Regex(
            "\\G(?<space>\\s+)(?<open>\\{\\{[^{}|]+\\|\\[)(?<state>[^\\]\\r\\n]+)(?<close>\\]\\}\\})",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ArmorStatsDisplayNameSuffixPattern =
        new Regex(@"^(?<base>.+?) (?<stats>\x04-?\d+ \t-?\d+)(?: \[(?<state>.+)\])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ArmorStatsDisplayNameSuffixSequencePattern =
        new Regex(
            @"^(?<base>.+?) (?<stats>\x04-?\d+ \t-?\d+)(?<suffixes>(?: (?:\[[^\]\r\n]+\]|<[^>\r\n]+>))+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CompactWeaponStatsDisplayNameSuffixPattern =
        new Regex(
            @"^(?<base>.+?) (?<stats>(?:\x1a[^\[\r\n]*(?: \x03[^\[\r\n]+)?|\x03[^\[\r\n]+))(?: \[(?<state>.+)\])?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CompactWeaponStatsDisplayNameSuffixSequencePattern =
        new Regex(
            @"^(?<base>.+?) (?<stats>(?:\x1a[^\[\r\n<]*(?: \x03[^\[\r\n<]+)?|\x03[^\[\r\n<]+))(?<suffixes>(?: (?:\[[^\]\r\n]+\]|<[^>\r\n]+>))+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CompactWeaponStatsOnlySuffixSequencePattern =
        new Regex(
            @"^ (?<stats>(?:\x1a[^\[\r\n<]*(?: \x03[^\[\r\n<]+)?|\x03[^\[\r\n<]+))(?<suffixes>(?: (?:\[[^\]\r\n]+\]|<[^>\r\n]+>))+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlainDisplayNameSuffixSequencePattern =
        new Regex(
            @"^(?<base>.+?)(?<suffixes>(?: (?:\[[^\]\r\n]+\]|<[^>\r\n]+>)){2,})$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NestedLoadedCellBracketedSuffixPattern =
        new Regex(
            @"^(?<prefix>.+?) (?<bracket>\[(?<cellBase>.+?) (?<liquidBracket>\[(?<liquidState>\d+ drams? of .+?)\]) (?<collectBracket>\[(?<collectState>auto-collecting)\]) (?<cellCode><[^>\r\n]+>)\])(?<tail>(?: <[^>\r\n]+>)*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CompactWeaponStatsOnlySuffixPattern =
        new Regex(
            @"^ (?<stats>(?:\x1a[^\[\r\n<]*(?: \x03[^\[\r\n]+)?|\x03[^\[\r\n]+))(?: \[(?<state>.+)\])?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WithClauseCompactWeaponStatsDisplayNameSuffixSequencePattern =
        new Regex(
            @"^(?<base>.+?) with (?<clause>.+?) (?<stats>(?:\x1a[^\[\r\n<]*(?: \x03[^\[\r\n<]+)?|\x03[^\[\r\n<]+))(?<suffixes>(?: (?:\[[^\]\r\n]+\]|<[^>\r\n]+>))+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WithClauseCompactWeaponStatsDisplayNameSuffixPattern =
        new Regex(
            @"^(?<base>.+?) with (?<clause>.+?) (?<stats>(?:\x1a[^\[\r\n<]*(?: \x03[^\[\r\n<]+)?|\x03[^\[\r\n<]+))(?: \[(?<state>.+)\])?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisplayNameTrailingSuffixPattern =
        new Regex(@"\G (?:(?<bracket>\[(?<state>[^\]\r\n]+)\])|(?<angle><[^>\r\n]+>))", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ParenthesizedDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+\\((?<state>[A-Za-z][A-Za-z\\s-]*)\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex QuantityDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+x(?<count>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EditionTitleSuffixPattern =
        new Regex("^(?<number>\\d+)(?:st|nd|rd|th) Edition$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GeneratedTitleSuffixPattern =
        new Regex("^(?<base>.+?)(?<separator>, | and |(?<![A-Za-z])and\\s+)(?<suffix>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WorshipperTitleSuffixPattern =
        new Regex("^worshipper of (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FriendToTitleSuffixPattern =
        new Regex("^friend to (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MemberOfTitleSuffixPattern =
        new Regex("^member of (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PariahToPeopleTitleSuffixPattern =
        new Regex("^pariah to (?<possessive>their|his|her|its|your) people$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CompoundStainedModifierPattern =
        new Regex("^(?<left>.+?)-and-(?<right>.+?)-stained$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SingleStainedModifierPattern =
        new Regex("^(?<liquid>.+?)-stained$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MkTierDisplayNameSuffixPattern =
        new Regex(
            "^(?<base>.+?)\\s+mk\\s+(?<tier>[IVXLC]+)(?:\\s+<(?<code>[^>]+)>)?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MinerGeneratedRoleDisplayNameSuffixPattern =
        new Regex(
            "^(?<base>.+?)\\s+(?<role>miner|bomber)\\s+mk\\s+(?<tier>[IVXLC]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AngleCodeDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?)\\s+(?<angle><(?<code>[^>]+)>)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TransparentAngleCodeWrapperPattern =
        new Regex("<\\{\\{\\|(?<inner>(?:\\{\\{[^{}]*\\}\\}|[^{}])*)\\}\\}>", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WithClauseDisplayNamePattern =
        new Regex("^(?<base>.+?) with (?<clause>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PairOfDisplayNamePattern =
        new Regex("^pair of (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisguiseClauseDisplayNameSuffixPattern =
        new Regex("^(?<base>.+?) and (?:(?<target>.+?) )?disguise$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingMarkupWrappedModifierPattern =
        new Regex(
            "^(?<modifier>\\{\\{[^|}]+\\|[A-Za-z][A-Za-z\\s\\-']*\\}\\}|\\[\\{\\{[^|}]+\\|[A-Za-z][A-Za-z\\s\\-']*\\}\\}\\])\\s+(?<rest>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingZeroWidthMarkupPrefixPattern =
        new Regex(
            "^(?<prefix>(?:\\{\\{[^|}]+\\|\\}\\}\\s*)+)(?<rest>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LocalizedPrefixAsciiModifierPattern =
        new Regex(
            "^(?<prefix>.*[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}])(?<modifier>[A-Za-z][A-Za-z\\-']*) (?<rest>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DisplayNameModifierLevelSuffixPattern =
        new Regex("^(?<modifier>[A-Za-z][A-Za-z\\-']*)\\((?<level>\\d+)\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ParenthesizedColoredChargeStatusPattern =
        new Regex(
            "(?<prefix>\\()(?<status>\\{\\{[^|}]+\\|[^{}]*\\}\\})(?<suffix>\\))",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PrepositionalStateTemplatePattern =
        new Regex(
            "^(?<template>sitting on|lying on|enclosed in|engulfed by|auto-collecting|stuck in|grabbed by) (?<target>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex QuantifiedLiquidStatePattern =
        new Regex(
            "^(?<amount>\\d+|\\{\\{[^{}|]+\\|\\d+\\}\\})\\s+drams? of (?<liquid>.+?)(?:,\\s+(?<state>.+))?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LiquidStatePattern =
        new Regex(
            "^(?<liquid>.+?),\\s+(?<state>[A-Za-z][A-Za-z\\s-]*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TimedDisplayNameStatePattern =
        new Regex("^(?<count>\\d+) sec$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CookingServingsDisplayNameStatePattern =
        new Regex("^(?<count>\\d+) cooking servings?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnergyCellsDisplayNameStatePattern =
        new Regex("^(?<count>\\d+) cells?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LoadedEnergyCellDisplayNameStatePattern =
        new Regex("^(?<cell>.+?) (?<chargeWithParens>\\((?<charge>.+?)\\)) (?<code><[^>]+>)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ChapterDisplayNameStatePattern =
        new Regex("^(?<owner>.+?) chapter$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FlywheelDisplayNameStatePattern =
        new Regex("^flywheel: (?<status>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GeneratedCanvasTentPattern =
        new Regex(
            "^(?<body>[A-Za-z][A-Za-z\\s-]*?) tent$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GeneratedRandomStatuePattern =
        new Regex(
            "^(?<material>[A-Za-z][A-Za-z\\s-]*?) statue of (?<subject>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GeneratedEnglishPrefixDisplayNamePattern =
        new Regex(
            "^(?<prefix>advertisement for|ruined mural of|mural of|shrine to|clone of|hologram of|phylactery of|villagers of|Cult of) (?<target>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex EvilTwinSpacedPrefixDisplayNamePattern =
        new Regex(
            "^(?<prefix>Evil|Refracted) (?<target>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex EvilTwinAntiPrefixDisplayNamePattern =
        new Regex(
            "^anti-(?<target>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex CyberneticsSchemasoftDisplayNamePattern =
        new Regex(
            "^Schemasoft \\[(?<category>.+?), (?<tier>Low Tier|Mid Tier|High Tier)\\]$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CyberneticsSchemasoftWrappedDisplayNamePattern =
        new Regex(
            "^\\{\\{(?<outer>[^|}]+)\\|Schemasoft \\[\\{\\{(?<inner>[^|}]+)\\|(?<category>.+?), (?<tier>Low Tier|Mid Tier|High Tier)\\}\\}\\]\\}\\}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CyberneticsSkillsoftWrappedDisplayNamePattern =
        new Regex(
            "^\\{\\{(?<outer>[^|}]+)\\|(?<kind>Skillsoft(?: Plus)?) \\[\\{\\{(?<inner>[^|}]+)\\|(?<skill>.+)\\}\\}\\]\\}\\}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Dictionary<string, string> CyclopeanPrismDisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{K|amaranthine}} prism"] = "{{K|アマランス色}}のプリズム",
            ["{{K|amara{{y|n}}thine}} prism"] = "{{K|アマラ}}{{y|ン}}{{K|ス色}}のプリズム",
            ["{{K|amar{{y|a{{Y|n}}t}}hine}} prism"] = "{{K|アマラ}}{{y|ン}}{{Y|ス}}{{K|色}}のプリズム",
            ["{{K|am{{y|ar{{Y|a{{R|n}}t}}hi}}ne}} prism"] = "{{K|アマ}}{{y|ラ}}{{Y|ン}}{{R|ス}}{{K|色}}のプリズム",
            ["{{y|am{{Y|a{{y|r{{r|a{{R|n}}t}}h}}i}}ne}} prism"] = "{{y|アマ}}{{Y|ラ}}{{y|ン}}{{r|ス}}{{R|色}}のプリズム",
            ["{{r|a{{R|m{{Y|a{{y|r{{r|a{{R|n}}t}}h}}i}}n}}e}} prism"] = "{{r|ア}}{{R|マ}}{{Y|ラ}}{{y|ン}}{{r|ス}}{{R|色}}のプリズム",
        };
    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EnglishWordPattern =
        new Regex("[A-Za-z]{2,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const string GeneratedCanvasTentComponentContext = "GetDisplayName.GeneratedCanvasTent.Component";
    private const string GeneratedRandomStatueComponentContext = "GetDisplayName.GeneratedRandomStatue.Component";
    private static Dictionary<string, string>? localizedBlueprintDisplayNameMarkup;
    private static string? localizedBlueprintDisplayNameMarkupRoot;

    internal static bool IsAlreadyLocalizedDisplayNameText(string source)
    {
        if (StringHelpers.ContainsOrdinalIgnoreCase(source, " with "))
        {
            return false;
        }

        return IsAlreadyLocalizedBracketedDisplayName(source)
            || IsAlreadyLocalizedParenthesizedDisplayName(source);
    }

    internal static bool IsAlreadyLocalizedDisplayNameStateText(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return JapaneseCharacterPattern.IsMatch(source)
            && !EnglishWordPattern.IsMatch(source);
    }

    internal static string TranslatePreservingColors(string? source, string? context = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var route = ObservabilityHelpers.ExtractPrimaryContext(context);
        if (route is null)
        {
            Trace.TraceWarning("QudJP: GetDisplayNameRouteTranslator could not extract a primary context; falling back to route name.");
            route = nameof(GetDisplayNameRouteTranslator);
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            return source!;
        }

        if (TryTranslateCyclopeanPrismDisplayName(source!, route, out var cyclopeanPrismTranslation))
        {
            return cyclopeanPrismTranslation;
        }

        if (TryTranslateRedundantStainedWholeWrapperWithSuffix(source!, route, out var redundantStainedWrapperTranslation))
        {
            return redundantStainedWrapperTranslation;
        }

        if (TryTranslateSourceWithClausePrefixPreservingSuffix(source!, route, out var sourceWithClauseTranslation))
        {
            return sourceWithClauseTranslation;
        }

        using var logScope = Translator.PushLogContext(context);

        if (TryTranslateLiquidPrepositionDisplayName(source!, route, out var coloredLiquidPrepositionTranslation))
        {
            return coloredLiquidPrepositionTranslation;
        }

        if (TryHandleParenthesizedColoredChargeStatusFallback(source!, route, out var chargeStatusFallback))
        {
            return chargeStatusFallback;
        }

        if (TryTranslateParenthesizedColoredChargeStatus(source!, route, out var chargeStatusTranslation))
        {
            source = chargeStatusTranslation;
        }

        if (TryTranslateCyberneticsSchemasoftWrappedDisplayName(source!, route, out var schemasoftTranslation))
        {
            return schemasoftTranslation;
        }

        if (TryTranslateCyberneticsSkillsoftWrappedDisplayName(source!, route, out var skillsoftTranslation))
        {
            return skillsoftTranslation;
        }

        if (TryTranslateGeneratedRandomStatueName(source!, route, out var randomStatueEarlyTranslation))
        {
            return randomStatueEarlyTranslation;
        }

        if (TryTranslateGeneratedEnglishPrefixDisplayName(source!, route, out var prefixTranslation))
        {
            return prefixTranslation;
        }

        if (TryTranslateEvilTwinSpacedPrefixDisplayName(source!, route, out var evilTwinPrefixTranslation))
        {
            return evilTwinPrefixTranslation;
        }

        if (TryTranslateMarkedUpBracketedStateSuffixSequence(source!, route, out var markedUpBracketedStateSequenceTranslation))
        {
            return markedUpBracketedStateSequenceTranslation;
        }

        if (TryTranslateMarkedUpBracketedStateSuffix(source!, route, out var markedUpBracketedStateTranslation))
        {
            return markedUpBracketedStateTranslation;
        }

        if (ColorAwareTranslationComposer.HasColorMarkup(source!)
            && TryTranslateGeneratedTitleSuffix(source!, route, out var markedUpTitleSuffixTranslation))
        {
            return markedUpTitleSuffixTranslation;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.Length == 0)
        {
            return source!;
        }

        if (TryTranslateWholeBracketedDisplayNameState(stripped, spans, route, out var wholeBracketedStateTranslation))
        {
            return wholeBracketedStateTranslation;
        }

        if (TryTranslateNestedLoadedCellBracketedSuffix(stripped, spans, route, out var nestedLoadedCellTranslation))
        {
            return nestedLoadedCellTranslation;
        }

        using var __ = Translator.PushMissingKeyLoggingSuppression(
            IsAlreadyLocalizedDisplayNameText(stripped)
            || IsAlreadyLocalizedDisplayNameStateText(stripped)
            || IsAlreadyLocalizedBracketLabel(stripped)
            ||
            UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(stripped, context));

        if (IsAlreadyLocalizedDisplayNameText(stripped))
        {
            return source!;
        }

        if (TryTranslateExactDisplayNameLookup(stripped, route, out var earlyExactTranslation))
        {
            return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                earlyExactTranslation,
                spans,
                stripped.Length);
        }

        if (TryTranslateExactBaseCompactWeaponStatsDisplayNameSuffix(stripped, spans, route, out var earlyCompactWeaponStatsTranslation))
        {
            return earlyCompactWeaponStatsTranslation;
        }

        if (TryTranslateLeadingZeroWidthMarkupPrefix(source!, route, out var zeroWidthPrefixTranslation))
        {
            return zeroWidthPrefixTranslation;
        }

        if ((source![0] == '{' || source[0] == '[')
            && TryTranslateLeadingModifierChain(source, route, out var modifierChainTranslation))
        {
            return RestoreLeadingChainStainedModifierColor(source, modifierChainTranslation);
        }

        if (StringHelpers.ContainsOrdinal(source, "{{")
            && TryTranslateLeadingModifierChain(source, route, out var visibleModifierChainTranslation))
        {
            return RestoreLeadingChainStainedModifierColor(source, visibleModifierChainTranslation);
        }

        if (TryTranslateLeadingMarkupWrappedModifier(source!, route, out var markupLeadingTranslation))
        {
            return markupLeadingTranslation;
        }

        if (TryTranslateArmorStatsDisplayNameSuffix(stripped, spans, route, out var armorStatsTranslation))
        {
            return armorStatsTranslation;
        }

        if (TryTranslateArmorStatsDisplayNameSuffixSequence(stripped, spans, route, out var armorStatsSuffixSequenceTranslation))
        {
            return armorStatsSuffixSequenceTranslation;
        }

        if (TryTranslateWithClauseCompactWeaponStatsDisplayNameSuffixSequence(stripped, spans, route, out var withClauseCompactWeaponStatsSuffixSequenceTranslation))
        {
            return withClauseCompactWeaponStatsSuffixSequenceTranslation;
        }

        if (TryTranslateWithClauseCompactWeaponStatsDisplayNameSuffix(stripped, spans, route, out var withClauseCompactWeaponStatsTranslation))
        {
            return withClauseCompactWeaponStatsTranslation;
        }

        if (TryTranslateCompactWeaponStatsDisplayNameSuffix(stripped, spans, route, out var compactWeaponStatsTranslation))
        {
            return compactWeaponStatsTranslation;
        }

        if (TryTranslateCompactWeaponStatsDisplayNameSuffixSequence(stripped, spans, route, out var compactWeaponStatsSuffixSequenceTranslation))
        {
            return compactWeaponStatsSuffixSequenceTranslation;
        }

        if (TryTranslateWithClauseDisplayNamePrefixPreservingSuffix(stripped, spans, route, out var withClausePrefixTranslation))
        {
            return withClausePrefixTranslation;
        }

        if (TryTranslateDisguiseClauseDisplayNameSuffix(stripped, spans, route, out var disguiseClauseTranslation))
        {
            return disguiseClauseTranslation;
        }

        if (TryTranslateAngleCodeDisplayNameSuffix(stripped, spans, route, out var angleCodeSuffixTranslation))
        {
            return angleCodeSuffixTranslation;
        }

        if (TryTranslatePlainDisplayNameSuffixSequence(stripped, spans, route, out var plainSuffixSequenceTranslation))
        {
            return plainSuffixSequenceTranslation;
        }

        if (TryTranslateBracketedDisplayNameSuffix(stripped, spans, route, out var bracketedSuffixTranslation))
        {
            return bracketedSuffixTranslation;
        }

        if (TryTranslateWithClauseDisplayName(stripped, spans, 0, route, out var withClauseTranslation))
        {
            return withClauseTranslation;
        }

        if (TryTranslateGeneratedTitleSuffix(stripped, spans, route, out var titleSuffixTranslation))
        {
            return titleSuffixTranslation;
        }

        if (TryTranslateDisplayNameRouteText(stripped, spans, route, out var translated))
        {
            return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translated,
                spans,
                stripped.Length);
        }

        return source!;
    }

    private static bool TryTranslateRedundantStainedWholeWrapperWithSuffix(
        string source,
        string route,
        out string translated)
    {
        translated = source;
        if (!source.StartsWith("{{", StringComparison.Ordinal))
        {
            return false;
        }

        var wrapperEnd = FindQudMarkupEnd(source, 0);
        if (wrapperEnd <= 0 || wrapperEnd >= source.Length || source[wrapperEnd] != ' ')
        {
            return false;
        }

        var pipeIndex = source.IndexOf('|', 2);
        if (pipeIndex <= 2 || pipeIndex >= wrapperEnd - 2)
        {
            return false;
        }

        var inner = source.Substring(pipeIndex + 1, wrapperEnd - pipeIndex - 3);
        if (!StringHelpers.ContainsOrdinal(inner, "-stained"))
        {
            return false;
        }

        var outerOpeningToken = source.Substring(0, pipeIndex + 1);
        var coloredSource = PreserveUnwrappedStainedModifierColor(inner, outerOpeningToken)
            + source.Substring(wrapperEnd);
        translated = RestoreLeadingChainStainedModifierColor(
            coloredSource,
            TranslatePreservingColors(coloredSource, route));
        if (!ColorAwareTranslationComposer.HasColorMarkup(inner))
        {
            translated = RestoreLeadingTranslatedStainedModifierOpening(translated, outerOpeningToken);
        }

        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static string RestoreLeadingTranslatedStainedModifierOpening(string translated, string openingToken)
    {
        if (!openingToken.StartsWith("{{", StringComparison.Ordinal)
            || !openingToken.EndsWith("|", StringComparison.Ordinal))
        {
            return translated;
        }

        const string stainedMarker = "に染まった";
        var stainedMarkerIndex = translated.IndexOf(stainedMarker, StringComparison.Ordinal);
        if (stainedMarkerIndex <= 0)
        {
            return translated;
        }

        if (translated.StartsWith("{{", StringComparison.Ordinal))
        {
            var wrapperEnd = FindQudMarkupEnd(translated, 0);
            var pipeIndex = translated.IndexOf('|', 2);
            if (wrapperEnd == stainedMarkerIndex
                && pipeIndex > 2
                && pipeIndex < wrapperEnd - 2)
            {
                return openingToken
                    + translated.Substring(pipeIndex + 1, wrapperEnd - pipeIndex - 3)
                    + "}}"
                    + translated.Substring(wrapperEnd);
            }
        }

        return openingToken
            + translated.Substring(0, stainedMarkerIndex)
            + "}}"
            + translated.Substring(stainedMarkerIndex);
    }

    private static bool TryTranslateParenthesizedColoredChargeStatus(
        string source,
        string route,
        out string translated)
    {
        var changed = false;
        translated = ParenthesizedColoredChargeStatusPattern.Replace(
            source,
            match =>
            {
                var status = match.Groups["status"].Value;
                if (!EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(status, out var translatedStatus))
                {
                    return match.Value;
                }

                changed = true;
                return match.Groups["prefix"].Value + translatedStatus + match.Groups["suffix"].Value;
            });

        if (changed)
        {
            DynamicTextObservability.RecordTransform(
                route,
                "DisplayName.ColoredChargeStatusSuffix",
                source,
                translated);
        }

        return changed;
    }

    private static bool TryHandleParenthesizedColoredChargeStatusFallback(
        string source,
        string route,
        out string translated)
    {
        var changed = false;
        var matchedFallback = false;
        translated = ParenthesizedColoredChargeStatusPattern.Replace(
            source,
            match =>
            {
                var status = match.Groups["status"].Value;
                if (TryStripDirectTranslationMarkerFromChargeStatus(status, out var markedStatus))
                {
                    changed = true;
                    matchedFallback = true;
                    return match.Groups["prefix"].Value + markedStatus + match.Groups["suffix"].Value;
                }

                if (EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(status, out _))
                {
                    return match.Value;
                }

                if (IsAlreadyLocalizedDisplayNameStateText(
                    ColorAwareTranslationComposer.GetVisibleText(status)))
                {
                    return match.Value;
                }

                matchedFallback = true;
                return match.Value;
            });

        if (changed)
        {
            DynamicTextObservability.RecordTransform(
                route,
                "DisplayName.ColoredChargeStatusSuffix",
                source,
                translated);
        }

        return matchedFallback;
    }

    private static bool TryStripDirectTranslationMarkerFromChargeStatus(string status, out string stripped)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(status, out stripped))
        {
            return true;
        }

        var separator = status.IndexOf('|');
        if (!status.StartsWith("{{", StringComparison.Ordinal)
            || !status.EndsWith("}}", StringComparison.Ordinal)
            || separator <= 2
            || separator >= status.Length - 2)
        {
            stripped = status;
            return false;
        }

        var inner = status.Substring(separator + 1, status.Length - separator - 3);
        if (!MessageFrameTranslator.TryStripDirectTranslationMarker(inner, out var strippedInner))
        {
            stripped = status;
            return false;
        }

        stripped = status.Substring(0, separator + 1) + strippedInner + "}}";
        return true;
    }

    private static bool TryTranslateDisplayNameRouteText(string source, string route, out string translated) =>
        TryTranslateDisplayNameRouteText(source, null, route, out translated);

    private static bool TryTranslateDisplayNameRouteText(
        string source,
        IReadOnlyList<ColorSpan>? spans,
        string route,
        out string translated)
    {
        translated = source;
        var transformed = source;
        var changed = false;

        if (TryTranslateParenthesizedDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateBracketedDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateQuantityDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateMinerGeneratedRoleDisplayNameSuffix(source, spans, route, out translated))
        {
            return true;
        }

        if (TryTranslateMkTierDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateAngleCodeDisplayNameSuffix(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateExactDisplayNameLookup(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateTrimmedDisplayNameLookup(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateCyberneticsSchemasoftDisplayName(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateCyberneticsSkillsoftWrappedDisplayName(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateEvilTwinSpacedPrefixDisplayName(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateEvilTwinAntiPrefixDisplayName(source, route, out translated))
        {
            return true;
        }

        if (HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.HistoricSpiceGeneratedName", source, translated);
            return true;
        }

        if (TryTranslateGeneratedTitleSuffix(transformed, route, out var titleTranslated))
        {
            transformed = titleTranslated;
            changed = true;
        }

        if (TryTranslateGeneratedRandomStatueName(transformed, route, out var randomStatueTranslated))
        {
            translated = randomStatueTranslated;
            return true;
        }

        if (TryTranslateGeneratedEnglishPrefixDisplayName(transformed, route, out var prefixTranslated))
        {
            translated = prefixTranslated;
            return true;
        }

        if (TryTranslateLocalizedPrefixAsciiModifierDisplayName(transformed, route, out var localizedPrefixModifierTranslated))
        {
            translated = localizedPrefixModifierTranslated;
            return true;
        }

        if (TryTranslateMixedDisplayName(transformed, route, out var modifierTranslated))
        {
            translated = modifierTranslated;
            return true;
        }

        if (TryTranslateLiquidPrepositionDisplayName(transformed, route, out var ofPhraseTranslated))
        {
            translated = ofPhraseTranslated;
            return true;
        }

        if (TryTranslateLocalizedPrefixAsciiTailDisplayName(transformed, route, out var localizedPrefixTailTranslated))
        {
            translated = localizedPrefixTailTranslated;
            return true;
        }

        if (TryTranslatePairOfDisplayName(transformed, route, out var pairOfTranslated))
        {
            translated = pairOfTranslated;
            return true;
        }

        if (TryTranslateWithClauseDisplayName(transformed, route, out var withClauseTranslated))
        {
            translated = withClauseTranslated;
            return true;
        }

        if (TryTranslateLiquidState(transformed, route, out var liquidStateTranslated))
        {
            translated = liquidStateTranslated;
            return true;
        }

        if (TryTranslateGeneratedCanvasTentName(transformed, route, out var canvasTentTranslated))
        {
            translated = canvasTentTranslated;
            return true;
        }

        if (TryTranslateLeadingModifierChain(transformed, route, out var modifierChainTranslated))
        {
            translated = modifierChainTranslated;
            return true;
        }

        if (TryTranslateGeneratedProperNameModifier(transformed, route, out var properNameModifierTranslated))
        {
            translated = properNameModifierTranslated;
            return true;
        }

        if (changed)
        {
            translated = transformed;
            return true;
        }

        return false;
    }

    private static bool TryTranslateParenthesizedDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = ParenthesizedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var stateSource = match.Groups["state"].Value;
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(baseSource, route);
        var translatedState = TranslateMarkedUpDisplayNameState(stateSource, route);

        translated = translatedBase + " (" + translatedState + ")";
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return IsStableDisplayNameFragment(baseSource, route) || IsStableDisplayNameState(stateSource);
        }

        DynamicTextObservability.RecordTransform(route, "DisplayName.ParenthesizedSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateBracketedDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = BracketedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var stateSource = match.Groups["state"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        var translatedState = TranslateDisplayNameState(stateSource, route);

        if (string.Equals(translatedBase, baseSource, StringComparison.Ordinal)
            && string.Equals(translatedState, stateSource, StringComparison.Ordinal))
        {
            translated = source;
            return IsStableDisplayNameFragment(baseSource, route) && IsStableDisplayNameState(stateSource);
        }

        translated = translatedBase + " [" + translatedState + "]";
        DynamicTextObservability.RecordTransform(route, "DisplayName.BracketedSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateBracketedDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = BracketedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var stateGroup = match.Groups["state"];
        var translatedBase = RestoreWholeSlice(TranslateDisplayNameFragment(baseGroup.Value, route), spans, baseGroup);
        var translatedState = TranslateDisplayNameStatePreservingColors(stateGroup, spans, route);

        if (string.Equals(translatedBase, baseGroup.Value, StringComparison.Ordinal)
            && string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedBase + " " + RestoreBracketedDisplayNameStateSuffix(translatedState, stateGroup, spans);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.BracketedSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateWholeBracketedDisplayNameState(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = WholeBracketedDisplayNameStatePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var stateGroup = match.Groups["state"];
        var translatedState = TranslateDisplayNameStatePreservingColors(stateGroup, spans, route);
        if (string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = RestoreBracketedDisplayNameStateSuffix(translatedState, stateGroup, spans);
        DynamicTextObservability.RecordTransform(route, "DisplayName.WholeBracketedState", source, translated);
        return true;
    }

    private static bool TryTranslateMarkedUpBracketedStateSuffix(
        string source,
        string route,
        out string translated)
    {
        var match = MarkedUpBracketedStateSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var stateSource = match.Groups["state"].Value;
        if (GeneratedTitleSuffixPattern.IsMatch(baseSource))
        {
            translated = source;
            return false;
        }

        string translatedBase;
        string translatedState;
        if (QuantifiedLiquidStatePattern.IsMatch(stateSource))
        {
            translatedBase = IsWholeQudWrapper(baseSource)
                ? TranslateDisplayNameFragmentPreservingWholeQudWrapper(baseSource, route)
                : TranslateDisplayNameFragment(baseSource, route);
            translatedState = TranslateDisplayNameState(stateSource, route);
        }
        else if (TryTranslateBracketedStateExact(stateSource, out var exactState)
            && !ColorAwareTranslationComposer.HasColorMarkup(exactState))
        {
            translatedBase = IsWholeQudWrapper(baseSource)
                ? TranslateDisplayNameFragmentPreservingWholeQudWrapper(baseSource, route)
                : TranslateDisplayNameFragmentPreservingColors(baseSource, route);
            translatedState = exactState;
        }
        else
        {
            translated = source;
            return false;
        }

        var changed = !string.Equals(translatedBase, baseSource, StringComparison.Ordinal)
            || !string.Equals(translatedState, stateSource, StringComparison.Ordinal);

        translated = translatedBase
            + " "
            + match.Groups["open"].Value
            + translatedState
            + match.Groups["close"].Value;

        if (match.Groups["parenState"].Success)
        {
            var parenStateSource = match.Groups["parenState"].Value;
            var translatedParenState = TranslateMarkedUpDisplayNameState(parenStateSource, route);
            changed |= !string.Equals(translatedParenState, parenStateSource, StringComparison.Ordinal);
            translated += " "
                + match.Groups["parenOpen"].Value
                + translatedParenState
                + match.Groups["parenClose"].Value;
        }

        if (!changed)
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "DisplayName.MarkedUpBracketedStateSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateMarkedUpBracketedStateSuffixSequence(
        string source,
        string route,
        out string translated)
    {
        var match = MarkedUpBracketedStateSuffixSequencePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(baseSource, route);
        var builder = new StringBuilder(translatedBase);
        var changed = !string.Equals(translatedBase, baseSource, StringComparison.Ordinal);
        var suffixes = match.Groups["suffixes"].Value;
        var offset = 0;
        var suffixCount = 0;
        while (offset < suffixes.Length)
        {
            var suffix = MarkedUpBracketedStateSuffixTokenPattern.Match(suffixes, offset);
            if (!suffix.Success)
            {
                translated = source;
                return false;
            }

            var stateSource = suffix.Groups["state"].Value;
            var translatedState = TranslateMarkedUpDisplayNameState(stateSource, route);
            changed |= !string.Equals(translatedState, stateSource, StringComparison.Ordinal);
            builder.Append(suffix.Groups["space"].Value);
            builder.Append(suffix.Groups["open"].Value);
            builder.Append(translatedState);
            builder.Append(suffix.Groups["close"].Value);
            suffixCount++;
            offset += suffix.Length;
        }

        if (suffixCount < 2 || !changed)
        {
            translated = source;
            return false;
        }

        translated = builder.ToString();
        DynamicTextObservability.RecordTransform(route, "DisplayName.MarkedUpBracketedStateSuffixSequence", source, translated);
        return true;
    }

    private static string TranslateMarkedUpDisplayNameState(string source, string route)
    {
        var direct = TranslateDisplayNameState(source, route);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        if (!ColorAwareTranslationComposer.HasColorMarkup(source))
        {
            return source;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TranslateDisplayNameState(visible, route));
    }


    private static bool TryTranslateArmorStatsDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        return TryTranslateStatDisplayNameSuffix(
            source,
            spans,
            route,
            ArmorStatsDisplayNameSuffixPattern,
            "DisplayName.ArmorStatsSuffix",
            out translated);
    }

    private static bool TryTranslateCompactWeaponStatsDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        return TryTranslateStatDisplayNameSuffix(
            source,
            spans,
            route,
            CompactWeaponStatsDisplayNameSuffixPattern,
            "DisplayName.CompactWeaponStatsSuffix",
            out translated);
    }

    private static bool TryTranslateExactBaseCompactWeaponStatsDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = CompactWeaponStatsDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var translatedBase = TranslateDisplayNameExactOrLowerAscii(baseGroup.Value);
        if (translatedBase is null)
        {
            translated = source;
            return false;
        }

        var stats = RestoreCompactWeaponStatsSlice(match.Groups["stats"], spans);
        var stateGroup = match.Groups["state"];
        var translatedState = stateGroup.Success
            ? TranslateDisplayNameStatePreservingColors(stateGroup, spans, route)
            : string.Empty;

        translated = RestoreWholeSlice(translatedBase, spans, baseGroup) + " " + stats;
        if (stateGroup.Success)
        {
            translated += " " + RestoreBracketedDisplayNameStateSuffix(translatedState, stateGroup, spans);
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.CompactWeaponStatsSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateWithClauseCompactWeaponStatsDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = WithClauseCompactWeaponStatsDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var clauseGroup = match.Groups["clause"];
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(baseGroup.Value, spans, baseGroup, route);
        var translatedClause = TranslateDisplayNameWithClause(clauseGroup.Value, spans, clauseGroup.Index, clauseGroup.Length);
        if (translatedClause is null)
        {
            translated = source;
            return false;
        }

        var stats = RestoreCompactWeaponStatsSlice(match.Groups["stats"], spans);
        var stateGroup = match.Groups["state"];
        var translatedState = stateGroup.Success
            ? TranslateDisplayNameStatePreservingColors(stateGroup, spans, route)
            : string.Empty;

        translated = translatedBase + "（" + translatedClause + "） " + stats;
        if (stateGroup.Success)
        {
            translated += " " + RestoreBracketedDisplayNameStateSuffix(translatedState, stateGroup, spans);
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.WithClauseCompactWeaponStatsSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateWithClauseCompactWeaponStatsDisplayNameSuffixSequence(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = WithClauseCompactWeaponStatsDisplayNameSuffixSequencePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var clauseGroup = match.Groups["clause"];
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(baseGroup.Value, spans, baseGroup, route);
        var translatedClause = TranslateDisplayNameWithClause(clauseGroup.Value, spans, clauseGroup.Index, clauseGroup.Length);
        if (translatedClause is null)
        {
            translated = source;
            return false;
        }

        var suffixes = match.Groups["suffixes"];
        var suffixEnd = suffixes.Index + suffixes.Length;
        var builder = new StringBuilder();
        var scannedTo = suffixes.Index;

        for (var suffixMatch = DisplayNameTrailingSuffixPattern.Match(source, suffixes.Index);
             suffixMatch.Success && suffixMatch.Index < suffixEnd;
             suffixMatch = suffixMatch.NextMatch())
        {
            scannedTo = suffixMatch.Index + suffixMatch.Length;
            if (suffixMatch.Groups["bracket"].Success)
            {
                var stateGroup = suffixMatch.Groups["state"];
                var translatedState = TranslateDisplayNameStatePreservingColors(stateGroup, spans, route);
                if (string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal))
                {
                    builder.Append(' ');
                    builder.Append(RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["bracket"], spans));
                    continue;
                }

                builder.Append(' ');
                builder.Append(RestoreBracketedDisplayNameSuffix(translatedState, suffixMatch.Groups["bracket"], spans));
                continue;
            }

            builder.Append(' ');
            builder.Append(RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["angle"], spans));
        }

        if (scannedTo != suffixEnd)
        {
            translated = source;
            return false;
        }

        translated = translatedBase + "（" + translatedClause + "） " + RestoreCompactWeaponStatsSlice(match.Groups["stats"], spans) + builder;
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.WithClauseCompactWeaponStatsSuffixSequence", source, translated);
        return true;
    }

    private static bool TryTranslateCompactWeaponStatsDisplayNameSuffixSequence(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = CompactWeaponStatsDisplayNameSuffixSequencePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var baseSource = baseGroup.Value;
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(baseSource, spans, baseGroup, route);
        var stats = RestoreCompactWeaponStatsSlice(match.Groups["stats"], spans);
        var suffixes = match.Groups["suffixes"];
        var suffixEnd = suffixes.Index + suffixes.Length;
        var builder = new StringBuilder();
        var changed = !string.Equals(translatedBase, baseSource, StringComparison.Ordinal);
        var scannedTo = suffixes.Index;

        for (var suffixMatch = DisplayNameTrailingSuffixPattern.Match(source, suffixes.Index);
             suffixMatch.Success && suffixMatch.Index < suffixEnd;
             suffixMatch = suffixMatch.NextMatch())
        {
            scannedTo = suffixMatch.Index + suffixMatch.Length;
            if (suffixMatch.Groups["bracket"].Success)
            {
                var stateGroup = suffixMatch.Groups["state"];
                var translatedState = TranslateDisplayNameStatePreservingColors(stateGroup, spans, route);
                if (string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal))
                {
                    builder.Append(' ');
                    builder.Append(RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["bracket"], spans));
                    continue;
                }

                builder.Append(' ');
                builder.Append(RestoreBracketedDisplayNameSuffix(translatedState, suffixMatch.Groups["bracket"], spans));
                changed = true;
                continue;
            }

            builder.Append(' ');
            builder.Append(RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["angle"], spans));
        }

        if (scannedTo != suffixEnd || !changed)
        {
            translated = source;
            return false;
        }

        translated = translatedBase + " " + stats + builder;
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.CompactWeaponStatsSuffixSequence", source, translated);
        return true;
    }

    private static bool TryTranslateArmorStatsDisplayNameSuffixSequence(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = ArmorStatsDisplayNameSuffixSequencePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var baseSource = baseGroup.Value;
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(baseSource, spans, baseGroup, route);
        var stats = RestoreVisibleSlice(match.Groups["stats"], spans);
        var suffixes = match.Groups["suffixes"];
        var suffixEnd = suffixes.Index + suffixes.Length;
        var builder = new StringBuilder();
        var changed = !string.Equals(translatedBase, baseSource, StringComparison.Ordinal);
        var scannedTo = suffixes.Index;

        for (var suffixMatch = DisplayNameTrailingSuffixPattern.Match(source, suffixes.Index);
             suffixMatch.Success && suffixMatch.Index < suffixEnd;
             suffixMatch = suffixMatch.NextMatch())
        {
            scannedTo = suffixMatch.Index + suffixMatch.Length;
            builder.Append(' ');
            if (suffixMatch.Groups["bracket"].Success)
            {
                var stateGroup = suffixMatch.Groups["state"];
                var translatedState = TranslateDisplayNameStatePreservingColors(stateGroup, spans, route);
                if (string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal))
                {
                    builder.Append(RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["bracket"], spans));
                    continue;
                }

                builder.Append(RestoreBracketedDisplayNameStateSuffix(translatedState, suffixMatch.Groups["bracket"], spans));
                changed = true;
                continue;
            }

            builder.Append(RestoreSemanticAngleCodeSuffixSlice(suffixMatch.Groups["angle"], spans));
        }

        if (scannedTo != suffixEnd || !changed)
        {
            translated = source;
            return false;
        }

        translated = translatedBase + " " + stats + builder;
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.ArmorStatsSuffixSequence", source, translated);
        return true;
    }

    private static bool TryTranslateStatDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        Regex pattern,
        string transformName,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var baseSource = baseGroup.Value;
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(baseSource, spans, baseGroup, route);
        var stats = string.Equals(transformName, "DisplayName.CompactWeaponStatsSuffix", StringComparison.Ordinal)
            ? RestoreCompactWeaponStatsSlice(match.Groups["stats"], spans)
            : RestoreVisibleSlice(match.Groups["stats"], spans);
        var stateGroup = match.Groups["state"];

        var translatedState = stateGroup.Success
            ? TranslateDisplayNameStatePreservingColors(stateGroup, spans, route)
            : string.Empty;

        if (string.Equals(translatedBase, baseSource, StringComparison.Ordinal)
            && (!stateGroup.Success || string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal)))
        {
            translated = source;
            return false;
        }

        translated = translatedBase + " " + stats;
        if (stateGroup.Success)
        {
            translated += " " + RestoreBracketedDisplayNameStateSuffix(translatedState, stateGroup, spans);
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, transformName, source, translated);
        return true;
    }

    private static bool TryTranslateQuantityDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = QuantityDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        translated = translatedBase + " x" + match.Groups["count"].Value;
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.QuantitySuffix", source, translated);
            return true;
        }

        return IsStableDisplayNameFragment(baseSource, route);
    }

    private static bool TryTranslateMinerGeneratedRoleDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan>? spans,
        string route,
        out string translated)
    {
        var match = MinerGeneratedRoleDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        if (spans is not null)
        {
            translatedBase = RestoreWholeSlice(translatedBase, spans, match.Groups["base"]);
        }

        var translatedRole = match.Groups["role"].Value switch
        {
            "miner" => "採掘機",
            "bomber" => "爆撃機",
            _ => match.Groups["role"].Value,
        };
        translated = translatedBase + " " + translatedRole + " mk " + match.Groups["tier"].Value;
        if (spans is not null)
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translated,
                spans,
                source.Length,
                source);
        }

        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.MinerGeneratedRoleSuffix", source, translated);
            return true;
        }

        return IsStableDisplayNameFragment(baseSource, route);
    }

    private static bool TryTranslateMkTierDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = MkTierDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        translated = translatedBase + " mk " + match.Groups["tier"].Value;
        var code = match.Groups["code"].Value;
        if (code.Length > 0)
        {
            translated += " <" + code + ">";
        }

        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.MkTierSuffix", source, translated);
            return true;
        }

        return IsStableDisplayNameFragment(baseSource, route);
    }

    private static bool TryTranslateAngleCodeDisplayNameSuffix(string source, string route, out string translated)
    {
        var match = AngleCodeDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseSource = match.Groups["base"].Value;
        var translatedBase = TranslateDisplayNameFragment(baseSource, route);
        translated = translatedBase + " <" + match.Groups["code"].Value + ">";
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "DisplayName.AngleCodeSuffix", source, translated);
            return true;
        }

        return IsStableDisplayNameFragment(baseSource, route);
    }

    private static bool TryTranslateAngleCodeDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = AngleCodeDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var baseSource = baseGroup.Value;
        var innerSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, source.Length);
        var translatedBase = TranslatePreservingColors(RestoreAngleCodeBaseSlice(baseGroup, innerSpans), route);
        translatedBase = RestoreLeadingSingleStainedModifierColorFromSource(
            baseSource,
            spans,
            baseGroup.Index,
            translatedBase);
        var angle = NormalizeTransparentAngleCodeWrapper(RestoreVisibleSlice(match.Groups["angle"], innerSpans));

        if (string.Equals(translatedBase, baseSource, StringComparison.Ordinal)
            && string.Equals(angle, match.Groups["angle"].Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedBase + " " + angle;
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.AngleCodeSuffix", source, translated);
        return true;
    }

    private static string RestoreLeadingSingleStainedModifierColorFromSource(
        string baseSource,
        IReadOnlyList<ColorSpan> spans,
        int baseStartIndex,
        string translatedBase)
    {
        if (ColorAwareTranslationComposer.HasColorMarkup(translatedBase)
            || !TryReadLeadingModifierToken(baseSource, 0, out var modifier, out _)
            || !SingleStainedModifierPattern.IsMatch(modifier))
        {
            return RestoreLocalizedBloodStainedColor(translatedBase);
        }

        var openingToken = FindQudOpeningTokenAt(spans, baseStartIndex);
        if (openingToken is null)
        {
            openingToken = FindColoredLiquidOpeningForSingleStainedModifier(modifier);
        }
        if (openingToken is null)
        {
            return translatedBase;
        }

        const string stainedMarker = "に染まった";
        var stainedMarkerIndex = translatedBase.IndexOf(stainedMarker, StringComparison.Ordinal);
        if (stainedMarkerIndex <= 0)
        {
            return translatedBase;
        }

        return openingToken
            + translatedBase.Substring(0, stainedMarkerIndex)
            + "}}"
            + translatedBase.Substring(stainedMarkerIndex);
    }

    private static string RestoreLocalizedBloodStainedColor(string translatedBase)
    {
        const string bloodStainedPrefix = "血に染まった";
        return translatedBase.StartsWith(bloodStainedPrefix, StringComparison.Ordinal)
            ? "{{r|血}}" + translatedBase.Substring("血".Length)
            : translatedBase;
    }

    private static string? FindQudOpeningTokenAt(IReadOnlyList<ColorSpan> spans, int index)
    {
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            if (span.Index == index
                && span.Token.StartsWith("{{", StringComparison.Ordinal)
                && span.Token.EndsWith("|", StringComparison.Ordinal))
            {
                return span.Token;
            }
        }

        return null;
    }

    private static string? FindColoredLiquidOpeningForSingleStainedModifier(string modifier)
    {
        var match = SingleStainedModifierPattern.Match(modifier);
        if (!match.Success)
        {
            return null;
        }

        var liquid = match.Groups["liquid"].Value;
        if (!LooksLikeAsciiPhrase(liquid))
        {
            return null;
        }

        var defaultOpening = GetDefaultLiquidColorOpening(liquid);
        if (defaultOpening is not null)
        {
            return defaultOpening;
        }

        for (var index = 0; index < LiquidColorCodes.Length; index++)
        {
            var opening = "{{" + LiquidColorCodes[index] + "|";
            var coloredKey = opening + liquid + "}}";
            var translated = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
                coloredKey,
                "XRL.Liquids",
                LiquidPhraseDictionaryFiles);
            if (translated is not null
                && !string.IsNullOrWhiteSpace(translated)
                && ColorAwareTranslationComposer.HasColorMarkup(translated))
            {
                return opening;
            }
        }

        return null;
    }

    private static string? GetDefaultLiquidColorOpening(string liquid)
    {
        return liquid switch
        {
            "blood" => "{{r|",
            "slime" => "{{g|",
            "goo" => "{{G|",
            "sludge" => "{{w|",
            "oil" => "{{K|",
            "water" => "{{B|",
            "acid" => "{{G|",
            "lava" => "{{R|",
            _ => null,
        };
    }

    private static string RestoreAngleCodeBaseSlice(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var sliceSpans = ColorCodePreserver.SliceSpans(spans, group.Index, group.Length);
        RemoveUnmatchedTrailingSliceClosers(sliceSpans);
        if (TryColorizeLeadingPlainStainedModifierFromWholeBaseWrapper(group.Value, sliceSpans, out var colorizedStainedBase))
        {
            return colorizedStainedBase;
        }

        var wholeRestored = ColorAwareTranslationComposer.Restore(group.Value, sliceSpans);
        if (TryUnwrapWholeQudWrapperContainingStainedModifier(wholeRestored, out var inner))
        {
            return inner;
        }

        var contentSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(sliceSpans, group.Length);
        var restored = ColorAwareTranslationComposer.Restore(group.Value, contentSpans);
        return restored;
    }

    private static bool TryColorizeLeadingPlainStainedModifierFromWholeBaseWrapper(
        string visibleBase,
        IReadOnlyList<ColorSpan> spans,
        out string restored)
    {
        restored = visibleBase;
        if (!TryReadLeadingModifierToken(visibleBase, 0, out var modifier, out _)
            || ColorAwareTranslationComposer.HasColorMarkup(modifier)
            || !StringHelpers.ContainsOrdinal(modifier, "-stained"))
        {
            return false;
        }

        for (var index = 0; index < spans.Count; index++)
        {
            var opening = spans[index];
            if (opening.Index != 0
                || !opening.Token.StartsWith("{{", StringComparison.Ordinal)
                || !opening.Token.EndsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            var coloredModifier = ColorizePlainStainedModifier(modifier, opening.Token);
            if (string.Equals(coloredModifier, modifier, StringComparison.Ordinal))
            {
                return false;
            }

            restored = coloredModifier + visibleBase.Substring(modifier.Length);
            return true;
        }

        return false;
    }
    private static bool TryUnwrapWholeQudWrapperContainingStainedModifier(string source, out string inner)
    {
        inner = source;
        if (!source.StartsWith("{{", StringComparison.Ordinal)
            || !source.EndsWith("}}", StringComparison.Ordinal)
            || FindQudMarkupEnd(source, 0) != source.Length)
        {
            return false;
        }

        var pipeIndex = source.IndexOf('|', 2);
        if (pipeIndex <= 2 || pipeIndex >= source.Length - 2)
        {
            return false;
        }

        var candidate = source.Substring(pipeIndex + 1, source.Length - pipeIndex - 3);
        if (!StringHelpers.ContainsOrdinal(candidate, "-stained"))
        {
            return false;
        }

        inner = PreserveUnwrappedStainedModifierColor(candidate, source.Substring(0, pipeIndex + 1));
        return true;
    }

    private static string PreserveUnwrappedStainedModifierColor(string source, string openingToken)
    {
        if (!TryReadLeadingModifierToken(source, 0, out var modifier, out _)
            || ColorAwareTranslationComposer.HasColorMarkup(modifier)
            || !StringHelpers.ContainsOrdinal(modifier, "-stained"))
        {
            return source;
        }

        var coloredModifier = ColorizePlainStainedModifier(modifier, openingToken);
        return string.Equals(coloredModifier, modifier, StringComparison.Ordinal)
            ? source
            : coloredModifier + source.Substring(modifier.Length);
    }

    private static string ColorizePlainStainedModifier(string modifier, string openingToken)
    {
        if (!openingToken.StartsWith("{{", StringComparison.Ordinal)
            || !openingToken.EndsWith("|", StringComparison.Ordinal))
        {
            return modifier;
        }

        var compound = CompoundStainedModifierPattern.Match(modifier);
        if (compound.Success)
        {
            return openingToken + compound.Groups["left"].Value + "}}-and-"
                + openingToken + compound.Groups["right"].Value + "}}-stained";
        }

        var single = SingleStainedModifierPattern.Match(modifier);
        return single.Success
            ? openingToken + single.Groups["liquid"].Value + "}}-stained"
            : modifier;
    }

    private static bool TryTranslateLeadingMarkupWrappedModifier(string source, string route, out string translated)
    {
        var match = LeadingMarkupWrappedModifierPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var modifier = match.Groups["modifier"].Value;
        var translatedModifier = TranslateDisplayNameModifier(modifier);
        if (translatedModifier is null)
        {
            translated = source;
            return false;
        }

        var restSource = match.Groups["rest"].Value;
        var rest = TranslatePreservingColors(restSource, route);
        translated = translatedModifier + GetModifierRestSeparator(modifier, restSource) + rest;
        DynamicTextObservability.RecordTransform(route, "DisplayName.MarkupLeadingModifier", source, translated);
        return true;
    }

    private static bool TryTranslateLeadingModifierChain(string source, string route, out string translated)
    {
        translated = source;
        var translatedModifiers = new List<string>();
        var sourceModifiers = new List<string>();
        var position = 0;
        var skippedArticle = false;

        while (TryReadLeadingModifierToken(source, position, out var modifier, out var restStart))
        {
            if (string.Equals(route, nameof(GetDisplayNamePatch), StringComparison.Ordinal)
                && IsDisplayNameArticleModifier(modifier))
            {
                position = restStart;
                skippedArticle = true;
                continue;
            }

            var translatedModifier = TranslateDisplayNameModifierForChain(modifier);
            if (translatedModifier is null)
            {
                if (!IsMarkupOrBracketedModifierToken(modifier))
                {
                    break;
                }

                translatedModifier = modifier;
            }

            translatedModifiers.Add(translatedModifier);
            sourceModifiers.Add(modifier);
            position = restStart;

            if (position >= source.Length)
            {
                break;
            }
        }

        if (position >= source.Length)
        {
            return false;
        }

        if (translatedModifiers.Count == 0)
        {
            if (!skippedArticle)
            {
                return false;
            }

            translated = TranslatePreservingColors(source.Substring(position), route);
            if (string.Equals(translated, source, StringComparison.Ordinal))
            {
                return false;
            }

            DynamicTextObservability.RecordTransform(route, "DisplayName.LeadingArticle", source, translated);
            return true;
        }

        if (!skippedArticle
            && translatedModifiers.Count == 1
            && source[0] != '{'
            && source[0] != '['
            && !IsStainedDisplayNameModifier(sourceModifiers[0]))
        {
            return false;
        }

        var restSource = source.Substring(position);
        var rest = TranslatePreservingColors(restSource, route);
        var builder = new StringBuilder();
        for (var index = 0; index < translatedModifiers.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(translatedModifiers[index]);
        }

        builder.Append(GetModifierRestSeparator(sourceModifiers[sourceModifiers.Count - 1], restSource));
        builder.Append(rest);
        translated = builder.ToString();

        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "DisplayName.LeadingModifierChain", source, translated);
        return true;
    }

    private static string RestoreLeadingChainStainedModifierColor(string source, string translated)
    {
        if (ColorAwareTranslationComposer.HasColorMarkup(translated)
            || !TryGetLeadingStainedModifierOpening(source, out var openingToken))
        {
            return translated;
        }

        const string stainedMarker = "に染まった";
        var markerIndex = translated.IndexOf(stainedMarker, StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return translated;
        }

        return openingToken
            + translated.Substring(0, markerIndex)
            + "}}"
            + translated.Substring(markerIndex);
    }

    internal static string TranslateScopedExactPreservingColors(string? source)
    {
        if (source is null)
        {
            Trace.TraceWarning(
                "QudJP: GetDisplayNameRouteTranslator.TranslateScopedExactPreservingColors received null source; returning empty string.");
            return string.Empty;
        }

        if (source.Length == 0)
        {
            return string.Empty;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible =>
            {
                var translated = TryTranslateDisplayNameScopedExact(visible);
                return translated is null ? visible : translated;
            });
    }

    private static bool TryGetLeadingStainedModifierOpening(string source, out string openingToken)
    {
        openingToken = string.Empty;
        var candidate = source;
        if (source.StartsWith("{{", StringComparison.Ordinal)
            && FindQudMarkupEnd(source, 0) > 0)
        {
            var pipeIndex = source.IndexOf('|', 2);
            if (pipeIndex > 2)
            {
                candidate = source.Substring(pipeIndex + 1);
                if (candidate.EndsWith("}}", StringComparison.Ordinal))
                {
                    candidate = candidate.Substring(0, candidate.Length - 2);
                }
            }
        }

        if (!TryReadLeadingModifierToken(candidate, 0, out var modifier, out _)
            || !StringHelpers.ContainsOrdinal(modifier, "-stained"))
        {
            return false;
        }

        if (modifier.StartsWith("{{", StringComparison.Ordinal))
        {
            var pipeIndex = modifier.IndexOf('|', 2);
            if (pipeIndex > 2)
            {
                openingToken = modifier.Substring(0, pipeIndex + 1);
                return true;
            }
        }

        var coloredLiquidOpening = FindColoredLiquidOpeningForSingleStainedModifier(modifier);
        openingToken = coloredLiquidOpening ?? string.Empty;
        return openingToken.Length > 0;
    }

    private static bool IsDisplayNameArticleModifier(string modifier)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(modifier);
        return visible is "a" or "an" or "the";
    }

    private static bool IsStainedDisplayNameModifier(string modifier)
    {
        return modifier.EndsWith("-stained", StringComparison.Ordinal)
            || CompoundStainedModifierPattern.IsMatch(modifier);
    }

    private static bool TryReadLeadingModifierToken(
        string source,
        int start,
        out string modifier,
        out int restStart)
    {
        modifier = string.Empty;
        restStart = start;
        int modifierEnd;

        if (start < 0 || start >= source.Length)
        {
            return false;
        }

        if (source[start] == '['
            && start + 2 < source.Length
            && source[start + 1] == '{'
            && source[start + 2] == '{')
        {
            var wrappedEnd = FindQudMarkupEnd(source, start + 1);
            if (wrappedEnd < 0 || wrappedEnd >= source.Length || source[wrappedEnd] != ']')
            {
                return false;
            }

            modifierEnd = wrappedEnd + 1;
        }
        else if (source[start] == '['
            && start + 2 < source.Length
            && IsAsciiModifierStart(source[start + 1]))
        {
            var close = source.IndexOf(']', start + 2);
            if (close < 0)
            {
                return false;
            }

            for (var index = start + 1; index < close; index++)
            {
                if (!IsAsciiModifierCharacter(source[index]) && source[index] != ' ')
                {
                    return false;
                }
            }

            modifierEnd = close + 1;
        }
        else if (start + 1 < source.Length && source[start] == '{' && source[start + 1] == '{')
        {
            var wrappedEnd = FindQudMarkupEnd(source, start);
            if (wrappedEnd < 0)
            {
                return false;
            }

            modifierEnd = wrappedEnd;
            if (source.Length >= wrappedEnd + "-stained".Length
                && string.Equals(
                    source.Substring(wrappedEnd, "-stained".Length),
                    "-stained",
                    StringComparison.Ordinal))
            {
                modifierEnd += "-stained".Length;
            }
        }
        else if (IsAsciiModifierStart(source[start]))
        {
            modifierEnd = start + 1;
            while (modifierEnd < source.Length && IsAsciiModifierCharacter(source[modifierEnd]))
            {
                modifierEnd++;
            }

            if (modifierEnd < source.Length && source[modifierEnd] == '(')
            {
                var close = source.IndexOf(')', modifierEnd + 1);
                if (close > modifierEnd + 1)
                {
                    var digitsOnly = true;
                    for (var index = modifierEnd + 1; index < close; index++)
                    {
                        if (source[index] < '0' || source[index] > '9')
                        {
                            digitsOnly = false;
                            break;
                        }
                    }

                    if (digitsOnly)
                    {
                        modifierEnd = close + 1;
                    }
                }
            }
        }
        else
        {
            return false;
        }

        modifierEnd = ExtendCompoundStainedModifierEnd(source, modifierEnd);
        if (modifierEnd <= start || modifierEnd >= source.Length || source[modifierEnd] != ' ')
        {
            return false;
        }

        restStart = modifierEnd + 1;
        while (restStart < source.Length && source[restStart] == ' ')
        {
            restStart++;
        }

        if (restStart >= source.Length)
        {
            return false;
        }

        modifier = source.Substring(start, modifierEnd - start);
        return true;
    }

    private static int ExtendCompoundStainedModifierEnd(string source, int modifierEnd)
    {
        const string separator = "-and-";
        const string suffix = "-stained";
        if (modifierEnd < 0
            || modifierEnd + separator.Length >= source.Length
            || !string.Equals(
                source.Substring(modifierEnd, separator.Length),
                separator,
                StringComparison.Ordinal))
        {
            return modifierEnd;
        }

        var rightStart = modifierEnd + separator.Length;
        int rightEnd;
        if (rightStart + 1 < source.Length && source[rightStart] == '{' && source[rightStart + 1] == '{')
        {
            rightEnd = FindQudMarkupEnd(source, rightStart);
            if (rightEnd < 0)
            {
                return modifierEnd;
            }
        }
        else if (rightStart < source.Length && IsAsciiModifierStart(source[rightStart]))
        {
            rightEnd = rightStart + 1;
            while (rightEnd < source.Length && IsAsciiModifierCharacter(source[rightEnd]))
            {
                rightEnd++;
            }
        }
        else
        {
            return modifierEnd;
        }

        return rightEnd + suffix.Length <= source.Length
            && string.Equals(source.Substring(rightEnd, suffix.Length), suffix, StringComparison.Ordinal)
            ? rightEnd + suffix.Length
            : modifierEnd;
    }

    private static int FindQudMarkupEnd(string source, int start)
    {
        if (start < 0
            || start + 1 >= source.Length
            || source[start] != '{'
            || source[start + 1] != '{')
        {
            return -1;
        }

        var depth = 0;
        for (var index = start; index < source.Length - 1; index++)
        {
            if (source[index] == '{' && source[index + 1] == '{')
            {
                depth++;
                index++;
                continue;
            }

            if (source[index] == '}' && source[index + 1] == '}')
            {
                depth--;
                index++;
                if (depth == 0)
                {
                    return index + 1;
                }
            }
        }

        return -1;
    }

    private static bool IsAsciiModifierStart(char character)
    {
        return character >= 'A' && character <= 'Z'
            || character >= 'a' && character <= 'z';
    }

    private static bool IsAsciiModifierCharacter(char character)
    {
        return character >= 'A' && character <= 'Z'
            || character >= 'a' && character <= 'z'
            || character == '-'
            || character == '\'';
    }

    private static bool IsMarkupOrBracketedModifierToken(string modifier)
    {
        if (modifier.StartsWith("{{", StringComparison.Ordinal))
        {
            return TryReadWrappedModifierVisible(modifier, out var visible)
                && IsAsciiModifierPhrase(visible);
        }

        if (modifier.Length >= 4
            && modifier[0] == '['
            && modifier[1] == '{'
            && modifier[2] == '{'
            && modifier[modifier.Length - 1] == ']')
        {
            var core = modifier.Substring(1, modifier.Length - 2);
            return TryReadWrappedModifierVisible(core, out var visible)
                && IsAsciiModifierPhrase(visible);
        }

        return modifier.Length >= 3
            && modifier[0] == '['
            && modifier[modifier.Length - 1] == ']'
            && IsAsciiModifierPhrase(modifier.Substring(1, modifier.Length - 2));
    }

    private static bool IsAsciiModifierPhrase(string value)
    {
        if (value.Length == 0 || !IsAsciiModifierStart(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsAsciiModifierCharacter(value[index]) && value[index] != ' ')
            {
                return false;
            }
        }

        return true;
    }

    private static string? TranslateDisplayNameModifierForChain(string source)
    {
        return TranslateDisplayNameModifierCore(source, allowGlobalFallback: false);
    }

    private static bool TryTranslateLeadingZeroWidthMarkupPrefix(string source, string route, out string translated)
    {
        var match = LeadingZeroWidthMarkupPrefixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var restSource = match.Groups["rest"].Value;
        var rest = TranslatePreservingColors(restSource, route);
        if (string.Equals(rest, restSource, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = rest;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LeadingZeroWidthMarkupPrefix", source, translated);
        return true;
    }

    private static bool TryTranslateMixedDisplayName(string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || !JapaneseCharacterPattern.IsMatch(source))
        {
            return false;
        }

        var separatorIndex = source.IndexOf(' ');
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 1)
        {
            return false;
        }

        var modifier = source.Substring(0, separatorIndex);
        var translatedModifier = TranslateDisplayNameModifierExact(modifier);
        if (translatedModifier is null)
        {
            return false;
        }

        var rest = source.Substring(separatorIndex + 1);
        var translatedRest = TranslateDisplayNameFragment(rest, route);
        translated = translatedModifier + GetModifierRestSeparator(modifier, rest) + translatedRest;
        DynamicTextObservability.RecordTransform(route, "DisplayName.MixedModifier", source, translated);
        return true;
    }

    private static bool TryTranslateLiquidPrepositionDisplayName(string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || !JapaneseCharacterPattern.IsMatch(source))
        {
            return false;
        }

        var separatorIndex = source.IndexOf(" of ", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 4)
        {
            return false;
        }

        var head = source.Substring(0, separatorIndex);
        var tail = source.Substring(separatorIndex + 4);
        if (tail.Length == 0)
        {
            return false;
        }

        if (!LooksLikeLocalizedLiquidDisplayNameHead(head))
        {
            return false;
        }

        if (TryTranslateQuantifiedLiquidPrepositionTail(tail, out var quantifiedTail))
        {
            translated = BuildQuantifiedLiquidPrepositionTranslation(head, quantifiedTail);
            DynamicTextObservability.RecordTransform(route, "DisplayName.LiquidPreposition.Quantified", source, translated);
            return true;
        }

        if (!LooksLikeAsciiPhrase(tail))
        {
            return false;
        }

        var translatedTail = TranslateAsciiPhraseByParts(tail);
        if (translatedTail is null)
        {
            translatedTail = TranslateAsciiPhrase(tail);
        }

        if (translatedTail is null)
        {
            return false;
        }

        translated = !IsBareLiquidDisplayNameHead(head) && HeadAlreadyContainsLiquid(head, BuildLiquidComparableCandidates(tail))
            ? head
            : translatedTail + "の" + head;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LiquidPreposition", source, translated);
        return true;
    }

    private static string BuildQuantifiedLiquidPrepositionTranslation(
        string head,
        (string Amount, string Liquid, IReadOnlyList<string> ComparableLiquids) quantifiedTail)
    {
        if (IsBareLiquidDisplayNameHead(head))
        {
            return quantifiedTail.Amount + "ドラムの" + quantifiedTail.Liquid + "の" + head;
        }

        if (HeadAlreadyContainsLiquid(head, quantifiedTail.Liquid))
        {
            return quantifiedTail.Amount + "ドラムの" + head;
        }

        return HeadAlreadyContainsLiquid(head, quantifiedTail.ComparableLiquids)
            ? quantifiedTail.Amount + "ドラムの" + quantifiedTail.Liquid + "の" + StripLiquidPrefixFromDisplayNameHead(head)
            : quantifiedTail.Amount + "ドラムの" + quantifiedTail.Liquid + "の" + head;
    }

    private static string StripLiquidPrefixFromDisplayNameHead(string head)
    {
        var poolIndex = head.LastIndexOf("の水たまり", StringComparison.Ordinal);
        if (poolIndex >= 0)
        {
            return head.Substring(poolIndex + 1);
        }

        var pondIndex = head.LastIndexOf("の池", StringComparison.Ordinal);
        return pondIndex >= 0 ? head.Substring(pondIndex + 1) : head;
    }

    private static bool TryTranslateQuantifiedLiquidPrepositionTail(
        string source,
        out (string Amount, string Liquid, IReadOnlyList<string> ComparableLiquids) translated)
    {
        var match = QuantifiedLiquidStatePattern.Match(source);
        if (!match.Success || match.Groups["state"].Success)
        {
            translated = default;
            return false;
        }

        var liquidSource = match.Groups["liquid"].Value;
        var visibleLiquid = ColorAwareTranslationComposer.GetVisibleText(liquidSource);
        var translatedLiquid = LiquidVolumeFragmentTranslator.TranslateLiquidPhrasePreservingColors(liquidSource);
        if (translatedLiquid is null)
        {
            translatedLiquid = TranslateAsciiPhraseByParts(visibleLiquid);
        }

        if (translatedLiquid is null)
        {
            var direct = Translator.Translate(liquidSource);
            if (string.Equals(direct, liquidSource, StringComparison.Ordinal))
            {
                translated = default;
                return false;
            }

            translatedLiquid = direct;
        }

        translated = (match.Groups["amount"].Value, translatedLiquid, BuildLiquidComparableCandidates(visibleLiquid));
        return true;
    }

    private static bool TryTranslateLocalizedPrefixAsciiModifierDisplayName(string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || !JapaneseCharacterPattern.IsMatch(source))
        {
            return false;
        }

        var match = LocalizedPrefixAsciiModifierPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var modifier = match.Groups["modifier"].Value;
        var translatedModifier = TranslateDisplayNameModifierExact(modifier);
        if (translatedModifier is null)
        {
            return false;
        }

        var rest = match.Groups["rest"].Value;
        var translatedRest = TranslateDisplayNameFragment(rest, route);
        translated = match.Groups["prefix"].Value
            + translatedModifier
            + GetModifierRestSeparator(modifier, rest)
            + translatedRest;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LocalizedPrefixAsciiModifier", source, translated);
        return true;
    }

    private static bool TryTranslateLocalizedPrefixAsciiTailDisplayName(string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || !JapaneseCharacterPattern.IsMatch(source))
        {
            return false;
        }

        var separatorIndex = FindLocalizedPrefixAsciiTailSeparator(source);
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 1)
        {
            return false;
        }

        var prefix = source.Substring(0, separatorIndex);
        var tail = source.Substring(separatorIndex + 1);
        if (!LooksLikeAsciiPhrase(tail))
        {
            return false;
        }

        var translatedTail = TranslateAsciiPhrase(tail);
        if (translatedTail is null)
        {
            return false;
        }

        translated = prefix + translatedTail;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LocalizedPrefixAsciiTail", source, translated);
        return true;
    }

    private static bool TryTranslateWithClauseDisplayName(string source, string route, out string translated)
    {
        var match = WithClauseDisplayNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedBase = TranslateDisplayNameFragment(match.Groups["base"].Value, route);
        var translatedClause = TranslateDisplayNameWithClause(match.Groups["clause"].Value);
        if (translatedClause is null)
        {
            translated = source;
            return false;
        }

        translated = translatedBase + "（" + translatedClause + "）";
        DynamicTextObservability.RecordTransform(route, "DisplayName.WithClause", source, translated);
        return true;
    }

    private static bool TryTranslateWithClauseDisplayName(
        string source,
        IReadOnlyList<ColorSpan> spans,
        int sourceStartIndex,
        string route,
        out string translated)
    {
        var match = WithClauseDisplayNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var clauseGroup = match.Groups["clause"];
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(
            baseGroup.Value,
            spans,
            sourceStartIndex + baseGroup.Index,
            baseGroup.Length,
            route);
        var translatedClause = TranslateDisplayNameWithClause(clauseGroup.Value, spans, sourceStartIndex + clauseGroup.Index, clauseGroup.Length);
        if (translatedClause is null)
        {
            translated = source;
            return false;
        }

        var visible = translatedBase + "（" + translatedClause + "）";
        translated = ColorAwareTranslationComposer.RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership(
            visible,
            spans,
            sourceStartIndex + match.Index,
            match.Length);
        DynamicTextObservability.RecordTransform(route, "DisplayName.WithClause", source, translated);
        return true;
    }

    private static bool TryTranslatePlainDisplayNameSuffixSequence(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = PlainDisplayNameSuffixSequencePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var baseGroup = match.Groups["base"];
        var baseSource = baseGroup.Value;
        var translatedBase = TranslateDisplayNameFragmentPreservingColors(baseSource, spans, baseGroup, route);
        var suffixes = match.Groups["suffixes"];
        var suffixEnd = suffixes.Index + suffixes.Length;
        var builder = new StringBuilder(translatedBase);
        var changed = !string.Equals(translatedBase, baseSource, StringComparison.Ordinal);
        var scannedTo = suffixes.Index;

        for (var suffixMatch = DisplayNameTrailingSuffixPattern.Match(source, suffixes.Index);
             suffixMatch.Success && suffixMatch.Index < suffixEnd;
             suffixMatch = suffixMatch.NextMatch())
        {
            scannedTo = suffixMatch.Index + suffixMatch.Length;
            builder.Append(' ');
            if (suffixMatch.Groups["bracket"].Success)
            {
                var stateGroup = suffixMatch.Groups["state"];
                var translatedState = TranslateDisplayNameStatePreservingColors(stateGroup, spans, route);
                if (string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal))
                {
                    builder.Append(RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["bracket"], spans));
                    continue;
                }

                builder.Append(RestoreBracketedDisplayNameSuffix(translatedState, suffixMatch.Groups["bracket"], spans));
                changed = true;
                continue;
            }

            builder.Append(RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["angle"], spans));
        }

        if (scannedTo != suffixEnd || !changed)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            builder.ToString(),
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.PlainSuffixSequence", source, translated);
        return true;
    }

    private static bool TryTranslateNestedLoadedCellBracketedSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = NestedLoadedCellBracketedSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var cellBaseGroup = match.Groups["cellBase"];
        var translatedCellBase = TranslateDisplayNameFragmentPreservingColors(
            cellBaseGroup.Value,
            spans,
            cellBaseGroup,
            route);
        var liquidStateGroup = match.Groups["liquidState"];
        var translatedLiquidState = TryTranslateQuantifiedLiquidStatePreservingCaptureColors(
            liquidStateGroup,
            spans,
            route,
            out var quantifiedLiquidState)
            ? quantifiedLiquidState
            : TranslateDisplayNameStatePreservingColors(liquidStateGroup, spans, route);
        var collectStateGroup = match.Groups["collectState"];
        var translatedCollectState = TranslateDisplayNameStatePreservingColors(collectStateGroup, spans, route);
        if (string.Equals(translatedCellBase, cellBaseGroup.Value, StringComparison.Ordinal)
            && string.Equals(translatedLiquidState, liquidStateGroup.Value, StringComparison.Ordinal)
            && string.Equals(translatedCollectState, collectStateGroup.Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var translatedBracketContent = string.Concat(
            translatedCellBase,
            " ",
            RestoreBracketedDisplayNameSuffix(translatedLiquidState, match.Groups["liquidBracket"], spans),
            " ",
            RestoreBracketedDisplayNameSuffix(translatedCollectState, match.Groups["collectBracket"], spans),
            " ",
            RestoreCompactWeaponSuffixSlice(match.Groups["cellCode"], spans));
        var prefixGroup = match.Groups["prefix"];
        var builder = new StringBuilder(TranslateDisplayNameFragmentPreservingColors(
            prefixGroup.Value,
            spans,
            prefixGroup,
            route));
        builder.Append(' ');
        builder.Append(RestoreBracketedDisplayNameSuffix(translatedBracketContent, match.Groups["bracket"], spans));

        var tail = match.Groups["tail"];
        var tailEnd = tail.Index + tail.Length;
        for (var tailMatch = DisplayNameTrailingSuffixPattern.Match(source, tail.Index);
             tailMatch.Success && tailMatch.Index < tailEnd;
             tailMatch = tailMatch.NextMatch())
        {
            builder.Append(' ');
            builder.Append(RestoreCompactWeaponSuffixSlice(tailMatch.Groups["angle"], spans));
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            builder.ToString(),
            spans,
            source.Length);
        DynamicTextObservability.RecordTransform(route, "DisplayName.NestedLoadedCellBracketedSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateQuantifiedLiquidStatePreservingCaptureColors(
        Group stateGroup,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = QuantifiedLiquidStatePattern.Match(stateGroup.Value);
        if (!match.Success)
        {
            translated = stateGroup.Value;
            return false;
        }

        var liquidGroup = match.Groups["liquid"];
        var liquidSource = liquidGroup.Value;
        var translatedLiquid = TranslateAsciiPhrase(liquidSource);
        if (translatedLiquid is null)
        {
            var direct = Translator.Translate(liquidSource);
            if (string.Equals(direct, liquidSource, StringComparison.Ordinal))
            {
                translated = stateGroup.Value;
                return false;
            }

            translatedLiquid = direct;
        }

        var amountGroup = match.Groups["amount"];
        var restoredLiquid = ColorAwareTranslationComposer.HasColorMarkup(translatedLiquid)
            ? translatedLiquid
            : RestoreVisibleSlice(
                translatedLiquid,
                spans,
                stateGroup.Index + liquidGroup.Index,
                liquidGroup.Length);

        translated = RestoreVisibleSlice(
                amountGroup.Value,
                spans,
                stateGroup.Index + amountGroup.Index,
                amountGroup.Length)
            + "ドラムの"
            + restoredLiquid;

        var liquidStateGroup = match.Groups["state"];
        if (liquidStateGroup.Length > 0)
        {
            var stateSource = RestoreVisibleSlice(
                liquidStateGroup.Value,
                spans,
                stateGroup.Index + liquidStateGroup.Index,
                liquidStateGroup.Length);
            var translatedState = TranslateLiquidVolumeState(stateSource, route);
            if (string.Equals(translatedState, stateSource, StringComparison.Ordinal))
            {
                translated = stateGroup.Value;
                return false;
            }

            translated += "、" + translatedState;
        }

        DynamicTextObservability.RecordTransform(route, "DisplayName.QuantifiedLiquidState", stateGroup.Value, translated);
        return true;
    }

    private static bool TryTranslateWithClauseDisplayNamePrefixPreservingSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        translated = source;
        var withIndex = source.IndexOf(" with ", StringComparison.OrdinalIgnoreCase);
        if (withIndex <= 0)
        {
            return false;
        }

        var baseSource = source.Substring(0, withIndex);
        var tailStart = withIndex + " with ".Length;
        var tail = source.Substring(tailStart);
        var splitIndex = tail.Length;
        while (splitIndex > 0)
        {
            var clause = tail.Substring(0, splitIndex);
            var suffix = tail.Substring(splitIndex);
            if (LooksLikeDisplayNameWithClauseSuffix(suffix))
            {
                var translatedClause = TranslateDisplayNameWithClause(clause, spans, tailStart, splitIndex);
                if (translatedClause is not null)
                {
                    var translatedBase = TranslateDisplayNameFragmentPreservingColors(
                        baseSource,
                        spans,
                        0,
                        baseSource.Length,
                        route);
                    var restoredSuffix = suffix.Length == 0
                        ? string.Empty
                        : TranslateDisplayNameSuffixPreservingStats(
                            RestoreVisibleSlice(suffix, spans, tailStart + splitIndex, suffix.Length),
                            route);

                    translated = translatedBase + "（" + translatedClause + "）" + restoredSuffix;
                    translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                        translated,
                        spans,
                        source.Length);
                    DynamicTextObservability.RecordTransform(route, "DisplayName.WithClausePrefix", source, translated);
                    return true;
                }
            }

            splitIndex = tail.LastIndexOf(' ', splitIndex - 1);
        }

        return false;
    }

    private static bool TryTranslatePairOfDisplayName(string source, string route, out string translated)
    {
        var match = PairOfDisplayNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = match.Groups["target"].Value;
        var translatedTarget = TranslateDisplayNameFragment(target, route);
        if (string.Equals(translatedTarget, target, StringComparison.Ordinal)
            && !IsAlreadyLocalizedDisplayNameTarget(target))
        {
            translated = source;
            return false;
        }

        translated = translatedTarget;
        DynamicTextObservability.RecordTransform(route, "DisplayName.PairOf", source, translated);
        return true;
    }

    private static bool IsAlreadyLocalizedDisplayNameTarget(string source)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(source).Trim();
        return ContainsJapanese(visible) && !EnglishWordPattern.IsMatch(visible);
    }

    private static bool TryTranslateDisguiseClauseDisplayNameSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = DisguiseClauseDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedBase = TranslateDisplayNameFragment(match.Groups["base"].Value, route);
        var targetGroup = match.Groups["target"];
        var translatedClause = targetGroup.Success && targetGroup.Length > 0
            ? TranslateDisplayNameFragment(targetGroup.Value, route) + "の変装"
            : "変装";
        translated = translatedBase + "（" + translatedClause + "）";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.DisguiseClause", source, translated);
        return true;
    }

    private static bool TryTranslateSourceWithClausePrefixPreservingSuffix(string source, string route, out string translated)
    {
        translated = source;
        var withIndex = source.IndexOf(" with ", StringComparison.OrdinalIgnoreCase);
        if (withIndex <= 0)
        {
            return false;
        }

        var baseSource = source.Substring(0, withIndex);
        var tail = source.Substring(withIndex + " with ".Length);
        if (!TryReadSourceWithClause(tail, out var clauseSource, out var suffix))
        {
            return false;
        }

        var translatedClause = TranslateDisplayNameWithClausePreservingSourceMarkup(clauseSource);
        if (translatedClause is null)
        {
            return false;
        }

        var translatedBase = TranslatePreservingColors(baseSource, route);
        var translatedSuffix = TranslateDisplayNameSuffixPreservingStats(suffix, route);
        translated = translatedBase + "（" + translatedClause + "）" + translatedSuffix;
        DynamicTextObservability.RecordTransform(route, "DisplayName.SourceWithClausePrefix", source, translated);
        return true;
    }

    private static string TranslateDisplayNameSuffixPreservingStats(string suffix, string route)
    {
        if (suffix.Length == 0)
        {
            return string.Empty;
        }

        if (StartsWithMarkupSuffixToken(suffix)
            && (StringHelpers.ContainsOrdinal(suffix, "\u001a") || StringHelpers.ContainsOrdinal(suffix, "\u0003")))
        {
            return TranslateBracketedStateInDisplayNameSuffix(suffix, route);
        }

        // A temporary Japanese base lets the compact-stat suffix parsers reuse
        // their normal display-name shape and is removed before returning.
        const string dummyBase = "基底";
        var emptySpans = Array.Empty<ColorSpan>();
        if (TryTranslateCompactWeaponStatsOnlySuffix(suffix, emptySpans, route, out var compactStatsOnlySuffix))
        {
            return compactStatsOnlySuffix;
        }

        if (TryTranslateCompactWeaponStatsDisplayNameSuffixSequence(dummyBase + suffix, emptySpans, route, out var compactStatsSuffixSequence)
            && compactStatsSuffixSequence.StartsWith(dummyBase, StringComparison.Ordinal))
        {
            return compactStatsSuffixSequence.Substring(dummyBase.Length);
        }

        if (TryTranslateCompactWeaponStatsDisplayNameSuffix(dummyBase + suffix, emptySpans, route, out var compactStatsSuffix)
            && compactStatsSuffix.StartsWith(dummyBase, StringComparison.Ordinal))
        {
            return compactStatsSuffix.Substring(dummyBase.Length);
        }

        var translated = TranslatePreservingColors(dummyBase + suffix, route);
        return translated.StartsWith(dummyBase, StringComparison.Ordinal)
            ? translated.Substring(dummyBase.Length)
            : TranslatePreservingColors(suffix, route);
    }

    private static bool StartsWithMarkupSuffixToken(string suffix)
    {
        return suffix.Length >= 3
            && suffix[0] == ' '
            && suffix[1] == '{'
            && suffix[2] == '{';
    }

    private static string TranslateBracketedStateInDisplayNameSuffix(string suffix, string route)
    {
        var bracketStart = suffix.LastIndexOf(" [", StringComparison.Ordinal);
        if (bracketStart < 0 || !suffix.EndsWith("]", StringComparison.Ordinal))
        {
            return suffix;
        }

        var stateStart = bracketStart + 2;
        var state = suffix.Substring(stateStart, suffix.Length - stateStart - 1);
        var translatedState = TranslateDisplayNameState(state, route);
        return string.Equals(translatedState, state, StringComparison.Ordinal)
            ? suffix
            : suffix.Substring(0, stateStart) + translatedState + "]";
    }

    private static bool TryTranslateCompactWeaponStatsOnlySuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var sequenceMatch = CompactWeaponStatsOnlySuffixSequencePattern.Match(source);
        if (sequenceMatch.Success)
        {
            var suffixes = sequenceMatch.Groups["suffixes"];
            var suffixEnd = suffixes.Index + suffixes.Length;
            var builder = new StringBuilder();
            var scannedTo = suffixes.Index;

            for (var suffixMatch = DisplayNameTrailingSuffixPattern.Match(source, suffixes.Index);
                 suffixMatch.Success && suffixMatch.Index < suffixEnd;
                 suffixMatch = suffixMatch.NextMatch())
            {
                scannedTo = suffixMatch.Index + suffixMatch.Length;
                if (suffixMatch.Groups["bracket"].Success)
                {
                    var stateGroup = suffixMatch.Groups["state"];
                    var translatedState = TranslateDisplayNameStatePreservingColors(stateGroup, spans, route);
                    builder.Append(' ');
                    builder.Append(string.Equals(translatedState, stateGroup.Value, StringComparison.Ordinal)
                        ? RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["bracket"], spans)
                        : RestoreBracketedDisplayNameSuffix(translatedState, suffixMatch.Groups["bracket"], spans));
                    continue;
                }

                builder.Append(' ');
                builder.Append(RestoreCompactWeaponSuffixSlice(suffixMatch.Groups["angle"], spans));
            }

            if (scannedTo == suffixEnd)
            {
                translated = " " + RestoreCompactWeaponStatsSlice(sequenceMatch.Groups["stats"], spans) + builder;
                return true;
            }
        }

        var match = CompactWeaponStatsOnlySuffixPattern.Match(source);
        if (match.Success)
        {
            translated = " " + RestoreCompactWeaponStatsSlice(match.Groups["stats"], spans);
            var stateGroup = match.Groups["state"];
            if (stateGroup.Success)
            {
                translated += " "
                    + RestoreBracketedDisplayNameStateSuffix(
                        TranslateDisplayNameStatePreservingColors(stateGroup, spans, route),
                        stateGroup,
                        spans);
            }

            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryReadSourceWithClause(string source, out string clause, out string suffix)
    {
        clause = source;
        suffix = string.Empty;
        if (!source.StartsWith("{{", StringComparison.Ordinal))
        {
            return false;
        }

        var splitIndex = FindQudMarkupEnd(source, 0);
        while (splitIndex > 0 && splitIndex < source.Length)
        {
            clause = source.Substring(0, splitIndex);
            suffix = source.Substring(splitIndex);
            if (suffix.Length > 0 && LooksLikeDisplayNameWithClauseSuffix(suffix))
            {
                return true;
            }

            splitIndex = source.IndexOf(' ', splitIndex + 1);
        }

        return false;
    }

    private static bool LooksLikeDisplayNameWithClauseSuffix(string suffix)
    {
        if (suffix.Length == 0)
        {
            return true;
        }

        return suffix[0] == ' '
            && (suffix.Length >= 2
                && (suffix[1] == '\u001a'
                    || suffix[1] == '\u0003'
                    || suffix[1] == '['
                    || suffix[1] == '<'
                    || suffix.StartsWith(" {{", StringComparison.Ordinal)));
    }

    private static string? TranslateDisplayNameWithClause(string source)
    {
        var direct = TranslateDisplayNameExactOrLowerAscii(source, DisplayNameAdjectiveContext);
        direct ??= TranslateDisplayNameExactOrLowerAscii(source);
        if (direct is not null)
        {
            return direct;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (!string.Equals(stripped, source, StringComparison.Ordinal))
        {
            direct = TranslateDisplayNameExactOrLowerAscii(stripped, DisplayNameAdjectiveContext);
            direct ??= TranslateDisplayNameExactOrLowerAscii(stripped);
            if (direct is not null)
            {
                return direct;
            }
        }

        return null;
    }

    private static string? TranslateDisplayNameWithClause(
        string source,
        IReadOnlyList<ColorSpan> spans,
        int sourceStartIndex,
        int sourceLength)
    {
        var translated = TranslateDisplayNameWithClause(source);
        return translated is null
            ? null
            : RestoreWithClauseSourceMarkup(source, translated, spans, sourceStartIndex, sourceLength);
    }

    private static string? TranslateDisplayNameWithClausePreservingSourceMarkup(string source)
    {
        var translated = TranslateDisplayNameWithClause(source);
        if (translated is null)
        {
            return null;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!string.Equals(stripped, source, StringComparison.Ordinal))
        {
            translated = RestoreWithClauseSourceMarkup(source, translated, spans, 0, stripped.Length);
        }

        return string.Equals(translated, source, StringComparison.Ordinal) ? null : translated;
    }

    private static string RestoreWithClauseSourceMarkup(
        string source,
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int sourceStartIndex,
        int sourceLength)
    {
        if (ColorAwareTranslationComposer.HasColorMarkup(translated))
        {
            return translated;
        }

        if (TryRestoreLeadingWrappedWithClauseComponent(source, translated, out var componentRestored))
        {
            return componentRestored;
        }

        var clauseSpans = ColorCodePreserver.SliceSpans(spans, sourceStartIndex, sourceLength);
        RemoveUnmatchedTrailingSliceClosers(clauseSpans);
        return ColorAwareTranslationComposer.Restore(translated, clauseSpans);
    }

    private static bool TryRestoreLeadingWrappedWithClauseComponent(
        string source,
        string translated,
        out string restored)
    {
        restored = translated;
        if (!source.StartsWith("{{", StringComparison.Ordinal))
        {
            return false;
        }

        var wrappedEnd = FindQudMarkupEnd(source, 0);
        if (wrappedEnd <= 0 || wrappedEnd >= source.Length)
        {
            return false;
        }

        var wrapped = source.Substring(0, wrappedEnd);
        if (!TryReadWrappedModifierVisible(wrapped, out var visible))
        {
            return false;
        }

        var translatedVisible = TranslateDisplayNameExactOrLowerAscii(visible, DisplayNameAdjectiveContext);
        translatedVisible ??= TranslateDisplayNameExactOrLowerAscii(visible);
        if (string.IsNullOrEmpty(translatedVisible)
            || !translated.StartsWith(translatedVisible, StringComparison.Ordinal))
        {
            return false;
        }

        var separator = wrapped.IndexOf('|', 2);
        if (separator <= 2)
        {
            return false;
        }

        var tag = wrapped.Substring(2, separator - 2);
        restored = "{{" + tag + "|" + translatedVisible + "}}" + translated.Substring(translatedVisible!.Length);
        return true;
    }

    private static int FindLocalizedPrefixAsciiTailSeparator(string source)
    {
        for (var index = 0; index < source.Length - 1; index++)
        {
            if (source[index] != ' ')
            {
                continue;
            }

            var next = source[index + 1];
            if ((next >= 'A' && next <= 'Z') || (next >= 'a' && next <= 'z'))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryTranslateGeneratedProperNameModifier(string source, string route, out string translated)
    {
        translated = source;
        var separatorIndex = source.IndexOf(' ');
        if (separatorIndex <= 0 || separatorIndex >= source.Length - 1)
        {
            return false;
        }

        var modifier = source.Substring(0, separatorIndex);
        if (!IsAsciiModifierToken(modifier))
        {
            return false;
        }

        var rest = source.Substring(separatorIndex + 1);
        if (!LooksLikeGeneratedProperName(rest))
        {
            return false;
        }

        var translatedModifier = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            modifier,
            DisplayNameAdjectiveContext,
            DisplayNameDictionaryFiles);
        if (translatedModifier is null)
        {
            return false;
        }

        translated = translatedModifier + rest;
        DynamicTextObservability.RecordTransform(route, "DisplayName.ProperNameModifier", source, translated);
        return true;
    }

    private static bool TryTranslateCyberneticsSchemasoftDisplayName(string source, string route, out string translated)
    {
        var match = CyberneticsSchemasoftDisplayNamePattern.Match(source);
        if (!match.Success
            || !TryTranslateSchemasoftCategory(match.Groups["category"].Value, out var category)
            || !TryTranslateSchemasoftTier(match.Groups["tier"].Value, out var tier))
        {
            translated = source;
            return false;
        }

        translated = "スキーマソフト [" + category + ", " + tier + "]";
        DynamicTextObservability.RecordTransform(
            route,
            "DisplayName.CyberneticsSchemasoft",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateCyclopeanPrismDisplayName(string source, string route, out string translated)
    {
        if (!CyclopeanPrismDisplayNames.TryGetValue(source, out var candidate))
        {
            translated = source;
            return false;
        }

        translated = candidate;
        DynamicTextObservability.RecordTransform(
            route,
            "DisplayName.CyclopeanPrism",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateEvilTwinSpacedPrefixDisplayName(string source, string route, out string translated)
    {
        var match = EvilTwinSpacedPrefixDisplayNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = PrepareGeneratedEvilTwinDisplayNameTarget(match.Groups["target"].Value);
        var translatedTarget = TranslateDisplayNameFragmentPreservingColors(target, route);
        translated = BuildEvilTwinGeneratedDisplayName(match.Groups["prefix"].Value, translatedTarget);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "DisplayName.EvilTwinGeneratedName",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateEvilTwinAntiPrefixDisplayName(string source, string route, out string translated)
    {
        var match = EvilTwinAntiPrefixDisplayNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = PrepareGeneratedEvilTwinDisplayNameTarget(match.Groups["target"].Value);
        var translatedTarget = TranslateDisplayNameFragmentPreservingColors(target, route);
        translated = BuildEvilTwinGeneratedDisplayName("anti-", translatedTarget);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "DisplayName.EvilTwinGeneratedName",
            source,
            translated);
        return true;
    }

    private static string PrepareGeneratedEvilTwinDisplayNameTarget(string source)
    {
        var target = MessageFrameTranslator.StripAllDirectTranslationMarkers(source);
        return StringHelpers.StripLeadingEnglishArticle(
            target,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);
    }

    private static string BuildEvilTwinGeneratedDisplayName(string prefix, string translatedTarget)
    {
        return prefix switch
        {
            "Evil" => "邪悪な" + translatedTarget,
            "Refracted" => "屈折した" + translatedTarget,
            "anti-" => "反" + translatedTarget,
            _ => string.Empty,
        };
    }

    private static bool TryTranslateCyberneticsSchemasoftWrappedDisplayName(string source, string route, out string translated)
    {
        var match = CyberneticsSchemasoftWrappedDisplayNamePattern.Match(source);
        if (!match.Success
            || !TryTranslateSchemasoftCategory(match.Groups["category"].Value, out var category)
            || !TryTranslateSchemasoftTier(match.Groups["tier"].Value, out var tier))
        {
            translated = source;
            return false;
        }

        translated = "{{"
            + match.Groups["outer"].Value
            + "|スキーマソフト [{{"
            + match.Groups["inner"].Value
            + "|"
            + category
            + ", "
            + tier
            + "}}]}}";
        DynamicTextObservability.RecordTransform(
            route,
            "DisplayName.CyberneticsSchemasoft",
            source,
            translated);
        return true;
    }

    private static bool TryTranslateCyberneticsSkillsoftWrappedDisplayName(string source, string route, out string translated)
    {
        var match = CyberneticsSkillsoftWrappedDisplayNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var skillSource = match.Groups["skill"].Value;
        _ = MessageFrameTranslator.TryStripDirectTranslationMarker(skillSource, out skillSource);
        var skill = CharGenProducerTranslationHelpers.TranslateText(skillSource);
        translated = "{{"
            + match.Groups["outer"].Value
            + "|"
            + TranslateSkillsoftKind(match.Groups["kind"].Value)
            + " [{{"
            + match.Groups["inner"].Value
            + "|"
            + skill
            + "}}]}}";
        DynamicTextObservability.RecordTransform(
            route,
            "DisplayName.CyberneticsSkillsoft",
            source,
            translated);
        return true;
    }

    private static string TranslateSkillsoftKind(string source)
    {
        return source switch
        {
            "Skillsoft Plus" => "スキルソフト・プラス",
            _ => "スキルソフト",
        };
    }

    private static bool TryTranslateSchemasoftCategory(string source, out string translated)
    {
        translated = source switch
        {
            "Ammo and Energy Cells" => "弾薬とエネルギーセル",
            "Pistols" => "ピストル",
            "Rifles" => "ライフル",
            "Melee Weapons" => "近接武器",
            "Grenades" => "グレネード",
            "Tonics" => "トニック",
            "Utility" => "ユーティリティ",
            "Armor" => "防具",
            "Heavy Weapons" => "重火器",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static bool TryTranslateSchemasoftTier(string source, out string translated)
    {
        translated = source switch
        {
            "Low Tier" => "下位",
            "Mid Tier" => "中位",
            "High Tier" => "上位",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static bool TryTranslateGeneratedTitleSuffix(string source, string route, out string translated)
    {
        translated = source;
        var match = GeneratedTitleSuffixPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var suffix = match.Groups["suffix"].Value;
        var baseText = match.Groups["base"].Value;
        if (!HasBalancedQudMarkupBoundaryPairs(baseText) || !HasBalancedQudMarkupBoundaryPairs(suffix))
        {
            return false;
        }

        var translatedSuffix = TranslateTitleSuffix(suffix, route);
        if (string.Equals(translatedSuffix, suffix, StringComparison.Ordinal))
        {
            return false;
        }

        string translatedBase;
        if (ColorAwareTranslationComposer.HasColorMarkup(baseText) && !IsWholeQudWrapper(baseText))
        {
            if (!TryTranslateMarkedUpBracketedStateSuffix(baseText, route, out translatedBase)
                && !TryTranslateMarkedUpBracketedStateSuffixSequence(baseText, route, out translatedBase))
            {
                return false;
            }
        }
        else
        {
            translatedBase = TranslateDisplayNameFragmentPreservingColors(baseText, route);
        }

        translated = translatedBase + "、" + translatedSuffix;
        DynamicTextObservability.RecordTransform(route, "DisplayName.TitleSuffix", source, translated);
        return true;
    }

    private static bool TryTranslateGeneratedTitleSuffix(
        string source,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        translated = source;
        var match = GeneratedTitleSuffixPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var suffixGroup = match.Groups["suffix"];
        var suffix = suffixGroup.Value;
        var translatedSuffixOwnsMarkup = TryTranslateWorshipperTitleSuffix(
            suffix,
            spans,
            suffixGroup.Index,
            route,
            out var worshipperTitleSuffix);
        var translatedSuffix = worshipperTitleSuffix;
        if (!translatedSuffixOwnsMarkup)
        {
            translatedSuffixOwnsMarkup = TryTranslateSocialRoleTitleSuffix(
                suffix,
                spans,
                suffixGroup.Index,
                route,
                out translatedSuffix);
        }

        if (!translatedSuffixOwnsMarkup)
        {
            translatedSuffix = TranslateTitleSuffix(suffix, route);
        }

        if (string.Equals(translatedSuffix, suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var baseGroup = match.Groups["base"];
        if (!TryTranslateDisplayNameRouteText(baseGroup.Value, route, out var translatedBase))
        {
            translatedBase = baseGroup.Value;
        }

        translated =
            RestoreWholeSlice(translatedBase, spans, baseGroup) +
            "、" +
            (translatedSuffixOwnsMarkup
                ? translatedSuffix
                : RestoreWholeSlice(translatedSuffix, spans, suffixGroup));
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            source.Length);

        DynamicTextObservability.RecordTransform(route, "DisplayName.TitleSuffix", source, translated);
        return true;
    }

    private static bool HasBalancedQudMarkupBoundaryPairs(string source)
    {
        return CountOccurrences(source, "{{") == CountOccurrences(source, "}}");
    }

    private static bool IsWholeQudWrapper(string source)
    {
        if (!source.StartsWith("{{", StringComparison.Ordinal)
            || !source.EndsWith("}}", StringComparison.Ordinal))
        {
            return false;
        }

        if (CountOccurrences(source, "{{") != 1 || CountOccurrences(source, "}}") != 1)
        {
            return false;
        }

        var pipeIndex = source.IndexOf('|');
        return pipeIndex > 2 && pipeIndex < source.Length - 2;
    }

    private static int CountOccurrences(string source, string token)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }

        return count;
    }

    private static string TranslateTitleSuffix(string suffix, string route)
    {
        var editionMatch = EditionTitleSuffixPattern.Match(suffix);
        if (editionMatch.Success)
        {
            return "第" + editionMatch.Groups["number"].Value + "版";
        }

        if (HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(suffix, out var historicSpiceTitle))
        {
            return historicSpiceTitle;
        }

        var worshipperMatch = WorshipperTitleSuffixPattern.Match(suffix);
        if (worshipperMatch.Success)
        {
            return TranslateWorshipperTitleSuffix(worshipperMatch.Groups["target"].Value, route);
        }

        if (TryTranslateSocialRoleTitleSuffix(suffix, route, out var socialRoleTitle))
        {
            return socialRoleTitle;
        }

        var contextual = TranslateDisplayNameExactOrLowerAscii(suffix, DisplayNameTitleContext);
        if (contextual is not null)
        {
            return contextual;
        }

        var displayName = TranslateDisplayNameExactOrLowerAscii(suffix);
        if (displayName is not null)
        {
            return displayName;
        }

        // SocialRoles titles can carry normal display-name suffixes, for example
        // "Mechanimist convert [sitting]"; keep the title separator owned here
        // while letting the display-name route translate the title fragment.
        if (TryTranslateDisplayNameRouteText(suffix, route, out var generatedTitle))
        {
            return generatedTitle;
        }

        return Translator.Translate(suffix);
    }

    private static bool TryTranslateSocialRoleTitleSuffix(string suffix, string route, out string translated)
    {
        var bracketedMatch = BracketedDisplayNameSuffixPattern.Match(suffix);
        if (bracketedMatch.Success
            && TryTranslateSocialRoleTitleSuffixCore(bracketedMatch.Groups["base"].Value, route, out var translatedBase))
        {
            translated = translatedBase
                + " ["
                + TranslateDisplayNameStatePreservingWholeQudWrapper(bracketedMatch.Groups["state"].Value, route)
                + "]";
            return true;
        }

        return TryTranslateSocialRoleTitleSuffixCore(suffix, route, out translated);
    }

    private static bool TryTranslateSocialRoleTitleSuffix(
        string suffix,
        IReadOnlyList<ColorSpan> spans,
        int suffixStart,
        string route,
        out string translated)
    {
        if (!IsSocialRoleTitleSuffix(suffix))
        {
            translated = suffix;
            return false;
        }

        var suffixSource = RestoreVisibleSliceWithAdjacentBoundary(suffix, spans, suffixStart, suffix.Length);
        return TryTranslateSocialRoleTitleSuffix(suffixSource, route, out translated);
    }

    private static bool IsSocialRoleTitleSuffix(string suffix)
    {
        var bracketedMatch = BracketedDisplayNameSuffixPattern.Match(suffix);
        var candidate = bracketedMatch.Success
            ? bracketedMatch.Groups["base"].Value
            : suffix;

        return FriendToTitleSuffixPattern.IsMatch(candidate)
            || MemberOfTitleSuffixPattern.IsMatch(candidate)
            || PariahToPeopleTitleSuffixPattern.IsMatch(candidate);
    }

    private static bool TryTranslateSocialRoleTitleSuffixCore(string suffix, string route, out string translated)
    {
        var friendMatch = FriendToTitleSuffixPattern.Match(suffix);
        if (friendMatch.Success)
        {
            translated = TranslateSocialRoleTarget(friendMatch.Groups["target"].Value, route) + "の友";
            return true;
        }

        var memberMatch = MemberOfTitleSuffixPattern.Match(suffix);
        if (memberMatch.Success)
        {
            translated = TranslateSocialRoleTarget(memberMatch.Groups["target"].Value, route) + "の一員";
            return true;
        }

        if (PariahToPeopleTitleSuffixPattern.IsMatch(suffix))
        {
            translated = "同胞からの追放者";
            return true;
        }

        translated = suffix;
        return false;
    }

    private static string TranslateSocialRoleTarget(string target, string route)
    {
        var withoutArticle = StringHelpers.StripLeadingEnglishArticle(
            target,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);
        return TranslateDisplayNameFragmentPreservingWholeQudWrapper(withoutArticle, route);
    }

    private static string TranslateWorshipperTitleSuffix(string target, string route)
    {
        if (TryTranslateMarkedUpBracketedStateSuffix(target, route, out var markedUpBracketedTarget))
        {
            return markedUpBracketedTarget + "の崇拝者";
        }

        var bracketedTargetMatch = BracketedDisplayNameSuffixPattern.Match(target);
        if (bracketedTargetMatch.Success)
        {
            return TranslateDisplayNameFragmentPreservingWholeQudWrapper(bracketedTargetMatch.Groups["base"].Value, route)
                + "の崇拝者 ["
                + TranslateDisplayNameStatePreservingWholeQudWrapper(bracketedTargetMatch.Groups["state"].Value, route)
                + "]";
        }

        return TranslateDisplayNameFragmentPreservingWholeQudWrapper(target, route) + "の崇拝者";
    }

    private static bool TryTranslateWorshipperTitleSuffix(
        string suffix,
        IReadOnlyList<ColorSpan> spans,
        int suffixStart,
        string route,
        out string translated)
    {
        var worshipperMatch = WorshipperTitleSuffixPattern.Match(suffix);
        if (!worshipperMatch.Success)
        {
            translated = suffix;
            return false;
        }

        var suffixSource = RestoreVisibleSliceWithAdjacentBoundary(suffix, spans, suffixStart, suffix.Length);
        translated = TranslateTitleSuffix(suffixSource, route);
        return true;
    }

    private static string TranslateDisplayNameFragment(string source, string route)
    {
        if (TryTranslateTrimmedDisplayNameFragment(source, route, out var trimmedTranslated))
        {
            return trimmedTranslated;
        }

        if (TryRestoreLocalizedBlueprintDisplayNameMarkup(source, route, out var blueprintMarkup))
        {
            return blueprintMarkup;
        }

        var alias = TranslateDisplayNameLegacyAliasExact(source);
        if (alias is not null)
        {
            return alias;
        }

        var direct = TranslateDisplayNameExactOrLowerAscii(source);
        if (direct is not null)
        {
            return direct;
        }

        if (IsStableDisplayNameFragment(source, route))
        {
            return source;
        }

        if (TryTranslateDisplayNameRouteText(source, route, out var translated))
        {
            return translated;
        }

        return source;
    }

    private static bool TryRestoreLocalizedBlueprintDisplayNameMarkup(string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrWhiteSpace(source)
            || !JapaneseCharacterPattern.IsMatch(source)
            || ColorAwareTranslationComposer.HasColorMarkup(source))
        {
            return false;
        }

        var trimmed = source.Trim();
        var map = GetLocalizedBlueprintDisplayNameMarkup();
        if (!map.TryGetValue(trimmed, out var restored)
            || string.Equals(restored, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        // Keep the caller's padding while replacing only the trimmed display-name body.
        var leadingWhitespace = source.Substring(0, source.Length - source.TrimStart().Length);
        var trailingWhitespace = source.Substring(source.TrimEnd().Length);
        translated = leadingWhitespace + restored + trailingWhitespace;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LocalizedBlueprintMarkup", source, translated);
        return true;
    }

    private static Dictionary<string, string> GetLocalizedBlueprintDisplayNameMarkup()
    {
        string objectBlueprintRoot;
        try
        {
            objectBlueprintRoot = LocalizationAssetResolver.GetLocalizationPath("ObjectBlueprints");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: failed to resolve ObjectBlueprints localization path: {0}", ex.Message);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        objectBlueprintRoot = Path.GetFullPath(objectBlueprintRoot);
        lock (LocalizedBlueprintDisplayNameMarkupLock)
        {
            if (localizedBlueprintDisplayNameMarkup is not null
                && string.Equals(localizedBlueprintDisplayNameMarkupRoot, objectBlueprintRoot, StringComparison.Ordinal))
            {
                return localizedBlueprintDisplayNameMarkup;
            }

            if (TryLoadLocalizedBlueprintDisplayNameMarkup(objectBlueprintRoot, out var loaded))
            {
                localizedBlueprintDisplayNameMarkup = loaded;
                localizedBlueprintDisplayNameMarkupRoot = objectBlueprintRoot;
                return localizedBlueprintDisplayNameMarkup;
            }

            localizedBlueprintDisplayNameMarkup = loaded;
            localizedBlueprintDisplayNameMarkupRoot = objectBlueprintRoot;
            return localizedBlueprintDisplayNameMarkup;
        }
    }

    private static bool TryLoadLocalizedBlueprintDisplayNameMarkup(
        string objectBlueprintRoot,
        out Dictionary<string, string> result)
    {
        result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(objectBlueprintRoot))
        {
            Trace.TraceWarning("QudJP: ObjectBlueprints localization directory does not exist: {0}", objectBlueprintRoot);
            return false;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(objectBlueprintRoot, "*.jp.xml");
            Array.Sort(files, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: failed to list ObjectBlueprints localization files in '{0}': {1}", objectBlueprintRoot, ex.Message);
            return false;
        }

        var hadFileFailure = false;
        var ambiguousVisibleNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in files)
        {
            try
            {
                var document = XDocument.Load(path, LoadOptions.None);
                foreach (var element in document.Descendants("part"))
                {
                    var displayName = element.Attribute("DisplayName")?.Value;
                    if (displayName is null
                        || string.IsNullOrWhiteSpace(displayName)
                        || !ColorAwareTranslationComposer.HasColorMarkup(displayName))
                    {
                        continue;
                    }

                    var (visible, _) = ColorAwareTranslationComposer.Strip(displayName);
                    if (string.IsNullOrWhiteSpace(visible)
                        || ambiguousVisibleNames.Contains(visible))
                    {
                        continue;
                    }

                    if (result.TryGetValue(visible, out var existingDisplayName))
                    {
                        if (!string.Equals(existingDisplayName, displayName, StringComparison.Ordinal))
                        {
                            result.Remove(visible);
                            ambiguousVisibleNames.Add(visible);
                        }

                        continue;
                    }

                    result.Add(visible, displayName);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("QudJP: failed to read localized blueprint display names from '{0}': {1}", path, ex.Message);
                hadFileFailure = true;
            }
        }

        return !hadFileFailure || result.Count > 0;
    }

    private static string TranslateDisplayNameFragmentPreservingWholeQudWrapper(string source, string route)
    {
        return TryTranslateWholeQudWrapper(source, inner => TranslateDisplayNameFragment(inner, route), out var translated)
            ? translated
            : TranslateDisplayNameFragmentPreservingColors(source, route);
    }

    private static string TranslateDisplayNameStatePreservingWholeQudWrapper(string source, string route)
    {
        return TryTranslateWholeQudWrapper(source, inner => TranslateDisplayNameState(inner, route), out var translated)
            ? translated
            : TranslateMarkedUpDisplayNameState(source, route);
    }

    private static bool TryTranslateWholeQudWrapper(
        string source,
        Func<string, string> translateInner,
        out string translated)
    {
        translated = source;
        if (!IsWholeQudWrapper(source))
        {
            return false;
        }

        var pipeIndex = source.IndexOf('|');
        if (pipeIndex <= 2 || pipeIndex >= source.Length - 2)
        {
            return false;
        }

        var inner = source.Substring(pipeIndex + 1, source.Length - pipeIndex - 3);
        var translatedInner = translateInner(inner);
        if (string.Equals(translatedInner, inner, StringComparison.Ordinal))
        {
            return false;
        }

        translated = source.Substring(0, pipeIndex + 1) + translatedInner + "}}";
        return true;
    }

    private static bool TryTranslateTrimmedDisplayNameFragment(string source, string route, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();
        if (trimmed.Length == 0 || trimmed.Length == source.Length)
        {
            return false;
        }

        var translatedTrimmed = TranslateDisplayNameFragment(trimmed, route);
        if (string.Equals(translatedTrimmed, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        translated =
            source.Substring(0, leadingLength) +
            translatedTrimmed +
            source.Substring(source.Length - trailingLength, trailingLength);
        return true;
    }

    private static string TranslateDisplayNameState(string source, string route)
    {
        if (IsAlreadyLocalizedDisplayNameStateText(source))
        {
            return source;
        }

        if (TryTranslateFlywheelDisplayNameState(source, route, out var flywheelState))
        {
            return flywheelState;
        }

        if (TryTranslateBracketedStateExact(source, out var exact))
        {
            return exact;
        }

        if (TryTranslateQuantifiedLiquidState(source, route, out var quantifiedLiquid))
        {
            return quantifiedLiquid;
        }

        if (TryTranslateDisplayNameStateTemplate(source, route, out var translated))
        {
            return translated;
        }

        if (TryTranslateGeneratedDisplayNameState(source, route, out var generatedState))
        {
            return generatedState;
        }

        var direct = TranslateAsciiTokenWithCaseFallback(source);
        if (direct is not null)
        {
            return direct;
        }

        return source;
    }

    private static string TranslateDisplayNameStatePreservingColors(
        Group stateGroup,
        IReadOnlyList<ColorSpan> spans,
        string route)
    {
        return TryTranslateLoadedEnergyCellDisplayNameState(stateGroup, spans, route, out var loadedCellState)
            ? loadedCellState
            : RestoreWholeSlice(TranslateDisplayNameState(stateGroup.Value, route), spans, stateGroup);
    }

    private static bool TryTranslateLoadedEnergyCellDisplayNameState(
        Group stateGroup,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = LoadedEnergyCellDisplayNameStatePattern.Match(stateGroup.Value);
        if (!match.Success)
        {
            translated = stateGroup.Value;
            return false;
        }

        var cellSource = RestoreStateComponent(stateGroup, match.Groups["cell"], spans);
        var translatedCell = ColorAwareTranslationComposer.TranslatePreservingColors(
            cellSource,
            visible => TranslateDisplayNameFragment(visible, route));
        translatedCell = ColorizeLoadedEnergyCellNameIfBare(translatedCell);

        var chargeWithParensSource = RestoreStateComponent(stateGroup, match.Groups["chargeWithParens"], spans);
        var translatedChargeWithParens = ColorAwareTranslationComposer.TranslatePreservingColors(
            chargeWithParensSource,
            visible =>
            {
                if (visible.Length < 2 || visible[0] != '(' || visible[visible.Length - 1] != ')')
                {
                    return visible;
                }

                var charge = visible.Substring(1, visible.Length - 2);
                if (TryStripDirectTranslationMarkerFromChargeStatus(charge, out var strippedCharge))
                {
                    charge = strippedCharge;
                }

                return EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(charge, out var translatedCharge)
                    ? "(" + translatedCharge + ")"
                    : "(" + charge + ")";
            });
        translatedChargeWithParens = ColorizeLoadedEnergyCellChargeIfBare(translatedChargeWithParens);

        var codeSource = ColorizeRawAngleCodeSuffix(RestoreStateComponent(stateGroup, match.Groups["code"], spans), semanticColors: true);

        translated = translatedCell + " " + translatedChargeWithParens + " " + codeSource;
        return !string.Equals(translated, stateGroup.Value, StringComparison.Ordinal);
    }

    private static string ColorizeLoadedEnergyCellNameIfBare(string source)
    {
        return ColorAwareTranslationComposer.HasColorMarkup(source) || source.Length == 0
            ? source
            : "{{c|" + source + "}}";
    }

    private static string ColorizeLoadedEnergyCellChargeIfBare(string source)
    {
        if (ColorAwareTranslationComposer.HasColorMarkup(source)
            || source.Length < 3
            || source[0] != '('
            || source[source.Length - 1] != ')')
        {
            return source;
        }

        return "{{y|({{G|" + source.Substring(1, source.Length - 2) + "}})}}";
    }

    private static string RestoreStateComponent(Group stateGroup, Group componentGroup, IReadOnlyList<ColorSpan> spans)
    {
        return RestoreVisibleSlice(
            componentGroup.Value,
            spans,
            stateGroup.Index + componentGroup.Index,
            componentGroup.Length);
    }

    private static bool TryTranslateGeneratedDisplayNameState(string source, string route, out string translated)
    {
        var timedMatch = TimedDisplayNameStatePattern.Match(source);
        if (timedMatch.Success)
        {
            return GeneratedDisplayNameStateTranslated(
                source,
                route,
                timedMatch.Groups["count"].Value + "秒",
                out translated);
        }

        var cookingServingsMatch = CookingServingsDisplayNameStatePattern.Match(source);
        if (cookingServingsMatch.Success)
        {
            return GeneratedDisplayNameStateTranslated(
                source,
                route,
                "調理" + cookingServingsMatch.Groups["count"].Value + "回分",
                out translated);
        }

        var energyCellsMatch = EnergyCellsDisplayNameStatePattern.Match(source);
        if (energyCellsMatch.Success)
        {
            return GeneratedDisplayNameStateTranslated(
                source,
                route,
                "セル" + energyCellsMatch.Groups["count"].Value + "個",
                out translated);
        }

        var chapterMatch = ChapterDisplayNameStatePattern.Match(source);
        if (chapterMatch.Success)
        {
            var translatedOwner = TranslateDisplayNameStateTarget(chapterMatch.Groups["owner"].Value, route);
            return GeneratedDisplayNameStateTranslated(source, route, translatedOwner + "支部", out translated);
        }

        var displayName = TranslateDisplayNameFragment(source, route);
        if (!string.Equals(displayName, source, StringComparison.Ordinal))
        {
            return GeneratedDisplayNameStateTranslated(source, route, displayName, out translated);
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateFlywheelDisplayNameState(string source, string route, out string translated)
    {
        var flywheelMatch = FlywheelDisplayNameStatePattern.Match(source);
        if (!flywheelMatch.Success)
        {
            translated = source;
            return false;
        }

        var status = flywheelMatch.Groups["status"].Value;
        if (!TryStripDirectTranslationMarkerFromChargeStatus(status, out var strippedStatus))
        {
            strippedStatus = status;
        }

        if (!EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(strippedStatus, out var translatedStatus))
        {
            translatedStatus = strippedStatus;
        }

        translated = "フライホイール: " + translatedStatus;
        DynamicTextObservability.RecordTransform(route, "DisplayName.FlywheelState", source, translated);
        return true;
    }

    private static bool TryTranslateLiquidVolumeStateLeaf(string source, out string translated)
    {
        if (string.Equals(source, "sealed", StringComparison.OrdinalIgnoreCase))
        {
            translated = "密封";
            return true;
        }

        if (string.Equals(source, "empty", StringComparison.OrdinalIgnoreCase))
        {
            translated = "空";
            return true;
        }

        if (string.Equals(source, "broken", StringComparison.OrdinalIgnoreCase))
        {
            translated = "破損";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool GeneratedDisplayNameStateTranslated(
        string source,
        string route,
        string value,
        out string translated)
    {
        translated = value;
        DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedState", source, translated);
        return true;
    }

    private static bool TryTranslateQuantifiedLiquidState(string source, string route, out string translated)
    {
        var match = QuantifiedLiquidStatePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var liquidSource = match.Groups["liquid"].Value;
        var translatedLiquid = TranslateAsciiPhrase(liquidSource);
        if (translatedLiquid is null)
        {
            var direct = Translator.Translate(liquidSource);
            if (string.Equals(direct, liquidSource, StringComparison.Ordinal))
            {
                translated = source;
                return false;
            }

            translatedLiquid = direct;
        }

        translated = match.Groups["amount"].Value + "ドラムの" + translatedLiquid;
        var state = match.Groups["state"].Value;
        if (state.Length > 0)
        {
            var translatedState = TranslateLiquidVolumeState(state, route);
            if (string.Equals(translatedState, state, StringComparison.Ordinal))
            {
                translated = source;
                return false;
            }

            translated += "、" + translatedState;
        }

        DynamicTextObservability.RecordTransform(route, "DisplayName.QuantifiedLiquidState", source, translated);
        return true;
    }

    private static string TranslateLiquidVolumeState(string source, string route)
    {
        if (TryTranslateLiquidVolumeStateLeaf(source, out var translated))
        {
            return translated;
        }

        if (ColorAwareTranslationComposer.HasColorMarkup(source))
        {
            var colorAware = ColorAwareTranslationComposer.TranslatePreservingColors(
                source,
                visible =>
                {
                    if (TryTranslateLiquidVolumeStateLeaf(visible, out var translatedVisible))
                    {
                        return translatedVisible;
                    }

                    return TranslateDisplayNameState(visible, route);
                });
            if (!string.Equals(colorAware, source, StringComparison.Ordinal))
            {
                return colorAware;
            }
        }

        return TranslateDisplayNameState(source, route);
    }

    private static bool TryTranslateLiquidState(string source, string route, out string translated)
    {
        var match = LiquidStatePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var liquidSource = match.Groups["liquid"].Value;
        var translatedLiquid = TranslateAsciiPhrase(liquidSource);
        if (translatedLiquid is null)
        {
            var direct = Translator.Translate(liquidSource);
            if (string.Equals(direct, liquidSource, StringComparison.Ordinal))
            {
                translated = source;
                return false;
            }

            translatedLiquid = direct;
        }

        var state = match.Groups["state"].Value;
        var translatedState = TranslateDisplayNameState(state, route);
        if (string.Equals(translatedState, state, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedLiquid + "、" + translatedState;
        DynamicTextObservability.RecordTransform(route, "DisplayName.LiquidState", source, translated);
        return true;
    }

    private static bool TryTranslateGeneratedCanvasTentName(string source, string route, out string translated)
    {
        var match = GeneratedCanvasTentPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if (!TryTranslateGeneratedCanvasTentComponents(match.Groups["body"].Value, out var translatedCreature, out var translatedMaterial))
        {
            translated = source;
            return false;
        }

        var translatedTent = TranslateDisplayNameExactOrLowerAscii("tent", GeneratedCanvasTentComponentContext);
        if (translatedTent is null)
        {
            translated = source;
            return false;
        }

        translated = translatedCreature + "の" + translatedMaterial + "の" + translatedTent;
        DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedCanvasTent", source, translated);
        return true;
    }

    private static bool TryTranslateGeneratedCanvasTentComponents(
        string body,
        out string translatedCreature,
        out string translatedMaterial)
    {
        for (var splitIndex = body.LastIndexOf(' '); splitIndex > 0; splitIndex = body.LastIndexOf(' ', splitIndex - 1))
        {
            var creature = body.Substring(0, splitIndex);
            var material = body.Substring(splitIndex + 1);
            var creatureTranslation = TranslateDisplayNameComponentPhrase(creature, GeneratedCanvasTentComponentContext);
            var materialTranslation = TranslateDisplayNameComponentPhrase(material, GeneratedCanvasTentComponentContext);
            if (creatureTranslation is null || materialTranslation is null)
            {
                continue;
            }

            translatedCreature = creatureTranslation;
            translatedMaterial = materialTranslation;
            return true;
        }

        translatedCreature = string.Empty;
        translatedMaterial = string.Empty;
        return false;
    }

    private static bool TryTranslateGeneratedRandomStatueName(string source, string route, out string translated)
    {
        var match = GeneratedRandomStatuePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        if (!TryTranslateRandomStatueMaterialPhrase(match.Groups["material"].Value, out var modifierPrefix, out var translatedMaterial))
        {
            translated = source;
            return false;
        }

        var translatedStatue = TranslateDisplayNameExactOrLowerAscii("statue", GeneratedRandomStatueComponentContext);
        if (translatedStatue is null)
        {
            translated = source;
            return false;
        }

        var subject = StringHelpers.StripLeadingEnglishArticle(
            match.Groups["subject"].Value,
            includeCapitalizedDefiniteArticle: true);
        var translatedSubject = TranslateDisplayNameFragmentPreservingColors(subject, route);
        translated = modifierPrefix + translatedSubject + "の" + translatedMaterial + "の" + translatedStatue;
        DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedRandomStatue", source, translated);
        return true;
    }

    private static bool TryTranslateGeneratedEnglishPrefixDisplayName(string source, string route, out string translated)
    {
        var match = GeneratedEnglishPrefixDisplayNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = MessageFrameTranslator.StripAllDirectTranslationMarkers(match.Groups["target"].Value);
        target = StringHelpers.StripLeadingEnglishArticle(
            target,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);

        if (TryTranslateGeneratedEnglishPrefixTargetWithSuffix(target, route, out var translatedTargetWithSuffix))
        {
            translated = BuildGeneratedEnglishPrefixDisplayName(
                match.Groups["prefix"].Value,
                translatedTargetWithSuffix.TranslatedTarget) + translatedTargetWithSuffix.TranslatedSuffix;
            if (string.Equals(translated, source, StringComparison.Ordinal))
            {
                return false;
            }

            DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedEnglishPrefix", source, translated);
            return true;
        }

        if (TryTranslateGeneratedTitleSuffix(target, route, out var translatedTitleTarget))
        {
            translated = BuildGeneratedEnglishPrefixDisplayName(match.Groups["prefix"].Value, translatedTitleTarget);
            if (string.Equals(translated, source, StringComparison.Ordinal))
            {
                return false;
            }

            DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedEnglishPrefix", source, translated);
            return true;
        }

        var translatedTarget = TranslateDisplayNameFragmentPreservingColors(target, route);

        translated = BuildGeneratedEnglishPrefixDisplayName(match.Groups["prefix"].Value, translatedTarget);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "DisplayName.GeneratedEnglishPrefix", source, translated);
        return true;
    }

    private static (string TranslatedTarget, string TranslatedSuffix) TranslateGeneratedEnglishPrefixTargetSuffix(
        Group baseGroup,
        Group stateGroup,
        string opening,
        string closing,
        string route)
    {
        return (
            TranslateDisplayNameFragmentPreservingColors(baseGroup.Value, route),
            " " + opening + TranslateDisplayNameState(stateGroup.Value, route) + closing);
    }

    private static bool TryTranslateGeneratedEnglishPrefixTargetWithSuffix(
        string target,
        string route,
        out (string TranslatedTarget, string TranslatedSuffix) translated)
    {
        var bracketedMatch = BracketedDisplayNameSuffixPattern.Match(target);
        if (bracketedMatch.Success)
        {
            translated = TranslateGeneratedEnglishPrefixTargetSuffix(
                bracketedMatch.Groups["base"],
                bracketedMatch.Groups["state"],
                "[",
                "]",
                route);
            return true;
        }

        var parenthesizedMatch = ParenthesizedDisplayNameSuffixPattern.Match(target);
        if (parenthesizedMatch.Success)
        {
            translated = TranslateGeneratedEnglishPrefixTargetSuffix(
                parenthesizedMatch.Groups["base"],
                parenthesizedMatch.Groups["state"],
                "(",
                ")",
                route);
            return true;
        }

        translated = default;
        return false;
    }

    private static string BuildGeneratedEnglishPrefixDisplayName(string prefix, string translatedTarget)
    {
        return prefix switch
        {
            "advertisement for" => translatedTarget + "の広告",
            "clone of" => translatedTarget + "のクローン",
            "hologram of" => translatedTarget + "のホログラム",
            "phylactery of" => translatedTarget + "のファイラクテリー",
            "mural of" => translatedTarget + "の壁画",
            "ruined mural of" => translatedTarget + "の崩れた壁画",
            "shrine to" => translatedTarget + "の祠",
            "villagers of" => translatedTarget + "の村人",
            "Cult of" => translatedTarget + "教団",
            _ => string.Empty,
        };
    }

    private static bool TryTranslateRandomStatueMaterialPhrase(
        string source,
        out string modifierPrefix,
        out string translatedMaterial)
    {
        modifierPrefix = string.Empty;
        translatedMaterial = string.Empty;

        var parts = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var material = parts[parts.Length - 1];
        var materialTranslation = TranslateDisplayNameComponentPhrase(material, GeneratedRandomStatueComponentContext);
        if (materialTranslation is null)
        {
            return false;
        }

        if (parts.Length == 1)
        {
            translatedMaterial = materialTranslation;
            return true;
        }

        var modifier = string.Join(" ", parts, 0, parts.Length - 1);
        var modifierTranslation = TranslateDisplayNameComponentPhrase(modifier, GeneratedRandomStatueComponentContext);
        if (modifierTranslation is null)
        {
            return false;
        }

        modifierPrefix = modifierTranslation;
        translatedMaterial = materialTranslation;
        return true;
    }

    private static bool TryTranslateBracketedStateExact(string source, out string translated)
    {
        var bracketed = "[" + source + "]";
        var direct = ScopedDictionaryLookup.TranslateExactOrLowerAscii(bracketed, DisplayNameDictionaryFiles);
        if (direct is null)
        {
            using var _ = Translator.PushMissingKeyLoggingSuppression(true);
            var global = Translator.Translate(bracketed);
            if (!string.Equals(global, bracketed, StringComparison.Ordinal))
            {
                direct = global;
            }
        }

        if (direct is null)
        {
            translated = source;
            return false;
        }

        translated = UnwrapSingleBracketPair(direct);
        return true;
    }

    private static bool TryTranslateDisplayNameStateTemplate(string source, string route, out string translated)
    {
        var match = PrepositionalStateTemplatePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = match.Groups["template"].Value + " {0}";
        var translatedTemplate = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
            templateKey,
            DisplayNameStateTemplateContext,
            DisplayNameStateTemplateDictionaryFile);
        if (translatedTemplate is null)
        {
            translated = source;
            return false;
        }

        var translatedTarget = TranslateDisplayNameStateTarget(match.Groups["target"].Value, route);
        translated = translatedTemplate.Replace("{0}", translatedTarget);
        DynamicTextObservability.RecordTransform(route, "DisplayName.BracketedStateTemplate", source, translated);
        return true;
    }

    private static string TranslateDisplayNameStateTarget(string source, string route)
    {
        var target = StringHelpers.StripLeadingEnglishArticle(source);
        return TranslateDisplayNameFragment(target, route);
    }

    private static string UnwrapSingleBracketPair(string source)
    {
        if (source.Length >= 2
            && source[0] == '['
            && source[source.Length - 1] == ']')
        {
            return source.Substring(1, source.Length - 2);
        }

        return source;
    }

    private static string RestoreVisibleSlice(Group group, IReadOnlyList<ColorSpan> spans)
    {
        return RestoreVisibleSlice(group.Value, spans, group.Index, group.Length);
    }

    private static string RestoreVisibleSlice(
        string value,
        IReadOnlyList<ColorSpan> spans,
        int startIndex,
        int length)
    {
        var sliceSpans = ColorCodePreserver.SliceSpans(spans, startIndex, length);
        RemoveUnmatchedTrailingSliceClosers(sliceSpans);

        return ColorAwareTranslationComposer.Restore(
            value,
            sliceSpans);
    }

    private static string RestoreVisibleSliceWithAdjacentBoundary(
        string value,
        IReadOnlyList<ColorSpan> spans,
        int startIndex,
        int length)
    {
        var sliceSpans = ColorCodePreserver.SliceSpans(spans, startIndex, length);
        sliceSpans.AddRange(ColorCodePreserver.SliceAdjacentCaptureBoundarySpans(spans, startIndex, length));
        RemoveUnmatchedTrailingSliceClosers(sliceSpans);

        return ColorAwareTranslationComposer.Restore(
            value,
            sliceSpans);
    }

    private static string RestoreCompactWeaponStatsSlice(Group group, IReadOnlyList<ColorSpan> spans)
    {
        return ColorizeRawCompactWeaponStats(RestoreVisibleSlice(group, spans));
    }

    private static string RestoreCompactWeaponSuffixSlice(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var restored = RestoreVisibleSlice(group, spans);
        restored = ColorizeRawBracketSuffix(restored);
        return ColorizeRawAngleCodeSuffix(restored, semanticColors: false);
    }

    private static string RestoreSemanticAngleCodeSuffixSlice(Group group, IReadOnlyList<ColorSpan> spans)
    {
        return ColorizeRawAngleCodeSuffix(RestoreVisibleSlice(group, spans), semanticColors: true);
    }

    private static string RestoreBracketedDisplayNameStateSuffix(
        string translatedState,
        Group stateGroup,
        IReadOnlyList<ColorSpan> spans)
    {
        if (stateGroup.Index <= 0)
        {
            return "[" + translatedState + "]";
        }

        return ColorAwareTranslationComposer.RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership(
            "[" + translatedState + "]",
            spans,
            stateGroup.Index - 1,
            stateGroup.Length + 2);
    }

    private static string RestoreBracketedDisplayNameSuffix(
        string translatedState,
        Group bracketGroup,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership(
            "[" + translatedState + "]",
            spans,
            bracketGroup.Index,
            bracketGroup.Length);
    }

    private static string ColorizeRawCompactWeaponStats(string source)
    {
        if (!StringHelpers.ContainsOrdinal(source, "\u001a") && !StringHelpers.ContainsOrdinal(source, "\u0003"))
        {
            return source;
        }

        var builder = new StringBuilder(source.Length + 16);
        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            if (current == '\u001a' && !IsAlreadyTaggedControlSymbol(source, index, "{{c|"))
            {
                builder.Append("{{c|\u001a}}");
                continue;
            }

            if (current == '\u0003' && !IsAlreadyTaggedControlSymbol(source, index, "{{r|"))
            {
                builder.Append("{{r|\u0003}}");
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool IsAlreadyTaggedControlSymbol(string source, int index, string prefix)
    {
        return (index >= prefix.Length
            && index + 2 < source.Length
            && string.Compare(source, index - prefix.Length, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0
            && source[index + 1] == '}'
            && source[index + 2] == '}')
            || IsAlreadyTaggedControlSymbol(source, index);
    }

    private static bool IsAlreadyTaggedControlSymbol(string source, int index)
    {
        if (index + 2 >= source.Length || source[index + 1] != '}' || source[index + 2] != '}')
        {
            return false;
        }

        var openingIndex = source.LastIndexOf("{{", index, StringComparison.Ordinal);
        if (openingIndex < 0)
        {
            return false;
        }

        var priorClosingIndex = source.LastIndexOf("}}", index, StringComparison.Ordinal);
        return priorClosingIndex < openingIndex
            && ContainsInRange(source, '|', openingIndex, index);
    }

    private static bool ContainsInRange(string source, char value, int startIndex, int endIndexExclusive)
    {
        for (var index = startIndex; index < endIndexExclusive; index++)
        {
            if (source[index] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static string ColorizeRawAngleCodeSuffix(string source, bool semanticColors)
    {
        source = NormalizeTransparentAngleCodeWrapper(source);
        if (source.Length < 3
            || source[0] != '<'
            || source[source.Length - 1] != '>'
            || ContainsCharacter(source, '{'))
        {
            return source;
        }

        var code = source.Substring(1, source.Length - 2);
        if (code.Length == 0)
        {
            return source;
        }

        var builder = new StringBuilder(source.Length + (code.Length * 6));
        builder.Append("{{y|<");
        for (var index = 0; index < code.Length; index++)
        {
            var current = code[index];
            if (current >= '0' && current <= '9')
            {
                builder.Append("{{");
                builder.Append(semanticColors ? GetAngleCodeDigitColor(current) : "g");
                builder.Append('|');
                builder.Append(current);
                builder.Append("}}");
                continue;
            }

            if ((current >= 'A' && current <= 'Z') || (current >= 'a' && current <= 'z'))
            {
                builder.Append("{{");
                builder.Append(semanticColors ? GetAngleCodeLetterColor(current) : "B");
                builder.Append('|');
                builder.Append(current);
                builder.Append("}}");
                continue;
            }

            builder.Append(current);
        }

        builder.Append(">}}");
        return builder.ToString();
    }

    private static string GetAngleCodeLetterColor(char source)
    {
        return char.ToUpperInvariant(source) switch
        {
            'A' => "R",
            'B' => "G",
            'C' => "B",
            'D' => "C",
            _ => "B",
        };
    }

    private static string GetAngleCodeDigitColor(char source)
    {
        return source switch
        {
            '1' => "r",
            '2' => "g",
            '3' => "b",
            '4' => "c",
            _ => "g",
        };
    }

    private static string NormalizeTransparentAngleCodeWrapper(string source)
    {
        return source.IndexOf("<{{|", StringComparison.Ordinal) < 0
            ? source
            : TransparentAngleCodeWrapperPattern.Replace(source, "<${inner}>");
    }

    private static string ColorizeRawBracketSuffix(string source)
    {
        return source.Length >= 3
            && source[0] == '['
            && source[source.Length - 1] == ']'
            && !ContainsCharacter(source, '{')
            && JapaneseCharacterPattern.IsMatch(source)
            ? "{{y|" + source + "}}"
            : source;
    }

    private static bool ContainsCharacter(string source, char value)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveUnmatchedTrailingSliceClosers(List<ColorSpan> spans)
    {
        var balance = 0;
        for (var index = 0; index < spans.Count; index++)
        {
            var token = spans[index].Token;
            if (IsExplicitClosingBoundaryToken(token))
            {
                balance--;
            }
            else if (ColorCodePreserver.IsOpeningBoundaryToken(token))
            {
                balance++;
            }
        }

        for (var index = spans.Count - 1; index >= 0 && balance < 0; index--)
        {
            if (!IsExplicitClosingBoundaryToken(spans[index].Token))
            {
                continue;
            }

            spans.RemoveAt(index);
            balance++;
        }
    }

    private static bool IsExplicitClosingBoundaryToken(string token)
    {
        return string.Equals(token, "}}", StringComparison.Ordinal)
            || string.Equals(token, "</color>", StringComparison.OrdinalIgnoreCase);
    }

    private static string RestoreWholeSlice(string translated, IReadOnlyList<ColorSpan> spans, Group group)
    {
        return ColorAwareTranslationComposer.RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            group.Index,
            group.Length);
    }

    private static string TranslateDisplayNameFragmentPreservingColors(
        string source,
        string route)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TranslateDisplayNameFragment(visible, route));
    }

    private static string TranslateDisplayNameFragmentPreservingColors(
        string source,
        IReadOnlyList<ColorSpan> spans,
        Group group,
        string route)
    {
        return TranslateDisplayNameFragmentPreservingColors(source, spans, group.Index, group.Length, route);
    }

    private static string TranslateDisplayNameFragmentPreservingColors(
        string source,
        IReadOnlyList<ColorSpan> spans,
        int startIndex,
        int length,
        string route)
    {
        if (TryTranslateWithClauseDisplayName(source, spans, startIndex, route, out var withClause))
        {
            return withClause;
        }

        var translated = TranslateDisplayNameFragment(source, route);
        return string.Equals(translated, source, StringComparison.Ordinal)
            ? RestoreVisibleSlice(source, spans, startIndex, length)
            : ColorAwareTranslationComposer.RestoreWholeSliceBoundaryWrappersPreservingTranslatedOwnership(
                translated,
                spans,
                startIndex,
                length);
    }

    private static bool IsStableDisplayNameFragment(string source, string route)
    {
        if (UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(source, route))
        {
            return true;
        }

        return !EnglishWordPattern.IsMatch(source);
    }

    private static bool IsStableDisplayNameState(string source)
    {
        return !EnglishWordPattern.IsMatch(source);
    }

    private static bool IsAsciiModifierToken(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || character == '-'
                || character == '\'')
            {
                continue;
            }

            return false;
        }

        return source.Length > 0;
    }

    private static bool LooksLikeLocalizedLiquidDisplayNameHead(string source)
    {
        return source.EndsWith("水たまり", StringComparison.Ordinal)
            || source.EndsWith("池", StringComparison.Ordinal);
    }

    private static bool IsBareLiquidDisplayNameHead(string source)
    {
        return string.Equals(source, "水たまり", StringComparison.Ordinal)
            || string.Equals(source, "池", StringComparison.Ordinal);
    }

    private static bool HeadAlreadyContainsLiquid(string head, IReadOnlyList<string> translatedLiquids)
    {
        for (var index = 0; index < translatedLiquids.Count; index++)
        {
            if (HeadAlreadyContainsLiquid(head, translatedLiquids[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HeadAlreadyContainsLiquid(string head, string translatedLiquid)
    {
        var visibleHead = ColorAwareTranslationComposer.GetVisibleText(head).Trim();
        var visibleLiquid = ColorAwareTranslationComposer.GetVisibleText(translatedLiquid).Trim();
        return visibleLiquid.Length > 0
            && visibleHead.StartsWith(visibleLiquid + "の", StringComparison.Ordinal);
    }

    private static bool LooksLikeAsciiPhrase(string source)
    {
        var hasLetter = false;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z'))
            {
                hasLetter = true;
                continue;
            }

            if (character == ' ' || character == '-' || character == '\'')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }

    private static string? TranslateAsciiPhrase(string source)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, LiquidPhraseDictionaryFiles);
        if (scoped is not null)
        {
            return scoped;
        }

        scoped = TryTranslateColoredLiquidToken(source);
        if (scoped is not null)
        {
            return scoped;
        }

        using var __ = Translator.PushMissingKeyLoggingSuppression(true);
        var direct = Translator.Translate(source);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        return TranslateAsciiPhraseByParts(source);
    }

    private static string? TranslateAsciiPhraseByParts(string source)
    {
        var parts = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < parts.Length; index++)
        {
            var translatedPart = TranslateAsciiPhrasePart(parts[index]);
            if (string.Equals(translatedPart, parts[index], StringComparison.Ordinal))
            {
                return null;
            }

            builder.Append(translatedPart);
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildLiquidComparableCandidates(string source)
    {
        var candidates = new List<string>();
        AddCandidate(candidates, TranslateAsciiPhraseByParts(source));
        AddCandidate(candidates, LiquidVolumeFragmentTranslator.TranslateLiquidPhrase(source));
        AddCandidate(candidates, TranslateAsciiPhrase(source));
        return candidates;
    }

    private static void AddCandidate(List<string> candidates, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            if (string.Equals(candidates[index], candidate, StringComparison.Ordinal))
            {
                return;
            }
        }

        candidates.Add(candidate!);
    }

    private static string TranslateAsciiPhrasePart(string source)
    {
        var translatedPart = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, LiquidPhraseDictionaryFiles);
        if (translatedPart is not null)
        {
            return translatedPart;
        }

        translatedPart = TryTranslateColoredLiquidToken(source);
        if (translatedPart is not null)
        {
            return translatedPart;
        }

        if (ColorAwareTranslationComposer.HasColorMarkup(source))
        {
            var translatedWithMarkup = ColorAwareTranslationComposer.TranslatePreservingColors(
                source,
                visible =>
                {
                    var visibleTranslation = ScopedDictionaryLookup.TranslateExactOrLowerAscii(visible, LiquidPhraseDictionaryFiles);
                    if (visibleTranslation is not null)
                    {
                        return visibleTranslation;
                    }

                    return Translator.Translate(visible);
                });
            if (!string.Equals(translatedWithMarkup, source, StringComparison.Ordinal))
            {
                return translatedWithMarkup;
            }
        }

        return Translator.Translate(source);
    }

    private static string? TranslateDisplayNameComponentPhrase(string source, string context)
    {
        var scoped = TranslateDisplayNameExactOrLowerAscii(source, context);
        return scoped ?? TranslateAsciiPhrase(source);
    }

    private static string? TryTranslateColoredLiquidToken(string source)
    {
        if (!LooksLikeAsciiPhrase(source) || HasAsciiSpace(source))
        {
            return null;
        }

        return ScopedDictionaryLookup.TranslateExactOrLowerAscii("{{C|" + source + "}}", LiquidPhraseDictionaryFiles);
    }

    private static bool HasAsciiSpace(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == ' ')
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeGeneratedProperName(string source)
    {
        var hasUppercase = false;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (character >= 'A' && character <= 'Z')
            {
                hasUppercase = true;
                continue;
            }

            if ((character >= 'a' && character <= 'z')
                || character == '-'
                || character == '\''
                || character == ' '
                || character == ',')
            {
                continue;
            }

            return false;
        }

        return hasUppercase;
    }

    private static string GetModifierRestSeparator(string modifier, string source)
    {
        var visibleModifier = ColorAwareTranslationComposer.GetVisibleText(modifier);
        if (CompoundStainedModifierPattern.IsMatch(visibleModifier)
            || StringHelpers.ContainsOrdinal(visibleModifier, "-and-"))
        {
            return string.Empty;
        }

        if (SingleStainedModifierPattern.IsMatch(visibleModifier))
        {
            return string.Empty;
        }

        if (LooksLikeGeneratedProperName(source))
        {
            return string.Empty;
        }

        if (LooksLikeGeneratedProperNameHead(source))
        {
            return string.Empty;
        }

        var bracketedMatch = BracketedDisplayNameSuffixPattern.Match(source);
        if (bracketedMatch.Success
            && (LooksLikeGeneratedProperName(bracketedMatch.Groups["base"].Value)
                || LooksLikeGeneratedProperNameHead(bracketedMatch.Groups["base"].Value)))
        {
            return string.Empty;
        }

        var parenthesizedMatch = ParenthesizedDisplayNameSuffixPattern.Match(source);
        if (parenthesizedMatch.Success
            && (LooksLikeGeneratedProperName(parenthesizedMatch.Groups["base"].Value)
                || LooksLikeGeneratedProperNameHead(parenthesizedMatch.Groups["base"].Value)))
        {
            return string.Empty;
        }

        return ShouldSpaceAfterModifier(modifier)
            ? " "
            : string.Empty;
    }

    private static bool ShouldSpaceAfterModifier(string modifier)
    {
        if (CompoundStainedModifierPattern.IsMatch(ColorAwareTranslationComposer.GetVisibleText(modifier)))
        {
            return false;
        }

        if (modifier.StartsWith("{{", StringComparison.Ordinal)
            || modifier.StartsWith("[{{", StringComparison.Ordinal))
        {
            return true;
        }

        var key = modifier;
        var levelMatch = DisplayNameModifierLevelSuffixPattern.Match(modifier);
        if (levelMatch.Success)
        {
            key = levelMatch.Groups["modifier"].Value;
        }

        return SpacedDisplayNameModifierKeys.Contains(key);
    }

    private static bool LooksLikeGeneratedProperNameHead(string source)
    {
        return source.Length > 0
            && source[0] >= 'A'
            && source[0] <= 'Z'
            && (ContainsCharacter(source, ',')
                || ContainsCharacter(source, '、'));
    }

    private static string? TranslateAsciiTokenWithCaseFallback(string source)
    {
        return TranslateDisplayNameExactOrLowerAscii(source);
    }

    private static bool TryTranslateExactDisplayNameLookup(string source, string route, out string translated)
    {
        var direct = TranslateDisplayNameLegacyAliasExact(source);
        if (direct is null)
        {
            direct = TranslateDisplayNameExactOrLowerAscii(source);
        }
        if (direct is not null)
        {
            translated = direct;
            DynamicTextObservability.RecordTransform(route, "DisplayName.ExactLookup", source, translated);
            return true;
        }
        translated = source;
        return false;
    }

    private static bool TryTranslateTrimmedDisplayNameLookup(string source, string route, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();
        if (trimmed.Length == 0 || trimmed.Length == source.Length)
        {
            return false;
        }

        var trimmedTranslation = TranslateDisplayNameLegacyAliasExact(trimmed);
        if (trimmedTranslation is null)
        {
            trimmedTranslation = TranslateDisplayNameExactOrLowerAscii(trimmed);
        }
        if (trimmedTranslation is null)
        {
            return false;
        }

        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        translated =
            source.Substring(0, leadingLength) +
            trimmedTranslation +
            source.Substring(source.Length - trailingLength, trailingLength);
        DynamicTextObservability.RecordTransform(route, "DisplayName.TrimmedLookup", source, translated);
        return true;
    }

    private static string? TryTranslateDisplayNameScopedExact(string source)
    {
        return ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, DisplayNameDictionaryFiles);
    }

    private static string? TranslateDisplayNameLegacyAliasExact(string source)
    {
        return ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, DisplayNameLegacyAliasDictionaryFiles);
    }

    private static string? TranslateDisplayNameExactOrLowerAscii(string source)
    {
        return TranslateDisplayNameExactOrLowerAscii(source, context: null);
    }

    private static string? TranslateDisplayNameExactOrLowerAscii(string source, string? context)
    {
        var scoped = context is null
            ? TryTranslateDisplayNameScopedExact(source)
            : ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(source, context, DisplayNameDictionaryFiles);
        if (scoped is not null)
        {
            return scoped;
        }

        if (context is null)
        {
            return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
                ? translated
                : null;
        }

        return StringHelpers.TranslateExactOrLowerAscii(source, context);
    }

    private static string? TranslateDisplayNameModifier(string source)
    {
        return TranslateDisplayNameModifierCore(source, allowGlobalFallback: true);
    }

    private static string? TranslateDisplayNameModifierCore(string source, bool allowGlobalFallback)
    {
        var directSource = TranslateDisplayNameModifierExact(source);
        if (directSource is not null)
        {
            return directSource;
        }

        var bracketWrapped = source.Length >= 2
            && source[0] == '['
            && source[source.Length - 1] == ']';
        var core = bracketWrapped
            ? source.Substring(1, source.Length - 2)
            : source;

        var direct = TranslateDisplayNameModifierExact(core);
        if (direct is null && bracketWrapped && TryReadWrappedModifierVisible(core, out var visible))
        {
            var bracketedVisible = "[" + visible + "]";
            var bracketedDirect = TranslateDisplayNameExactOrLowerAscii(bracketedVisible, DisplayNameAdjectiveContext);
            if (bracketedDirect is not null)
            {
                return bracketedDirect;
            }
        }

        if (direct is null)
        {
            if (!allowGlobalFallback)
            {
                return null;
            }

            var global = Translator.Translate(source);
            if (string.Equals(global, source, StringComparison.Ordinal))
            {
                return null;
            }

            return global;
        }

        return bracketWrapped
            ? "[" + direct + "]"
            : direct;
    }

    private static string? TranslateDisplayNameModifierExact(string source)
    {
        string? direct = null;
        if (TryTranslateMarkupWrappedDisplayNameModifier(source, out var wrappedDirect))
        {
            direct = wrappedDirect;
        }

        if (direct is null)
        {
            direct = TryTranslateCompoundStainedDisplayNameModifier(source);
        }
        if (direct is null && IsSingleStainedModifierWithMarkupLiquid(source))
        {
            direct = TryTranslateSingleStainedDisplayNameModifier(source);
        }
        if (direct is null)
        {
            direct = TranslateDisplayNameExactOrLowerAscii(source, DisplayNameAdjectiveContext);
        }
        if (direct is null)
        {
            direct = TryTranslateSingleStainedDisplayNameModifier(source);
        }
        if (direct is null)
        {
            direct = TranslateLeveledDisplayNameModifier(source);
        }

        return direct;
    }

    private static string? TryTranslateSingleStainedDisplayNameModifier(string source)
    {
        var match = SingleStainedModifierPattern.Match(source);
        if (!match.Success
            || !TryTranslateStainedLiquidComponent(match.Groups["liquid"].Value, out var liquid))
        {
            return null;
        }

        if (ColorAwareTranslationComposer.GetVisibleText(liquid).Trim().Length == 0)
        {
            return null;
        }

        return liquid + "に染まった";
    }

    private static bool IsSingleStainedModifierWithMarkupLiquid(string source)
    {
        var match = SingleStainedModifierPattern.Match(source);
        return match.Success
            && match.Groups["liquid"].Value.StartsWith("{{", StringComparison.Ordinal);
    }

    private static string? TryTranslateCompoundStainedDisplayNameModifier(string source)
    {
        var match = CompoundStainedModifierPattern.Match(source);
        if (!match.Success
            || !TryTranslateStainedLiquidComponent(match.Groups["left"].Value, out var left)
            || !TryTranslateStainedLiquidComponent(match.Groups["right"].Value, out var right))
        {
            return null;
        }

        // LiquidStained.cs can combine any two liquid stain names as A-and-B-stained.
        return left + "と" + right + "で汚れた";
    }

    private static bool TryTranslateStainedLiquidComponent(string source, out string translated)
    {
        var (visibleRaw, sourceSpans) = ColorAwareTranslationComposer.Strip(source);
        var visible = visibleRaw.Trim();
        var direct = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext(
                visible,
                "XRL.Liquids",
                LiquidPhraseDictionaryFiles);
        if (direct is null)
        {
            direct = TranslateDisplayNameExactOrLowerAscii(visible, DisplayNameAdjectiveContext);
        }
        if (direct is null)
        {
            direct = TranslateAsciiPhrase(visible);
        }
        if (direct is null)
        {
            direct = LiquidVolumeFragmentTranslator.TranslateLiquidPhrase(visible);
        }

        if (direct is null)
        {
            translated = source;
            return false;
        }

        var translatedVisible = ColorAwareTranslationComposer.GetVisibleText(direct).Trim();
        if (translatedVisible.EndsWith("の", StringComparison.Ordinal))
        {
            translatedVisible = translatedVisible.Substring(0, translatedVisible.Length - 1);
        }

        if (translatedVisible.Length == 0)
        {
            translated = source;
            return false;
        }

        translated = sourceSpans.Count == 0
            ? translatedVisible
            : ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translatedVisible,
                sourceSpans,
                visibleRaw.Length);
        return true;
    }

    private static string? TranslateLeveledDisplayNameModifier(string source)
    {
        var match = DisplayNameModifierLevelSuffixPattern.Match(source);
        if (!match.Success)
        {
            return null;
        }

        var translatedModifier = TranslateDisplayNameExactOrLowerAscii(
            match.Groups["modifier"].Value,
            DisplayNameAdjectiveContext);
        return translatedModifier is null
            ? null
            : translatedModifier + "(" + match.Groups["level"].Value + ")";
    }

    private static bool TryTranslateMarkupWrappedDisplayNameModifier(string source, out string translated)
    {
        translated = source;
        if (!TryReadWrappedModifierVisible(source, out var visible))
        {
            return false;
        }

        var separator = source.IndexOf('|', 2);
        var tag = source.Substring(2, separator - 2);
        var direct = TranslateDisplayNameExactOrLowerAscii(visible, DisplayNameAdjectiveContext);
        if (direct is null)
        {
            return false;
        }

        translated = ColorAwareTranslationComposer.HasColorMarkup(direct)
            ? direct
            : "{{" + tag + "|" + direct + "}}";
        return true;
    }

    private static bool TryReadWrappedModifierVisible(string source, out string visible)
    {
        visible = string.Empty;
        if (!source.StartsWith("{{", StringComparison.Ordinal) || !source.EndsWith("}}", StringComparison.Ordinal))
        {
            return false;
        }

        var separator = source.IndexOf('|', 2);
        if (separator <= 2)
        {
            return false;
        }

        visible = source.Substring(separator + 1, source.Length - separator - 3);
        return visible.Length > 0;
    }

    private static bool IsAlreadyLocalizedBracketedDisplayName(string source)
    {
        var match = BracketedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var baseText = match.Groups["base"].Value;
        return ContainsJapanese(baseText)
            && !EnglishWordPattern.IsMatch(baseText)
            && IsAlreadyLocalizedDisplayNameStateText(match.Groups["state"].Value);
    }

    private static bool IsAlreadyLocalizedParenthesizedDisplayName(string source)
    {
        var match = ParenthesizedDisplayNameSuffixPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var baseText = match.Groups["base"].Value;
        return ContainsJapanese(baseText)
            && !EnglishWordPattern.IsMatch(baseText)
            && IsAlreadyLocalizedDisplayNameStateText(match.Groups["state"].Value);
    }

    private static bool ContainsJapanese(string source)
    {
        return !string.IsNullOrEmpty(source) && JapaneseCharacterPattern.IsMatch(source);
    }

    private static bool IsAlreadyLocalizedBracketLabel(string source)
    {
        return source.Length >= 2
            && source[0] == '['
            && source[source.Length - 1] == ']'
            && ContainsJapanese(source);
    }
}
