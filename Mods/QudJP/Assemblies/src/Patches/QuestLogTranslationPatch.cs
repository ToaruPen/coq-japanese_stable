using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QuestLogTranslationPatch
{
    private const string Context = nameof(QuestLogTranslationPatch);
    private const string DictionaryFile = "ui-quests.ja.json";
    private const string BonusRewardTemplate = "Bonus reward for completing this quest by level &C{0}&y.";
    private static readonly Regex OptionalPrefixPattern =
        new Regex("^(?<prefix>.*?)(?<label>Optional:\\s)(?<suffix>.*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BonusRewardPattern =
        new Regex("^(?<indent>\\s*)Bonus reward for completing this quest by level &C(?<value>.+?)&y\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.UI.QuestLog", "QuestLog");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: QuestLogTranslationPatch target type not found.");
            return null;
        }

        var questType = GameTypeResolver.FindType("XRL.World.Quest", "Quest");
        var method = questType is null
            ? null
            : AccessTools.Method(targetType, "GetLinesForQuest", new[] { questType, typeof(bool), typeof(bool), typeof(int) });
        if (method is null)
        {
            Trace.TraceError("QudJP: QuestLogTranslationPatch.GetLinesForQuest(Quest,bool,bool,int) not found.");
        }

        return method;
    }

    public static void Postfix(ref List<string>? __result)
    {
        try
        {
            if (__result is null || __result.Count == 0)
            {
                return;
            }

            for (var index = 0; index < __result.Count; index++)
            {
                __result[index] = TranslateQuestLogLine(
                    __result[index],
                    ObservabilityHelpers.ComposeContext(Context, "line=" + index));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QuestLogTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static string TranslateQuestLogLine(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var optionalMatch = OptionalPrefixPattern.Match(source);
        if (optionalMatch.Success)
        {
            var translatedLabel = ScopedDictionaryLookup.TranslateExactOrLowerAscii(optionalMatch.Groups["label"].Value, DictionaryFile);
            if (!string.IsNullOrEmpty(translatedLabel)
                && !string.Equals(translatedLabel, optionalMatch.Groups["label"].Value, StringComparison.Ordinal))
            {
                var translated = optionalMatch.Groups["prefix"].Value + translatedLabel + optionalMatch.Groups["suffix"].Value;
                DynamicTextObservability.RecordTransform(route, "QuestLog.OptionalPrefix", source, translated);
                return translated;
            }
        }

        var bonusMatch = BonusRewardPattern.Match(source);
        if (bonusMatch.Success)
        {
            var translatedTemplate = ScopedDictionaryLookup.TranslateExactOrLowerAscii(BonusRewardTemplate, DictionaryFile);
            if (!string.IsNullOrEmpty(translatedTemplate)
                && !string.Equals(translatedTemplate, BonusRewardTemplate, StringComparison.Ordinal))
            {
                var translatedBody = string.Format(CultureInfo.InvariantCulture, translatedTemplate, bonusMatch.Groups["value"].Value);
                var translated = bonusMatch.Groups["indent"].Value + translatedBody;
                DynamicTextObservability.RecordTransform(route, "QuestLog.BonusReward", source, translated);
                return translated;
            }
        }

        return source;
    }
}
