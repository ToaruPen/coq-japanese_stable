using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TinkeringHelpersMakersMarkTranslationPatch
{
    private const string Context = nameof(TinkeringHelpersMakersMarkTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Tinkering.TinkeringHelpers");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var modificationType = AccessTools.TypeByName("XRL.World.Parts.IModification");
        if (targetType is null || gameObjectType is null || modificationType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve TinkeringHelpers, GameObject, or IModification.", Context);
            yield break;
        }

        var method = AccessTools.Method(
            targetType,
            "CheckMakersMark",
            [
                gameObjectType,
                gameObjectType,
                modificationType,
                typeof(string),
            ]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.CheckMakersMark(...) not found.", Context);
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (string.Equals(stripped, "Select your maker's mark.", StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.Restore("作り手の印を選ぶ。", spans);
            Record(route, family, "Select", source, translated);
            return true;
        }

        if (string.Equals(stripped, "Choose a color for your maker's mark.", StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.Restore("作り手の印の色を選ぶ。", spans);
            Record(route, family, "Color", source, translated);
            return true;
        }

        if (string.Equals(stripped, "none", StringComparison.Ordinal))
        {
            translated = ColorAwareTranslationComposer.Restore("なし", spans);
            Record(route, family, "None", source, translated);
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
