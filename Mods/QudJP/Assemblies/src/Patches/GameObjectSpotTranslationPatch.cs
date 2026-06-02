using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectSpotTranslationPatch
{
    private const string Context = nameof(GameObjectSpotTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var ongoingActionType = AccessTools.TypeByName("XRL.OngoingAction");
        if (gameObjectType is null || ongoingActionType is null)
        {
            Trace.TraceError("QudJP: GameObjectSpotTranslationPatch target types not found.");
            return null;
        }

        var method = AccessTools.Method(
            gameObjectType,
            "ArePerceptibleHostilesNearby",
            new[]
            {
                typeof(bool),
                typeof(bool),
                typeof(string),
                ongoingActionType,
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(bool),
                typeof(bool),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectSpotTranslationPatch.ArePerceptibleHostilesNearby(...) not found.");
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
            Trace.TraceError("QudJP: GameObjectSpotTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: GameObjectSpotTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "Spot");
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        // Route and family are kept for compatibility with popup owner-route delegates.
        translated = source;

        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var message = source;
        var hasDirectMarker = MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var strippedSource);
        if (hasDirectMarker)
        {
            message = strippedSource;
        }

        if (activeDepth <= 0)
        {
            translated = message;
            return hasDirectMarker;
        }

        if (!MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "Spot"))
        {
            translated = message;
            return hasDirectMarker;
        }

        translated = MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var stripped)
            ? stripped
            : message;
        return true;
    }
}
