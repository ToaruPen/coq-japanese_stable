using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhysicsApplyDischargeTranslationPatch
{
    private const string Context = nameof(PhysicsApplyDischargeTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var physicsType = AccessTools.TypeByName("XRL.World.Parts.Physics");
        if (physicsType is null)
        {
            Trace.TraceError("QudJP: PhysicsApplyDischargeTranslationPatch target type not found.");
            return null;
        }

        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var dieRollType = AccessTools.TypeByName("XRL.Rules.DieRoll");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (cellType is null || dieRollType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: PhysicsApplyDischargeTranslationPatch parameter types not found.");
            return null;
        }

        var listOfCellType = typeof(List<>).MakeGenericType(cellType);
        var listOfGameObjectType = typeof(List<>).MakeGenericType(gameObjectType);
        var method = AccessTools.Method(
            physicsType,
            "ApplyDischarge",
            new[]
            {
                cellType,
                cellType,
                typeof(int),
                typeof(int),
                typeof(string),
                dieRollType,
                gameObjectType,
                listOfCellType,
                gameObjectType,
                gameObjectType,
                gameObjectType,
                gameObjectType,
                listOfGameObjectType,
                typeof(bool?),
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(bool),
                typeof(bool),
                gameObjectType,
                gameObjectType,
                typeof(string),
                typeof(bool),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: PhysicsApplyDischargeTranslationPatch.ApplyDischarge overload not found.");
        }

        return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: PhysicsApplyDischargeTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: PhysicsApplyDischargeTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && message.Contains("electrical arc")
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "ApplyDischarge");
    }
}
