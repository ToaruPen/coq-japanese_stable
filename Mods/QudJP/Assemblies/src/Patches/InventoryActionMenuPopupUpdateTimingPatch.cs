using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
internal static class InventoryActionMenuPopupUpdateTimingPatch
{
    private const string Context = nameof(InventoryActionMenuPopupUpdateTimingPatch);

    private static int hideNextFrameReadWarningLogged;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        return InventoryActionMenuPopupTimingHelpers.ResolvePopupMessageMethod(Context, "Update");
    }

    public static void Prefix(object? __instance, ref int __state)
    {
        try
        {
            __state = 0;
            if (!InventoryActionMenuCloseTimingObservability.ShouldObservePopupUpdate())
            {
                return;
            }

            var hideNextFrame = InventoryActionMenuPopupTimingHelpers.GetHideNextFrame(__instance);
            if (hideNextFrame is null)
            {
                WarnHideNextFrameReadOnce("Prefix");
                return;
            }

            __state = hideNextFrame.Value;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
            __state = 0;
        }
    }

    public static void Postfix(object? __instance, int __state)
    {
        try
        {
            if (__state <= 0 && !InventoryActionMenuCloseTimingObservability.ShouldObservePopupUpdate())
            {
                return;
            }

            var currentHideNextFrameValue = InventoryActionMenuPopupTimingHelpers.GetHideNextFrame(__instance);
            if (currentHideNextFrameValue is null)
            {
                WarnHideNextFrameReadOnce("Postfix");
                return;
            }

            var currentHideNextFrame = currentHideNextFrameValue.Value;
            if (__state > 0 && currentHideNextFrame <= 0)
            {
                InventoryActionMenuCloseTimingObservability.LogPopupHiddenAfterFrameDelay(
                    InventoryActionMenuPopupTimingHelpers.GetPopupId(__instance),
                    __state,
                    currentHideNextFrame);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.Postfix failed: {1}", Context, ex);
        }
    }

    internal static void ResetForTests()
    {
        hideNextFrameReadWarningLogged = 0;
    }

    private static void WarnHideNextFrameReadOnce(string phase)
    {
        if (Interlocked.Exchange(ref hideNextFrameReadWarningLogged, 1) == 0)
        {
            Trace.TraceWarning("QudJP: {0}.{1} could not read HideNextFrame.", Context, phase);
        }
    }
}
