using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CookingEffectTranslationPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in ResolveTargetMethods())
        {
            yield return method;
        }
    }

    public static void Postfix(MethodBase __originalMethod, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            var family = __originalMethod.DeclaringType?.Name is { Length: > 0 } typeName
                ? "Cooking." + typeName + "." + __originalMethod.Name
                : "Cooking." + __originalMethod.Name;
            if (!CookingEffectFragmentTranslator.TryTranslate(__result, nameof(CookingEffectTranslationPatch), family, out var translated))
            {
                return;
            }

            __result = translated;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CookingEffectTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    private static IEnumerable<MethodBase> ResolveTargetMethods()
    {
        foreach (var target in new (string typeName, string methodName)[]
        {
            ("XRL.World.Effects.CookingDomainElectric_Discharge_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_Discharge_ProceduralCookingTriggeredAction", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainElectric_EMP_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_EMP_ProceduralCookingTriggeredAction", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainElectric_OnElectricDamaged", "GetTriggerDescription"),
            ("XRL.World.Effects.CookingDomainElectric_OnElectricDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainArmor_OnPenetration", "GetTriggerDescription"),
            ("XRL.World.Effects.CookingDomainArmor_OnPenetration", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect", "GetDetails"),
            ("XRL.World.Effects.CookingDomainHP_IncreaseHP_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainHP_IncreaseHP_ProceduralCookingTriggeredAction", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainHP_IncreaseHP_ProceduralCookingTriggeredActionEffect", "GetDetails"),
        })
        {
            var type = AccessTools.TypeByName(target.typeName);
            if (type is null)
            {
                Trace.TraceError("QudJP: CookingEffectTranslationPatch failed to resolve type '{0}'.", target.typeName);
                continue;
            }

            var method = AccessTools.Method(type, target.methodName, Type.EmptyTypes);
            if (method is null)
            {
                Trace.TraceError("QudJP: CookingEffectTranslationPatch failed to resolve {0}.{1}().", target.typeName, target.methodName);
                continue;
            }

            yield return method;
        }
    }
}
