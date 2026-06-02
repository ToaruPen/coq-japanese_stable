using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MutationActivatedAbilityNameTranslationPatch
{
    internal const string Context = nameof(MutationActivatedAbilityNameTranslationPatch);
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

        var targets = new List<MethodBase>(capacity: 62);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.WillForce", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.BurrowingClaws", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.ElectricalGeneration", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.LightManipulation", gameObjectType);
        AddNoArgumentTarget(targets, "XRL.World.Parts.Mutation.LightManipulation", "SyncAbilityName");
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Precognition", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.SlogGlands", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Beguiling", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.AcidSlimeGlands", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.AdrenalControl2", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Burgeoning", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Burrowing", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Carapace", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Clairvoyance", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Confusion", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Decarbonizer", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.DefensiveChromatophores", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Domination", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.ElectromagneticPulse", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.ErosTeleportation", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.ForceWall", gameObjectType);
        AddNoArgumentTarget(targets, "XRL.World.Parts.Mutation.FreezeBreath", "AddAbility");
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.FrostWebs", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Infiltrate", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.IrisdualBeam", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Kindle", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.LeyShifting", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.LifeDrain", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.LiquidSpitter", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.MassMind", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.MentalMirror", gameObjectType);
        AddSingleGameObjectTarget(targets, "XRL.World.Parts.Mutation.Metamorphed", "Apply", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Metamorphosis", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Phasing", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Serenity", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.SpacetimeVortex", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.SpiderWebs", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Spinnerets", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.StickyTongue", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Stinger", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.StunningForce", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.SunderMind", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.TeleportOther", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.TimeDilation", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.WaveformWorm", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Cryokinesis", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Disintegration", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.FearAura", gameObjectType);
        AddNoArgumentTarget(targets, "XRL.World.Parts.Mutation.FlamingRay", "AddAbility");
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.ForceBubble", gameObjectType);
        AddNoArgumentTarget(targets, "XRL.World.Parts.Mutation.FreezingRay", "AddAbility");
        AddSingleGameObjectTarget(targets, "XRL.World.Parts.Mutation.MagneticPulse", "AddAbility", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Pyrokinesis", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.RepellingForce", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.SlimeGlands", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Telepathy", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Teleportation", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Belcher", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.BreatherBase", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.GasGeneration", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.IDelayedLineMutation", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.Quills", gameObjectType);
        AddMutateTarget(targets, "XRL.World.Parts.Mutation.TemporalFugue", gameObjectType);
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

    private static void AddMutateTarget(ICollection<MethodBase> targets, string typeName, Type gameObjectType)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var method = targetType is null ? null : AccessTools.Method(targetType, "Mutate", new[] { gameObjectType, typeof(int) });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.Mutate(GameObject,int).", Context, typeName);
            return;
        }

        targets.Add(method);
    }

    private static void AddSingleGameObjectTarget(
        ICollection<MethodBase> targets,
        string typeName,
        string methodName,
        Type gameObjectType)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var method = targetType is null ? null : AccessTools.Method(targetType, methodName, new[] { gameObjectType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0} target not found: {1}.{2}(GameObject).", Context, typeName, methodName);
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
}
