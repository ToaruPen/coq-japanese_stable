using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EquipmentApiTwiddleObjectTranslationPatch
{
    private const string Context = nameof(EquipmentApiTwiddleObjectTranslationPatch);
    private const char DoesMarkerPrefix = '\x02';
    private const char DoesMarkerTerminator = '\x03';

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var inventoryActionType = AccessTools.TypeByName("XRL.World.InventoryAction");
        var equipmentApiType = AccessTools.TypeByName("Qud.API.EquipmentAPI");
        if (equipmentApiType is null || gameObjectType is null || inventoryActionType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var target = AccessTools.DeclaredMethod(
            equipmentApiType,
            "TwiddleObject",
            new[]
            {
                gameObjectType,
                gameObjectType,
                typeof(bool).MakeByRefType(),
                inventoryActionType.MakeByRefType(),
                typeof(bool),
                typeof(bool),
                typeof(bool),
            });
        if (target is null)
        {
            Trace.TraceError("QudJP: {0}.EquipmentAPI.TwiddleObject target not found.", Context);
            yield break;
        }

        yield return target;
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
        _ = family;
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

        if (source.Contains("out of your telekinetic range")
            && TryTranslateTelekineticRange(source, out translated))
        {
            Record(route, "TelekineticRange", source, translated);
            return true;
        }

        if (string.Equals(source, "You cannot do that from here.", StringComparison.Ordinal))
        {
            translated = Translator.Translate(source);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                Record(route, "CannotDoFromHere", source, translated);
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateTelekineticRange(string source, out string translated)
    {
        if (DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out translated))
        {
            return true;
        }

        return DoesVerbRouteTranslator.TryTranslatePlainSentence(
            StripDoesMarkerHeader(source),
            out translated);
    }

    private static string StripDoesMarkerHeader(string source)
    {
        if (source.Length == 0 || source[0] != DoesMarkerPrefix)
        {
            return source;
        }

        var markerEnd = source.IndexOf(DoesMarkerTerminator);
        return markerEnd >= 0 && markerEnd + 1 < source.Length
            ? source.Substring(markerEnd + 1)
            : source;
    }

    private static void Record(string route, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
    }
}
