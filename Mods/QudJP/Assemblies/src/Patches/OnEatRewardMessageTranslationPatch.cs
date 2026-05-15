using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class OnEatRewardMessageTranslationPatch
{
    private const string Context = nameof(OnEatRewardMessageTranslationPatch);
    private static readonly Regex MutationPointPattern = new Regex(
        "^You gain (?<amount>\\d+) mutation points?!$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CooldownRefreshPattern = new Regex(
        "^You suddenly feel ready to use (?<ability>.+) again\\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} Event type not found.", Context);
            return targets;
        }

        AddTarget(targets, "XRL.World.Parts.MPOnEat", "FireEvent", new[] { eventType });
        AddTarget(targets, "XRL.World.Parts.RefreshAllCooldownsOnEat", "FireEvent", new[] { eventType });
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

        var match = MutationPointPattern.Match(message);
        if (match.Success)
        {
            var translated = $"変異ポイントを{match.Groups["amount"].Value}獲得した！";
            DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
            message = MessageFrameTranslator.MarkDirectTranslation(translated);
            return true;
        }

        match = CooldownRefreshPattern.Match(message);
        if (match.Success)
        {
            var translated = $"急に{match.Groups["ability"].Value}を再使用できそうな気がしてきた。";
            DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
            message = MessageFrameTranslator.MarkDirectTranslation(translated);
            return true;
        }

        return false;
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
