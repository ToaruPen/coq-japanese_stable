using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ConversationRewardPopupTranslationPatch
{
    private const string Context = nameof(ConversationRewardPopupTranslationPatch);

    private static readonly Regex SlynthSanctuaryPattern = new(
        "^(?<sanctuary>.+?) (?:is|are) now a sanctuary option for the slynth\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PaxInfectLimbPattern = new(
        "^You've contracted (?<infection>.+?) on your (?<part>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReceiveItemPattern = new(
        "^You receive (?<items>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var enteredElementEventType = AccessTools.TypeByName("XRL.World.Conversations.EnteredElementEvent");
        var bodyPartType = AccessTools.TypeByName("XRL.World.Anatomy.BodyPart");
        if (enteredElementEventType is null || bodyPartType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve conversation reward target types.", Context);
            yield break;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.AddSlynthCandidate",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }

        var bodyPartListType = typeof(List<>).MakeGenericType(bodyPartType);
        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.PaxInfectLimb",
                     "InfectLimb",
                     [bodyPartListType, bodyPartType, typeof(string)]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Conversations.Parts.ReceiveItem",
                     "HandleEvent",
                     [enteredElementEventType]))
        {
            yield return method;
        }
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
        _ = family;

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
            return TryTranslateSlynthSanctuary(source, stripped, spans, route, out translated)
                || TryTranslatePaxInfectLimb(source, stripped, spans, route, out translated)
                || TryTranslateReceiveItem(source, stripped, spans, route, out translated);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TryTranslatePopupMessage failed: {1}", Context, ex);
            translated = source;
            return false;
        }
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }

    private static bool TryTranslateSlynthSanctuary(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = SlynthSanctuaryPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            Restore(match, spans, "sanctuary") + "がスリンスの聖域候補になった。",
            stripped,
            spans,
            source);
        Record(route, "SlynthSanctuary", source, translated);
        return true;
    }

    private static bool TryTranslatePaxInfectLimb(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = PaxInfectLimbPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            $"{Restore(match, spans, "part")}に{Restore(match, spans, "infection")}を発症した。",
            stripped,
            spans,
            source);
        Record(route, "PaxInfectLimb", source, translated);
        return true;
    }

    private static bool TryTranslateReceiveItem(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        out string translated)
    {
        var match = ReceiveItemPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreWholeSourceBoundary(
            Restore(match, spans, "items") + "を受け取った！",
            stripped,
            spans,
            source);
        Record(route, "ReceiveItem", source, translated);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
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

    private static void Record(string route, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
    }
}
