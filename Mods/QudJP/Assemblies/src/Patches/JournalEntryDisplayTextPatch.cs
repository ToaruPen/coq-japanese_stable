using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class JournalEntryDisplayTextPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.API.IBaseJournalEntry", "IBaseJournalEntry");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: JournalEntryDisplayTextPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "GetDisplayText", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: JournalEntryDisplayTextPatch.GetDisplayText not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance, ref string __result)
    {
        try
        {
            if (__instance is null || string.IsNullOrEmpty(__result))
            {
                return;
            }

            if (!JournalTextTranslator.TryTranslateBaseEntry(
                    __instance,
                    __result,
                    nameof(JournalEntryDisplayTextPatch),
                    out var translated))
            {
                return;
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: JournalEntryDisplayTextPatch.Postfix failed: {0}", ex);
        }
    }
}
