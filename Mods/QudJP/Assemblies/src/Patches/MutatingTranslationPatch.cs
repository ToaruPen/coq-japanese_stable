using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MutatingTranslationPatch
{
    private const string Context = nameof(MutatingTranslationPatch);

    private static readonly Regex NewMutationPattern = new(
        "^Your genome destabilizes and you gain a new mutation:\\n\\n(?<name>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NewDefectPattern = new(
        "^Your genome destabilizes and you gain a new defect:\\n\\n(?<name>[\\s\\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MutationPointsPattern = new(
        "^Your genome destabilizes and you gain (?<points>.+?) mutation points?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.Mutating");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var endTurnEventType = AccessTools.TypeByName("XRL.World.EndTurnEvent");
        if (targetType is null || gameObjectType is null || endTurnEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "Apply", new[] { gameObjectType });
        AddTarget(targets, targetType, "HandleEvent", new[] { endTurnEventType });
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

        var translated = message switch
        {
            "You start to feel unstable." => "不安定になり始めた。",
            "You feel increasingly unstable." => "ますます不安定になってきた。",
            _ => string.Empty,
        };

        if (translated.Length == 0)
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "Mutating.Queue", message, translated);
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

        if (!TryTranslatePopupCore(source, out translated))
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

    private static bool TryTranslatePopupCore(string source, out string translated)
    {
        return TryTranslatePattern(
            NewMutationPattern,
            source,
            (match, spans) => $"ゲノムが不安定化し、新しい変異を得た:\n\n{Restore(match, spans, "name")}",
            out translated)
            || TryTranslatePattern(
                NewDefectPattern,
                source,
                (match, spans) => $"ゲノムが不安定化し、新しい欠陥を得た:\n\n{Restore(match, spans, "name")}",
                out translated)
            || TryTranslatePattern(
                MutationPointsPattern,
                source,
                (match, spans) => $"ゲノムが不安定化し、変異ポイントを{Restore(match, spans, "points")}得た。",
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
