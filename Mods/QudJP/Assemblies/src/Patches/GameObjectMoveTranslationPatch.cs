using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectMoveTranslationPatch
{
    private const string Context = nameof(GameObjectMoveTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: GameObjectMoveTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(
            gameObjectType,
            "Move",
            new[]
            {
                typeof(string),
                gameObjectType.MakeByRefType(),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                gameObjectType,
                gameObjectType,
                typeof(bool),
                typeof(int?),
                typeof(string),
                typeof(int?),
                typeof(bool),
                typeof(bool),
                gameObjectType,
                gameObjectType,
                typeof(int),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectMoveTranslationPatch.Move(string, out GameObject, ...) overload not found.");
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
            Trace.TraceError("QudJP: GameObjectMoveTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: GameObjectMoveTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && IsTargetMessage(message)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "Move");
    }

    private static bool IsTargetMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var value = message!;
        return string.Equals(value, "You cannot go that way.", StringComparison.Ordinal)
            || value.StartsWith("You are stopped short by ", StringComparison.Ordinal)
            || value.EndsWith(" cannot be moved.", StringComparison.Ordinal)
            || value.EndsWith(" is stuck.", StringComparison.Ordinal)
            || value.EndsWith(" are stuck.", StringComparison.Ordinal)
            || value.StartsWith("You can't budge ", StringComparison.Ordinal)
            || (value.Contains(" Move ")
                && value.EndsWith(" and start swimming.", StringComparison.Ordinal))
            || value.StartsWith("Are you sure you want to move into ", StringComparison.Ordinal)
            || value.StartsWith("Are you sure you want to drop down a level? Move ", StringComparison.Ordinal);
    }
}
