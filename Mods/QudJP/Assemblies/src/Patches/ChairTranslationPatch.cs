using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ChairTranslationPatch
{
    private const string Context = nameof(ChairTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var chairType = AccessTools.TypeByName("XRL.World.Parts.Chair");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (chairType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: ChairTranslationPatch failed to resolve Chair, GameObject, or IEvent.");
            return null;
        }

        var method = AccessTools.Method(chairType, "SitDown", new[] { gameObjectType, eventType });
        if (method is null)
        {
            Trace.TraceError("QudJP: ChairTranslationPatch.SitDown(GameObject, IEvent) not found.");
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
            Trace.TraceError("QudJP: ChairTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: ChairTranslationPatch.Finalizer failed: {0}", ex);
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

        return BedChairFragmentTranslator.TryTranslateChairMessage(source, route, family + "." + Context, out translated);
    }
}
