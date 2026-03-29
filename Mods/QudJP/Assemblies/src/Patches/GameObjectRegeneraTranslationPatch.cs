using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectRegeneraTranslationPatch
{
    private const string Context = nameof(GameObjectRegeneraTranslationPatch);
    private const string RegeneraEventId = "Regenera";

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static Stack<bool>? prefixTrackStack;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: GameObjectRegeneraTranslationPatch target types not found.");
            return null;
        }

        var method = AccessTools.Method(gameObjectType, "FireEvent", new[] { eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: GameObjectRegeneraTranslationPatch.FireEvent(Event) not found.");
        }

        return method;
    }

    public static void Prefix(object? E = null)
    {
        try
        {
            var tracked = ShouldTrack(E);
            if (prefixTrackStack is null)
            {
                prefixTrackStack = new Stack<bool>();
            }

            prefixTrackStack.Push(tracked);
            if (tracked)
            {
                activeDepth++;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GameObjectRegeneraTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            if (prefixTrackStack is not null
                && prefixTrackStack.Count > 0
                && prefixTrackStack.Pop()
                && activeDepth > 0)
            {
                activeDepth--;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: GameObjectRegeneraTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && (message.Contains("cures you of")
                || message.EndsWith(" a malady.", StringComparison.Ordinal))
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "Regenera");
    }

    private static bool ShouldTrack(object? eventObject)
    {
        return TryGetEventId(eventObject, out var id)
            && string.Equals(id, RegeneraEventId, StringComparison.Ordinal);
    }

    private static bool TryGetEventId(object? eventObject, out string id)
    {
        if (eventObject is null)
        {
            id = string.Empty;
            return false;
        }

        var eventType = eventObject.GetType();
        var property = eventType.GetProperty("ID", BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(eventObject) is string propertyValue)
        {
            id = propertyValue;
            return true;
        }

        var field = eventType.GetField("ID", BindingFlags.Instance | BindingFlags.Public);
        if (field?.GetValue(eventObject) is string fieldValue)
        {
            id = fieldValue;
            return true;
        }

        id = string.Empty;
        return false;
    }
}
