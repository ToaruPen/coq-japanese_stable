using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CombatMeleeAttackTranslationPatch
{
    private const string Context = nameof(CombatMeleeAttackTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var combatType = AccessTools.TypeByName("XRL.World.Parts.Combat");
        if (combatType is null)
        {
            Trace.TraceError("QudJP: CombatMeleeAttackTranslationPatch target type not found.");
            return null;
        }

        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var bodyPartType = AccessTools.TypeByName("XRL.World.Anatomy.BodyPart");
        if (gameObjectType is null || bodyPartType is null)
        {
            Trace.TraceError("QudJP: CombatMeleeAttackTranslationPatch dependent parameter types not found.");
            return null;
        }

        var method = AccessTools.Method(
            combatType,
            "MeleeAttackWithWeaponInternal",
            new[]
            {
                gameObjectType,
                gameObjectType,
                gameObjectType,
                bodyPartType,
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(bool),
                typeof(bool),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: CombatMeleeAttackTranslationPatch.MeleeAttackWithWeaponInternal(...) not found.");
        }

        return method;
    }

    [HarmonyPrefix]
    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CombatMeleeAttackTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CombatMeleeAttackTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0 || string.IsNullOrEmpty(message))
        {
            return false;
        }

        // Strip color codes before guard check to avoid fragile color-dependent conditions.
        var (strippedMessage, _) = ColorAwareTranslationComposer.Strip(message);
        if (!IsGuardedMessage(strippedMessage))
        {
            return false;
        }

        return MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "MeleeAttackWithWeaponInternal");
    }

    private static bool IsGuardedMessage(string message)
    {
        // Guards check stripped messages (color codes removed by TryTranslateQueuedMessage).
        return message.StartsWith("You miss!", StringComparison.Ordinal)
            || message.StartsWith("You miss with ", StringComparison.Ordinal)
            || StringHelpers.ContainsOrdinal(message, " misses you")
            || message.StartsWith("Your mental attack does not affect ", StringComparison.Ordinal)
            || message.StartsWith("You fail to deal damage with your attack!", StringComparison.Ordinal)
            || StringHelpers.ContainsOrdinal(message, " fail to deal damage with ")
            || StringHelpers.ContainsOrdinal(message, " fails to deal damage with ")
            || message.StartsWith("You don't penetrate ", StringComparison.Ordinal)
            || StringHelpers.ContainsOrdinal(message, " penetrate your armor");
    }
}
