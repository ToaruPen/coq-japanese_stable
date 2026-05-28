using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ZoneManagerGenerateZoneTranslationPatch
{
    private const string Context = nameof(ZoneManagerGenerateZoneTranslationPatch);
    private const string ForceStopPrompt =
        "This zone isn't building properly. Do you want to force it to stop and build immediately?";
    private const string TranslatedForceStopPrompt =
        "このゾーンは正しく構築されていない。強制的に停止して、すぐに構築しますか？";
    private const string ReportIssuePrefix = "There was an issue building this zone. Automatically report it to us? ";
    private const string TranslatedReportIssuePrefix = "このゾーンの構築中に問題が発生した。自動的に報告しますか？ ";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var zoneManagerType = AccessTools.TypeByName("XRL.World.ZoneManager");
        if (zoneManagerType is null)
        {
            Trace.TraceError("QudJP: ZoneManagerGenerateZoneTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(zoneManagerType, "GenerateZone", new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: ZoneManagerGenerateZoneTranslationPatch.GenerateZone(string) not found.");
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ZoneManagerGenerateZoneTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: ZoneManagerGenerateZoneTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && message.StartsWith("Zone build failure:", StringComparison.Ordinal)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "GenerateZone");
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = family;

        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (source == ForceStopPrompt)
        {
            translated = TranslatedForceStopPrompt;
            DynamicTextObservability.RecordTransform(
                route,
                "Popup.ProducerText." + Context + ".GenerateZoneForceStopPopup",
                source,
                translated);
            return true;
        }

        if (!source.StartsWith(ReportIssuePrefix, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = TranslatedReportIssuePrefix + source.Substring(ReportIssuePrefix.Length);
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".GenerateZoneReportIssuePopup",
            source,
            translated);
        return true;
    }
}
