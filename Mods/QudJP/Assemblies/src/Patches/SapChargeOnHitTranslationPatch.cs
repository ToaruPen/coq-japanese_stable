using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SapChargeOnHitTranslationPatch
{
    private const string Context = nameof(SapChargeOnHitTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.SapChargeOnHit");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target or Event type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(targetType, "CheckApply", new[] { eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.CheckApply(Event) not found.", Context);
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
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
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
            Trace.TraceError("QudJP: {0}.Finalizer failed: {1}", Context, ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0 || string.IsNullOrEmpty(message) || !message.Contains("ribbon of electricity"))
        {
            return false;
        }

        var patternMessage = message.Replace("{{W|ribbon of electricity}}", "ribbon of electricity");
        if (!MessageLogProducerTranslationHelpers.TryPreparePatternMessage(
                ref patternMessage,
                Context,
                "CheckApply",
                markJapaneseAsDirect: true))
        {
            return false;
        }

        message = patternMessage;
        return true;
    }
}
