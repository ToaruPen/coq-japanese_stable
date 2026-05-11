using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PersuasionRebukeRobotTranslationPatch
{
    private const string Context = nameof(PersuasionRebukeRobotTranslationPatch);
    private const string SourceFailure = "Your argument does not compute.";
    private const string TranslatedFailure = "あなたの論理は処理されなかった。";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Skill.Persuasion_RebukeRobot");
        var mentalAttackEventType = AccessTools.TypeByName("XRL.World.MentalAttackEvent");
        if (targetType is null || mentalAttackEventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "Rebuke", new[] { mentalAttackEventType });
        return targets;
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

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _)
            || !string.Equals(message, SourceFailure, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "PersuasionRebukeRobot.Queue", message, TranslatedFailure);
        message = MessageFrameTranslator.MarkDirectTranslation(TranslatedFailure);
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
