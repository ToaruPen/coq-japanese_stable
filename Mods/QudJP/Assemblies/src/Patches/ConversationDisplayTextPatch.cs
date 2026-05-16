using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ConversationDisplayTextPatch
{
    private static readonly Regex TrailingActionMarkerPattern =
        new Regex(
            @"\s+(?:\{\{[^|{}]+?\|)?\[[^\]]+\](?:\}\})?\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WaterRitualReputationSummaryPattern = new(
        @"\{\{C\|-----\}\}\n\{\{y\|Your reputation with (?<faction>.+?) is \{\{C\|(?<current>-?\d+)\}\}\.\n(?<speaker>.+?) can award an additional \{\{C\|(?<available>-?\d+)\}\} reputation\.\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MoundCountdownPattern = new(
        @"(?<=まだ！ )(?<countdown>soon|in (?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty) days?)(?= に戻って。)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SeekSkillPattern = new(
        @"^I seek (?<skill>.+?)\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();

        AddTargetMethod(targets, "XRL.World.Conversations.IConversationElement");
        AddTargetMethod(targets, "XRL.World.Conversations.Choice");

        if (targets.Count == 0)
        {
            Trace.TraceError("QudJP: Failed to resolve conversation GetDisplayText(bool) targets. Patch will not apply.");
        }

        return targets;
    }

    private static void AddTargetMethod(List<MethodBase> targets, string typeName)
    {
        var method = AccessTools.Method(typeName + ":GetDisplayText", new[] { typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve {0}.GetDisplayText(bool).", typeName);
            return;
        }

        targets.Add(method);
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            __result = NormalizeConversationDisplayText(__result);
            __result = TranslateStructuredConversationDisplayText(__result);
            __result = TranslateConversationDisplayText(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ConversationDisplayTextPatch.Postfix failed: {0}", ex);
        }
    }

    private static string NormalizeConversationDisplayText(string source)
    {
        var match = TrailingActionMarkerPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        return source.Substring(0, match.Index);
    }

    private static string TranslateStructuredConversationDisplayText(string source)
    {
        var translated = TranslateWaterRitualReputationSummary(source);
        translated = TranslateMoundCountdown(translated);
        translated = TranslateQuestSignpostDirections(translated);
        translated = TranslateWaterRitualRecipeLabel(translated);
        translated = TranslateHermitOathFallback(translated);
        return translated;
    }

    private static string TranslateWaterRitualReputationSummary(string source)
    {
        return WaterRitualReputationSummaryPattern.Replace(
            source,
            match =>
            {
                var translated =
                    "{{C|-----}}\n{{y|"
                    + match.Groups["faction"].Value
                    + "との評判は{{C|"
                    + match.Groups["current"].Value
                    + "}}。\n"
                    + match.Groups["speaker"].Value
                    + "から追加で{{C|"
                    + match.Groups["available"].Value
                    + "}}の評判を得られる。}}";
                DynamicTextObservability.RecordTransform(
                    nameof(ConversationDisplayTextPatch),
                    "ConversationDisplay.WaterRitualReputationSummary",
                    match.Value,
                    translated);
                return translated;
            });
    }

    private static string TranslateMoundCountdown(string source)
    {
        return MoundCountdownPattern.Replace(
            source,
            match =>
            {
                var translated = TranslateCountdown(match.Groups["countdown"].Value);
                DynamicTextObservability.RecordTransform(
                    nameof(ConversationDisplayTextPatch),
                    "ConversationDisplay.MoundCountdown",
                    match.Value,
                    translated);
                return translated;
            });
    }

    private static string TranslateCountdown(string countdown)
    {
        if (string.Equals(countdown, "soon", StringComparison.Ordinal))
        {
            return "もうすぐ";
        }

        const string prefix = "in ";
        var value = countdown.Substring(prefix.Length);
        value = value.EndsWith(" days", StringComparison.Ordinal)
            ? value.Substring(0, value.Length - " days".Length)
            : value.Substring(0, value.Length - " day".Length);
        return TranslateEnglishCardinal(value) + "日後";
    }

    private static string TranslateEnglishCardinal(string source)
    {
        return source switch
        {
            "one" => "1",
            "two" => "2",
            "three" => "3",
            "four" => "4",
            "five" => "5",
            "six" => "6",
            "seven" => "7",
            "eight" => "8",
            "nine" => "9",
            "ten" => "10",
            "eleven" => "11",
            "twelve" => "12",
            "thirteen" => "13",
            "fourteen" => "14",
            "fifteen" => "15",
            "sixteen" => "16",
            "seventeen" => "17",
            "eighteen" => "18",
            "nineteen" => "19",
            "twenty" => "20",
            _ => source,
        };
    }

    private static string TranslateQuestSignpostDirections(string source)
    {
        if (!IsQuestSignpostText(source))
        {
            return source;
        }

        var result = source;
        result = ReplaceDirection(result, "also to the northeast", "も北東側");
        result = ReplaceDirection(result, "also to the northwest", "も北西側");
        result = ReplaceDirection(result, "also to the southeast", "も南東側");
        result = ReplaceDirection(result, "also to the southwest", "も南西側");
        result = ReplaceDirection(result, "also to the north", "も北側");
        result = ReplaceDirection(result, "also to the south", "も南側");
        result = ReplaceDirection(result, "also to the east", "も東側");
        result = ReplaceDirection(result, "also to the west", "も西側");
        result = ReplaceDirection(result, "to the northeast", "北東側");
        result = ReplaceDirection(result, "to the northwest", "北西側");
        result = ReplaceDirection(result, "to the southeast", "南東側");
        result = ReplaceDirection(result, "to the southwest", "南西側");
        result = ReplaceDirection(result, "to the north", "北側");
        result = ReplaceDirection(result, "to the south", "南側");
        result = ReplaceDirection(result, "to the east", "東側");
        result = ReplaceDirection(result, "to the west", "西側");
        result = ReplaceDirection(result, "somewhere", "どこか");
        result = ReplaceDirection(result, "nearby", "近く");
        result = ReplaceDirection(result, "above", "上方");
        result = ReplaceDirection(result, "below", "下方");
        result = ReplaceDirection(result, "here", "ここ");
        if (!string.Equals(result, source, StringComparison.Ordinal))
        {
            result = ReplaceDirection(result, ", or ", "、または ");
            result = ReplaceDirection(result, " or ", " または ");
        }

        return result;
    }

    private static bool IsQuestSignpostText(string source)
    {
        return source.Contains("{{y|")
            && (source.Contains("に会いに行くといい。")
                || source.Contains("と話してみてくれ。")
                || source.Contains("を探してみてくれ。"));
    }

    private static string ReplaceDirection(string source, string english, string japanese)
    {
        if (source.IndexOf(english, StringComparison.Ordinal) < 0)
        {
            return source;
        }

        var translated = source.Replace(english, japanese);
        DynamicTextObservability.RecordTransform(
            nameof(ConversationDisplayTextPatch),
            "ConversationDisplay.QuestSignpostDirection",
            source,
            translated);
        return translated;
    }

    private static string TranslateWaterRitualRecipeLabel(string source)
    {
        return ReplaceStructuredSegment(
            source,
            "[{{W|Item mod}}]",
            "[{{W|アイテム改造}}]",
            "ConversationDisplay.WaterRitualRecipeLabel");
    }

    private static string TranslateHermitOathFallback(string source)
    {
        return ReplaceStructuredSegment(
            source,
            "hermit、もう二度と邪魔しないと誓う。",
            "隠者、もう二度と邪魔しないと誓う。",
            "ConversationDisplay.HermitOathFallback");
    }

    private static string ReplaceStructuredSegment(string source, string english, string japanese, string detail)
    {
        if (source.IndexOf(english, StringComparison.Ordinal) < 0)
        {
            return source;
        }

        var translated = source.Replace(english, japanese);
        DynamicTextObservability.RecordTransform(
            nameof(ConversationDisplayTextPatch),
            detail,
            source,
            translated);
        return translated;
    }

    private static string TranslateConversationDisplayText(string source)
    {
        var route = nameof(ConversationDisplayTextPatch);
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TryTranslateVisibleConversationText(visible, route, out var translated)
                ? translated
                : visible);
    }

    private static bool TryTranslateVisibleConversationText(string source, string route, out string translated)
    {
        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "ConversationDisplay.ExactLeaf", source, translated);
            return true;
        }

        var seekSkillMatch = SeekSkillPattern.Match(source);
        if (seekSkillMatch.Success)
        {
            var skill = seekSkillMatch.Groups["skill"].Value;
            var translatedSkill = StringHelpers.TranslateExactOrLowerAscii(skill);
            if (translatedSkill is null)
            {
                translatedSkill = skill;
            }

            translated = translatedSkill + "を求めている。";
            DynamicTextObservability.RecordTransform(route, "ConversationDisplay.SeekSkill", source, translated);
            return true;
        }

        translated = source;
        return false;
    }
}
