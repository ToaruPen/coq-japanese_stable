using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EmbarkBuilderValidationPopupTranslationPatch
{
    private const string Context = nameof(EmbarkBuilderValidationPopupTranslationPatch);
    private const string ContinueAnywaySuffix = "\n\nContinue anyway?";
    private const string ContinueAnywaySuffixTranslation = "\n\n続行しますか？";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.CharacterBuilds.EmbarkBuilder");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddStateMachineTarget(targets, targetType, "checkStateAsync", Type.EmptyTypes);
        return targets;
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
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (!OwnerTranslationScope.IsActive(activeDepth))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (string.Equals(source, "{{r|Error!}}", StringComparison.Ordinal))
        {
            translated = "{{r|エラー！}}";
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        if (string.Equals(source, "{{W|Warning!}}", StringComparison.Ordinal))
        {
            translated = "{{W|警告！}}";
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        if (source.EndsWith(ContinueAnywaySuffix, StringComparison.Ordinal))
        {
            translated = source.Substring(0, source.Length - ContinueAnywaySuffix.Length)
                + ContinueAnywaySuffixTranslation;
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static void AddStateMachineTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var sourceMethod = AccessTools.Method(targetType, methodName, parameters);
        if (sourceMethod is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.{2} async source target not found.", Context, targetType.FullName, methodName);
            return;
        }

        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        var moveNext = asyncStateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
        if (moveNext is not null)
        {
            targets.Add(moveNext);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} async state machine MoveNext not found.", Context, targetType.FullName, methodName);
    }
}
