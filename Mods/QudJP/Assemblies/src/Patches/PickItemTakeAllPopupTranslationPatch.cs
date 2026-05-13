using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PickItemTakeAllPopupTranslationPatch
{
    private const string Context = nameof(PickItemTakeAllPopupTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("XRL.UI.PickItem", "PickItem");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        if (targetType is null || gameObjectType is null || cellType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return null;
        }

        var itemListType = typeof(IList<>).MakeGenericType(gameObjectType);
        var method = AccessTools.Method(
            targetType,
            "TakeAll",
            new[] { gameObjectType, gameObjectType, cellType, itemListType, typeof(bool).MakeByRefType() });
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.TakeAll() not found.", Context);
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

    internal static void ResetForTests()
    {
        activeDepth = 0;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        translated = source switch
        {
            "Taking this object will put you over your weight limit. Are you sure you want to do it?"
                => "これを取ると重量制限を超えます。本当に実行しますか？",
            "Taking these objects will put you over your weight limit. Are you sure you want to do it?"
                => "これらを取ると重量制限を超えます。本当に実行しますか？",
            "Taking all these objects will put you over your weight limit. Are you sure you want to do it?"
                => "これらすべてを取ると重量制限を超えます。本当に実行しますか？",
            _ => source,
        };

        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }
}
