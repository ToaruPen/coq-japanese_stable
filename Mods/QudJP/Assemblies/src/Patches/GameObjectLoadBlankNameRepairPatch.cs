using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectLoadBlankNameRepairPatch
{
    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObject = AccessTools.TypeByName("XRL.World.GameObject");
        var reader = AccessTools.TypeByName("XRL.World.SerializationReader");
        var method = gameObject is not null && reader is not null
            ? AccessTools.Method(gameObject, "Load", [reader])
            : null;
        if (method is null)
        {
            RuntimeDiagnostics.LogError("[QudJP] GameObjectLoadBlankNameRepairPatch: GameObject.Load(SerializationReader) not found.");
        }

        return method;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            var blueprint = DescriptionPartReflectionHelpers.GetStringMemberValue(__instance, "Blueprint");
            if (__instance is null || !SavedBlankItemNameRepair.IsKnownBlueprint(blueprint))
            {
                return;
            }

            // Read persisted flags only. HasProperName traverses other parts and blueprint state.
            // Even 0/-1 or empty values are explicit saved designations: conservatively preserve them.
            if (DescriptionPartReflectionHelpers.GetMemberValue(__instance, "Property") is not Dictionary<string, string> properties
                || DescriptionPartReflectionHelpers.GetMemberValue(__instance, "IntProperty") is not Dictionary<string, int> intProperties
                || properties.ContainsKey("Renamed") || properties.ContainsKey("ProperNoun")
                || intProperties.ContainsKey("Renamed") || intProperties.ContainsKey("ProperNoun"))
            {
                return;
            }

            var render = DescriptionPartReflectionHelpers.GetMemberValue(__instance, "Render");
            var source = DescriptionPartReflectionHelpers.GetStringMemberValue(render, "DisplayName");
            var repaired = SavedBlankItemNameRepair.Repair(blueprint, source);
            if (render is null || repaired is null || string.Equals(source, repaired, StringComparison.Ordinal))
            {
                return;
            }

            var resetNameCache = AccessTools.Method(__instance.GetType(), "ResetNameCache", Type.EmptyTypes);
            if (resetNameCache is not null && DescriptionPartReflectionHelpers.SetStringMemberValue(render, "DisplayName", repaired))
            {
                resetNameCache.Invoke(__instance, null);
            }
        }
        catch (Exception ex)
        {
            RuntimeDiagnostics.LogError($"[QudJP] GameObjectLoadBlankNameRepairPatch failed: {ex}");
        }
    }
}
