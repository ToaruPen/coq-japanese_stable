using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BuddingTranslationPatch
{
    private const string Context = nameof(BuddingTranslationPatch);

    private static readonly Regex BeginBuddingPattern = new(
        "^A grotesque protuberance swells from (?<origin>.+?) as (?<subject>.+?) begins? to bud!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SubsideOnPattern = new(
        "^The grotesque protuberance on (?<origin>.+?) subsides\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PossessiveSubsidePattern = new(
        "^(?<protuberance>.+? grotesque protuberance) subsides\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.Budding");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "Apply", new[] { gameObjectType });
        AddTarget(targets, targetType, "Remove", new[] { gameObjectType });
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

        DynamicTextObservability.RecordTransform(Context, "Budding.Queue", message, translated);
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
            BeginBuddingPattern,
            source,
            (match, spans) =>
                $"{Restore(match, spans, "origin")}からグロテスクな突起が膨らみ、{Restore(match, spans, "subject")}が芽吹き始めた！",
            out translated)
            || TryTranslatePattern(
                SubsideOnPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "origin")}のグロテスクな突起が引っ込んだ。",
                out translated)
            || TryTranslatePattern(
                PossessiveSubsidePattern,
                source,
                (match, spans) => $"{Restore(match, spans, "protuberance")}が引っ込んだ。",
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
