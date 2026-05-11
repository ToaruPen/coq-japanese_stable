using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LightManipulationTranslationPatch
{
    private const string Context = nameof(LightManipulationTranslationPatch);

    private static readonly Regex EnableAmbientLightCooldownPattern = new(
        "^You must wait (?<duration>.+?) before you can enable ambient light\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YourBeamNoPenetrationPattern = new(
        "^Your laser beam doesn't penetrate (?<armor>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TheirBeamNoPenetrationPattern = new(
        "^(?<beam>.+?) doesn't penetrate your armor\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.LightManipulation");
        var commandEventType = AccessTools.TypeByName("XRL.World.CommandEvent");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        if (targetType is null || commandEventType is null || cellType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "HandleEvent", new[] { commandEventType });
        AddTarget(targets, targetType, "Lase", new[] { cellType, typeof(int) });
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

        DynamicTextObservability.RecordTransform(Context, "LightManipulation.Queue", message, translated);
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
        if (source == "The darkness absorbs the laser beam.")
        {
            translated = "闇がレーザービームを吸収した。";
            return true;
        }

        return TryTranslatePattern(
            EnableAmbientLightCooldownPattern,
            source,
            (match, spans) => $"環境光を有効化するには{Restore(match, spans, "duration")}待つ必要がある。",
            out translated)
            || TryTranslatePattern(
                YourBeamNoPenetrationPattern,
                source,
                (match, spans) => $"あなたのレーザービームは{Restore(match, spans, "armor")}を貫通しなかった。",
                out translated)
            || TryTranslatePattern(
                TheirBeamNoPenetrationPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "beam")}はあなたの装甲を貫通しなかった。",
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
