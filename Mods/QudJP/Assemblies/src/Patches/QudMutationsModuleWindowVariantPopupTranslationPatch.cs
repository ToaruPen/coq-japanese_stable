using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class QudMutationsModuleWindowVariantPopupTranslationPatch
{
    private const string Context = nameof(QudMutationsModuleWindowVariantPopupTranslationPatch);
    private const string ChooseVariantTitle = "Choose variant";
    private const string ChooseVariantTitleTranslation = "変種を選択";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = GameTypeResolver.FindType(
            "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow",
            "QudMutationsModuleWindow");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddStateMachineTarget(targets, targetType, "SelectVariant", Type.EmptyTypes);
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

        if (string.Equals(source, ChooseVariantTitle, StringComparison.Ordinal))
        {
            translated = ChooseVariantTitleTranslation;
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
