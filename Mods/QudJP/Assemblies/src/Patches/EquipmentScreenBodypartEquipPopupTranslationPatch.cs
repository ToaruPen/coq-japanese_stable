using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EquipmentScreenBodypartEquipPopupTranslationPatch
{
    private const string Context = nameof(EquipmentScreenBodypartEquipPopupTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.UI.EquipmentScreen");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var bodyPartType = AccessTools.TypeByName("XRL.World.Anatomy.BodyPart");
        if (targetType is null || gameObjectType is null || bodyPartType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve EquipmentScreen, GameObject, or BodyPart.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "ShowBodypartEquipUI", [gameObjectType, bodyPartType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.ShowBodypartEquipUI(GameObject,BodyPart) not found.", Context);
            yield break;
        }

        yield return method;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
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
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            return false;
        }

        if (string.Equals(source, "You don't have anything to use in that slot.", StringComparison.Ordinal))
        {
            translated = "そのスロットで使えるものがない。";
            Record(route, family, "NoSlotItem", source, translated);
            return true;
        }

        if (string.Equals(source, "You have no inventory!", StringComparison.Ordinal))
        {
            translated = "持ち物がない！";
            Record(route, family, "NoInventory", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
