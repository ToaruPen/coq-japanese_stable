using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhysicsObjectEnteringCellTranslationPatch
{
    private const string Context = nameof(PhysicsObjectEnteringCellTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var physicsType = AccessTools.TypeByName("XRL.World.Parts.Physics");
        if (physicsType is null)
        {
            Trace.TraceError("QudJP: PhysicsObjectEnteringCellTranslationPatch target type not found.");
            return null;
        }

        var eventType = AccessTools.TypeByName("XRL.World.ObjectEnteringCellEvent");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: PhysicsObjectEnteringCellTranslationPatch event type not found.");
            return null;
        }

        var method = AccessTools.Method(physicsType, "HandleEvent", new[] { eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: PhysicsObjectEnteringCellTranslationPatch.HandleEvent(ObjectEnteringCellEvent) not found.");
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
            Trace.TraceError("QudJP: PhysicsObjectEnteringCellTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: PhysicsObjectEnteringCellTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "ObjectEnteringCell");
    }
}
