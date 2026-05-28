using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

internal static class SifrahTokenGetDescriptionTargetResolver
{
    internal static IEnumerable<MethodBase> ResolveTargetMethods(string context)
    {
        var sifrahGameType = AccessTools.TypeByName("XRL.SifrahGame");
        var sifrahSlotType = AccessTools.TypeByName("XRL.SifrahSlot");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (sifrahGameType is null || sifrahSlotType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} Sifrah GetDescription parameter type not found.", context);
            yield break;
        }

        var parameterTypes = new[] { sifrahGameType, sifrahSlotType, gameObjectType };
        foreach (var typeName in SifrahTokenDescriptionTranslationPatch.NoArgumentTokenTypeNames)
        {
            var targetType = AccessTools.TypeByName(typeName);
            var method = targetType?.GetMethod(
                "GetDescription",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            if (method is not null)
            {
                yield return method;
            }
        }
    }
}

[HarmonyPatch]
public static class SifrahTokenGetDescriptionTranslationPatch
{
    internal const string Context = nameof(SifrahTokenGetDescriptionTranslationPatch);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return SifrahTokenGetDescriptionTargetResolver.ResolveTargetMethods(Context);
    }

    public static void Postfix(ref string __result)
    {
        try
        {
            if (!SifrahTokenDescriptionTranslator.TryTranslateDescription(__result, out var translated, out var detail))
            {
                return;
            }

            if (detail.Length > 0)
            {
                DynamicTextObservability.RecordTransform(Context, SifrahTokenDescriptionTranslationPatch.Family + "." + detail, __result, translated);
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }
}
