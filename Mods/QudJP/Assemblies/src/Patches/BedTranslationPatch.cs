using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BedTranslationPatch
{
    private const string Context = nameof(BedTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var bedType = AccessTools.TypeByName("XRL.World.Parts.Bed");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (bedType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: BedTranslationPatch failed to resolve Bed or GameObject.");
            return null;
        }

        var method = AccessTools.Method(
            bedType,
            "AttemptSleep",
            new[]
            {
                gameObjectType,
                typeof(bool).MakeByRefType(),
                typeof(bool).MakeByRefType(),
                typeof(bool).MakeByRefType(),
            });
        if (method is null)
        {
            Trace.TraceError("QudJP: BedTranslationPatch.AttemptSleep(GameObject, out bool, out bool, out bool) not found.");
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
            Trace.TraceError("QudJP: BedTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: BedTranslationPatch.Finalizer failed: {0}", ex);
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

        return BedChairFragmentTranslator.TryTranslateBedMessage(source, route, family + "." + Context, out translated);
    }
}
