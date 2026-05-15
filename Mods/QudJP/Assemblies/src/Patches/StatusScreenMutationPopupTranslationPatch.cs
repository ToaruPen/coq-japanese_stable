using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class StatusScreenMutationPopupTranslationPatch
{
    private const string Context = nameof(StatusScreenMutationPopupTranslationPatch);

    private static readonly Regex UpgradePromptPattern = new(
        "^It will cost (?<cost>.+?) mutation point to increase (?<name>.+?)'s rank by 1\\.\\nDo you wish to increase this (?<term>.+?) rank\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IncreasedRankPattern = new(
        "^You have increased (?<name>.+?)'s base rank to (?<rank>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InsufficientPointsPattern = new(
        "^You do not have enough mutation points to increase that (?<term>.+?) rank\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AdvanceNotAllowedPattern = new(
        "^You may not advance this (?<term>.+?) rank yet\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BaseRankReasonPattern = new(
        "^(?<prefix>[*+\\-]) This (?<term>.+?) base rank is (?<rank>\\d+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HighLowStatReasonPattern = new(
        "^(?<prefix>[+\\-]) This (?<term>.+?) rank is (?<direction>increased|decreased) by (?<amount>\\d+) due to your (?<quality>high|low) (?<stat>Strength|Toughness|Willpower|Agility|Ego|Intelligence)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoInherentReasonPattern = new(
        "^\\* You do not possess this (?<term>.+?) inherently, and so you cannot advance its rank\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllMutationRanksReasonPattern = new(
        "^(?<prefix>[+\\-]) All your (?<term>.+?) ranks are (?<direction>increased|decreased) by (?<amount>\\d+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CategoryRanksReasonPattern = new(
        "^(?<prefix>[+\\-]) All your (?<category>.+?) (?<term>.+?) ranks are (?<direction>increased|decreased) by (?<amount>\\d+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HighAdrenalineReasonPattern = new(
        "^\\+ This (?<term>.+?) rank is increased by (?<amount>\\d+) due to your high adrenaline\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RapidAdvanceReasonPattern = new(
        "^\\+ This (?<term>.+?) rank is increased by (?<amount>\\d+) due to being rapidly advanced (?<times>\\d+) times?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EffectReasonPattern = new(
        "^\\+ This (?<term>.+?) rank is increased by (?<amount>\\d+) due to a (?<effect>metabolizing|tonic) effect\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EquippedItemReasonPattern = new(
        "^\\+ This (?<term>.+?) rank is increased by (?<amount>\\d+) due to your equipped item, (?<source>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExternalReasonPattern = new(
        "^\\+ This (?<term>.+?) rank is increased by (?<amount>\\d+) due to (?<source>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OwnedSourceReasonPattern = new(
        "^\\+ This (?<term>.+?) rank is increased by (?<amount>\\d+) due to your (?<source>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MinimumRankReasonPattern = new(
        "^\\+ (?<term>Mutation|Defect|mutation|defect) ranks cannot be reduced below 1\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LevelCapReasonPattern = new(
        "^\\- This (?<term>.+?) rank is capped at (?<cap>\\d+) due to your level\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static object? currentMutation;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var statusScreenType = AccessTools.TypeByName("XRL.UI.StatusScreen");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var baseMutationType = AccessTools.TypeByName("XRL.World.Parts.Mutation.BaseMutation");
        if (statusScreenType is null || gameObjectType is null || baseMutationType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(statusScreenType, "ShowMutationPopup", new[] { gameObjectType, baseMutationType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ShowMutationPopup target not found.", Context);
            return targets;
        }

        targets.Add(method);
        return targets;
    }

    public static void Prefix(object? __1, out object? __state)
    {
        try
        {
            __state = currentMutation;
            OwnerTranslationScope.Enter(ref activeDepth);
            currentMutation = __1;
        }
        catch (Exception ex)
        {
            __state = null;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception, object? __state)
    {
        try
        {
            currentMutation = __state;
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static void ResetForTests()
    {
        activeDepth = 0;
        currentMutation = null;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || currentMutation is null || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!TryTranslateCore(source, currentMutation, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static bool TryTranslateCore(string source, object mutation, out string translated)
    {
        if (TryTranslateIncreasedRankPopup(source, out translated))
        {
            return true;
        }

        var tailStart = FindMutationTailStart(source);
        if (tailStart < 0)
        {
            return CharacterStatusScreenTextTranslator.TryTranslateMutationDetails(
                mutation,
                source,
                Context,
                out translated);
        }

        var details = source.Substring(0, tailStart).TrimEnd();
        var tail = source.Substring(tailStart).TrimEnd();
        if (!TryTranslateTail(tail, out var translatedTail))
        {
            translated = source;
            return false;
        }

        if (details.Length == 0)
        {
            translated = translatedTail;
            return true;
        }

        var translatedDetails = TranslateMutationDetailsWithRankBoosts(mutation, details);
        translated = translatedDetails + "\n\n" + translatedTail;
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static int FindMutationTailStart(string source)
    {
        var starts = new[]
        {
            "It will cost ",
            "You do not have enough mutation points ",
            "You may not advance this ",
        };

        var best = -1;
        for (var index = 0; index < starts.Length; index++)
        {
            var candidate = source.LastIndexOf(starts[index], StringComparison.Ordinal);
            if (candidate > best)
            {
                best = candidate;
            }
        }

        if (best <= 0)
        {
            return best;
        }

        var colorStart = source.LastIndexOf("{{", best, StringComparison.Ordinal);
        var newlineStart = source.LastIndexOf('\n', best);
        if (colorStart > newlineStart
            && source.IndexOf('|', colorStart, best - colorStart) >= 0)
        {
            return colorStart;
        }

        return best;
    }

    private static string TranslateMutationDetailsWithRankBoosts(object mutation, string source)
    {
        var separator = source.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (separator > 0)
        {
            var details = source.Substring(0, separator).TrimEnd();
            var rankBoosts = source.Substring(separator + 2).Trim();
            if (TryTranslateRankBoostLines(rankBoosts, out var translatedRankBoosts)
                && CharacterStatusScreenTextTranslator.TryTranslateMutationDetails(mutation, details, Context, out var translatedDetails))
            {
                return translatedDetails + "\n\n" + translatedRankBoosts;
            }
        }

        if (CharacterStatusScreenTextTranslator.TryTranslateMutationDetails(mutation, source, Context, out var translated))
        {
            return translated;
        }

        return source;
    }

    private static bool TryTranslateIncreasedRankPopup(string source, out string translated)
    {
        var firstLineEnd = source.IndexOf('\n');
        var firstLine = firstLineEnd < 0 ? source : source.Substring(0, firstLineEnd).TrimEnd();
        if (!TryTranslateIncreasedRankLine(firstLine, out var translatedFirstLine))
        {
            translated = source;
            return false;
        }

        if (firstLineEnd < 0)
        {
            translated = translatedFirstLine;
            return true;
        }

        var rest = source.Substring(firstLineEnd).Trim();
        var translatedRest = TranslateRankBoostLines(rest);
        translated = translatedRest.Length == 0
            ? translatedFirstLine
            : translatedFirstLine + "\n\n" + translatedRest;
        return true;
    }

    private static bool TryTranslateTail(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateUpgradePrompt(stripped, spans, source, out translated)
            || TryTranslateInsufficientPoints(stripped, spans, source, out translated)
            || TryTranslateAdvanceNotAllowed(stripped, spans, source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateUpgradePrompt(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = UpgradePromptPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var name = StatusScreenPopupTranslationPatch.TranslateMutationDisplayName(Restore(match, spans, "name"));
        var cost = Restore(match, spans, "cost");
        translated = RestoreWhole(
            $"{name}のランクを1上げるには変異ポイントが{cost}ポイント必要だ。\nこの{TranslatePossessiveMutationTerm(Restore(match, spans, "term"))}ランクを上げますか？",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateIncreasedRankLine(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = IncreasedRankPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var name = StatusScreenPopupTranslationPatch.TranslateMutationDisplayName(Restore(match, spans, "name"));
        translated = RestoreWhole(
            $"{name}の基本ランクを{Restore(match, spans, "rank")}に上げた！",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateInsufficientPoints(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = InsufficientPointsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"その{TranslatePossessiveMutationTerm(Restore(match, spans, "term"))}ランクを上げるための変異ポイントが足りない。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateAdvanceNotAllowed(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = AdvanceNotAllowedPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"この{TranslatePossessiveMutationTerm(Restore(match, spans, "term"))}ランクはまだ上げられない。",
            source,
            stripped,
            spans);
        return true;
    }

    private static string TranslateRankBoostLines(string source)
    {
        return TryTranslateRankBoostLines(source, out var translated) ? translated : source;
    }

    private static bool TryTranslateRankBoostLines(string source, out string translated)
    {
        if (source.Length == 0)
        {
            translated = string.Empty;
            return false;
        }

        var lines = source.Split('\n');
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            if (TryTranslateRankBoostLine(lines[index], out var translatedLine))
            {
                lines[index] = translatedLine;
                changed = true;
            }
        }

        translated = changed ? string.Join("\n", lines) : source;
        return changed;
    }

    private static bool TryTranslateRankBoostLine(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateBaseRankReason(stripped, spans, source, out translated)
            || TryTranslateHighLowStatReason(stripped, spans, source, out translated)
            || TryTranslateNoInherentReason(stripped, spans, source, out translated)
            || TryTranslateCategoryRanksReason(stripped, spans, source, out translated)
            || TryTranslateAllMutationRanksReason(stripped, spans, source, out translated)
            || TryTranslateHighAdrenalineReason(stripped, spans, source, out translated)
            || TryTranslateRapidAdvanceReason(stripped, spans, source, out translated)
            || TryTranslateEffectReason(stripped, spans, source, out translated)
            || TryTranslateEquippedItemReason(stripped, spans, source, out translated)
            || TryTranslateOwnedSourceReason(stripped, spans, source, out translated)
            || TryTranslateExternalReason(stripped, spans, source, out translated)
            || TryTranslateMinimumRankReason(stripped, spans, source, out translated)
            || TryTranslateLevelCapReason(stripped, spans, source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateBaseRankReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = BaseRankReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"{match.Groups["prefix"].Value} この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}基本ランクは{match.Groups["rank"].Value}。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateNoInherentReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = NoInherentReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"* この{TranslateMutationTerm(match.Groups["term"].Value)}を本来持っていないため、ランクを上げることはできない。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateAllMutationRanksReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = AllMutationRanksReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"{match.Groups["prefix"].Value} すべての{TranslatePluralPossessiveMutationTerm(match.Groups["term"].Value)}ランクが{match.Groups["amount"].Value}{TranslateDirection(match.Groups["direction"].Value)}している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateCategoryRanksReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = CategoryRanksReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"{match.Groups["prefix"].Value} すべての{TranslateMutationCategory(match.Groups["category"].Value)}{TranslatePluralPossessiveMutationTerm(match.Groups["term"].Value)}ランクが{match.Groups["amount"].Value}{TranslateDirection(match.Groups["direction"].Value)}している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateHighAdrenalineReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = HighAdrenalineReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"+ この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}ランクは高いアドレナリンにより{match.Groups["amount"].Value}上昇している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateRapidAdvanceReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = RapidAdvanceReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"+ この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}ランクは{match.Groups["times"].Value}回の急速成長により{match.Groups["amount"].Value}上昇している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateEffectReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = EffectReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var effect = match.Groups["effect"].Value == "metabolizing" ? "代謝効果" : "トニック効果";
        translated = RestoreWhole(
            $"+ この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}ランクは{effect}により{match.Groups["amount"].Value}上昇している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateEquippedItemReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = EquippedItemReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"+ この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}ランクは装備品 {Restore(match, spans, "source")} により{match.Groups["amount"].Value}上昇している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateOwnedSourceReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = OwnedSourceReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"+ この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}ランクはあなたの{Restore(match, spans, "source")}により{match.Groups["amount"].Value}上昇している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateExternalReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = ExternalReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"+ この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}ランクは{Restore(match, spans, "source")}により{match.Groups["amount"].Value}上昇している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateMinimumRankReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = MinimumRankReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"+ {TranslateMutationTerm(match.Groups["term"].Value)}ランクは1未満には下げられない。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateLevelCapReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = LevelCapReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWhole(
            $"- この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}ランクはあなたのレベルにより{match.Groups["cap"].Value}に制限されている。",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateHighLowStatReason(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = HighLowStatReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var direction = match.Groups["direction"].Value == "increased" ? "上昇" : "低下";
        var quality = match.Groups["quality"].Value == "high" ? "高い" : "低い";
        translated = RestoreWhole(
            $"{match.Groups["prefix"].Value} この{TranslatePossessiveMutationTerm(match.Groups["term"].Value)}ランクは{quality}{TranslateStat(match.Groups["stat"].Value)}により{match.Groups["amount"].Value}{direction}している。",
            source,
            stripped,
            spans);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreWhole(
        string translated,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }

    private static string TranslatePossessiveMutationTerm(string term)
    {
        return term switch
        {
            "mutation's" or "mutation" => "変異の",
            "mutations'" or "mutations" => "変異の",
            "defect's" or "defect" => "欠陥の",
            "defects'" or "defects" => "欠陥の",
            _ => term + " ",
        };
    }

    private static string TranslatePluralPossessiveMutationTerm(string term)
    {
        return term switch
        {
            "mutation's" or "mutations'" or "mutation" or "mutations" => "変異",
            "defect's" or "defects'" or "defect" or "defects" => "欠陥",
            _ => term + " ",
        };
    }

    private static string TranslateMutationTerm(string term)
    {
        return term switch
        {
            "mutation" or "Mutation" or "mutations" => "変異",
            "defect" or "Defect" or "defects" => "欠陥",
            _ => term,
        };
    }

    private static string TranslateMutationCategory(string category)
    {
        return category switch
        {
            "Physical" => "身体的",
            "Mental" => "精神的",
            _ => category + " ",
        };
    }

    private static string TranslateDirection(string direction)
    {
        return direction == "increased" ? "上昇" : "低下";
    }

    private static string TranslateStat(string stat)
    {
        return stat switch
        {
            "Strength" => "筋力",
            "Toughness" => "頑健",
            "Willpower" => "意志力",
            "Agility" => "敏捷",
            "Ego" => "自我",
            "Intelligence" => "知力",
            _ => stat,
        };
    }
}
