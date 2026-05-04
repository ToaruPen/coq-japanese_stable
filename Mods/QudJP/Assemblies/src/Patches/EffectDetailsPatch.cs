using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EffectDetailsPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = GameTypeResolver.FindType("XRL.World.Effect", "Effect");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: EffectDetailsPatch target type not found.");
            yield break;
        }

        foreach (var method in ActiveEffectOwnerTargetResolver.ResolveTargetMethods(targetType, "GetDetails"))
        {
            yield return method;
        }
    }

    public static void Postfix(MethodBase? __originalMethod, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            if (!ActiveEffectTextTranslator.TryTranslateText(
                    __result,
                    nameof(EffectDetailsPatch),
                    ComposeFamily(__originalMethod),
                    out var translated))
            {
                return;
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: EffectDetailsPatch.Postfix failed: {0}", ex);
        }
    }

    private static string ComposeFamily(MethodBase? originalMethod)
    {
        var typeName = originalMethod?.DeclaringType?.Name;
        return string.IsNullOrEmpty(typeName)
            ? "ActiveEffects.Details"
            : "ActiveEffects.Details." + typeName;
    }
}
