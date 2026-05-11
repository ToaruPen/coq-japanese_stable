using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhasedTranslationPatch
{
    private const string Context = nameof(PhasedTranslationPatch);

    private static readonly Regex PhaseBackInTimerPattern = new(
        "^You will phase back in in (?<duration>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.Phased");
        var effectAppliedEventType = AccessTools.TypeByName("XRL.World.EffectAppliedEvent");
        var beginTakeActionEventType = AccessTools.TypeByName("XRL.World.BeginTakeActionEvent");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || effectAppliedEventType is null || beginTakeActionEventType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "HandleEvent", new[] { effectAppliedEventType });
        AddTarget(targets, targetType, "HandleEvent", new[] { beginTakeActionEventType });
        AddTarget(targets, targetType, "Remove", new[] { gameObjectType });
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslateCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "Phased.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
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

    private static bool TryTranslateCore(string source, out string translated)
    {
        switch (source)
        {
            case "You phase out.":
                translated = "位相がずれた。";
                return true;
            case "You phase back in.":
                translated = "位相が戻った。";
                return true;
        }

        return TryTranslatePattern(source, out translated);
    }

    private static bool TryTranslatePattern(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PhaseBackInTimerPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{Restore(match, spans, "duration")}後に位相が戻る。",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
