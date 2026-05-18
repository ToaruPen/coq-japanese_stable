using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class VillageWallDescriptionTranslationPatch
{
    private const string Context = nameof(VillageWallDescriptionTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>(capacity: 4);
        AddTargetMethod(targets, "XRL.World.ZoneBuilders.VillageBase", "getAVillageCanvas");
        AddTargetMethod(targets, "XRL.World.ZoneBuilders.VillageBase", "getAVillageWall");
        AddTargetMethod(targets, "XRL.World.ZoneBuilders.VillageCodaBase", "getAVillageCanvas");
        AddTargetMethod(targets, "XRL.World.ZoneBuilders.VillageCodaBase", "getAVillageWall");
        return targets;
    }

    public static void Postfix(object? __result)
    {
        try
        {
            if (__result is null
                || !DescriptionPartReflectionHelpers.TryGetDescriptionPart(__result, Context, logFallback: true, out var descriptionPart))
            {
                return;
            }

            var source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "Short");
            if (source is null)
            {
                Trace.TraceWarning("QudJP: {0} falling back from Description.Short to _Short.", Context);
                source = DescriptionPartReflectionHelpers.GetStringMemberValue(descriptionPart, "_Short");
            }

            if (!VillageWallDescriptionTranslator.TryTranslate(source, out var translated))
            {
                return;
            }

            if (DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "Short", translated)
                || DescriptionPartReflectionHelpers.SetStringMemberValue(descriptionPart, "_Short", translated))
            {
                DynamicTextObservability.RecordTransform(Context, Context + ".DescriptionShort", source!, translated);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    private static void AddTargetMethod(ICollection<MethodBase> targets, string typeName, string methodName)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var method = targetType is null ? null : AccessTools.Method(targetType, methodName, Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}().", Context, typeName, methodName);
            return;
        }

        targets.Add(method);
    }

}
