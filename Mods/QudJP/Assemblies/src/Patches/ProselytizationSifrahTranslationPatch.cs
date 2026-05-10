using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ProselytizationSifrahTranslationPatch
{
    private const string Context = nameof(ProselytizationSifrahTranslationPatch);

    private static readonly Regex CriticalFailurePattern =
        new Regex("^(?<target>.+?) (?:is|are) offended by your impertinence\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FailurePattern =
        new Regex("^(?<target>.+?) (?:is|are) unconvinced by your pleas\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PartialSuccessPattern =
        new Regex("^(?<target>.+?) (?:is|are) unconvinced by your pleas, but interested in hearing more\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SympatheticButUnablePattern =
        new Regex("^(?<target>.+?) (?:is|are) sympathetic, but unable to join you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var sifrahType = AccessTools.TypeByName("XRL.World.ProselytizationSifrah");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (sifrahType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve ProselytizationSifrah or GameObject.", Context);
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
        if (TryTranslate(CriticalFailurePattern, "はあなたの無礼に気分を害した。", source, stripped, spans, route, family, "CriticalFailure", out translated)
            || TryTranslate(FailurePattern, "はあなたの懇願に納得しなかった。", source, stripped, spans, route, family, "Failure", out translated)
            || TryTranslate(PartialSuccessPattern, "はあなたの懇願に納得しなかったが、さらに聞きたがっている。", source, stripped, spans, route, family, "PartialSuccess", out translated)
            || TryTranslate(SympatheticButUnablePattern, "は同情的だが、あなたに加われない。", source, stripped, spans, route, family, "SympatheticButUnable", out translated))
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
