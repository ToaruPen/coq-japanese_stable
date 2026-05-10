using System;
using System.Diagnostics;
using HarmonyLib;

namespace QudJP.Patches;

public static class CombatAndLogMessageQueuePatch
{
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
