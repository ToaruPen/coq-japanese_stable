using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MessageLogPatch
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

    public static bool Prefix(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            _ = Color;
            _ = Capitalize;
            if (MessageFrameTranslator.TryStripDirectTranslationMarker(ref Message))
            {
                return true;
            }

            Message = MessagePatternTranslator.Translate(Message, nameof(MessageLogPatch));
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MessageLogPatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}
