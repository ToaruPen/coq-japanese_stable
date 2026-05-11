using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FlightTranslationPatch
{
    private const string Context = nameof(FlightTranslationPatch);

    private static readonly Regex ThirdPersonBeginFlyingPattern = new(
        "^(?<subject>.+?) begins? flying\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ThirdPersonReturnToGroundPattern = new(
        "^(?<subject>.+?) returns? to the ground\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ThirdPersonFallToGroundPattern = new(
        "^(?<subject>.+?) falls? to the ground\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var flightType = AccessTools.TypeByName("XRL.World.Capabilities.Flight");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var flightSourceType = AccessTools.TypeByName("XRL.World.Capabilities.IFlightSource");
        if (flightType is null || gameObjectType is null || flightSourceType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, flightType, "StartFlying", new[] { gameObjectType, gameObjectType, flightSourceType });
        AddTarget(
            targets,
            flightType,
            "StopFlying",
            new[] { gameObjectType, gameObjectType, flightSourceType, typeof(bool), typeof(bool) });
        AddTarget(targets, flightType, "Land", new[] { gameObjectType, typeof(bool) });
        AddTarget(targets, flightType, "FailFlying", new[] { gameObjectType, gameObjectType, flightSourceType });
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

        var source = message;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!TryTranslateCore(source, stripped, spans, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "Flight", source, translated);
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

    private static bool TryTranslateCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        translated = stripped switch
        {
            "You begin flying!" => "飛行を開始した！",
            "You begin using an additional flight capability." => "追加の飛行手段を使い始めた。",
            "You return to the ground." => "地上に戻った。",
            "You cease using one of your flight capabilities." => "飛行手段の1つの使用をやめた。",
            "You fall to the ground!" => "地面に落下した！",
            "One of your flight capabilities fails." => "飛行能力のひとつが失われた。",
            _ => string.Empty,
        };

        if (translated.Length > 0)
        {
            translated = RestoreWholeSourceBoundary(translated, source, stripped, spans);
            return true;
        }

        return TryTranslateThirdPerson(
            ThirdPersonBeginFlyingPattern,
            source,
            stripped,
            spans,
            subject => subject + "が飛行を開始した。",
            out translated)
            || TryTranslateThirdPerson(
                ThirdPersonReturnToGroundPattern,
                source,
                stripped,
                spans,
                subject => subject + "が地上に戻った。",
                out translated)
            || TryTranslateThirdPerson(
                ThirdPersonFallToGroundPattern,
                source,
                stripped,
                spans,
                subject => subject + "が地面に落下した。",
                out translated);
    }

    private static bool TryTranslateThirdPerson(
        Regex pattern,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Func<string, string> translate,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = RestoreSubject(match, spans);
        translated = RestoreWholeSourceBoundary(translate(subject), source, stripped, spans);
        return true;
    }

    private static string RestoreSubject(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["subject"];
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
