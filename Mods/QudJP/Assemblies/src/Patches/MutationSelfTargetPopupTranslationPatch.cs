using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MutationSelfTargetPopupTranslationPatch
{
    private const string Context = nameof(MutationSelfTargetPopupTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve Event type.", Context);
            yield break;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Parts.Mutation.BreatherBase",
                     "Cast",
                     ["XRL.World.Parts.Mutation.BreatherBase"]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Parts.Mutation.FlamingRay",
                     "Cast",
                     ["XRL.World.Parts.Mutation.FlamingRay", typeof(string)]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Parts.Mutation.FreezeBreath",
                     "FireEvent",
                     [eventType]))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.World.Parts.Mutation.FreezingRay",
                     "Cast",
                     ["XRL.World.Parts.Mutation.FreezingRay", typeof(string)]))
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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = family;

        if (!OwnerTranslationScope.IsActive(activeDepth)
            || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!source.StartsWith("Are you sure you want to target ", StringComparison.Ordinal)
            || !source.EndsWith("?", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var patternTranslated = MessagePatternTranslator.Translate(source, route);
        if (string.Equals(patternTranslated, source, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".SelfTargetConfirmation",
            source,
            patternTranslated);
        translated = patternTranslated;
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, object[] parameterSpecs)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var parameters = new Type[parameterSpecs.Length];
        for (var index = 0; index < parameterSpecs.Length; index++)
        {
            if (parameterSpecs[index] is Type parameterType)
            {
                parameters[index] = parameterType;
                continue;
            }

            var parameterTypeName = (string)parameterSpecs[index];
            parameterType = AccessTools.TypeByName(parameterTypeName);
            if (parameterType is null)
            {
                Trace.TraceError("QudJP: {0} parameter type not found: {1}", Context, parameterTypeName);
                yield break;
            }

            parameters[index] = parameterType;
        }

        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
    }
}
