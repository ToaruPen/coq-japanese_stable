using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ExaminerTranslationPatch
{
    private const string Context = nameof(ExaminerTranslationPatch);

    private static readonly Regex UnderstandPattern =
        new Regex("^You now understand (?<target>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DiscoverHiddenPattern =
        new Regex("^You discover something about (?<target>.+?) that was hidden!$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PuzzledPattern =
        new Regex("^You are puzzled by (?<target>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BrokePattern =
        new Regex("^You think you broke (?<target>.+?)\\.\\.\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var examinerType = AccessTools.TypeByName("XRL.World.Parts.Examiner");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (examinerType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Examiner or GameObject.", Context);
            yield break;
        }

        foreach (var methodName in new[]
                 {
                     "ResultSuccess",
                     "ResultExceptionalSuccess",
                     "ResultFailure",
                     "ResultFakeConfusionFailure",
                 })
        {
            var method = AccessTools.Method(examinerType, methodName, [gameObjectType]);
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
        if (TryTranslate(UnderstandPattern, static target => target + "を理解した。", source, stripped, spans, route, family, "Understand", out translated)
            || TryTranslate(DiscoverHiddenPattern, static target => target + "について隠されていたことを発見した！", source, stripped, spans, route, family, "DiscoverHidden", out translated)
            || TryTranslate(PuzzledPattern, static target => target + "のことがわからない。", source, stripped, spans, route, family, "Puzzled", out translated)
            || TryTranslate(BrokePattern, static target => target + "を壊してしまった気がする。", source, stripped, spans, route, family, "Broke", out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslate(
        Regex pattern,
        Func<string, string> build,
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

        translated = RestoreWholeSourceBoundary(build(RestoreCapture(match, spans, "target")), stripped, spans);
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
        string translatedCore,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length);
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
