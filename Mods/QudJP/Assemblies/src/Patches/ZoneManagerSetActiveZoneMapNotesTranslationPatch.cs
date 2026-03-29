using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ZoneManagerSetActiveZoneMapNotesTranslationPatch
{
    private const string Context = nameof(ZoneManagerSetActiveZoneMapNotesTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var zoneManagerType = AccessTools.TypeByName("XRL.World.ZoneManager");
        if (zoneManagerType is null)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneMapNotesTranslationPatch target type not found.");
            return null;
        }

        var zoneType = AccessTools.TypeByName("XRL.World.Zone");
        if (zoneType is null)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneMapNotesTranslationPatch zone type not found.");
            return null;
        }

        var method = AccessTools.Method(zoneManagerType, "SetActiveZone", new[] { zoneType });
        if (method is null)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneMapNotesTranslationPatch.SetActiveZone(Zone) not found.");
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
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneMapNotesTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneMapNotesTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && !string.Equals(color, "C", StringComparison.Ordinal)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "SetActiveZoneMapNotes", markJapaneseAsDirect: true);
    }
}
