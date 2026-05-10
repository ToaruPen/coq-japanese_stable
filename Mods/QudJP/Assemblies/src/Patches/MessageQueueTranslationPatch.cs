using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MessageQueueTranslationPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(
            "XRL.Messages.MessageQueue:AddPlayerMessage",
            new[] { typeof(string), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve MessageQueue.AddPlayerMessage(string, string, bool). Patch will not apply.");
        }

        return method;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool PrefixPhysicsEnterCellPassBy(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            return PhysicsEnterCellPassByTranslationPatch.Prefix(ref Message, Color, Capitalize);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageQueueTranslationPatch.PrefixPhysicsEnterCellPassBy failed: {0}", ex);
            return true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool PrefixZoneManagerSetActiveZone(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            return ZoneManagerSetActiveZoneMessageQueuePatch.Prefix(ref Message, Color, Capitalize);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageQueueTranslationPatch.PrefixZoneManagerSetActiveZone failed: {0}", ex);
            return true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First - 1)]
    public static bool PrefixCombatAndLog(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            return CombatAndLogMessageQueuePatch.Prefix(ref Message, Color, Capitalize);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageQueueTranslationPatch.PrefixCombatAndLog failed: {0}", ex);
            return true;
        }
    }

    [HarmonyPrefix]
    public static bool PrefixMessageLog(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            return MessageLogPatch.Prefix(ref Message, Color, Capitalize);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageQueueTranslationPatch.PrefixMessageLog failed: {0}", ex);
            return true;
        }
    }
}
