using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhysicsEnterCellPassByTranslationPatch
{
    private const string Context = nameof(PhysicsEnterCellPassByTranslationPatch);
    private const string PassByPrefix = "You pass by ";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(
            "XRL.Messages.MessageQueue:AddPlayerMessage",
            new[] { typeof(string), typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: Failed to resolve MessageQueue.AddPlayerMessage(string, string, bool) for PhysicsEnterCellPassByTranslationPatch.");
        }

        return method;
    }

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(ref string Message, string? Color = null, bool Capitalize = true)
    {
        try
        {
            _ = Color;
            _ = Capitalize;

            if (string.IsNullOrEmpty(Message)
                || !Message.StartsWith(PassByPrefix, StringComparison.Ordinal))
            {
                return true;
            }

            Message = MessageLogProducerTranslationHelpers.PreparePassByMessage(Message, Context);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PhysicsEnterCellPassByTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }
}
