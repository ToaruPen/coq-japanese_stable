using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DanceRitualOpponentTranslationPatch
{
    private const string Context = nameof(DanceRitualOpponentTranslationPatch);
    private const string QueueFamily = Context;
    private static readonly Regex BusyDancingPattern = new(
        "^(?:The |the |A |a |An |an )?(?<actor>.+?) (?:is|are) busy dancing!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TakingTurnPattern = new(
        "^Debug: (?<actor>.+?) taking a turn\\.\\.\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DancePhaseEndsPattern = new(
        "^Debug: Dance Phase Ends Positive:(?<positive>.+?) Negative:(?<negative>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ChoosesPattern = new(
        "^Debug: (?<actor>.+?) chooses (?<choice>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BeganDancePattern = new(
        "^Debug: (?<actor>.+?) Began The Dance$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.DanceRitualOpponent");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "FireEvent", AccessTools.TypeByName("XRL.World.Event"));
        AddTarget(targets, targetType, "HandleEvent", AccessTools.TypeByName("XRL.World.BeforeAITakingActionEvent"));
        AddTarget(
            targets,
            targetType,
            "Register",
            AccessTools.TypeByName("XRL.World.GameObject"),
            AccessTools.TypeByName("XRL.IEventRegistrar"));
        return targets;
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
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
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = route;
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
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
        var match = BusyDancingPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "actor")}は踊りの最中だ！";
        DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
        return true;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = MessageFrameTranslator.MarkDirectTranslation(markedText);
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(message);
        if (!TryTranslateDebugQueue(message, stripped, spans, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            QueueFamily + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, params Type?[] parameterTypes)
    {
        if (Array.Exists(parameterTypes, parameterType => parameterType is null))
        {
            Trace.TraceError("QudJP: {0}.{1} parameter type not found.", Context, methodName);
            return;
        }

        var nonNullParameterTypes = Array.ConvertAll(parameterTypes, parameterType => parameterType!);
        var method = AccessTools.Method(targetType, methodName, nonNullParameterTypes);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslateDebugQueue(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        if (TryTranslateTakingTurn(source, stripped, spans, out translated))
        {
            detail = "HandleEvent.Debug";
            return true;
        }

        if (TryTranslateDancePhaseEnds(source, stripped, spans, out translated))
        {
            detail = "HandleEvent.Debug";
            return true;
        }

        if (TryTranslateChooses(source, stripped, spans, out translated))
        {
            detail = "HandleEvent.Debug";
            return true;
        }

        if (TryTranslateBeganDance(source, stripped, spans, out translated))
        {
            detail = "Register.Debug";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateTakingTurn(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = TakingTurnPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            "デバッグ: " + RestoreCapture(match, spans, "actor") + "がターンを実行中...",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateDancePhaseEnds(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = DancePhaseEndsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            "デバッグ: ダンスフェーズ終了 成功:"
            + RestoreCapture(match, spans, "positive")
            + " 失敗:"
            + RestoreCapture(match, spans, "negative"),
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateChooses(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = ChoosesPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            "デバッグ: "
            + RestoreCapture(match, spans, "actor")
            + "が"
            + RestoreCapture(match, spans, "choice")
            + "を選択",
            source,
            stripped,
            spans);
        return true;
    }

    private static bool TryTranslateBeganDance(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        var match = BeganDancePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            "デバッグ: " + RestoreCapture(match, spans, "actor") + "がダンスを始めた",
            source,
            stripped,
            spans);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreWholeSourceBoundary(
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
}
