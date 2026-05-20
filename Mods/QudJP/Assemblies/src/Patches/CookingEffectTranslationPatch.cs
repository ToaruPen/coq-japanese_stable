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
            if (!TryTranslateResult(__result, family, out var translated))
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

    private static bool TryTranslateResult(string source, string family, out string translated)
    {
        if (CookingEffectFragmentTranslator.TryTranslate(source, nameof(CookingEffectTranslationPatch), family, out translated))
        {
            return true;
        }

        if (source.IndexOf('\n') < 0)
        {
            return false;
        }

        var changed = false;
        var result = new System.Text.StringBuilder(source.Length);
        var lineStart = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] != '\n')
            {
                continue;
            }

            var lineEnd = index;
            var hasCarriageReturn = lineEnd > lineStart && source[lineEnd - 1] == '\r';
            if (hasCarriageReturn)
            {
                lineEnd--;
            }

            AppendTranslatedLine(source, lineStart, lineEnd - lineStart, family, result, ref changed);
            if (hasCarriageReturn)
            {
                result.Append('\r');
            }

            result.Append('\n');
            lineStart = index + 1;
        }

        AppendTranslatedLine(source, lineStart, source.Length - lineStart, family, result, ref changed);
        if (!changed)
        {
            translated = source;
            return false;
        }

        translated = result.ToString();
        return true;
    }

    private static void AppendTranslatedLine(
        string source,
        int start,
        int length,
        string family,
        System.Text.StringBuilder result,
        ref bool changed)
    {
        if (length == 0)
        {
            return;
        }

        var line = source.Substring(start, length);
        if (CookingEffectFragmentTranslator.TryTranslate(line, nameof(CookingEffectTranslationPatch), family, out var translatedLine))
        {
            result.Append(translatedLine);
            changed = true;
            return;
        }

        result.Append(line);
    }

    private static IEnumerable<MethodBase> ResolveTargetMethods()
    {
        foreach (var target in new (string typeName, string methodName)[]
        {
            ("XRL.World.Effects.ProceduralCookingEffect", "GetDescription"),
            ("XRL.World.Effects.ProceduralCookingEffect", "GetProceduralEffectDescription"),
            ("XRL.World.Effects.ProceduralCookingEffect", "GetTemplatedProceduralEffectDescription"),
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
            ("XRL.World.Effects.CookingDomainHP_OnDamaged", "GetTriggerDescription"),
            ("XRL.World.Effects.CookingDomainHP_OnDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainHP_OnDamagedMidTier", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainReflect_OnDamaged", "GetTriggerDescription"),
            ("XRL.World.Effects.CookingDomainReflect_OnDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainReflect_OnDamagedHighTier", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainRegenLowtier_OnDamaged", "GetTriggerDescription"),
            ("XRL.World.Effects.CookingDomainRegenLowtier_OnDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainRegenHightier_OnDamaged", "GetTemplatedTriggerDescription"),
            ("XRL.World.Effects.CookingDomainAgility_LargeAgilityBuff_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainArmor_LargeAVBuff_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainStrength_LargeStrengthBuff_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainCold_ColdResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainCold_LargeColdResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_SmallElectricResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainElectric_LargeElectricResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainHeat_HeatResist_ProceduralCookingTriggeredAction", "GetDescription"),
            ("XRL.World.Effects.CookingDomainHeat_LargeHeatResist_ProceduralCookingTriggeredAction", "GetDescription"),
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
