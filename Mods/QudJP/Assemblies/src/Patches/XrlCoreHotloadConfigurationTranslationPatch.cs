using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class XrlCoreHotloadConfigurationTranslationPatch
{
    private const string Context = nameof(XrlCoreHotloadConfigurationTranslationPatch);
    private const string Detail = "HotloadConfiguration";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var coreType = GameTypeResolver.FindType("XRL.Core.XRLCore", "XRLCore");
        if (coreType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return null;
        }

        var method = AccessTools.Method(coreType, "HotloadConfiguration", [typeof(bool)]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.HotloadConfiguration(bool) not found.", Context);
        }

        return method;
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
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!string.Equals(message, "Configuration hotloaded...", StringComparison.Ordinal))
        {
            return false;
        }

        const string translated = "設定をホットロードした...";
        DynamicTextObservability.RecordTransform(Context, Detail, message, translated);
        message = translated;
        return true;
    }
}
