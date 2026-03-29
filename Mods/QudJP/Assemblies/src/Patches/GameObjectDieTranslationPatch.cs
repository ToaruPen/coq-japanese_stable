using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectDieTranslationPatch
{
    private const string Context = nameof(GameObjectDieTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: GameObjectDieTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(
            gameObjectType,
            "Die",
            new[]
            {
                gameObjectType,
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                gameObjectType,
                gameObjectType,
                typeof(bool),
                typeof(bool),
                typeof(string),
                typeof(string),
                typeof(string),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectDieTranslationPatch.Die(...) overload not found.");
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
            Trace.TraceError("QudJP: GameObjectDieTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: GameObjectDieTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && message.StartsWith("Your companion, ", StringComparison.Ordinal)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "Die");
    }
}
