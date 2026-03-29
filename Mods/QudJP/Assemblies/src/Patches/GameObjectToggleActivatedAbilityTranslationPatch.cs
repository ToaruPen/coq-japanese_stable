using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectToggleActivatedAbilityTranslationPatch
{
    private const string Context = nameof(GameObjectToggleActivatedAbilityTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: GameObjectToggleActivatedAbilityTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(gameObjectType, "ToggleActivatedAbility", new[] { typeof(Guid), typeof(bool), typeof(bool?) });
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectToggleActivatedAbilityTranslationPatch.ToggleActivatedAbility(Guid, bool, bool?) not found.");
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
            Trace.TraceError("QudJP: GameObjectToggleActivatedAbilityTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: GameObjectToggleActivatedAbilityTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && message.StartsWith("You toggle ", StringComparison.Ordinal)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "ToggleActivatedAbility");
    }
}
