using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ZoneManagerSetActiveZoneTranslationPatch
{
    private const string Context = nameof(ZoneManagerSetActiveZoneTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static int pendingBannerCount;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.ZoneManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneTranslationPatch target type not found.");
            return null;
        }

        var zoneType = AccessTools.TypeByName("XRL.World.Zone");
        if (zoneType is null)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneTranslationPatch zone type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "SetActiveZone", new[] { zoneType });
        if (method is null)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneTranslationPatch.SetActiveZone(Zone) not found.");
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
            pendingBannerCount++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneTranslationPatch.Prefix failed: {0}", ex);
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

            if (activeDepth == 0)
            {
                pendingBannerCount = 0;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        if (activeDepth <= 0
            || pendingBannerCount <= 0
            || !string.Equals(color, "C", StringComparison.Ordinal))
        {
            return false;
        }

        pendingBannerCount--;
        message = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(message, Context);
        return true;
    }
}
