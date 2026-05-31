using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PlayerDanceRitualTranslationPatch
{
    private const string Context = nameof(PlayerDanceRitualTranslationPatch);
    private const string QueueFamily = "PlayerDanceRitual.Queue";

    private static readonly Regex ExecuteMovePattern =
        new Regex("^(?<actor>.+?) steps (?<direction>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PassStepPattern =
        new Regex("^You executed that step correctly! \\[(?<reason>.*?)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FailStepPattern =
        new Regex("^You executed that step incorrectly! \\[(?<reason>.*?)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FailDancePattern =
        new Regex("^The dance ended in failure! \\[(?<reason>.*?)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SuccessDancePattern =
        new Regex("^The dance ended in success! \\[(?<reason>.*?)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DebugTurnTickPattern =
        new Regex(
            "^Debug: Dance party turn tick (?<tick>.+?) Current Approval:(?<approval>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var ritualType = AccessTools.TypeByName("XRL.World.Parts.PlayerDanceRitual");
        if (ritualType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve PlayerDanceRitual.", Context);
            yield break;
        }

        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve XRL.World.Event.", Context);
        }

        var targetSpecs = new List<(string MethodName, Type[] ParameterTypes)>
        {
            ("ExecuteMove", new[] { typeof(string), typeof(string) }),
            ("PassStep", new[] { typeof(string) }),
            ("FailStep", new[] { typeof(string) }),
            ("FailDance", new[] { typeof(string) }),
            ("SuccessDance", new[] { typeof(string) }),
        };
        if (eventType is not null)
        {
            targetSpecs.Insert(0, ("FireEvent", new[] { eventType }));
        }

        foreach (var (methodName, parameterTypes) in targetSpecs)
        {
            var method = AccessTools.Method(ritualType, methodName, parameterTypes);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}({2}) not found.", Context, methodName, string.Join(", ", (IEnumerable<Type>)parameterTypes));
            }
        }
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0 || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (!TryTranslateMessage(message, Context, QueueFamily, out var translated))
        {
            return false;
        }

        message = translated;
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        return TryTranslatePopup(source, route, family + "." + Context, out translated);
    }

    internal static bool TryTranslateMessage(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source) || MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateExecuteMove(source, stripped, spans, route, family, out translated)
            || TryTranslateStep(PassStepPattern, "そのステップを正しく実行した！", source, stripped, spans, route, family, "PassStep", out translated)
            || TryTranslateStep(FailStepPattern, "そのステップを誤って実行した！", source, stripped, spans, route, family, "FailStep", out translated)
            || TryTranslateDebugTurnTick(source, stripped, spans, route, family, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslatePopup(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source) || MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateStep(FailDancePattern, "踊りは失敗に終わった！", source, stripped, spans, route, family, "FailDance", out translated)
            || TryTranslateStep(SuccessDancePattern, "踊りは成功に終わった！", source, stripped, spans, route, family, "SuccessDance", out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateExecuteMove(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ExecuteMovePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var actor = TranslateActor(match, spans);
        var direction = RestoreCapture(match, spans, "direction");
        translated = RestoreWholeSourceBoundary(actor + "は" + direction + "へ一歩進んだ。", source, stripped, spans);
        Record(route, family, "ExecuteMove", source, translated);
        return true;
    }

    private static bool TryTranslateDebugTurnTick(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = DebugTurnTickPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            "デバッグ: ダンスパーティーのターン tick "
            + RestoreCapture(match, spans, "tick")
            + " 現在の評価:"
            + RestoreCapture(match, spans, "approval"),
            source,
            stripped,
            spans);
        Record(route, family, "FireEvent.DebugTurnTick", source, translated);
        return true;
    }

    private static bool TryTranslateStep(
        Regex pattern,
        string message,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string detail,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            message + " [" + RestoreCapture(match, spans, "reason") + "]",
            source,
            stripped,
            spans);
        Record(route, family, detail, source, translated);
        return true;
    }

    private static string TranslateActor(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["actor"];
        var translated = group.Value switch
        {
            "Player" => "あなた",
            "Opponent" => "相手",
            _ => group.Value,
        };

        return ColorAwareTranslationComposer.RestoreCapture(translated, spans, group).Trim();
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
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

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + detail, source, translated);
    }
}
