using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SkillActivatedAbilityNameTranslationPatch
{
    internal const string Context = nameof(SkillActivatedAbilityNameTranslationPatch);
    internal const string Family = Context + ".RegisteredName";

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter type XRL.World.GameObject not found.", Context);
            return Array.Empty<MethodBase>();
        }

        var targets = new List<MethodBase>(capacity: 40);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Tinkering_LayMine", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Pistol_EmptyTheClips", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Tinkering_Tinker1", gameObjectType);
        AddNoArgumentTarget(targets, "XRL.World.Parts.Skill.Axe_Decapitate", "AddAbility");
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Axe_Dismember", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Axe_HookAndDrag", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.CookingAndGathering_Harvestry", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.LongBladesDuelingStance", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Persuasion_RebukeRobot", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.ShortBlades_Shank", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Axe_Berserk", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.CookingAndGathering_Butchery", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Cudgel_Slam", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Cudgel_SmashUp", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Discipline_Meditate", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.LongBladesDeathblow", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.LongBladesLunge", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.LongBladesSwipe", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Multiweapon_Flurry", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Persuasion_Proselytize", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Physic_AmputateLimb", gameObjectType);
        AddNoArgumentTarget(targets, "XRL.World.Parts.Skill.Pistol_Akimbo", "AddAbility");
        AddSkillTarget(targets, "XRL.World.Parts.Skill.ShortBlades_Hobble", gameObjectType);
        AddNoArgumentTarget(targets, "XRL.World.Parts.Skill.ShortBlades_Rejoinder", "AddAbility");
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Survival_Camp", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Tinkering_DeployTurret", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Smash_Floor", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Snapjaw_Howl", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Submersion", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Cudgel_Conk", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.HeavyWeapons_Sweep", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Persuasion_Berate", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Persuasion_Intimidate", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Rifle_DrawABead", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Shield_ShieldWall", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Shield_Slam", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Tactics_Charge", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Tactics_DeathFromAbove", gameObjectType);
        AddSkillTarget(targets, "XRL.World.Parts.Skill.Tactics_Juke", gameObjectType);
        AddBooleanTarget(targets, "XRL.World.Parts.Skill.Acrobatics_Jump", "SyncAbility");
        return targets;
    }

    public static void Postfix(object? __instance)
    {
        try
        {
            TranslateRegisteredAbilityNamesForTests(__instance);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void TranslateRegisteredAbilityNamesForTests(object? instance)
    {
        ActivatedAbilityRegistrationNameTranslation.TranslateRegisteredAbilityNames(instance, Context, Family);
    }

    private static void AddSkillTarget(ICollection<MethodBase> targets, string typeName, Type gameObjectType)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var method = targetType is null ? null : AccessTools.Method(targetType, "AddSkill", new[] { gameObjectType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.AddSkill(GameObject).", Context, typeName);
            return;
        }

        targets.Add(method);
    }

    private static void AddNoArgumentTarget(ICollection<MethodBase> targets, string typeName, string methodName)
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

    private static void AddBooleanTarget(ICollection<MethodBase> targets, string typeName, string methodName)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var method = targetType is null ? null : AccessTools.Method(targetType, methodName, new[] { typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}(bool).", Context, typeName, methodName);
            return;
        }

        targets.Add(method);
    }
}
