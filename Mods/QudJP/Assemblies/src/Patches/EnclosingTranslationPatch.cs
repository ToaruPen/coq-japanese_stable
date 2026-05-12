using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EnclosingTranslationPatch
{
    private const string Context = nameof(EnclosingTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Enclosing");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        var enclosedType = AccessTools.TypeByName("XRL.World.Effects.Enclosed");
        if (targetType is null || gameObjectType is null || eventType is null || enclosedType is null)
        {
            Trace.TraceError("QudJP: EnclosingTranslationPatch failed to resolve Enclosing target types.");
            yield break;
        }

        var enterEnclosure = AccessTools.Method(targetType, "EnterEnclosure", [gameObjectType, eventType]);
        if (enterEnclosure is not null)
        {
            yield return enterEnclosure;
        }
        else
        {
            Trace.TraceError("QudJP: EnclosingTranslationPatch.EnterEnclosure(GameObject, IEvent) not found.");
        }

        var exitEnclosure = AccessTools.Method(targetType, "ExitEnclosure", [gameObjectType, eventType, enclosedType]);
        if (exitEnclosure is not null)
        {
            yield return exitEnclosure;
        }
        else
        {
            Trace.TraceError("QudJP: EnclosingTranslationPatch.ExitEnclosure(GameObject, IEvent, Enclosed) not found.");
        }

        var enclosureExitImpeded = AccessTools.Method(targetType, "EnclosureExitImpeded", [gameObjectType, typeof(bool), enclosedType]);
        if (enclosureExitImpeded is not null)
        {
            yield return enclosureExitImpeded;
        }
        else
        {
            Trace.TraceError("QudJP: EnclosingTranslationPatch.EnclosureExitImpeded(GameObject, bool, Enclosed) not found.");
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
            Trace.TraceError("QudJP: EnclosingTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: EnclosingTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0)
        {
            translated = source;
            return false;
        }

        return EnclosingFragmentTranslator.TryTranslatePopupMessage(source, route, family + "." + Context, out translated);
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0 || string.IsNullOrEmpty(message))
        {
            return false;
        }

        return EnclosingFragmentTranslator.TryTranslateQueuedMessage(
            ref message,
            color,
            nameof(EnclosingTranslationPatch),
            Context + ".Queued");
    }
}
