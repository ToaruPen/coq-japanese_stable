using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ModDisguiseBeingAppliedPopupTranslationPatch
{
    private const string Context = nameof(ModDisguiseBeingAppliedPopupTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.ModDisguise");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve ModDisguise or GameObject.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "BeingAppliedBy", [gameObjectType, gameObjectType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.BeingAppliedBy(GameObject,GameObject) not found.", Context);
            yield break;
        }

        yield return method;
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

    internal static void ResetForTests()
    {
        activeDepth = 0;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        if (string.Equals(source, "You aren't familiar enough with any creatures to make a disguise.", StringComparison.Ordinal))
        {
            translated = "変装に使えるほど見知った生き物がいない。";
            Record(route, family, "NoFamiliarCreatures", source, translated);
            return true;
        }

        if (string.Equals(source, "Choose a disguise to make.", StringComparison.Ordinal))
        {
            translated = "作る変装を選ぶ。";
            Record(route, family, "PickerTitle", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
