using System;
using System.Diagnostics;
using HarmonyLib;

namespace QudJP.Patches;

public static class PhysicsEnterCellPassByTranslationPatch
{
    private const string Context = nameof(PhysicsEnterCellPassByTranslationPatch);
    private const string PassByPrefix = "You pass by ";

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
