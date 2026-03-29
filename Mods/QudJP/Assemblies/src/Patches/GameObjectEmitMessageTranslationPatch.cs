using System;
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

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: GameObjectEmitMessageTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(gameObjectType, "EmitMessage", new[] { typeof(string), gameObjectType, typeof(string), typeof(bool) });
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectEmitMessageTranslationPatch.EmitMessage(string, GameObject, string, bool) not found.");
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
