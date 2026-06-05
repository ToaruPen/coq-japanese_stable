using System;
using System.Collections;
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
    // Preserve quest-step status markers (X/û/ù) and Optional/任意 prefixes while translating only the step name.
    private static readonly Regex QuestStepStatusPrefixPattern =
        new Regex("^(?<prefix>\\s*(?:[Xûù]\\s*)?(?:(?:Optional|任意):\\s)?)(?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

    public static void Prefix(object? __0)
    {
        try
        {
            TranslateSavedQuestSteps(__0);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: QuestLogTranslationPatch.Prefix failed: {0}", ex);
        }
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
                translated = GeneratedQuestTitleTranslator.TranslateEmbeddedPreservingColors(translated, route);
                translated = TranslateAuthoredQuestStepLine(translated, route);
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

        var stepTranslated = TranslateAuthoredQuestStepLine(source, route);
        if (!string.Equals(stepTranslated, source, StringComparison.Ordinal))
        {
            return stepTranslated;
        }

        return GeneratedQuestTitleTranslator.TranslateEmbeddedPreservingColors(source, route);
    }

    private static void TranslateSavedQuestSteps(object? quest)
    {
        if (quest is null)
        {
            return;
        }

        if (GetMemberValue(quest, "StepsByID") is not IDictionary stepsById)
        {
            return;
        }

        var index = 0;
        foreach (var step in stepsById.Values)
        {
            if (step is not null)
            {
                TranslateSavedQuestStepMember(step, "Name", index, "QuestLog.SavedQuestStepName");
                TranslateSavedQuestStepMember(step, "Text", index, "QuestLog.SavedQuestStepText");
            }

            index++;
        }
    }

    private static void TranslateSavedQuestStepMember(object step, string memberName, int index, string family)
    {
        var source = GetStringMemberValue(step, memberName);
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        if (!DynamicQuestGeneratedQuestTextTranslator.TryTranslate(source, out var translated)
            && string.Equals(source, translated, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(source, translated, StringComparison.Ordinal)
            || !SetStringMemberValue(step, memberName, translated))
        {
            return;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            return;
        }

        var route = ObservabilityHelpers.ComposeContext(Context, "step=" + index + ";field=" + memberName);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
    }

    internal static string TranslateAuthoredQuestStepLine(string source, string route)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TranslateQuestStepVisible(visible, route, source));
    }

    private static string TranslateQuestStepVisible(string visible, string route, string originalSource)
    {
        var direct = ScopedDictionaryLookup.TranslateExactOrLowerAscii(visible, DictionaryFile);
        if (direct is not null
            && direct.Length != 0
            && !string.Equals(direct, visible, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "QuestLog.StepName", originalSource, direct);
            return direct;
        }

        var match = QuestStepStatusPrefixPattern.Match(visible);
        if (!match.Success)
        {
            return visible;
        }

        var prefix = match.Groups["prefix"].Value;
        var name = match.Groups["name"].Value;
        var translatedName = ScopedDictionaryLookup.TranslateExactOrLowerAscii(name, DictionaryFile);
        if (!string.IsNullOrEmpty(translatedName) && !string.Equals(translatedName, name, StringComparison.Ordinal))
        {
            var translated = match.Groups["prefix"].Value + translatedName;
            DynamicTextObservability.RecordTransform(route, "QuestLog.StepName", originalSource, translated);
            return translated;
        }

        if (prefix.Length == 0)
        {
            return visible;
        }

        if (prefix.Contains("Optional:")
            || prefix.Contains("任意:"))
        {
            return visible;
        }

        if (!DynamicQuestGeneratedQuestTextTranslator.TryTranslate(name, out var generatedName)
            || string.Equals(generatedName, name, StringComparison.Ordinal))
        {
            return visible;
        }

        var generatedTranslated = match.Groups["prefix"].Value + generatedName;
        DynamicTextObservability.RecordTransform(route, "QuestLog.GeneratedQuestText", originalSource, generatedTranslated);
        return generatedTranslated;
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(instance);
        }

        var field = AccessTools.Field(type, memberName);
        return field?.GetValue(instance);
    }

    private static string? GetStringMemberValue(object instance, string memberName) =>
        GetMemberValue(instance, memberName) as string;

    private static bool SetStringMemberValue(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(instance, value);
            return true;
        }

        var field = AccessTools.Field(type, memberName);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
            return true;
        }

        return false;
    }
}
