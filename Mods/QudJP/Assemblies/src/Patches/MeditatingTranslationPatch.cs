using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MeditatingTranslationPatch
{
    private const string Context = nameof(MeditatingTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.Meditating");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target or GameObject type not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "Remove", new[] { gameObjectType });
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
            "You stop meditating and feel refreshed." => "瞑想を終え、気分がすっきりした。",
            "You stop meditating." => "瞑想をやめた。",
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
