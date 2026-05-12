using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PoweredFloatingTranslationPatch
{
    private const string Context = nameof(PoweredFloatingTranslationPatch);
    private const string DoesVerbDetail = "DoesVerb";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.PoweredFloating");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var checkFloating = AccessTools.Method(targetType, "CheckFloating", Type.EmptyTypes);
        if (checkFloating is not null)
        {
            yield return checkFloating;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.CheckFloating() not found.", Context);
        }
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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        translated = source;
        try
        {
            if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
            {
                return false;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
            {
                translated = markedText;
                return true;
            }

            if (!DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out translated)
                && !DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated))
            {
                return false;
            }

            DynamicTextObservability.RecordTransform(
                route,
                family + "." + Context + "." + DoesVerbDetail,
                source,
                translated);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TryTranslatePopupMessage failed: {1}", Context, ex);
            translated = source;
            return false;
        }
    }
}
