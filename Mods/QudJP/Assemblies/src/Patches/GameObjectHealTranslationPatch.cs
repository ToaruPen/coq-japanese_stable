using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectHealTranslationPatch
{
    private const string Context = nameof(GameObjectHealTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: GameObjectHealTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(gameObjectType, "Heal", new[] { typeof(int), typeof(bool), typeof(bool), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectHealTranslationPatch.Heal(int, bool, bool, bool) not found.");
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
            Trace.TraceError("QudJP: GameObjectHealTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: GameObjectHealTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && message.Contains(" hit point")
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "Heal");
    }
}
