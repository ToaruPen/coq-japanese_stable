using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CombatGetDefenderHitDiceTranslationPatch
{
    private const string Context = nameof(CombatGetDefenderHitDiceTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var combatType = AccessTools.TypeByName("XRL.World.Parts.Combat");
        if (combatType is null)
        {
            Trace.TraceError("QudJP: CombatGetDefenderHitDiceTranslationPatch target type not found.");
            return null;
        }

        var eventType = AccessTools.TypeByName("XRL.World.GetDefenderHitDiceEvent");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: CombatGetDefenderHitDiceTranslationPatch event type not found.");
            return null;
        }

        var method = AccessTools.Method(combatType, "HandleEvent", new[] { eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: CombatGetDefenderHitDiceTranslationPatch.HandleEvent(GetDefenderHitDiceEvent) not found.");
        }

        return method;
    }

    [HarmonyPrefix]
    public static void Prefix()
    {
        try
        {
            activeDepth++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: CombatGetDefenderHitDiceTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: CombatGetDefenderHitDiceTranslationPatch.Finalizer failed: {0}", ex);
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

        // Strip color codes before guard check to avoid fragile color-dependent conditions.
        var (strippedMessage, _) = ColorAwareTranslationComposer.Strip(message);
        return IsGuardedMessage(strippedMessage)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "HandleEvent");
    }

    private static bool IsGuardedMessage(string message)
    {
        return message.StartsWith("You block with ", StringComparison.Ordinal)
            || message.StartsWith("You stagger ", StringComparison.Ordinal)
            || message.StartsWith("You are staggered by ", StringComparison.Ordinal);
    }
}
