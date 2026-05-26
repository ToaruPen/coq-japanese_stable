using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ActiveEffectMessageFrameOwnerTranslationPatch
{
    internal const string Family = "ActiveEffectMessageFrameOwner";

    private const string Context = nameof(ActiveEffectMessageFrameOwnerTranslationPatch);

    private const string CardiacArrestRemovePopupDetail = "CardiacArrestRemove.Popup";
    private const string CardiacArrestRemoveIllApplyPopupDetail = "CardiacArrestRemove.IllApplyPopup";
    private const string CardiacArrestRemoveIllApplyMessage = "You feel shaken and infirm.";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var beginTakeActionEventType = AccessTools.TypeByName("XRL.World.BeginTakeActionEvent");
        if (gameObjectType is null || beginTakeActionEventType is null)
        {
            Trace.TraceError("QudJP: {0} required target parameter types not found.", Context);
            yield break;
        }

        foreach (var method in ResolveTarget("XRL.World.Effects.Immobilized", "Apply", [gameObjectType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.World.Effects.Stuck", "Apply", [gameObjectType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.World.Effects.LatchedOnto", "HandleEvent", [beginTakeActionEventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.World.Effects.Lovesick", "Apply", [gameObjectType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.World.Effects.Beguiled", "Apply", [gameObjectType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.World.Effects.Proselytized", "Apply", [gameObjectType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.World.Effects.Rebuked", "Apply", [gameObjectType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.World.Effects.CardiacArrest", "Remove", [gameObjectType]))
        {
            yield return method;
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

    internal static void RecordMessageFrameTransformIfActive(string route, string sourceFrame, string translated)
    {
        if (!OwnerTranslationScope.IsActive(activeDepth))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(route, Family, sourceFrame, translated);
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

        if (IsCardiacArrestRemoveRestartPopup(source))
        {
            translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, route);
            if (string.Equals(translated, source, StringComparison.Ordinal))
            {
                return false;
            }

            DynamicTextObservability.RecordTransform(route, Family + "." + CardiacArrestRemovePopupDetail, source, translated);
            return true;
        }

        if (!string.Equals(source, CardiacArrestRemoveIllApplyMessage, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = MessagePatternTranslator.Translate(source, Context);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, Family + "." + CardiacArrestRemoveIllApplyPopupDetail, source, translated);
        return true;
    }

    private static bool IsCardiacArrestRemoveRestartPopup(string source)
    {
        return string.Equals(source, "{{G|Your heart restarts!}}", StringComparison.Ordinal)
            || string.Equals(source, "{{G|Your hearts restart!}}", StringComparison.Ordinal);
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
            yield break;
        }

        yield return method;
    }
}
