using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VehicleSeatTranslationPatch
{
    private const string Context = nameof(VehicleSeatTranslationPatch);

    private static readonly Regex ConfirmationPattern = new(
        "^Accessing the pilot console requires the permanent insertion of (?<item>.+?)\\.\\n\\nAre you sure you want to proceed\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RequirementPattern = new(
        "^Accessing the pilot console requires the permanent insertion of (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var seatType = AccessTools.TypeByName("XRL.World.Parts.VehicleSeat");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (seatType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var attemptPilot = AccessTools.Method(seatType, "AttemptPilot", [gameObjectType]);
        if (attemptPilot is not null)
        {
            yield return attemptPilot;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.AttemptPilot(GameObject) not found.", Context);
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        var confirmationMatch = ConfirmationPattern.Match(stripped);
        if (confirmationMatch.Success)
        {
            translated = RestoreWhole(
                $"操縦コンソールへアクセスするには{RestoreItem(confirmationMatch, spans)}を恒久的に挿入する必要がある。\n\n続行しますか？",
                spans,
                stripped.Length,
                source);
            Record(route, "VehicleSeatPilotConsoleConfirmation", source, translated);
            return true;
        }

        var requirementMatch = RequirementPattern.Match(stripped);
        if (requirementMatch.Success)
        {
            translated = RestoreWhole(
                $"操縦コンソールへアクセスするには{RestoreItem(requirementMatch, spans)}を恒久的に挿入する必要がある。",
                spans,
                stripped.Length,
                source);
            Record(route, "VehicleSeatPilotConsoleRequirement", source, translated);
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

    private static string RestoreItem(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var item = match.Groups["item"];
        return ColorAwareTranslationComposer.RestoreCapture(item.Value, spans, item).Trim();
    }
}
