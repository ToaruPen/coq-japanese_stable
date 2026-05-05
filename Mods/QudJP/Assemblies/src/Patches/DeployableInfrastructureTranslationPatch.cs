using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DeployableInfrastructureTranslationPatch
{
    private const string Context = nameof(DeployableInfrastructureTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.DeployableInfrastructure");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        if (targetType is null || gameObjectType is null || cellType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target types.", Context);
            yield break;
        }

        var deployOne = AccessTools.Method(
            targetType,
            "DeployOne",
            [gameObjectType, cellType, typeof(bool), typeof(bool)]);
        if (deployOne is not null)
        {
            yield return deployOne;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.DeployOne(GameObject, Cell, bool, bool) not found.", Context);
        }
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
            || !DoesVerbRouteTranslator.TryTranslatePlainSentence(message, out var translated))
        {
            return false;
        }

        translated = MessageFrameTranslator.MarkDirectTranslation(translated);
        DynamicTextObservability.RecordTransform(Context, "DeployableInfrastructure.DoesVerb", message, translated);
        message = translated;
        return true;
    }
}
