using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ZoneDisplayNameTranslationPatch
{
    private const string Context = nameof(ZoneDisplayNameTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.ZoneManager");
        var zoneBlueprintType = AccessTools.TypeByName("XRL.World.ZoneBlueprint");
        if (targetType is null || zoneBlueprintType is null)
        {
            Trace.TraceError("QudJP: ZoneDisplayNameTranslationPatch failed to resolve ZoneManager or ZoneBlueprint.");
            yield break;
        }

        yield return AccessTools.Method(
            targetType,
            "GetZoneDisplayName",
            new[] { typeof(string), typeof(int), zoneBlueprintType, typeof(bool), typeof(bool), typeof(bool), typeof(bool) })!;
        yield return AccessTools.Method(
            targetType,
            "GetZoneDisplayName",
            new[] { typeof(string), typeof(string), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })!;
        yield return AccessTools.Method(
            targetType,
            "GetZoneDisplayName",
            new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })!;
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            if (!MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(__result, Context, out var translated))
            {
                return;
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: ZoneDisplayNameTranslationPatch.Postfix failed: {0}", ex);
        }
    }
}
