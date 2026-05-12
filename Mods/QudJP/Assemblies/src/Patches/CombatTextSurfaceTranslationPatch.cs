using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CombatTextSurfaceTranslationPatch
{
    private const string Context = nameof(CombatTextSurfaceTranslationPatch);
    private const string ShieldBlockDetail = "HandleEvent";
    private const string MeleeAttackDetail = "MeleeAttackWithWeaponInternal";

    [ThreadStatic]
    private static int shieldBlockDepth;

    [ThreadStatic]
    private static int meleeAttackDepth;

    [ThreadStatic]
    private static Stack<OwnerRoute>? activeRoutes;

    private enum OwnerRoute
    {
        Unknown,
        ShieldBlock,
        MeleeAttack,
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var combatType = AccessTools.TypeByName("XRL.World.Parts.Combat");
        if (combatType is null)
        {
            Trace.TraceError("QudJP: CombatTextSurfaceTranslationPatch target type not found.");
            yield break;
        }

        var eventType = AccessTools.TypeByName("XRL.World.GetDefenderHitDiceEvent");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: CombatTextSurfaceTranslationPatch event type not found.");
        }
        else
        {
            var method = AccessTools.Method(combatType, "HandleEvent", new[] { eventType });
            if (method is null)
            {
                Trace.TraceError("QudJP: CombatTextSurfaceTranslationPatch.HandleEvent(GetDefenderHitDiceEvent) not found.");
            }
            else
            {
                yield return method;
            }
        }

        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var bodyPartType = AccessTools.TypeByName("XRL.World.Anatomy.BodyPart");
        if (gameObjectType is null || bodyPartType is null)
        {
            Trace.TraceError("QudJP: CombatTextSurfaceTranslationPatch dependent parameter types not found.");
            yield break;
        }

        var meleeMethod = AccessTools.Method(
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
        if (meleeMethod is null)
        {
            Trace.TraceError("QudJP: CombatTextSurfaceTranslationPatch.MeleeAttackWithWeaponInternal(...) not found.");
        }
        else
        {
            yield return meleeMethod;
        }
    }

    [HarmonyPrefix]
    public static void Prefix(MethodBase __originalMethod)
    {
        try
        {
            var route = ResolveOwnerRoute(__originalMethod);
            Enter(route);
            if (activeRoutes is null)
            {
                activeRoutes = new Stack<OwnerRoute>();
            }

            activeRoutes.Push(route);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CombatTextSurfaceTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            if (activeRoutes is { Count: > 0 })
            {
                Exit(activeRoutes.Pop());
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CombatTextSurfaceTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if ((!OwnerTranslationScope.IsActive(shieldBlockDepth) && !OwnerTranslationScope.IsActive(meleeAttackDepth))
            || string.IsNullOrEmpty(message))
        {
            return false;
        }

        var (strippedMessage, _) = ColorAwareTranslationComposer.Strip(message);
        if (OwnerTranslationScope.IsActive(shieldBlockDepth) && IsShieldBlockMessage(strippedMessage))
        {
            return MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, ShieldBlockDetail);
        }

        if (OwnerTranslationScope.IsActive(meleeAttackDepth) && IsMeleeAttackMessage(strippedMessage))
        {
            return MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, MeleeAttackDetail);
        }

        return false;
    }

    private static bool IsShieldBlockMessage(string message)
    {
        return message.StartsWith("You block with ", StringComparison.Ordinal)
            || message.StartsWith("You stagger ", StringComparison.Ordinal)
            || message.StartsWith("You are staggered by ", StringComparison.Ordinal);
    }

    private static bool IsMeleeAttackMessage(string message)
    {
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

    private static OwnerRoute ResolveOwnerRoute(MethodBase originalMethod)
    {
        return originalMethod.Name switch
        {
            ShieldBlockDetail => OwnerRoute.ShieldBlock,
            MeleeAttackDetail => OwnerRoute.MeleeAttack,
            _ => OwnerRoute.Unknown,
        };
    }

    private static void Enter(OwnerRoute route)
    {
        switch (route)
        {
            case OwnerRoute.ShieldBlock:
                OwnerTranslationScope.Enter(ref shieldBlockDepth);
                break;
            case OwnerRoute.MeleeAttack:
                OwnerTranslationScope.Enter(ref meleeAttackDepth);
                break;
        }
    }

    private static void Exit(OwnerRoute route)
    {
        switch (route)
        {
            case OwnerRoute.ShieldBlock:
                OwnerTranslationScope.Exit(ref shieldBlockDepth);
                break;
            case OwnerRoute.MeleeAttack:
                OwnerTranslationScope.Exit(ref meleeAttackDepth);
                break;
        }
    }
}
