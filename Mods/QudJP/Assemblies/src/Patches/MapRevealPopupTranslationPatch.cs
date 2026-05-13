using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MapRevealPopupTranslationPatch
{
    private const string Context = nameof(MapRevealPopupTranslationPatch);

    private static readonly Regex OwnerConsumptionWarningPattern = new(
        "^(?<owner>.+?) (?:is|are) not owned by you, and using (?<target>.+?) will consume (?<consumed>.+?)\\. Are you sure you want to do so\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OrdinaryPaperPattern = new(
        "^(?<subject>.+?) seems? to be behaving as nothing more than an ordinary piece of paper\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MapOfSurroundingsPattern = new(
        "^(?:It's|They're|You're|It is|They are|You are|(?<subject>.+?) (?:is|are)) a map of your surroundings!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = GameTypeResolver.FindType("XRL.World.Parts.MapReveal", "MapReveal");
        var inventoryActionEventType = GameTypeResolver.FindType("XRL.World.InventoryActionEvent", "InventoryActionEvent");
        if (targetType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve MapReveal or InventoryActionEvent.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", [inventoryActionEventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HandleEvent(InventoryActionEvent) target not found.", Context);
            yield break;
        }

        yield return method;
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
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
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        try
        {
            var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
            return TryTranslateOwnerConsumptionWarning(source, stripped, spans, route, family, out translated)
                || TryTranslateOrdinaryPaper(source, stripped, spans, route, family, out translated)
                || TryTranslateMapOfSurroundings(source, stripped, spans, route, family, out translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TryTranslatePopupMessage failed: {1}", Context, ex);
            translated = source;
            return false;
        }
    }

    private static bool TryTranslateOwnerConsumptionWarning(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = OwnerConsumptionWarningPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            string.Concat(
                RestoreCapture(match, spans, "owner"),
                "はあなたのものではなく、",
                RestoreCapture(match, spans, "target"),
                "を使うと",
                RestoreCapture(match, spans, "consumed"),
                "は消費される。本当に行うか？"),
            stripped,
            spans,
            source);
        Record(route, family, "OwnerConsumptionWarning", source, translated);
        return true;
    }

    private static bool TryTranslateOrdinaryPaper(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = OrdinaryPaperPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            RestoreCapture(match, spans, "subject") + "は普通の紙切れとしてしか振る舞っていないようだ。",
            stripped,
            spans,
            source);
        Record(route, family, "OrdinaryPaper", source, translated);
        return true;
    }

    private static bool TryTranslateMapOfSurroundings(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = MapOfSurroundingsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var subject = match.Groups["subject"];
        translated = RestoreWholeSourceBoundary(
            subject.Success
                ? RestoreCapture(match, spans, "subject") + "は周囲の地図だ！"
                : "周囲の地図だ！",
            stripped,
            spans,
            source);
        Record(route, family, "MapOfSurroundings", source, translated);
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreWholeSourceBoundary(
        string translated,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        _ = family;
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
    }
}
