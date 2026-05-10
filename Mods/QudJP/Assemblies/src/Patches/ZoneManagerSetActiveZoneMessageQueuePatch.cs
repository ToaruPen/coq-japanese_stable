using System;
using System.Diagnostics;
using HarmonyLib;

namespace QudJP.Patches;

public static class ZoneManagerSetActiveZoneMessageQueuePatch
{
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            _ = Capitalize;
            _ = ZoneManagerSetActiveZoneTranslationPatch.TryTranslateQueuedMessage(ref Message, Color);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ZoneManagerSetActiveZoneMessageQueuePatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}
