using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GeomagneticDiscTranslationPatch
{
    private const string Context = nameof(GeomagneticDiscTranslationPatch);

    private static readonly Regex FailureGlyphPattern = new(
        "^A loud buzz is emitted\\. The failure glyph flashes on the side of (?<disc>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LowPowerGlyphPattern = new(
        "^A loud buzz is emitted\\. The low power glyph flashes on the side of (?<disc>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SuddenlyFlyingPattern = new(
        "^(?<disc>.+?) suddenly starts? flying around!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.GeomagneticDisc");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var examineEventType = AccessTools.TypeByName("XRL.World.IExamineEvent");
        if (targetType is null || gameObjectType is null || examineEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "SignalFailure", new[] { gameObjectType });
        AddTarget(targets, targetType, "SignalLowPower", new[] { gameObjectType });
        AddTarget(targets, targetType, "ExamineFailure", new[] { examineEventType, typeof(int) });
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

        if (!TryTranslatePattern(
                FailureGlyphPattern,
                source,
                disc => $"大きなブザー音が鳴り、{disc}の側面で故障のグリフが点滅した。",
                out translated)
            && !TryTranslatePattern(
                LowPowerGlyphPattern,
                source,
                disc => $"大きなブザー音が鳴り、{disc}の側面で低電力のグリフが点滅した。",
                out translated)
            && !TryTranslatePattern(
                SuddenlyFlyingPattern,
                source,
                disc => $"{disc}が突然飛び回り始めた！",
                out translated))
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

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<string, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var group = match.Groups["disc"];
        var disc = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(disc),
            spans,
            stripped.Length,
            source);
        return true;
    }
}
