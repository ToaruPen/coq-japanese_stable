using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EffectStaticMessageTranslationPatch
{
    private const string Context = nameof(EffectStaticMessageTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (eventType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Effects.AxonsDeflated", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.AxonsInflated", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.BasiliskPoison", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Effects.EmptyTheClips", "Apply", new[] { gameObjectType });
        AddTarget(targets, "XRL.World.Effects.NocturnalApexed", "Apply", new[] { gameObjectType });
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
            "You start to feel sluggish." => "体がだるくなってきた。",
            "The hurdles that separate the will and the way begin to collapse." => "志と道を隔てていた障害が崩れ始める。",
            "You feel stiff as a stone." => "石のように体がこわばる。",
            "You begin itching for a trigger." => "引き金を求めてうずうずしてきた。",
            "You start to prowl." => "うろつき始めた。",
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

    private static void AddTarget(List<MethodBase> targets, string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}
