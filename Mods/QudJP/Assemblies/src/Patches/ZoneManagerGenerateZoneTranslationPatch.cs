using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ZoneManagerGenerateZoneTranslationPatch
{
    private const string Context = nameof(ZoneManagerGenerateZoneTranslationPatch);

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
}
