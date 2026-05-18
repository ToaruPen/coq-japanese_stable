using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillageTerrainRevealDescriptionTranslationPatch
{
    private const string Context = nameof(VillageTerrainRevealDescriptionTranslationPatch);
    private const string VillageRevealEventId = "VillageReveal";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.VillageTerrain");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var method = targetType is null || eventType is null
            ? null
            : AccessTools.Method(targetType, "FireEvent", new[] { eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: XRL.World.Parts.VillageTerrain.FireEvent(Event).", Context);
        }

        return method;
    }

    public static void Postfix(object? __instance, object? E, bool __result)
    {
        try
        {
            if (!__result || __instance is null || !IsVillageRevealEvent(E))
            {
                return;
            }

            TryTranslateRevealedDescription(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static bool TryTranslateRevealedDescriptionForTests(object? instance)
    {
        return TryTranslateRevealedDescription(instance);
    }

    private static bool TryTranslateRevealedDescription(object? instance)
    {
        if (instance is null || !DescriptionPartReflectionHelpers.TryGetParentObject(instance, out var parentObject)
            || !DescriptionPartReflectionHelpers.TryGetDescriptionPart(parentObject, Context, logFallback: true, out var descriptionPart))
        {
            return false;
        }

        var source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "Short");
        if (source is null)
        {
            Trace.TraceWarning("QudJP: {0} falling back from Description.Short to _Short.", Context);
            source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "_Short");
        }

        if (!VillageTerrainRevealDescriptionTranslator.TryTranslate(source, out var translated))
        {
            return false;
        }

        if (!DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "Short", translated)
            && !DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "_Short", translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, Context + ".DescriptionShort", source!, translated);
        return true;
    }

    private static bool IsVillageRevealEvent(object? eventObject)
    {
        return string.Equals(
            DescriptionPartReflectionHelpers.GetStringMemberValue(eventObject, "ID"),
            VillageRevealEventId,
            StringComparison.Ordinal);
    }
}
