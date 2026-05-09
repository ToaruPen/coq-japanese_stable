using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BeguilingSifrahTranslationPatch
{
    private const string Context = nameof(BeguilingSifrahTranslationPatch);

    private static readonly Regex CriticalFailurePattern =
        new Regex("^Your coquetry infuriates (?<target>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FailurePattern =
        new Regex("^Your coquetry does not impress (?<target>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PartialSuccessPattern =
        new Regex("^Your coquetry does not overcome (?<target>.+?), but (?<subject>.+?) interested in hearing more\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InterestedButUnablePattern =
        new Regex("^(?<target>.+?) (?:is|are) interested, but unable to join you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var sifrahType = AccessTools.TypeByName("XRL.World.BeguilingSifrah");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (sifrahType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve BeguilingSifrah or GameObject.", Context);
            yield break;
        }

        foreach (var methodName in new[]
                 {
                     "ResultCriticalFailure",
                     "ResultFailure",
                     "ResultPartialSuccess",
                     "ResultSuccess",
                     "ResultExceptionalSuccess",
                 })
        {
            var method = AccessTools.Method(sifrahType, methodName, [gameObjectType]);
            if (method is not null)
            {
                yield return method;
            }
            else
            {
                Trace.TraceError("QudJP: {0}.{1}(GameObject) not found.", Context, methodName);
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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslate(CriticalFailurePattern, "を口説こうとして怒らせた。", source, stripped, spans, route, family, "CriticalFailure", out translated)
            || TryTranslate(FailurePattern, "に口説き文句は響かなかった。", source, stripped, spans, route, family, "Failure", out translated)
            || TryTranslatePartialSuccess(source, stripped, spans, route, family, out translated)
            || TryTranslateInterestedButUnable(source, stripped, spans, route, family, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslate(
        Regex pattern,
        string suffix,
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

        translated = RestoreWholeSourceBoundary(RestoreCapture(match, spans, "target") + suffix, stripped, spans);
        Record(route, family, detail, source, translated);
        return true;
    }

    private static bool TryTranslatePartialSuccess(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = PartialSuccessPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            RestoreCapture(match, spans, "target") + "を口説き落とせなかったが、さらに聞きたがっている。",
            stripped,
            spans);
        Record(route, family, "PartialSuccess", source, translated);
        return true;
    }

    private static bool TryTranslateInterestedButUnable(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = InterestedButUnablePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            RestoreCapture(match, spans, "target") + "は興味を示しているが、あなたに加われない。",
            stripped,
            spans);
        Record(route, family, "InterestedButUnable", source, translated);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            group.Value,
            spans,
            group).Trim();
    }

    private static string RestoreWholeSourceBoundary(
        string translated,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length);
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
