using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MissileWeaponHitTranslationPatch
{
    private const string Context = nameof(MissileWeaponHitTranslationPatch);
    private const string Detail = "MissileHit";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.MissileWeapon");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var projectileType = AccessTools.TypeByName("XRL.World.Parts.Projectile");
        var missilePathType = AccessTools.TypeByName("XRL.World.Parts.MissilePath");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var fireType = AccessTools.TypeByName("XRL.World.Parts.FireType");
        if (targetType is null
            || gameObjectType is null
            || projectileType is null
            || missilePathType is null
            || cellType is null
            || fireType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(
            targetType,
            "MissileHit",
            [
                gameObjectType,
                gameObjectType,
                gameObjectType,
                gameObjectType,
                projectileType,
                gameObjectType,
                gameObjectType,
                missilePathType,
                cellType,
                fireType,
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(bool),
                gameObjectType,
                typeof(bool),
                typeof(bool).MakeByRefType(),
                typeof(bool).MakeByRefType(),
                typeof(bool).MakeByRefType(),
                typeof(bool),
                typeof(bool),
            ]);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.MissileHit target not found.", Context);
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            OwnerTranslationScope.Exit(ref activeDepth);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        var (stripped, _) = ColorAwareTranslationComposer.Strip(message);
        if (!IsMultiplierDamageHit(stripped))
        {
            return false;
        }

        return MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, Detail);
    }

    private static bool IsMultiplierDamageHit(string message)
    {
        return StringHelpers.ContainsOrdinal(message, " (x")
            && StringHelpers.ContainsOrdinal(message, " for ")
            && StringHelpers.ContainsOrdinal(message, " damage!")
            && (StringHelpers.ContainsOrdinal(message, "You hit ")
                || StringHelpers.ContainsOrdinal(message, "You critically hit ")
                || StringHelpers.ContainsOrdinal(message, " hits you with ")
                || StringHelpers.ContainsOrdinal(message, " hits with ")
                || (StringHelpers.ContainsOrdinal(message, " hits ")
                    && StringHelpers.ContainsOrdinal(message, " with ")));
    }
}
