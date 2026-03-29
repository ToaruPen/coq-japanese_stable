using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectPerformThrowTranslationPatch
{
    private const string Context = nameof(GameObjectPerformThrowTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        var missilePathType = AccessTools.TypeByName("XRL.World.Parts.MissilePath");
        if (gameObjectType is null || cellType is null || missilePathType is null)
        {
            Trace.TraceError("QudJP: GameObjectPerformThrowTranslationPatch parameter types not found.");
            return null;
        }

        var method = AccessTools.Method(
            gameObjectType,
            "PerformThrow",
            new[]
            {
                gameObjectType,
                cellType,
                gameObjectType,
                missilePathType,
                typeof(int),
                typeof(int?),
                typeof(int?),
                typeof(int?),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectPerformThrowTranslationPatch.PerformThrow(GameObject, Cell, GameObject, MissilePath, int, int?, int?, int?) not found.");
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
            Trace.TraceError("QudJP: GameObjectPerformThrowTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: GameObjectPerformThrowTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && message.Contains(" with ")
            && message.Contains(" (x")
            && message.EndsWith(" damage!", StringComparison.Ordinal)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "PerformThrow");
    }
}
