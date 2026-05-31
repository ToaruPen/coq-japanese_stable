using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DescriptionTextTranslator
{
    private static readonly Regex FactionDispositionPattern =
        new Regex("^(?<relation>Loved by|Admired by|Hated by|Disliked by) (?<target>.+?)(?: for (?<reason>.+?))?\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LabeledListPattern =
        new Regex("^(?<label>Physical features:|Equipped:|身体的特徴:|装備:) (?<items>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BrainDispositionLinePattern =
        new Regex("^(?<label>Base demeanor:|Engagement style:) (?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex VillageDispositionTargetPattern =
        new Regex("^(?:the|The) villagers of (?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StatAbbreviationPattern =
        new Regex("^[A-Z]{2,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SignedStatAbbreviationPattern =
        new Regex("^[+-]\\d+\\s+[A-Z]{2,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttributeTermPattern =
        new Regex("^(?:Strength|Toughness|Willpower|Agility|Ego|Intelligence)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SignedAttributeTermPattern =
        new Regex("^[+-]\\d+\\s+(?:Strength|Toughness|Willpower|Agility|Ego|Intelligence)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AsciiLetterPattern =
        new Regex("[A-Za-z]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PreservedWeightUnitPattern =
        new Regex("(?:\\.lbs|lbs\\.)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedLocalizedEnglishTokenPattern =
        new Regex("(?<![A-Za-z])(?:AV|DV|HP|MA|PV|Qud|Quickness|SP|XP)(?![A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AddsCookingEffectsPattern =
        new Regex("^Adds (?<effect>.+?) effects to cooked meals\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RegainsChargeWhenWornOrHeldPattern =
        new Regex("^Regains charge when worn(?: or |または)held in hand, much more quickly while in combat\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MakersMarkDescriptionPattern =
        new Regex("^(?:(?<markPrefix>.+?):\\s*|:\\s*)?(?<subject>This|These|That|Those) (?<category>.+?) (?<verb>bears|bear) the (?<mark>mark|marks) of (?<crafter>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex VillageHistoryTattooPattern =
        new Regex("^(?<owner>Its|its|Your|your|His|his|Her|her|Their|their) (?<part>.+?) (?<verb>bears|bear) (?<kind>a tattoo|tattoos|an engraving|engravings) of a scene from the history of the village (?<village>.+?)(?<body>: .+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MultipleAmmoUsedPerShotPattern =
        new Regex("^Multiple ammo used per shot: (?<count>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MultipleProjectilesPerShotPattern =
        new Regex("^Multiple projectiles per shot: (?<count>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HistoricNarrativeLinePattern =
        new Regex(
            "^(?:In\\s+.+?\\s+(?:BR|AR),|Early\\s+in\\s+.+?\\s+(?:BR|AR),|Late\\s+in\\s+.+?\\s+(?:BR|AR),|At\\s+.+?,|Around\\s+.+?,|Through(?:out)?\\s+.+?,|Sometime\\s+in\\s+.+?,|While\\s+.+?,|Deep\\s+in\\s+.+?,|Acting\\s+against\\s+.+?,|Near\\s+the\\s+location\\s+of\\s+.+?,)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TombMuralWrapperPattern =
        new Regex(
            "^The tomb mural depicts a significant event from the life of the (?<ancient>ancient )?sultan (?<sultan>.+?):\\n\\n(?<body>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TombMuralHeaderPattern =
        new Regex(
            "^The tomb mural depicts a significant event from the life of the (?<ancient>ancient )?sultan (?<sultan>.+?):$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HistoricSceneHeaderPattern =
        new Regex(
            "^(?<kind>Painted|Engraved): This item is (?:painted|engraved) with a scene from the life of the ancient (?<subject>.+):$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SplitHistoricSceneHeaderStartPattern =
        new Regex(
            "^(?<kind>Painted|Engraved): This item is (?:painted|engraved) with a scene from the life of the ancient (?<subjectPrefix>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SplitTombMuralHeaderStartPattern =
        new Regex(
            "^The tomb mural depicts a significant event from the life of the (?<ancient>ancient )?sultan (?<sultanPrefix>.*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SplitVillageHistoryHeaderStartPattern =
        new Regex(
            "^(?<kind>Painted|Engraved|Holographic): (?:(?:This object is (?:painted|engraved) with)|(?:This hologram depicts)) a scene from the history of the village (?<villagePrefix>.*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StuckInStateLinePattern =
        new Regex("^stuck in (?:the |a |an )?(?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ResistanceLinePattern =
        new Regex("^\\+(?<amount>\\d+) (?<element>Heat|Cold|Electrical|Acid) Resistance$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ToHitLinePattern =
        new Regex("^\\+(?<amount>\\d+) to hit$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WallPenetrationLinePattern =
        new Regex("^(?<powered>When powered, )?(?<amount>[+-]\\d+) penetration vs\\. walls\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DestroysWallsLinePattern =
        new Regex("^(?<powered>When powered, )?(?:D|d)estroys\\s+walls after (?<hits>\\d+) penetrating hits?\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MoveSpeedLinePattern =
        new Regex("^(?<amount>[+-]\\d+) move speed$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CarryCapacityBonusLinePattern =
        new Regex("^\\+(?<amount>\\d+)% carry capacity$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EnergyCostReductionLinePattern =
        new Regex("^Provides (?<amount>\\d+)% reduction in (?<scope>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BroadcastPowerReceiverLinePattern =
        new Regex("^This object has a broadcast power receiver that can pick up electrical charge(?<satellite> either from satellites if not too far underground or)? from a nearby broadcast power transmitter\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DamageReflectionLinePattern =
        new Regex("^Reflects (?<amount>\\d+)% damage back at your attackers, rounded up\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FightingLinePattern =
        new Regex("^Fighting (?:a |an |the )?(?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RuntimeObservedRandomStatueLinePattern =
        new Regex("^(?<material>[A-Za-z][A-Za-z -]+?) で作られた細やかな彫像で、(?:a |an |the )?(?<subject>.+?) を表現している。(?<rest>.*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WaterBondedLinePattern =
        new Regex("^You are water-bonded with (?<target>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PoweredOffLinePattern =
        new Regex("^.+? (?:is|are) powered off\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StatAdjustLinePattern =
        new Regex(
            "^(?<activated>When activated, )?(?<amount>[+-]\\d+)(?<percent>%?) (?<stat>Strength|Toughness|Willpower|Agility|Ego|Intelligence|quickness|hit points|move speed|acid resistance|cold resistance|electric resistance|heat resistance|AV|DV|MA|PV)(?<suffix>（[^）]+）)?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ItReadsLinePattern =
        new Regex("^It reads, '(?<text>.+)'\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StartupReadoutLinePattern =
        new Regex("^Its readout indicates that its startup sequence will take an estimated (?<rounds>\\d+) more rounds?\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LostChanceReducedLinePattern =
        new Regex("^Chance of becoming lost reduced by (?<amount>\\d+)%\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DisarmedSuffixPattern =
        new Regex("^(?<body>.+) It's been disarmed\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BleedingOnPenetrationPattern =
        new Regex(
            "^On penetration, this weapon causes bleeding: (?<damage>\\d+) damage per round; save difficulty (?<difficulty>\\d+)\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SwarmAlphaPattern =
        new Regex(
            "^Swarm Alpha: As long as this creature is adjacent to (?<possessive>\\S+) target, (?<subject>\\S+) grants? \\+?(?<bonus>\\d+) to the swarm bonuses of each other swarmer who is adjacent to \\k<possessive> target\\.$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SwarmerPattern =
        new Regex(
            "^Swarmer: This creature receives \\+1 to hit in melee and \\+1 to penetration rolls for each other hostile swarmer beyond the first who is in another square adjacent to (?<possessive>\\S+) target\\. \\(currently (?<current>[+-]?\\d+)\\)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Keep TranslateShortDescription and TranslateLongDescription separate even though they
    // currently delegate to TranslateDescriptionText, so short/long description routes can
    // diverge later without changing their patch call sites.
    internal static string TranslateShortDescription(string source, string route)
    {
        return TranslateDescriptionText(source, route);
    }

    internal static string TranslateLongDescription(string source, string route)
    {
        return TranslateDescriptionText(source, route);
    }

    private static string TranslateDescriptionText(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (TryTranslateSegmentPreservingColors(
            source,
            route,
            allowMessagePatternTranslation: source.IndexOf('\n') < 0,
            allowGenericLeafTranslation: source.IndexOf('\n') < 0,
            out var wholeTranslated))
        {
            return wholeTranslated;
        }

        if (source.IndexOf('\n') < 0)
        {
            return source;
        }

        var newline = source.Contains("\r\n") ? "\r\n" : "\n";
        var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var changed = false;
        string? activeBoundaryToken = null;
        string? pendingHistoryHeaderSuffix = null;
        string? pendingHistoryHeaderOuterBoundaryToken = null;
        for (var index = 0; index < lines.Length; index++)
        {
            if (pendingHistoryHeaderSuffix is not null
                && TryTranslateSplitHistoryHeaderContinuationLine(
                    lines[index],
                    pendingHistoryHeaderSuffix,
                    out var continuedHeaderLine))
            {
                lines[index] = continuedHeaderLine;
                activeBoundaryToken = pendingHistoryHeaderOuterBoundaryToken;
                pendingHistoryHeaderSuffix = null;
                pendingHistoryHeaderOuterBoundaryToken = null;
                changed = true;
                continue;
            }

            if (TryTranslateSplitHistoryHeaderStartLine(
                    lines[index],
                    out var translatedHeaderStartLine,
                    out pendingHistoryHeaderSuffix,
                    out pendingHistoryHeaderOuterBoundaryToken))
            {
                lines[index] = translatedHeaderStartLine;
                changed = true;
                continue;
            }

            if (!TryTranslatePossiblySplitColorLine(lines[index], route, ref activeBoundaryToken, out var translatedLine))
            {
                continue;
            }

            lines[index] = translatedLine;
            changed = true;
        }

        return changed ? string.Join(newline, lines) : source;
    }

    private static bool TryTranslateSplitHistoryHeaderStartLine(
        string source,
        out string translated,
        out string? continuationSuffix,
        out string? outerBoundaryToken)
    {
        translated = source;
        continuationSuffix = null;
        outerBoundaryToken = null;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (stripped.EndsWith(":", StringComparison.Ordinal))
        {
            return false;
        }

        var match = SplitHistoricSceneHeaderStartPattern.Match(stripped);
        if (match.Success)
        {
            var subjectPrefix = TranslateHistoricSceneSubjectPrefix(match.Groups["subjectPrefix"].Value);
            var isEngraved = string.Equals(match.Groups["kind"].Value, "Engraved", StringComparison.Ordinal);
            var visible = (isEngraved ? "彫刻" : "彩色") + ": この品には古代の" + subjectPrefix;
            continuationSuffix = isEngraved
                ? "の生涯の一場面が彫り刻まれている:"
                : "の生涯の一場面が描かれている:";

            translated = RewrapSplitHistoryHeaderStartLine(source, visible, spans, stripped.Length);
            outerBoundaryToken = TryGetLeadingBoundaryOpeningToken(source);
            return true;
        }

        match = SplitTombMuralHeaderStartPattern.Match(stripped);
        if (match.Success)
        {
            var ancientPrefix = match.Groups["ancient"].Success ? "古代の" : string.Empty;
            var sultanPrefix = TranslateTombMuralSultanNamePrefix(match.Groups["sultanPrefix"].Value);
            translated = RewrapSplitHistoryHeaderStartLine(
                source,
                "墓所の壁画には、" + ancientPrefix + "スルタン " + sultanPrefix,
                spans,
                stripped.Length);
            continuationSuffix = "の生涯における重要な出来事が描かれている:";
            outerBoundaryToken = TryGetLeadingBoundaryOpeningToken(source);
            return true;
        }

        match = SplitVillageHistoryHeaderStartPattern.Match(stripped);
        if (match.Success)
        {
            var kind = match.Groups["kind"].Value;
            var villagePrefix = match.Groups["villagePrefix"].Value;
            var isEngraved = string.Equals(kind, "Engraved", StringComparison.Ordinal);
            var visible = string.Equals(kind, "Holographic", StringComparison.Ordinal)
                ? "このホログラムには" + villagePrefix
                : "この物体には" + villagePrefix;
            continuationSuffix = isEngraved
                ? "村の歴史の一場面が彫り刻まれている:"
                : "村の歴史の一場面が描かれている:";

            translated = RewrapSplitHistoryHeaderStartLine(source, visible, spans, stripped.Length);
            outerBoundaryToken = TryGetLeadingBoundaryOpeningToken(source);
            return true;
        }

        return false;
    }

    private static string TranslateHistoricSceneSubjectPrefix(string source)
    {
        var hasTrailingSpace = source.EndsWith(" ", StringComparison.Ordinal);
        var trimmed = source.TrimEnd();
        string translated;
        if (trimmed.StartsWith("sultan ", StringComparison.Ordinal))
        {
            translated = "スルタン " + trimmed.Substring("sultan ".Length);
        }
        else
        {
            translated = StringHelpers.TryGetTranslationExactOrLowerAscii(trimmed, out var exact)
                ? exact
                : trimmed;
        }

        return hasTrailingSpace ? translated + " " : translated;
    }

    private static string TranslateTombMuralSultanNamePrefix(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var hasTrailingSpace = source.EndsWith(" ", StringComparison.Ordinal);
        var trimmed = source.TrimEnd();
        var translated = TranslateTombMuralSultanName(trimmed);
        return hasTrailingSpace ? translated + " " : translated;
    }

    private static string RewrapSplitHistoryHeaderStartLine(
        string source,
        string visible,
        IReadOnlyList<ColorSpan>? spans,
        int sourceLength)
    {
        var restored = RestoreWholeLineBoundaryWrappers(visible, spans, sourceLength);
        var leading = CollectLeadingBoundaryOpeningTokens(source);
        if (leading.Length > 0 && !restored.StartsWith(leading, StringComparison.Ordinal))
        {
            restored = leading + restored;
        }

        if (TryGetTrailingBoundaryOpeningToken(source, out var trailing)
            && !restored.EndsWith(trailing, StringComparison.Ordinal))
        {
            restored += trailing;
        }

        return restored;
    }

    private static string? TryGetLeadingBoundaryOpeningToken(string source)
    {
        if (source.StartsWith("{{", StringComparison.Ordinal))
        {
            var pipeIndex = source.IndexOf('|');
            return pipeIndex > 1 ? source.Substring(0, pipeIndex + 1) : null;
        }

        if (source.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
        {
            var closeIndex = source.IndexOf('>');
            return closeIndex > "<color=".Length ? source.Substring(0, closeIndex + 1) : null;
        }

        return null;
    }

    private static string CollectLeadingBoundaryOpeningTokens(string source)
    {
        var index = 0;
        var tokens = new List<string>();
        while (index < source.Length)
        {
            if (source.IndexOf("{{", index, StringComparison.Ordinal) == index)
            {
                var pipeIndex = source.IndexOf('|', index + 2);
                if (pipeIndex < 0)
                {
                    break;
                }

                tokens.Add(source.Substring(index, (pipeIndex - index) + 1));
                index = pipeIndex + 1;
                continue;
            }

            if (source.IndexOf("<color=", index, StringComparison.OrdinalIgnoreCase) == index)
            {
                var closeIndex = source.IndexOf('>', index + "<color=".Length);
                if (closeIndex < 0)
                {
                    break;
                }

                tokens.Add(source.Substring(index, (closeIndex - index) + 1));
                index = closeIndex + 1;
                continue;
            }

            if (index + 1 < source.Length
                && (source[index] == '&' || source[index] == '^')
                && source[index + 1] != source[index])
            {
                tokens.Add(source.Substring(index, 2));
                index += 2;
                continue;
            }

            break;
        }

        return string.Concat(tokens);
    }

    private static bool TryGetTrailingBoundaryOpeningToken(string source, out string token)
    {
        token = string.Empty;
        if (!TryFindDanglingBoundaryOpening(source, out var danglingToken))
        {
            return false;
        }

        if (!source.TrimEnd().EndsWith(danglingToken, StringComparison.Ordinal))
        {
            return false;
        }

        token = danglingToken;
        return true;
    }

    private static bool TryTranslateSplitHistoryHeaderContinuationLine(
        string source,
        string continuationSuffix,
        out string translated)
    {
        translated = source;
        var colonIndex = source.LastIndexOf(':');
        if (colonIndex < 0)
        {
            return false;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(source);
        if (!stripped.EndsWith(":", StringComparison.Ordinal))
        {
            return false;
        }

        translated = source.Substring(0, colonIndex) + continuationSuffix;
        return true;
    }

    private static bool TryTranslatePossiblySplitColorLine(
        string source,
        string route,
        ref string? activeBoundaryToken,
        out string translated)
    {
        var syntheticPrefix = string.Empty;
        var syntheticSuffix = string.Empty;
        var lineClosesActiveBoundary = false;
        var danglingOpenToken = string.Empty;

        if (!string.IsNullOrEmpty(activeBoundaryToken))
        {
            syntheticPrefix = activeBoundaryToken!;
            if (HasColorBoundaryClosing(source, activeBoundaryToken!))
            {
                lineClosesActiveBoundary = true;
            }
        }

        if (HasColorBoundaryOpening(source) && TryFindDanglingBoundaryOpening(source, out danglingOpenToken))
        {
            syntheticSuffix = GetSyntheticClosingToken(danglingOpenToken);
        }

        if (!string.IsNullOrEmpty(activeBoundaryToken) && !lineClosesActiveBoundary)
        {
            syntheticSuffix += GetSyntheticClosingToken(activeBoundaryToken!);
        }

        var sourceForTranslation = syntheticPrefix + source + syntheticSuffix;
        if (!TryTranslateSegmentPreservingColors(
            sourceForTranslation,
            route,
            allowMessagePatternTranslation: true,
            allowGenericLeafTranslation: true,
            out var translatedWithSyntheticBoundaries))
        {
            if (lineClosesActiveBoundary)
            {
                activeBoundaryToken = null;
            }
            else if (!string.IsNullOrEmpty(danglingOpenToken))
            {
                activeBoundaryToken = danglingOpenToken;
            }

            translated = source;
            return false;
        }

        if (syntheticPrefix.Length > 0
            && translatedWithSyntheticBoundaries.StartsWith(syntheticPrefix, StringComparison.Ordinal))
        {
            translatedWithSyntheticBoundaries = translatedWithSyntheticBoundaries.Substring(syntheticPrefix.Length);
        }

        if (syntheticSuffix.Length > 0
            && translatedWithSyntheticBoundaries.EndsWith(syntheticSuffix, StringComparison.Ordinal))
        {
            translatedWithSyntheticBoundaries = translatedWithSyntheticBoundaries.Substring(
                0,
                translatedWithSyntheticBoundaries.Length - syntheticSuffix.Length);
        }

        if (lineClosesActiveBoundary)
        {
            activeBoundaryToken = null;
        }
        else if (!string.IsNullOrEmpty(danglingOpenToken))
        {
            activeBoundaryToken = danglingOpenToken;
        }

        translated = translatedWithSyntheticBoundaries;
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool TryFindDanglingBoundaryOpening(string source, out string token)
    {
        token = string.Empty;
        var (_, spans) = ColorAwareTranslationComposer.Strip(source);
        var stack = new Stack<string>();
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (IsSelfContainedBoundaryToken(span.Token))
            {
                continue;
            }

            if (ColorCodePreserver.IsClosingBoundaryToken(span.Token))
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }

                continue;
            }

            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                stack.Push(span.Token);
            }
        }

        if (stack.Count > 0)
        {
            token = stack.Peek();
            return true;
        }

        var openingIndex = source.LastIndexOf("{{", StringComparison.Ordinal);
        if (openingIndex < 0)
        {
            return false;
        }

        var pipeIndex = source.IndexOf('|', openingIndex);
        if (pipeIndex < 0)
        {
            return false;
        }

        var closingIndex = source.IndexOf("}}", pipeIndex + 1, StringComparison.Ordinal);
        if (closingIndex >= 0)
        {
            return false;
        }

        token = source.Substring(openingIndex, (pipeIndex - openingIndex) + 1);
        return true;
    }

    private static bool HasColorBoundaryOpening(string source)
    {
        var (_, spans) = ColorAwareTranslationComposer.Strip(source);
        return spans.Any(static span => ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            || TryFindDanglingBoundaryOpening(source, out _);
    }

    private static bool HasColorBoundaryClosing(string source, string openingToken)
    {
        if (openingToken.StartsWith("{{", StringComparison.Ordinal))
        {
            var depth = 1;
            for (var index = 0; index + 1 < source.Length; index++)
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
                    if (depth == 0)
                    {
                        return true;
                    }

                    index++;
                }
            }

            return false;
        }

        var closingToken = GetSyntheticClosingToken(openingToken);
        var (_, spans) = ColorAwareTranslationComposer.Strip(source);
        var spanDepth = 1;
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (IsSelfContainedBoundaryToken(span.Token))
            {
                continue;
            }

            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                spanDepth++;
                continue;
            }

            if (string.Equals(span.Token, closingToken, StringComparison.OrdinalIgnoreCase))
            {
                spanDepth--;
                if (spanDepth == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetSyntheticClosingToken(string openingToken)
    {
        if (openingToken.StartsWith("{{", StringComparison.Ordinal))
        {
            return "}}";
        }

        if (openingToken.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
        {
            return "</color>";
        }

        return openingToken;
    }

    private static bool IsSelfContainedBoundaryToken(string token)
    {
        return token.Length == 2 && (token[0] == '&' || token[0] == '^');
    }

    private static bool TryTranslateSegmentPreservingColors(
        string source,
        string route,
        bool allowMessagePatternTranslation,
        bool allowGenericLeafTranslation,
        out string translated)
    {
        if (TryTranslateBrainDispositionLinePreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateFactionDispositionLinePreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateWaterBondedLinePreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateSultanShrineWrapperPreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateTombMuralWrapperPreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateTombMuralHeaderPreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateRuntimeObservedDescriptionLine(source, route, out translated))
        {
            return true;
        }

        if (WorldModsTextTranslator.TryTranslate(source, route, "Description.WorldMods", out translated))
        {
            return true;
        }

        if (TryTranslateHistoricSceneHeaderPreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateVillageHistoryTattooPreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateVillageHistoryPatternPreservingColors(source, route, allowMessagePatternTranslation, out translated))
        {
            return true;
        }

        if (!allowGenericLeafTranslation)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TryTranslateVisibleSegment(visible, route, allowMessagePatternTranslation, out var candidate)
                ? candidate
                : visible);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateSultanShrineWrapperPreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (SultanShrineWrapperTranslator.TryTranslateMessage(stripped, spans, route, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateTombMuralWrapperPreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = TombMuralWrapperPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var sultan = TranslateTombMuralSultanName(match.Groups["sultan"].Value);
        sultan = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(sultan, spans, match.Groups["sultan"]);
        var body = HistoricNarrativeTextTranslator.Translate(match.Groups["body"].Value, route);
        body = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            body,
            spans,
            match.Groups["body"]);

        var ancientPrefix = match.Groups["ancient"].Success ? "古代の" : string.Empty;
        translated = $"墓所の壁画には、{ancientPrefix}スルタン {sultan}の生涯における重要な出来事が描かれている:\n\n{body}";
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.TombMuralWrapper", source, translated);
        return true;
    }

    private static string TranslateTombMuralSultanName(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var translated = Translator.Translate(source);
        return string.Equals(translated, source, StringComparison.Ordinal) ? source : translated;
    }

    private static bool TryTranslateTombMuralHeaderPreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = TombMuralHeaderPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var sultan = TranslateTombMuralSultanName(match.Groups["sultan"].Value);
        sultan = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(sultan, spans, match.Groups["sultan"]);
        var ancientPrefix = match.Groups["ancient"].Success ? "古代の" : string.Empty;
        translated = $"墓所の壁画には、{ancientPrefix}スルタン {sultan}の生涯における重要な出来事が描かれている:";
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.TombMuralHeader", source, translated);
        return true;
    }

    private static bool TryTranslateHistoricSceneHeaderPreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = HistoricSceneHeaderPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = TranslateHistoricSceneSubjectPrefix(match.Groups["subject"].Value);
        subject = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(subject, spans, match.Groups["subject"]);
        var visible = string.Equals(match.Groups["kind"].Value, "Engraved", StringComparison.Ordinal)
            ? "彫刻: この品には古代の" + subject + "の生涯の一場面が彫り刻まれている:"
            : "彩色: この品には古代の" + subject + "の生涯の一場面が描かれている:";
        translated = RestoreWholeLineBoundaryWrappers(visible, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.HistoricSceneHeader", source, translated);
        return true;
    }

    private static bool TryTranslateStuckInStateLine(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = StuckInStateLinePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["target"].Value,
            spans,
            match.Groups["target"]);
        translated = target + "にはまっている";
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.StuckInState", source, translated);
        return true;
    }

    private static bool TryTranslateRegainsChargeWhenWornOrHeldLine(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!RegainsChargeWhenWornOrHeldPattern.IsMatch(stripped))
        {
            translated = source;
            return false;
        }

        translated = "装備中または手に持っているとチャージが回復する。戦闘中は大幅に速く回復する。";
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.RegainsChargeWhenWornOrHeld", source, translated);
        return true;
    }

    private static bool TryTranslateVisibleSegment(
        string source,
        string route,
        bool allowMessagePatternTranslation,
        out string translated)
    {
        if (ShouldSkipExactLeafTranslation(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateRuntimeObservedDescriptionLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateLabeledList(source, route, out translated))
        {
            return true;
        }

        if (WorldModsTextTranslator.TryTranslate(source, route, "Description.WorldMods", out translated))
        {
            return true;
        }

        if (TryTranslateTombMuralHeaderPreservingColors(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateHistoricSceneHeaderPreservingColors(source, route, out translated))
        {
            return true;
        }

        if (StatusLineTranslationHelpers.TryTranslateCompareStatusLine(source, route, "Description.CompareStatus", out translated))
        {
            return true;
        }

        if (StatusLineTranslationHelpers.TryTranslateCompareStatusSequence(source, route, "Description.CompareSequence", out translated))
        {
            return true;
        }

        if (StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(source, route, "Description.ActiveEffects", out translated))
        {
            return true;
        }

        if (TryTranslateAddsCookingEffectsLine(source, route, out translated))
        {
            return true;
        }

        if (CookingEffectFragmentTranslator.TryTranslate(source, route, "Description.CookingEffect", out translated))
        {
            return true;
        }

        if (TryTranslateStuckInStateLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateRegainsChargeWhenWornOrHeldLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateMakersMarkDescription(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateMissileWeaponRuntimeLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateHistoricNarrativeLine(source, route, out translated))
        {
            return true;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "Description.ExactLeaf", source, translated);
            return true;
        }

        if (!allowMessagePatternTranslation || ShouldSkipMessagePatternTranslation(source))
        {
            translated = source;
            return false;
        }

        translated = MessagePatternTranslator.Translate(source, route);
        if (!string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "Description.Pattern", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateVillageHistoryTattooPreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = VillageHistoryTattooPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var owner = TranslateTattooOwner(match.Groups["owner"].Value);
        var part = TranslateTattooBodyPart(match.Groups["part"].Value);
        var kind = TranslateTattooKind(match.Groups["kind"].Value);
        var village = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["village"].Value,
            spans,
            match.Groups["village"]);
        var body = TranslateTattooStoryBody(match.Groups["body"].Value.Substring(2), route);
        var bodyWithColon = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            ": " + body,
            spans,
            match.Groups["body"]);

        translated = $"{owner}{part}には{village}村の歴史の一場面を描いた{kind}がある{bodyWithColon}";
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.VillageHistoryTattoo", source, translated);
        return true;
    }

    private static string TranslateTattooStoryBody(string source, string route)
    {
        var message = MessagePatternTranslator.Translate(source, route);
        if (!string.Equals(message, source, StringComparison.Ordinal))
        {
            return message;
        }

        var historic = HistoricNarrativeTextTranslator.Translate(source, route);
        return !string.Equals(historic, source, StringComparison.Ordinal) ? historic : source;
    }

    private static string TranslateTattooOwner(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "YOUR" => "あなたの",
            "HIS" => "彼の",
            "HER" => "彼女の",
            "THEIR" => "彼らの",
            _ => "その",
        };
    }

    private static string TranslateTattooKind(string source)
    {
        return source.Contains("engraving") ? "刻印" : "刺青";
    }

    private static string TranslateTattooBodyPart(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "RIGHT HAND" => "右手",
            "LEFT HAND" => "左手",
            "RIGHT FOOT" => "右足",
            "LEFT FOOT" => "左足",
            "RIGHT ARM" => "右腕",
            "LEFT ARM" => "左腕",
            "HAND" => "手",
            "FOOT" => "足",
            "HEAD" => "頭",
            "FACE" => "顔",
            "ARM" => "腕",
            "LEG" => "脚",
            "TAIL" => "尾",
            "WING" => "翼",
            "HORN" => "角",
            "LIMBS" => "肢",
            _ => source,
        };
    }

    private static bool TryTranslateVillageHistoryPatternPreservingColors(
        string source,
        string route,
        bool allowMessagePatternTranslation,
        out string translated)
    {
        if (!allowMessagePatternTranslation)
        {
            translated = source;
            return false;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        if (!IsVillageHistoryDescriptionPattern(visible) || ShouldSkipMessagePatternTranslation(visible))
        {
            translated = source;
            return false;
        }

        translated = MessagePatternTranslator.Translate(source, route);
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "Description.Pattern", source, translated);
        return true;
    }

    private static bool IsVillageHistoryDescriptionPattern(string visible)
    {
        return visible.StartsWith("This object is a monument to a scene from the history of the village ", StringComparison.Ordinal)
            || visible.StartsWith("Painted: This object is painted with a scene from the history of the village ", StringComparison.Ordinal)
            || visible.StartsWith("Engraved: This object is engraved with a scene from the history of the village ", StringComparison.Ordinal)
            || visible.StartsWith("Holographic: This hologram depicts a scene from the history of the village ", StringComparison.Ordinal);
    }

    private static bool TryTranslateHistoricNarrativeLine(string source, string route, out string translated)
    {
        translated = source;
        if (!HistoricNarrativeLinePattern.IsMatch(source))
        {
            return false;
        }

        var candidate = HistoricNarrativeTextTranslator.Translate(source, route);
        if (string.Equals(candidate, source, StringComparison.Ordinal))
        {
            return false;
        }

        translated = candidate;
        DynamicTextObservability.RecordTransform(route, "Description.HistoricNarrative", source, translated);
        return true;
    }

    private static bool TryTranslateAddsCookingEffectsLine(string source, string route, out string translated)
    {
        var match = AddsCookingEffectsPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var effect = match.Groups["effect"].Value;
        var translatedEffect = StringHelpers.TranslateExactOrLowerAsciiFallback(effect);
        if (string.Equals(translatedEffect, effect, StringComparison.Ordinal)
            && !ContainsJapaneseCharacters(effect))
        {
            translated = source;
            return false;
        }

        translated = translatedEffect + "の効果を調理した食事に加える。";
        DynamicTextObservability.RecordTransform(route, "Description.CookingEffects", source, translated);
        return true;
    }

    private static string FormatPoweredPrefix(bool powered) => powered ? "電源投入時、" : string.Empty;

    private static bool TryTranslateMakersMarkDescription(string source, string route, out string translated)
    {
        var match = MakersMarkDescriptionPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var rawCrafter = match.Groups["crafter"].Value;
        var crafter = StringHelpers.TryGetTranslationExactOrLowerAscii(rawCrafter, out var translatedCrafter)
            ? translatedCrafter
            : rawCrafter;
        translated = match.Groups["markPrefix"].Success
            ? FormatMakersMarkPrefix(match.Groups["markPrefix"].Value) + crafter + "の印を帯びている。"
            : crafter + "の印を帯びている。";
        DynamicTextObservability.RecordTransform(route, "Description.MakersMark", source, translated);
        return true;
    }

    private static string FormatMakersMarkPrefix(string prefix)
    {
        var trimmed = prefix.TrimEnd();
        return trimmed + ": ";
    }

    private static bool TryTranslateMissileWeaponRuntimeLine(string source, string route, out string translated)
    {
        var ammoMatch = MultipleAmmoUsedPerShotPattern.Match(source);
        if (ammoMatch.Success)
        {
            translated = "1射撃あたりの消費弾薬数: " + ammoMatch.Groups["count"].Value;
            DynamicTextObservability.RecordTransform(route, "Description.MissileWeaponRuntime", source, translated);
            return true;
        }

        var projectilesMatch = MultipleProjectilesPerShotPattern.Match(source);
        if (projectilesMatch.Success)
        {
            translated = "1射撃あたりの発射体数: " + projectilesMatch.Groups["count"].Value;
            DynamicTextObservability.RecordTransform(route, "Description.MissileWeaponRuntime", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateRuntimeObservedDescriptionLine(string source, string route, out string translated)
    {
        var resistanceMatch = ResistanceLinePattern.Match(source);
        if (resistanceMatch.Success)
        {
            var element = resistanceMatch.Groups["element"].Value switch
            {
                "Heat" => "熱",
                "Cold" => "冷気",
                "Electrical" => "電撃",
                "Acid" => "酸",
                _ => string.Empty,
            };
            translated = element + "耐性+" + resistanceMatch.Groups["amount"].Value;
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var toHitMatch = ToHitLinePattern.Match(source);
        if (toHitMatch.Success)
        {
            translated = "命中+" + toHitMatch.Groups["amount"].Value;
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var carryCapacityMatch = CarryCapacityBonusLinePattern.Match(source);
        if (carryCapacityMatch.Success)
        {
            translated = "運搬容量+" + carryCapacityMatch.Groups["amount"].Value + "%";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var energyCostReductionMatch = EnergyCostReductionLinePattern.Match(source);
        if (energyCostReductionMatch.Success)
        {
            translated = TranslateRuntimeObservedDisplayNameCapture(energyCostReductionMatch.Groups["scope"].Value)
                + "が"
                + energyCostReductionMatch.Groups["amount"].Value
                + "%軽減される。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var broadcastPowerReceiverMatch = BroadcastPowerReceiverLinePattern.Match(source);
        if (broadcastPowerReceiverMatch.Success)
        {
            translated = broadcastPowerReceiverMatch.Groups["satellite"].Success
                ? "この物体にはブロードキャスト電力受信機があり、地下深すぎない場所では衛星から、または近くのブロードキャスト電力送信機から電荷を受け取れる。"
                : "この物体にはブロードキャスト電力受信機があり、近くのブロードキャスト電力送信機から電荷を受け取れる。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var damageReflectionMatch = DamageReflectionLinePattern.Match(source);
        if (damageReflectionMatch.Success)
        {
            translated = "攻撃者に受けたダメージの"
                + damageReflectionMatch.Groups["amount"].Value
                + "%（端数切り上げ）を反射する。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(
                source,
                "Gigantic: This item has twice the energy capacity and is much heavier than usual.",
                StringComparison.Ordinal))
        {
            translated = "巨大: このアイテムはエネルギー容量が2倍で、通常より大幅に重い。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var fightingMatch = FightingLinePattern.Match(source);
        if (fightingMatch.Success)
        {
            translated = TranslateRuntimeObservedDisplayNameCapture(fightingMatch.Groups["target"].Value) + "と交戦中";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var randomStatueMatch = RuntimeObservedRandomStatueLinePattern.Match(source);
        if (randomStatueMatch.Success)
        {
            translated = TranslateRuntimeObservedStatueMaterial(randomStatueMatch.Groups["material"].Value)
                + "で作られた細やかな彫像で、"
                + TranslateRuntimeObservedDisplayNameCapture(randomStatueMatch.Groups["subject"].Value.Trim())
                + "を表現している。"
                + randomStatueMatch.Groups["rest"].Value;
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (TryTranslateRuntimeObservedHistoricResidueLine(source, route, out translated))
        {
            return true;
        }

        var itReadsMatch = ItReadsLinePattern.Match(source);
        if (itReadsMatch.Success)
        {
            translated = "「" + itReadsMatch.Groups["text"].Value + "」と書かれている。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "Graffiti is scrawled across the surface. It reads: ", StringComparison.Ordinal))
        {
            translated = "表面に落書きが走り書きされている。そこにはこう書かれている: ";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var startupReadoutMatch = StartupReadoutLinePattern.Match(source);
        if (startupReadoutMatch.Success)
        {
            translated = "表示には、起動シーケンス完了まであとおよそ"
                + startupReadoutMatch.Groups["rounds"].Value
                + "ラウンドかかると示されている。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var disarmedMatch = DisarmedSuffixPattern.Match(source);
        if (disarmedMatch.Success)
        {
            translated = disarmedMatch.Groups["body"].Value + " 解除済み。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var bleedingMatch = BleedingOnPenetrationPattern.Match(source);
        if (bleedingMatch.Success)
        {
            translated = "貫通時、この武器は出血を引き起こす: 1ラウンドあたり"
                + bleedingMatch.Groups["damage"].Value
                + "ダメージ; セーブ難度"
                + bleedingMatch.Groups["difficulty"].Value
                + "。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var swarmAlphaMatch = SwarmAlphaPattern.Match(source);
        if (swarmAlphaMatch.Success)
        {
            // Swarmer.cs composes this line from pronouns and ExtraBonus, so avoid
            // freezing one observed alpha creature's gender or bonus value.
            translated = "群れのアルファ: このクリーチャーが対象に隣接している限り、対象に隣接している他の各スウォーマーの群れボーナスに"
                + swarmAlphaMatch.Groups["bonus"].Value
                + "を付与する。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var swarmerMatch = SwarmerPattern.Match(source);
        if (swarmerMatch.Success)
        {
            translated = "スウォーマー: 対象に隣接する別のマスにいる、最初の1体を超える敵対的なスウォーマー1体ごとに、このクリーチャーは近接命中+1と貫通ロール+1を得る。(現在"
                + swarmerMatch.Groups["current"].Value
                + ")";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "Contains wiring enabling it to function as part of power grid, producing electrical charge.", StringComparison.Ordinal))
        {
            translated = "電力網の一部として機能する配線を備え、電荷を生成する。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "Contains wiring enabling it to function as part of power grid, consuming electrical charge.", StringComparison.Ordinal))
        {
            translated = "電力網の一部として機能する配線を備え、電荷を消費する。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "Contains plumbing enabling it to function as part of hydraulic transmission system, consuming hydraulic power.", StringComparison.Ordinal))
        {
            translated = "油圧伝達システムの一部として機能する配管を備え、油圧を消費する。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "Contains plumbing enabling it to function as part of hydraulic transmission system, producing hydraulic power.", StringComparison.Ordinal))
        {
            translated = "油圧伝達システムの一部として機能する配管を備え、油圧を生成する。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (source.StartsWith("This item is a named ", StringComparison.Ordinal)
            && source.EndsWith(".", StringComparison.Ordinal))
        {
            var item = source.Substring("This item is a named ".Length);
            item = item.Substring(0, item.Length - 1);
            translated = "このアイテムは名前付きの"
                + TranslateRuntimeObservedDisplayNameCapture(item)
                + "である。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "Spray fire: This item can be fired while adjacent to multiple enemies without risk of the shot going wild.", StringComparison.Ordinal))
        {
            translated = "スプレーファイア: 複数の敵に隣接していても、このアイテムは射撃が逸れる危険なしに発射できる。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var waterBondedMatch = WaterBondedLinePattern.Match(source);
        if (waterBondedMatch.Success)
        {
            translated = "あなたは"
                + TranslateWaterBondedTarget(waterBondedMatch.Groups["target"].Value, route)
                + "と水の絆で結ばれている。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "This item's AV and DV modifiers are being averaged across all body parts of the same type.", StringComparison.Ordinal))
        {
            translated = "このアイテムのAVとDV修正は同じ種類の全身体部位で平均化されている。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "+2 DV while occupying the same tile as foliage", StringComparison.Ordinal))
        {
            translated = "植物と同じタイルにいる間DV+2";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var wallPenetrationMatch = WallPenetrationLinePattern.Match(source);
        if (wallPenetrationMatch.Success)
        {
            translated = FormatPoweredPrefix(wallPenetrationMatch.Groups["powered"].Success)
                + "壁に対する貫通"
                + wallPenetrationMatch.Groups["amount"].Value
                + "。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var destroysWallsMatch = DestroysWallsLinePattern.Match(source);
        if (destroysWallsMatch.Success)
        {
            translated = FormatPoweredPrefix(destroysWallsMatch.Groups["powered"].Success)
                + destroysWallsMatch.Groups["hits"].Value
                + "回の貫通ヒット後に壁を破壊する。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var moveSpeedMatch = MoveSpeedLinePattern.Match(source);
        if (moveSpeedMatch.Success)
        {
            translated = "移動速度" + moveSpeedMatch.Groups["amount"].Value;
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var lostChanceReducedMatch = LostChanceReducedLinePattern.Match(source);
        if (lostChanceReducedMatch.Success)
        {
            translated = "道に迷う確率が" + lostChanceReducedMatch.Groups["amount"].Value + "%低下する。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (PoweredOffLinePattern.IsMatch(source))
        {
            translated = "電源が切れている。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        var statAdjustMatch = StatAdjustLinePattern.Match(source);
        if (statAdjustMatch.Success)
        {
            translated = (statAdjustMatch.Groups["activated"].Success ? "起動時、" : string.Empty)
                + TranslateRuntimeStatAdjustLabel(statAdjustMatch.Groups["stat"].Value)
                + statAdjustMatch.Groups["amount"].Value
                + statAdjustMatch.Groups["percent"].Value
                + statAdjustMatch.Groups["suffix"].Value;
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "At the center of a particularly thick copse, the vegetation clears. Flower-bedecked huts huddle in the clearing within, surrounded by phalanxes of tidy watervine rows and carefully-tended lah.", StringComparison.Ordinal))
        {
            translated = "ひときわ密な雑木林の中心で植生が開けている。花で飾られた小屋がその空き地に寄り集まり、整然としたウォーターヴァインの畝と丹念に世話されたラーの列に囲まれている。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "a dromad caravan", StringComparison.Ordinal))
        {
            translated = "ドロマドのキャラバン";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (string.Equals(source, "Notes:", StringComparison.Ordinal))
        {
            translated = "注記:";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        if (source.Contains("遺伝子の strand を語る。"))
        {
            translated = source.Replace("遺伝子の strand を語る。", "遺伝子の系統を物語る。");
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedLine", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateRuntimeObservedStatueMaterial(string source)
    {
        return source switch
        {
            "gold" => "金",
            "jasper" => "碧玉",
            _ => source,
        };
    }

    private static string TranslateRuntimeObservedDisplayNameCapture(string source)
    {
        return GetDisplayNameRouteTranslator.TranslatePreservingColors(source, nameof(GetDisplayNamePatch));
    }

    private static bool TryTranslateRuntimeObservedHistoricResidueLine(string source, string route, out string translated)
    {
        translated = source;

        if (string.Equals(
                source,
                "At daybreak on the first day of autumn、ひとりの嬰児（with colossal mace in each hand）がin the mouth of a she-wolfにて産着に包まれて見いだされた。その嬰児はのちにウーヒム IIとして知られるようになった。",
                StringComparison.Ordinal))
        {
            translated = "秋の第一日、夜明けに、両手に巨大なメイスを握ったひとりの嬰児が雌狼の口の中で産着に包まれて見いだされた。その嬰児はのちにウーヒム IIとして知られるようになった。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedHistoricResidue", source, translated);
            return true;
        }

        if (string.Equals(
                source,
                "While、visiting an obscure observatory in the Jewelersの Province of ドゥシュル, ウーヒム IV fabricated horoscope reading that evoked the presence of lucent ruby. SheはそれをRubycusと名づけた。",
                StringComparison.Ordinal))
        {
            translated = "宝石商の州ドゥシュルの無名の天文台を訪れていたとき、ウーヒム IVは透明なルビーの存在を呼び起こす星占いを作り上げた。彼女はそれをRubycusと名づけた。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedHistoricResidue", source, translated);
            return true;
        }

        if (string.Equals(
                source,
                "After treating with 昆虫, ウーヒム IV convinced them to help her found observatory in the Stargazersの Province of カルクヘタラ for the purpose of mapping stars to the shapes of jewels. They named it the Jeweled O...",
                StringComparison.Ordinal))
        {
            translated = "昆虫と交渉した後、ウーヒム IVは宝石の形に星を対応づける目的で、カルクヘタラの星見の州に天文台を創設する手助けをするよう彼らを説得した。彼らはそれをJeweled O...と名づけた。";
            DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedHistoricResidue", source, translated);
            return true;
        }

        var candidate = source
            .Replace("Sorrow of ダビッパ", "ダビッパの悲哀")
            .Replace("Cyan Bad Omen", "青き凶兆")
            .Replace("Shining Heir of 犬", "犬の輝く後継者")
            .Replace("Wife to テッム", "テッムの妻")
            .Replace("Bane of ナシャン", "ナシャンの災い");
        if (string.Equals(candidate, source, StringComparison.Ordinal))
        {
            return false;
        }

        translated = candidate;
        DynamicTextObservability.RecordTransform(route, "Description.RuntimeObservedHistoricResidue", source, translated);
        return true;
    }

    private static string TranslateRuntimeStatAdjustLabel(string stat)
    {
        return stat switch
        {
            "Strength" => "筋力",
            "Agility" => "敏捷",
            "Toughness" => "頑健",
            "Intelligence" => "知力",
            "Willpower" => "意志力",
            "Ego" => "自我",
            "quickness" => "俊敏",
            "hit points" => "ヒットポイント",
            "move speed" => "移動速度",
            "acid resistance" => "酸耐性",
            "cold resistance" => "冷気耐性",
            "electric resistance" => "電気耐性",
            "heat resistance" => "熱耐性",
            _ => stat,
        };
    }

    private static bool TryTranslateWaterBondedLinePreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = WaterBondedLinePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var targetGroup = match.Groups["target"];
        var target = TranslateWaterBondedTarget(targetGroup.Value, route);
        target = RestoreBalancedCapture(target, spans, targetGroup);
        translated = "あなたは" + target + "と水の絆で結ばれている。";
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.WaterBonded", source, translated);
        return true;
    }

    private static string TranslateWaterBondedTarget(string source, string route)
    {
        var target = source.Trim();
        return target switch
        {
            "him" => "彼",
            "her" => "彼女",
            "it" => "それ",
            "them" => "彼ら",
            _ => TryTranslateVisibleSegment(target, route, allowMessagePatternTranslation: true, out var translatedTarget)
                ? translatedTarget
                : target,
        };
    }

    private static bool TryTranslateBrainDispositionLinePreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = BrainDispositionLinePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var label = match.Groups["label"].Value switch
        {
            "Base demeanor:" => "基本態度:",
            "Engagement style:" => "交戦スタイル:",
            _ => string.Empty,
        };
        var rawValue = match.Groups["value"].Value;
        var value = rawValue switch
        {
            "aggressive" => "攻撃的",
            "defensive" => "防御的",
            "docile" => "温和",
            _ => rawValue,
        };
        if (string.IsNullOrEmpty(label))
        {
            translated = source;
            return false;
        }

        label = RestoreBalancedCapture(label, spans, match.Groups["label"]);
        value = RestoreBalancedCapture(value, spans, match.Groups["value"]);
        translated = label + " " + value;
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.BrainDispositionLine", source, translated);
        return true;
    }

    private static bool TryTranslateFactionDispositionLinePreservingColors(string source, string route, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = FactionDispositionPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var relation = match.Groups["relation"].Value switch
        {
            "Loved by" => "愛されている",
            "Admired by" => "敬愛されている",
            "Hated by" => "憎まれている",
            "Disliked by" => "嫌われている",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(relation))
        {
            translated = source;
            return false;
        }

        relation = RestoreBalancedCapture(relation, spans, match.Groups["relation"]);
        var targetGroup = match.Groups["target"];
        var isVillageDispositionTarget = VillageDispositionTargetPattern.IsMatch(targetGroup.Value);
        string target;
        if (!TryTranslateVillageDispositionTarget(targetGroup, spans, out target))
        {
            if (isVillageDispositionTarget)
            {
                target = RestoreBalancedCapture(targetGroup.Value, spans, targetGroup);
            }
            else
            {
                var strippedTarget = StringHelpers.StripLeadingEnglishArticle(
                    targetGroup.Value,
                    includeCapitalizedDefiniteArticle: true);
                target = TranslateDispositionTarget(targetGroup.Value);

                if (!string.Equals(strippedTarget, targetGroup.Value, StringComparison.Ordinal) && spans is not null && spans.Count > 0)
                {
                    var articleLength = targetGroup.Value.Length - strippedTarget.Length;
                    var strippedStart = targetGroup.Index + articleLength;
                    var hasWrapperCrossingStrippedStart = false;
                    var targetEnd = targetGroup.Index + targetGroup.Length;
                    var openingStack = new Stack<int>();
                    for (var index = 0; index < spans.Count; index++)
                    {
                        var span = spans[index];
                        if (span.Index < targetGroup.Index || span.Index > targetEnd)
                        {
                            continue;
                        }

                        if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
                        {
                            openingStack.Push(span.Index);
                            continue;
                        }

                        if (!ColorCodePreserver.IsClosingBoundaryToken(span.Token) || openingStack.Count == 0)
                        {
                            continue;
                        }

                        var openingIndex = openingStack.Pop();
                        if (openingIndex < strippedStart && span.Index > strippedStart)
                        {
                            hasWrapperCrossingStrippedStart = true;
                            break;
                        }
                    }

                    target = hasWrapperCrossingStrippedStart
                        ? RestoreBalancedCapture(target, spans, targetGroup)
                        : RestoreCaptureAtOffset(target, spans, strippedStart, strippedTarget.Length);
                }
                else
                {
                    target = RestoreBalancedCapture(target, spans, targetGroup);
                }
            }
        }
        var reasonGroup = match.Groups["reason"];
        if (!reasonGroup.Success)
        {
            translated = target + "に" + relation + "。";
            translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
            DynamicTextObservability.RecordTransform(route, "Description.FactionDisposition", source, translated);
            return true;
        }

        var reason = TranslateDispositionReason(reasonGroup.Value, route);
        reason = RestoreBalancedCapture(reason, spans, reasonGroup);
        translated = target + "に" + relation + "。理由: " + reason + "。";
        translated = RestoreWholeLineBoundaryWrappers(translated, spans, stripped.Length);
        DynamicTextObservability.RecordTransform(route, "Description.FactionDisposition", source, translated);
        return true;
    }

    private static string RestoreBalancedCapture(string value, IReadOnlyList<ColorSpan>? spans, Group group)
    {
        if (spans is null || spans.Count == 0 || !group.Success)
        {
            return value;
        }

        var captureSpans = ColorCodePreserver.SliceSpans(spans, group.Index, group.Length);
        captureSpans.AddRange(ColorCodePreserver.SliceAdjacentCaptureBoundarySpans(spans, group.Index, group.Length));
        captureSpans = FilterBalancedBoundarySpans(captureSpans);
        return captureSpans.Count == 0
            ? value
            : ColorAwareTranslationComposer.Restore(value, captureSpans);
    }

    private static List<ColorSpan> FilterBalancedBoundarySpans(List<ColorSpan> spans)
    {
        if (spans.Count == 0)
        {
            return spans;
        }

        var keep = new bool[spans.Count];
        var openingStack = new Stack<int>();
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
            {
                openingStack.Push(index);
                continue;
            }

            if (!ColorCodePreserver.IsClosingBoundaryToken(span.Token) || openingStack.Count == 0)
            {
                continue;
            }

            var openingIndex = openingStack.Pop();
            keep[openingIndex] = true;
            keep[index] = true;
        }

        var filtered = new List<ColorSpan>();
        for (var index = 0; index < spans.Count; index++)
        {
            if (keep[index])
            {
                filtered.Add(spans[index]);
            }
        }

        return filtered;
    }

    private static string RestoreWholeLineBoundaryWrappers(string translated, IReadOnlyList<ColorSpan>? spans, int sourceLength)
    {
        if (spans is null || spans.Count == 0)
        {
            return translated;
        }

        var wholeLinePairs = ColorAwareTranslationComposer.SliceWholeBoundaryPairs(spans, sourceStart: 0, sourceLength);
        var wholeLineSpans = ColorAwareTranslationComposer.ProjectWholeBoundaryPairsAbsolute(wholeLinePairs, translated.Length);
        return wholeLineSpans.Count == 0
            ? translated
            : ColorAwareTranslationComposer.Restore(translated, wholeLineSpans);
    }

    private static string TranslateDispositionReason(string source, string route)
    {
        if (ShouldSkipExactLeafTranslation(source))
        {
            return source;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            return translated;
        }

        if (ShouldSkipMessagePatternTranslation(source))
        {
            return source;
        }

        translated = MessagePatternTranslator.Translate(source, route);
        return translated;
    }

    private static string TranslateDispositionTarget(string source)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            return translated;
        }

        var strippedArticle = StringHelpers.StripLeadingEnglishArticle(source, includeCapitalizedDefiniteArticle: true);
        if (string.Equals(strippedArticle, source, StringComparison.Ordinal))
        {
            return source;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(strippedArticle, out translated)
            && !string.Equals(strippedArticle, translated, StringComparison.Ordinal))
        {
            return translated;
        }

        return ContainsJapaneseCharacters(strippedArticle)
            ? strippedArticle
            : source;
    }

    private static bool TryTranslateVillageDispositionTarget(Group targetGroup, IReadOnlyList<ColorSpan>? spans, out string translated)
    {
        var match = VillageDispositionTargetPattern.Match(targetGroup.Value);
        if (!match.Success)
        {
            translated = targetGroup.Value;
            return false;
        }

        var translatedTemplate = Translator.Translate("The villagers of {0}");
        if (string.Equals(translatedTemplate, "The villagers of {0}", StringComparison.Ordinal))
        {
            translated = targetGroup.Value;
            return false;
        }

        var translatedName = RestoreCaptureAtOffset(
            match.Groups["name"].Value,
            spans,
            targetGroup.Index + match.Groups["name"].Index,
            match.Groups["name"].Length);
        translated = translatedTemplate.Replace("{0}", translatedName);

        var targetSpans = spans is not null && spans.Count > 0
            ? ColorCodePreserver.SliceSpans(spans, targetGroup.Index, targetGroup.Length)
            : null;

        var targetBoundarySpans = ColorAwareTranslationComposer.SliceBoundarySpans(
            targetSpans,
            match,
            targetGroup.Length,
            translated.Length);
        if (targetSpans is not null && targetSpans.Count > 0)
        {
            var nameStart = match.Groups["name"].Index;
            var openingStack = new Stack<ColorSpan>();
            for (var index = 0; index < targetSpans.Count; index++)
            {
                var span = targetSpans[index];
                if (ColorCodePreserver.IsOpeningBoundaryToken(span.Token))
                {
                    openingStack.Push(span);
                    continue;
                }

                if (!ColorCodePreserver.IsClosingBoundaryToken(span.Token) || span.Index != targetGroup.Length || openingStack.Count == 0)
                {
                    continue;
                }

                var opening = openingStack.Pop();
                if (opening.Index < nameStart)
                {
                    targetBoundarySpans.Add(new ColorSpan(translated.Length, span.Token));
                }
            }
        }

        translated = ColorAwareTranslationComposer.Restore(translated, targetBoundarySpans);
        return true;
    }

    private static string RestoreCaptureAtOffset(string value, IReadOnlyList<ColorSpan>? spans, int startIndex, int length)
    {
        if (spans is null || spans.Count == 0 || length < 0)
        {
            return value;
        }

        var captureSpans = ColorCodePreserver.SliceSpans(spans, startIndex, length);
        captureSpans.AddRange(ColorCodePreserver.SliceAdjacentCaptureBoundarySpans(spans, startIndex, length));
        captureSpans = FilterBalancedBoundarySpans(captureSpans);
        return captureSpans.Count == 0
            ? value
            : ColorAwareTranslationComposer.Restore(value, captureSpans);
    }

    private static bool ContainsJapaneseCharacters(string source)
    {
        return !string.IsNullOrEmpty(source) && JapaneseCharacterPattern.IsMatch(source);
    }

    private static bool ShouldSkipMessagePatternTranslation(string source)
    {
        if (!ContainsJapaneseCharacters(source))
        {
            return false;
        }

        var normalized = PreservedWeightUnitPattern.Replace(source, string.Empty);
        normalized = AllowedLocalizedEnglishTokenPattern.Replace(normalized, string.Empty);
        return !AsciiLetterPattern.IsMatch(normalized);
    }

    private static bool TryTranslateLabeledList(string source, string route, out string translated)
    {
        var match = LabeledListPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var label = match.Groups["label"].Value switch
        {
            "Physical features:" => "身体的特徴:",
            "Equipped:" => "装備:",
            "身体的特徴:" => "身体的特徴:",
            "装備:" => "装備:",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(label))
        {
            translated = source;
            return false;
        }

        var parts = match.Groups["items"].Value.Split(new[] { ", ", "、" }, StringSplitOptions.None);
        for (var index = 0; index < parts.Length; index++)
        {
            if (TryTranslateLabeledListPart(parts[index], out var translatedPart))
            {
                parts[index] = translatedPart;
            }
        }

        translated = label + " " + string.Join("、", parts);
        DynamicTextObservability.RecordTransform(route, "Description.LabeledList", source, translated);
        return true;
    }

    private static bool TryTranslateLabeledListPart(string source, out string translated)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated))
        {
            return true;
        }

        translated = source switch
        {
            "flaming pseudopod" => "{{fiery|燃え盛る}}仮足",
            "thick fur" => "厚い毛皮",
            _ => source,
        };
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool ShouldSkipExactLeafTranslation(string source)
    {
        if (StatAdjustLinePattern.IsMatch(source))
        {
            return false;
        }

        // Tooltip and description stat names are game contract labels; keep names like
        // Strength, Intelligence, and Ego in English even when broad dictionaries know them.
        return StatAbbreviationPattern.IsMatch(source)
            || SignedStatAbbreviationPattern.IsMatch(source)
            || AttributeTermPattern.IsMatch(source)
            || SignedAttributeTermPattern.IsMatch(source);
    }
}
