using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QuestLifecyclePopupTranslationPatch
{
    private const string Context = nameof(QuestLifecyclePopupTranslationPatch);

    private static readonly Regex QuestReceivedPattern = new(
        "^You have received a new quest, (?<quest>.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QuestFailedPattern = new(
        "^You have failed the quest (?<quest>.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QuestStepFailedPattern = new(
        "^You have failed the step, (?<step>.+), of the quest (?<quest>.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QuestCompletedPattern = new(
        "^You have completed the quest (?<quest>.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var questType = AccessTools.TypeByName("XRL.World.Quest");
        var questStepType = AccessTools.TypeByName("XRL.World.QuestStep");
        if (questType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddTarget(targets, questType, "ShowStartPopup", Type.EmptyTypes);
        AddTarget(targets, questType, "ShowFailPopup", Type.EmptyTypes);
        if (questStepType is not null)
        {
            AddTarget(targets, questType, "ShowFailStepPopup", new[] { questStepType });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ShowFailStepPopup QuestStep target type not found.", Context);
        }

        AddTarget(targets, questType, "ShowFinishPopup", Type.EmptyTypes);
        return targets;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateQuestReceived(source, stripped, spans, route, family, out translated)
            || TryTranslateQuestFailed(source, stripped, spans, route, family, out translated)
            || TryTranslateQuestStepFailed(source, stripped, spans, route, family, out translated)
            || TryTranslateQuestCompleted(source, stripped, spans, route, family, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslateQuestReceived(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        return TryTranslateQuestPattern(
            QuestReceivedPattern,
            source,
            stripped,
            spans,
            route,
            family,
            "Received",
            match => $"新しいクエスト「{RestoreQuest(match, spans)}」を受けた！",
            out translated);
    }

    private static bool TryTranslateQuestFailed(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        return TryTranslateQuestPattern(
            QuestFailedPattern,
            source,
            stripped,
            spans,
            route,
            family,
            "Failed",
            match => $"クエスト「{RestoreQuest(match, spans)}」に失敗した！",
            out translated);
    }

    private static bool TryTranslateQuestStepFailed(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        return TryTranslateQuestPattern(
            QuestStepFailedPattern,
            source,
            stripped,
            spans,
            route,
            family,
            "StepFailed",
            match => $"クエスト「{RestoreQuest(match, spans)}」のステップ「{RestoreStep(match, spans)}」に失敗した！",
            out translated);
    }

    private static bool TryTranslateQuestCompleted(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        return TryTranslateQuestPattern(
            QuestCompletedPattern,
            source,
            stripped,
            spans,
            route,
            family,
            "Completed",
            match => $"クエスト「{RestoreQuest(match, spans)}」を完了した！",
            out translated);
    }

    private static bool TryTranslateQuestPattern(
        Regex pattern,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string detail,
        Func<Match, string> translate,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match),
            spans,
            stripped.Length);
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
        return true;
    }

    private static string RestoreQuest(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["quest"];
        var quest = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        return GeneratedQuestTitleTranslator.TranslatePreservingColors(quest, Context);
    }

    private static string RestoreStep(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["step"];
        return ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            group.Value,
            spans,
            group).Trim();
    }
}
