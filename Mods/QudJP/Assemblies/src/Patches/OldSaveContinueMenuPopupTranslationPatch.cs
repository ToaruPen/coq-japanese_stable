using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class OldSaveContinueMenuPopupTranslationPatch
{
    private const string Context = nameof(OldSaveContinueMenuPopupTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in ResolveTarget("Qud.UI.MainMenu", "ContinueMenu", Type.EmptyTypes))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("Qud.UI.SaveManagement", "ContinueMenu", Type.EmptyTypes))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget("XRL.Core.XRLCore", "SaveManagement", Type.EmptyTypes))
        {
            yield return method;
        }

        foreach (var method in ResolveTarget(
                     "XRL.XRLGame",
                     "LoadGame",
                     [typeof(string), typeof(bool), typeof(bool), typeof(Dictionary<string, object>)]))
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

        translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, family + "." + Context);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameters)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var logicalMethod = AccessTools.Method(targetType, methodName, parameters);
        if (logicalMethod is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodName);
            yield break;
        }

        var stateMachineType = logicalMethod.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (stateMachineType is null)
        {
            Trace.TraceError("QudJP: {0}.{1}.{2} async state machine not found.", Context, typeName, methodName);
            yield break;
        }

        var moveNext = AccessTools.Method(stateMachineType, nameof(IAsyncStateMachine.MoveNext), Type.EmptyTypes);
        if (moveNext is not null)
        {
            yield return moveNext;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} MoveNext target not found.", Context, typeName, methodName);
    }
}
