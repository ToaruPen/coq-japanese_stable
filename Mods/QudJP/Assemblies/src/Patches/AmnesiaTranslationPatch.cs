using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AmnesiaTranslationPatch
{
    private const string Context = nameof(AmnesiaTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.Amnesia");
        var secretVisibilityChangedEventType = AccessTools.TypeByName("XRL.World.SecretVisibilityChangedEvent");
        var enteredCellEventType = AccessTools.TypeByName("XRL.World.EnteredCellEvent");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        if (secretVisibilityChangedEventType is not null)
        {
            AddTarget(targets, targetType, "HandleEvent", new[] { secretVisibilityChangedEventType });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent SecretVisibilityChangedEvent type not found.", Context);
        }

        if (enteredCellEventType is not null)
        {
            AddTarget(targets, targetType, "HandleEvent", new[] { enteredCellEventType });
        }
        else
        {
            Trace.TraceError("QudJP: {0}.HandleEvent EnteredCellEvent type not found.", Context);
        }

        return targets;
    }

    public static void Prefix()
    {
        try
        {
            OwnerTranslationScope.Enter(ref activeDepth);
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
            OwnerTranslationScope.Exit(ref activeDepth);
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
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        var translated = message switch
        {
            "You feel like you forgot something important." => "大事な何かを忘れた気がする。",
            "This place feels vaguely familiar." => "この場所にはどこか見覚えがある。",
            _ => null,
        };
        if (translated is null)
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
