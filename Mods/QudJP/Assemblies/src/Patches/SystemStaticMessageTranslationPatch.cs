using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SystemStaticMessageTranslationPatch
{
    private const string Context = nameof(SystemStaticMessageTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var zoneType = AccessTools.TypeByName("XRL.World.Zone");
        var factionType = AccessTools.TypeByName("XRL.World.Faction");
        if (eventType is null || zoneType is null || factionType is null)
        {
            Trace.TraceError("QudJP: {0} target parameter types not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.CheckpointingSystem", "CheckpointOn", Type.EmptyTypes);
        AddTarget(targets, "XRL.HolyPlaceSystem", "SetHolyZone", new[] { zoneType, factionType });
        AddTarget(targets, "XRL.World.Parts.Mutation.HeightenedIntelligence", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Parts.TrembleEarthquakes", "Quake", Type.EmptyTypes);
        AddTarget(targets, "XRL.World.Parts.DoorSwitch", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Parts.SpawningEggSac", "tickEgg", Type.EmptyTypes);
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
            "Checkpointing enabled" => "チェックポイント機能を有効化した。",
            "You feel a sense of holiness here." => "この場所には神聖さを感じる。",
            "&CA flash of insight overcomes you!" => "&Cひらめきがあなたを満たした！",
            "The ground shakes violently!" => "地面が激しく揺れた！",
            "The ground shakes violently and loose rock falls from the ceiling!" => "地面が激しく揺れ、天井から岩が崩れ落ちた！",
            "The security door unlocks with a loud clank and swings open." => "頑丈なドアが大きな音とともに解錠され開いた。",
            "The security door swings closed and locks with a loud clank." => "頑丈なドアが閉じて大きな音で施錠された。",
            "Nothing seems to happen when you hit the switch." => "スイッチを押しても何も起こらない。",
            "The membrane of the egg sac snots apart." => "卵嚢の膜がぐしゃりと裂けた。",
            "The svardym eggs hatch." => "スヴァーディムの卵が孵化した。",
            "The svardym egg hatches." => "スヴァーディムの卵が孵化した。",
            _ => null,
        };
        if (translated is null)
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
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
