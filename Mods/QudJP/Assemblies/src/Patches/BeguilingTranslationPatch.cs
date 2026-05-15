using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BeguilingTranslationPatch
{
    private const string Context = nameof(BeguilingTranslationPatch);

    private static readonly Regex CannotBeguilePattern = new(
        "^You can't beguile (?<target>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ImperviousPattern = new(
        "^(?<target>.+?) seems? utterly impervious to your charms\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AlreadyBeguiledPattern = new(
        "^You have already beguiled (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AlreadyFollowerPattern = new(
        "^(?<target>.+?) (?:is|are) already your follower\\. Do you want to beguile (?<them>.+?) anyway\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutshinePattern = new(
        "^You fail to outshine the current object of (?<affection>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CoquetryPattern = new(
        "^Your coquetry infuriates (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Beguiling");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var mentalAttackEventType = AccessTools.TypeByName("XRL.World.MentalAttackEvent");
        if (targetType is null || gameObjectType is null || eventType is null || mentalAttackEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "Cast", new[] { gameObjectType, targetType, eventType, typeof(int) });
        AddTarget(targets, targetType, "Beguile", new[] { mentalAttackEventType });
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

        DynamicTextObservability.RecordTransform(Context, "Beguiling.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
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

        if (!TryTranslateCore(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
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
            CannotBeguilePattern,
            source,
            (match, spans) => $"{Restore(match, spans, "target")}を魅了できない！",
            out translated)
            || TryTranslatePattern(
                ImperviousPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "target")}はあなたの魅力にまったく動じない。",
                out translated)
            || TryTranslatePattern(
                AlreadyBeguiledPattern,
                source,
                (match, spans) => $"すでに{Restore(match, spans, "target")}を魅了している。",
                out translated)
            || TryTranslatePattern(
                AlreadyFollowerPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "target")}はすでにあなたの仲間だ。それでも{Restore(match, spans, "them")}を魅了しますか？",
                out translated)
            || TryTranslatePattern(
                OutshinePattern,
                source,
                (match, spans) => $"{Restore(match, spans, "affection")}の現在の想い人を上回れなかった。",
                out translated)
            || TryTranslatePattern(
                CoquetryPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "target")}を口説こうとして怒らせた。",
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
