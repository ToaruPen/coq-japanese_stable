using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LatchesOnTranslationPatch
{
    private const string Context = nameof(LatchesOnTranslationPatch);

    private static readonly Regex ReleasePattern = new(
        "^Since (?<item>.+?) (?:is|are) still latched onto (?<target>.+?), releasing (?<released>.+?) leaves (?<left>.+?) in (?<possession>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorReleasePattern = new(
        "^Since (?<item>.+?) (?:is|are) still latched onto you, (?<actor>.+?) releasing (?<released>.+?) leaves (?<left>.+?) in your possession!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LatchPattern = new(
        "^(?<subject>.+?) latches? onto (?<target>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.LatchesOn");
        var unequippedEventType = AccessTools.TypeByName("XRL.World.UnequippedEvent");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || unequippedEventType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "HandleEvent", new[] { unequippedEventType });
        AddTarget(targets, targetType, "FireEvent", new[] { eventType });
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        if (!TryTranslateCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "LatchesOn.Queue", message, translated);
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
        return TryTranslatePattern(
            ActorReleasePattern,
            source,
            (match, spans) =>
                $"{Restore(match, spans, "item")}はまだあなたに噛み付いているため、{Restore(match, spans, "actor")}が{Restore(match, spans, "released")}を放すと{Restore(match, spans, "left")}はあなたの所有物として残る！",
            out translated)
            || TryTranslatePattern(
                ReleasePattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "item")}はまだ{Restore(match, spans, "target")}に噛み付いているため、{Restore(match, spans, "released")}を放すと{Restore(match, spans, "left")}は{Restore(match, spans, "possession")}に残る！",
                out translated)
            || TryTranslatePattern(
                LatchPattern,
                source,
                (match, spans) =>
                    $"{Restore(match, spans, "subject")}が{Restore(match, spans, "target")}に噛み付いた！",
                out translated);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
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
