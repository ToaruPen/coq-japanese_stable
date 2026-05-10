using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CombatAndLogMessageQueuePatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(
            "XRL.Messages.MessageQueue:AddPlayerMessage",
            new[] { typeof(string), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve MessageQueue.AddPlayerMessage(string, string, bool) for CombatAndLogMessageQueuePatch.");
        }

        return method;
    }

    [HarmonyPriority(Priority.First - 1)]
    public static bool Prefix(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            _ = Capitalize;

            _ = MessageQueueSemanticPipeline.TryTranslateQueuedMessage(ref Message, Color);

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CombatAndLogMessageQueuePatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}
