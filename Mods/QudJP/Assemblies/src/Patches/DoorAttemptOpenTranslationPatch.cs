using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DoorAttemptOpenTranslationPatch
{
    private const string Context = nameof(DoorAttemptOpenTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        var doorType = AccessTools.TypeByName("XRL.World.Parts.Door");
        if (gameObjectType is null || eventType is null || doorType is null)
        {
            Trace.TraceError("QudJP: DoorAttemptOpenTranslationPatch target types not found.");
            return null;
        }

        var method = AccessTools.Method(
            doorType,
            "AttemptOpen",
            new[]
            {
                gameObjectType,
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                eventType,
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: DoorAttemptOpenTranslationPatch.AttemptOpen(GameObject, bool, bool, bool, bool, bool, bool, IEvent) not found.");
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
            Trace.TraceError("QudJP: DoorAttemptOpenTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: DoorAttemptOpenTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && IsTargetMessage(message)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "AttemptOpen");
    }

    private static bool IsTargetMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var value = message!;
        return value.StartsWith("You cannot open ", StringComparison.Ordinal)
            || value.StartsWith("You are out of phase with ", StringComparison.Ordinal)
            || value.StartsWith("You cannot reach ", StringComparison.Ordinal)
            || value.StartsWith("You can't unlock ", StringComparison.Ordinal)
            || value.StartsWith("You interface with ", StringComparison.Ordinal)
            || value.StartsWith("You lay your hand upon ", StringComparison.Ordinal)
            || value.Contains(" unlocks ")
            || value.Contains(" unlock.");
    }
}
