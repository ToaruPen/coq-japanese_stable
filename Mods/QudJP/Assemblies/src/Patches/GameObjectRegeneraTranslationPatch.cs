using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectRegeneraTranslationPatch
{
    private const string Context = nameof(GameObjectRegeneraTranslationPatch);
    private const string RegeneraEventId = "Regenera";
    private static readonly Regex RegenerateLimbPattern =
        new Regex("^You regenerate your (?<part>.+?)!$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static Stack<bool>? prefixTrackStack;

    [ThreadStatic]
    private static bool accessorResolved;

    [ThreadStatic]
    private static Func<object, string?>? cachedIdAccessor;

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

        if (activeDepth <= 0 || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if ((message.Contains("cures you of")
                || message.EndsWith(" a malady.", StringComparison.Ordinal))
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "Regenera"))
        {
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(message);
        var limbMatch = RegenerateLimbPattern.Match(stripped);
        if (!limbMatch.Success)
        {
            return false;
        }

        var part = ColorAwareTranslationComposer.RestoreCapture(limbMatch.Groups["part"].Value, spans, limbMatch.Groups["part"]).Trim();
        var translated = string.Concat(part, "を再生した！");
        DynamicTextObservability.RecordTransform(Context, "Regenera.RegenerateLimb", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool ShouldTrack(object? eventObject)
    {
        if (eventObject is null)
        {
            return false;
        }

        var id = GetEventIdCached(eventObject);
        return string.Equals(id, RegeneraEventId, StringComparison.Ordinal);
    }

    private static string? GetEventIdCached(object eventObject)
    {
        if (!accessorResolved)
        {
            accessorResolved = true;
            var eventType = eventObject.GetType();
            var property = eventType.GetProperty("ID", BindingFlags.Instance | BindingFlags.Public);
            if (property is not null)
            {
                cachedIdAccessor = obj => property.GetValue(obj) as string;
            }
            else
            {
                var field = eventType.GetField("ID", BindingFlags.Instance | BindingFlags.Public);
                if (field is not null)
                {
                    cachedIdAccessor = obj => field.GetValue(obj) as string;
                }
            }
        }

        return cachedIdAccessor?.Invoke(eventObject);
    }
}
