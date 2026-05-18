using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PreacherHomilyTranslationPatch
{
    private const string Context = nameof(PreacherHomilyTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Preacher");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "PreacherHomily", [gameObjectType, typeof(bool)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.PreacherHomily(GameObject, bool) target not found.", Context);
            yield break;
        }

        yield return method;
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

        if (!OwnerTranslationScope.IsActive(activeDepth)
            || string.IsNullOrEmpty(message)
            || !FloatingSpeechTranslationHelpers.TryNormalizeWhiteQuotedFrame(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + ".QuotedHomilyFrame",
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslateParticleText(ref string text)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth)
            || string.IsNullOrEmpty(text)
            || !FloatingSpeechTranslationHelpers.TryNormalizeWhiteQuotedParticleFrame(text, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "GameObject.ParticleText",
            Context + ".FloatingHomilyFrame",
            text,
            translated);
        text = translated;
        return true;
    }

}
