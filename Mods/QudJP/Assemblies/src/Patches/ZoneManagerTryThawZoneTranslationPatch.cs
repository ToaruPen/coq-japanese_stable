using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ZoneManagerTryThawZoneTranslationPatch
{
    private const string Context = nameof(ZoneManagerTryThawZoneTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var zoneManagerType = AccessTools.TypeByName("XRL.World.ZoneManager");
        var zoneType = AccessTools.TypeByName("XRL.World.Zone");
        if (zoneManagerType is null || zoneType is null)
        {
            Trace.TraceError("QudJP: ZoneManagerTryThawZoneTranslationPatch target types not found.");
            return null;
        }

        var method = AccessTools.Method(zoneManagerType, "TryThawZone", new[] { typeof(string), zoneType.MakeByRefType() });
        if (method is null)
        {
            Trace.TraceError("QudJP: ZoneManagerTryThawZoneTranslationPatch.TryThawZone(string, out Zone) not found.");
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
            Trace.TraceError("QudJP: ZoneManagerTryThawZoneTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: ZoneManagerTryThawZoneTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && string.Equals(message, "ThawZone exception", StringComparison.Ordinal)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "TryThawZone");
    }
}
