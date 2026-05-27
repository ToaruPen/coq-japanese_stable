using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VehicleUnpoweredTranslationPatch
{
    private const string Context = nameof(VehicleUnpoweredTranslationPatch);

    private static readonly Regex CellDrainedPattern = new(
        "^(?<cell>.+?) (?:is|are) drained or nearly drained\\.\\n\\nRecharge or replace (?<pronoun>it|them|him|her) to power (?<vehicle>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InsertCellPattern = new(
        "^Insert (?<slot>.+?) to power (?<vehicle>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LacksPowerPattern = new(
        "^(?<vehicle>.+?) lacks the power to act\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;
    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var effectType = AccessTools.TypeByName("XRL.World.Effects.VehicleUnpowered");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (effectType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var preventActionMessage = AccessTools.Method(effectType, "PreventActionMessage", [gameObjectType]);
        if (preventActionMessage is not null)
        {
            yield return preventActionMessage;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.PreventActionMessage(GameObject) not found.", Context);
        }
    }

    public static void Prefix(out string? __state)
    {
        try
        {
            __state = directMarkerPassThroughText;
            OwnerDirectMarkerPopupScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            __state = directMarkerPassThroughText;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception, string? __state)
    {
        try
        {
            OwnerDirectMarkerPopupScope.Exit(ref activeDepth, ref directMarkerPassThroughText, __state);
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

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(
            source,
            ref directMarkerPassThroughText,
            out translated))
        {
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        var cellDrainedMatch = CellDrainedPattern.Match(stripped);
        if (cellDrainedMatch.Success)
        {
            translated = RestoreWhole(
                RestoreDisplayName(cellDrainedMatch, spans, "cell")
                + "は消耗しているか、ほとんど空だ。\n\n"
                + RestoreDisplayName(cellDrainedMatch, spans, "vehicle")
                + "に電力を供給するには再充電するか交換する必要がある。",
                spans,
                stripped.Length,
                source);
            Record(route, "CellDrained", source, translated);
            return true;
        }

        var insertCellMatch = InsertCellPattern.Match(stripped);
        if (insertCellMatch.Success)
        {
            translated = RestoreWhole(
                RestoreDisplayName(insertCellMatch, spans, "vehicle")
                + "に電力を供給するには"
                + RestoreDisplayName(insertCellMatch, spans, "slot")
                + "を挿入する必要がある。",
                spans,
                stripped.Length,
                source);
            Record(route, "InsertCell", source, translated);
            return true;
        }

        var lacksPowerMatch = LacksPowerPattern.Match(stripped);
        if (lacksPowerMatch.Success)
        {
            translated = RestoreWhole(
                RestoreDisplayName(lacksPowerMatch, spans, "vehicle") + "は行動する力がない。",
                spans,
                stripped.Length,
                source);
            Record(route, "LacksPower", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static void Record(string route, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreDisplayName(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return DisplayNameCaptureTranslator.StripLeadingEnglishArticlePreservingColors(Restore(match, spans, groupName));
    }

    private static string RestoreWhole(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int strippedLength,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            strippedLength,
            source);
    }
}
