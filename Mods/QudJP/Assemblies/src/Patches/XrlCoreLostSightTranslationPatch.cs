using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class XrlCoreLostSightTranslationPatch
{
    private const string Context = nameof(XrlCoreLostSightTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var coreType = GameTypeResolver.FindType("XRL.Core.XRLCore", "XRLCore");
        if (coreType is null)
        {
            Trace.TraceError("QudJP: XrlCoreLostSightTranslationPatch target type not found.");
            return null;
        }

        var screenBufferType = AccessTools.TypeByName("ConsoleLib.Console.ScreenBuffer");
        if (screenBufferType is null)
        {
            Trace.TraceError("QudJP: XrlCoreLostSightTranslationPatch screen buffer type not found.");
            return null;
        }

        var method = AccessTools.Method(coreType, "RenderBaseToBuffer", new[] { screenBufferType });
        if (method is null)
        {
            Trace.TraceError("QudJP: XrlCoreLostSightTranslationPatch.RenderBaseToBuffer(ScreenBuffer) not found.");
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
            Trace.TraceError("QudJP: XrlCoreLostSightTranslationPatch.Prefix failed: {0}", ex);
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
            Trace.TraceError("QudJP: XrlCoreLostSightTranslationPatch.Finalizer failed: {0}", ex);
        }

        return __exception;
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        return activeDepth > 0
            && !string.IsNullOrEmpty(message)
            && message.StartsWith("You have lost sight of ", StringComparison.Ordinal)
            && MessageLogProducerTranslationHelpers.TryPreparePatternMessage(ref message, Context, "LostSight");
    }
}
