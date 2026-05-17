using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CanticlesChromaicParticleTextTranslationPatch
{
    private const string Context = nameof(CanticlesChromaicParticleTextTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.SocialSifrahTokenReadFromTheCanticlesChromaic");
        var sifrahGameType = AccessTools.TypeByName("XRL.SifrahGame");
        var sifrahSlotType = AccessTools.TypeByName("XRL.SifrahSlot");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || sifrahGameType is null || sifrahSlotType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "UseToken", [sifrahGameType, sifrahSlotType, gameObjectType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.UseToken(SifrahGame, SifrahSlot, GameObject) target not found.", Context);
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

    internal static bool TryTranslateParticleText(ref string text)
    {
        var source = text;
        if (!OwnerTranslationScope.IsActive(activeDepth)
            || string.IsNullOrEmpty(text)
            || !FloatingSpeechTranslationHelpers.TryNormalizeWhiteQuotedParticleFrame(text, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "GameObject.ParticleText",
            Context + ".FloatingCanticleFrame",
            source,
            translated);
        text = translated;
        return true;
    }
}
