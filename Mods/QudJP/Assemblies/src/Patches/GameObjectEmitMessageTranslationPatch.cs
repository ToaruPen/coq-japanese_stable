using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectEmitMessageTranslationPatch
{
    private const string Context = nameof(GameObjectEmitMessageTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: GameObjectEmitMessageTranslationPatch target type not found.");
            yield break;
        }

        var gameObjectMethod = AccessTools.Method(gameObjectType, "EmitMessage", new[] { typeof(string), gameObjectType, typeof(string), typeof(bool) });
        if (gameObjectMethod is not null)
        {
            yield return gameObjectMethod;
        }
        else
        {
            Trace.TraceError("QudJP: GameObjectEmitMessageTranslationPatch.EmitMessage(string, GameObject, string, bool) not found.");
        }

        var messagingType = AccessTools.TypeByName("XRL.World.Capabilities.Messaging");
        if (messagingType is null)
        {
            Trace.TraceError("QudJP: GameObjectEmitMessageTranslationPatch messaging type not found.");
            yield break;
        }

        var messagingMethod = AccessTools.Method(
            messagingType,
            "EmitMessage",
            new[]
            {
                gameObjectType,
                typeof(string),
                typeof(char),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                gameObjectType,
                gameObjectType,
            });
        if (messagingMethod is not null)
        {
            yield return messagingMethod;
        }
        else
        {
            Trace.TraceError("QudJP: GameObjectEmitMessageTranslationPatch.Messaging.EmitMessage(GameObject, string, char, bool, bool, bool, GameObject, GameObject) not found.");
        }
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GameObjectEmitMessageTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: GameObjectEmitMessageTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "EmitMessage", markJapaneseAsDirect: true);
    }
}
