using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TradeUiVendorPopupTranslationPatch
{
    private const string Context = nameof(TradeUiVendorPopupTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var tradeUiType = AccessTools.TypeByName("XRL.UI.TradeUI");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (tradeUiType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve TradeUI or GameObject.", Context);
            yield break;
        }

        var gameObjectListType = typeof(List<>).MakeGenericType(gameObjectType);
        var tryRemove = AccessTools.Method(
            tradeUiType,
            "TryRemove",
            [gameObjectType, gameObjectType, gameObjectListType, gameObjectListType, typeof(bool)]);
        if (tryRemove is not null)
        {
            yield return tryRemove;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.TryRemove(...) not found.", Context);
        }

        var doVendorRepair = AccessTools.Method(tradeUiType, "DoVendorRepair", [gameObjectType, gameObjectType]);
        if (doVendorRepair is not null)
        {
            yield return doVendorRepair;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.DoVendorRepair(GameObject, GameObject) not found.", Context);
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
        _ = route;
        _ = family;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        return TradeUiPopupTranslationPatch.TryTranslateTradeUiPopupText(source, out translated);
    }
}
