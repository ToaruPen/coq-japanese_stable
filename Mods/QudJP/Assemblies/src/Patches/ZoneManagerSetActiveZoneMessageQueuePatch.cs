using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ZoneManagerSetActiveZoneMessageQueuePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(
            "XRL.Messages.MessageQueue:AddPlayerMessage",
            new[] { typeof(string), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve MessageQueue.AddPlayerMessage(string, string, bool) for ZoneManagerSetActiveZoneMessageQueuePatch.");
        }

        return method;
    }

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
