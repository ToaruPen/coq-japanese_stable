using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RebukingSifrahTranslationPatch
{
    private const string Context = nameof(RebukingSifrahTranslationPatch);

    private static readonly Regex CriticalFailurePattern =
        new Regex("^(?<target>.+?) (?:is|are) enraged by your poor reasoning\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PartialSuccessPattern =
        new Regex("^(?<target>.+?) (?:wanders|wander) away disinterestedly\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var sifrahType = AccessTools.TypeByName("XRL.World.RebukingSifrah");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (sifrahType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve RebukingSifrah or GameObject.", Context);
            yield break;
        }

        foreach (var methodName in new[]
                 {
                     "ResultCriticalFailure",
                     "ResultPartialSuccess",
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
        if (TryTranslate(CriticalFailurePattern, "はあなたの拙い論理に激怒した。", source, stripped, spans, route, family, "CriticalFailure", out translated)
            || TryTranslate(PartialSuccessPattern, "は興味なさげに立ち去った。", source, stripped, spans, route, family, "PartialSuccess", out translated))
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
