using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillageBaseCreateVillageFactionDisplayNameTranslationPatch
{
    private const string Context = nameof(VillageBaseCreateVillageFactionDisplayNameTranslationPatch);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.ZoneBuilders.VillageBase");
        var snapshotType = AccessTools.TypeByName("HistoryKit.HistoricEntitySnapshot");
        var method = targetType is null || snapshotType is null
            ? null
            : AccessTools.Method(targetType, "CreateVillageFaction", [snapshotType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found.", Context);
        }

        return method;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            _ = GeneratedDisplayNameOwnerTranslationHelpers.TranslateVillageFactionDisplayName(__result);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
