using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VehicleFollowerPopupTranslationPatch
{
    private const string Context = nameof(VehicleFollowerPopupTranslationPatch);

    private static readonly Regex NoFollowersPattern = new(
        "^You have no followers that can enter (?<vehicle>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var vehicleType = AccessTools.TypeByName("XRL.World.Parts.Vehicle");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (vehicleType is null || inventoryActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var method = AccessTools.Method(vehicleType, "HandleEvent", [inventoryActionEventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.Vehicle.HandleEvent(InventoryActionEvent) not found.", Context);
        }

        return method;
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
        translated = source;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = NoFollowersPattern.Match(stripped);
        if (!match.Success)
        {
            return false;
        }

        var translatedWithoutWholeSourceWrappers = RestoreCapture(match, spans, "vehicle") + "に入れる仲間はいない。";
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedWithoutWholeSourceWrappers,
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Context + ".NoFollowers", source, translated);
        return true;
    }

    internal static bool TryTranslatePopupProducerText(string source, string route, string family, out string translated)
    {
        translated = source;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!string.Equals(stripped, "Choose a follower", StringComparison.Ordinal))
        {
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "仲間を選ぶ。",
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(
            route,
            family + "." + Context + ".PickGameObjectTitle",
            source,
            translated);
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
}
