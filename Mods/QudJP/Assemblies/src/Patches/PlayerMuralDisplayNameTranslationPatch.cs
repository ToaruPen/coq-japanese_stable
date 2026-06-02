using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PlayerMuralDisplayNameTranslationPatch
{
    private const string Context = nameof(PlayerMuralDisplayNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.PlayerMuralController");
        var locationType = AccessTools.TypeByName("Genkit.Location2D");
        var accomplishmentType = AccessTools.TypeByName("Qud.API.JournalAccomplishment");
        var method = targetType is null || locationType is null || accomplishmentType is null
            ? null
            : AccessTools.Method(
                targetType,
                "updatePlayerMural",
                [typeof(List<>).MakeGenericType(locationType), accomplishmentType, typeof(int)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __instance, object? __0, int __2)
    {
        try
        {
            _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslatePlayerMuralPanel(__instance, __0, __2);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
