using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ZoneManagerTickTranslationPatch
{
    private const string Context = nameof(ZoneManagerTickTranslationPatch);
    private const string WarningPrefix = "&RWARNING: You have the Disable Zone Caching option enabled, this will cause massive memory use over time.";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var zoneManagerType = AccessTools.TypeByName("XRL.World.ZoneManager");
        if (zoneManagerType is null)
        {
            Trace.TraceError("QudJP: ZoneManagerTickTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(zoneManagerType, "Tick", new[] { typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: ZoneManagerTickTranslationPatch.Tick(bool) not found.");
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
            Trace.TraceError("QudJP: ZoneManagerTickTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static void Postfix()
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
            Trace.TraceError("QudJP: ZoneManagerTickTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && string.Equals(message, WarningPrefix, StringComparison.Ordinal)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "Tick");
    }
}
