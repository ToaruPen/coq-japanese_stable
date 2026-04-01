using System;
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

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Enclosing");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        var enclosedType = AccessTools.TypeByName("XRL.World.Effects.Enclosed");
        if (targetType is null || gameObjectType is null || eventType is null || enclosedType is null)
        {
            Trace.TraceError("QudJP: EnclosingTranslationPatch failed to resolve Enclosing target types.");
            return null;
        }

        var method = AccessTools.Method(targetType, "ExitEnclosure", [gameObjectType, eventType, enclosedType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: EnclosingTranslationPatch.ExitEnclosure(GameObject, IEvent, Enclosed) not found.");
        }

        return method;
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
}
