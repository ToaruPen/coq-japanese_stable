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
            ("XRL.World.Effects.CookingDomainHP_UnitHP", "GetDescription"),
            ("XRL.World.Effects.CookingDomainHP_UnitHP", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainAcid_UnitResist", "GetDescription"),
            ("XRL.World.Effects.CookingDomainAcid_UnitResist", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainCold_UnitResist", "GetDescription"),
            ("XRL.World.Effects.CookingDomainCold_UnitResist", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainElectric_ResistUnit", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_ResistUnit", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainElectric_ExtraResistUnit", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_ExtraResistUnit", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainHeat_UnitResist", "GetDescription"),
            ("XRL.World.Effects.CookingDomainHeat_UnitResist", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainAgility_UnitAgility", "GetDescription"),
            ("XRL.World.Effects.CookingDomainAgility_UnitAgility", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainArmor_UnitAV", "GetDescription"),
            ("XRL.World.Effects.CookingDomainArmor_UnitAV", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainDarkness_UnitDV", "GetDescription"),
            ("XRL.World.Effects.CookingDomainDarkness_UnitDV", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainFear_UnitBonusMA", "GetDescription"),
            ("XRL.World.Effects.CookingDomainFear_UnitBonusMA", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainLove_UnitEgo", "GetDescription"),
            ("XRL.World.Effects.CookingDomainLove_UnitEgo", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainQuickness_UnitQuickness", "GetDescription"),
            ("XRL.World.Effects.CookingDomainQuickness_UnitQuickness", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainStrength_UnitStrength", "GetDescription"),
            ("XRL.World.Effects.CookingDomainStrength_UnitStrength", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainWillpower_UnitWillpower", "GetDescription"),
            ("XRL.World.Effects.CookingDomainWillpower_UnitWillpower", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainRegenLowtier_RegenerationUnit", "GetDescription"),
            ("XRL.World.Effects.CookingDomainRegenLowtier_RegenerationUnit", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainRegenHightier_RegenerationUnit", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainRegenLowtier_BleedResistUnit", "GetDescription"),
            ("XRL.World.Effects.CookingDomainRegenLowtier_BleedResistUnit", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainReflect_UnitReflectDamage", "GetDescription"),
            ("XRL.World.Effects.CookingDomainReflect_UnitReflectDamage", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainReflect_UnitReflectDamageHighTier", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainHP_OnDamaged", "GetTriggerDescription"),
            ("XRL.World.Effects.CookingDomainHP_OnDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainHP_OnDamagedMidTier", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainReflect_OnDamaged", "GetTriggerDescription"),
            ("XRL.World.Effects.CookingDomainReflect_OnDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainReflect_OnDamagedHighTier", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainRegenLowtier_OnDamaged", "GetTriggerDescription"),
            ("XRL.World.Effects.CookingDomainRegenLowtier_OnDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainRegenHightier_OnDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainPhase_UnitPhaseOnDamage", "GetDescription"),
            ("XRL.World.Effects.CookingDomainPhase_UnitPhaseOnDamage", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainTeleport_UnitBlink", "GetDescription"),
            ("XRL.World.Effects.CookingDomainTeleport_UnitBlink", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainAgility_LargeAgilityBuff_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainArmor_LargeAVBuff_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainStrength_LargeStrengthBuff_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainCold_ColdResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainCold_LargeColdResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_SmallElectricResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_LargeElectricResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainHeat_HeatResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainHeat_LargeHeatResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_EMPUnit", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_EMPUnit", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainEgo_UnitEgoProjection", "GetDescription"),
            ("XRL.World.Effects.CookingDomainEgo_UnitEgoProjection", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainBurrowing_UnitBurrowingClaws", "GetDescription"),
            ("XRL.World.Effects.CookingDomainBurrowing_UnitBurrowingClaws", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainArtifact_UnitPsychometry", "GetDescription"),
            ("XRL.World.Effects.CookingDomainArtifact_UnitPsychometry", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainPlant_UnitBurgeoningHighTier", "GetDescription"),
            ("XRL.World.Effects.CookingDomainPlant_UnitBurgeoningHighTier", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainPlant_UnitBurgeoningLowTier", "GetDescription"),
            ("XRL.World.Effects.CookingDomainPlant_UnitBurgeoningLowTier", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainReflect_UnitQuills", "GetDescription"),
            ("XRL.World.Effects.CookingDomainReflect_UnitQuills", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainTongue_UnitStickyTongue", "GetDescription"),
            ("XRL.World.Effects.CookingDomainTongue_UnitStickyTongue", "GetTemplatedDescription"),
            ("XRL.World.Effects.CookingDomainFear_UnitIntimidate", "GetDescription"),
            ("XRL.World.Effects.CookingDomainFear_UnitIntimidate", "GetTemplatedDescription"),
            ("XRL.World.Effects.BasicCookingEffect_Hitpoints", "GetDetails"),
            ("XRL.World.Effects.BasicCookingEffect_MA", "GetDetails"),
            ("XRL.World.Effects.BasicCookingEffect_MS", "GetDetails"),
            ("XRL.World.Effects.BasicCookingEffect_Quickness", "GetDetails"),
            ("XRL.World.Effects.BasicCookingEffect_ToHit", "GetDetails"),
            ("XRL.World.Effects.BasicCookingEffect_XP", "GetDetails"),
            ("XRL.World.Effects.BasicCookingEffect_Regeneration", "GetDetails"),
            ("XRL.World.Effects.BasicCookingEffect_RandomStat", "GetDetails"),
            ("XRL.World.Effects.BasicTriggeredCookingStatEffect", "GetDetails"),
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
