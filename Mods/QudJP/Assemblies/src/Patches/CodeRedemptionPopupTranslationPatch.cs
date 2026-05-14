using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CodeRedemptionPopupTranslationPatch
{
    private const string Context = nameof(CodeRedemptionPopupTranslationPatch);

    private static readonly Regex ErrorDownloadingPetPattern = new(
        "^Error downloading pet: (?<error>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("CodeRedemptionManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var redeemNoProgress = ResolveStateMachineMoveNext(targetType, "redeemNoProgress", [typeof(string)]);
        if (redeemNoProgress is not null)
        {
            yield return redeemNoProgress;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.redeemNoProgress async state machine target not found.", Context);
        }

        var redeemDelegate = ResolveAsyncDelegateMoveNext(targetType, "<redeem>b__0");
        if (redeemDelegate is not null)
        {
            yield return redeemDelegate;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.redeem async delegate state machine target not found.", Context);
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ErrorDownloadingPetPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var error = match.Groups["error"].Value.Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "ペットのダウンロード中にエラーが発生した: " + error,
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".CodeRedemptionPetDownloadError",
            source,
            translated);
        return true;
    }

    private static MethodInfo? ResolveStateMachineMoveNext(Type targetType, string methodName, Type[] parameters)
    {
        var sourceMethod = AccessTools.Method(targetType, methodName, parameters);
        if (sourceMethod is null)
        {
            return null;
        }

        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        return asyncStateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
    }

    private static MethodInfo? ResolveAsyncDelegateMoveNext(Type targetType, string methodNameContains)
    {
#pragma warning disable S3011 // Compiler-generated async delegate state machines are non-public game members.
        const BindingFlags nestedFlags = BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags methodFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        foreach (var nestedType in targetType.GetNestedTypes(nestedFlags))
        {
            foreach (var method in nestedType.GetMethods(methodFlags))
            {
                if (!method.Name.Contains(methodNameContains))
                {
                    continue;
                }

                var asyncStateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>();
                if (asyncStateMachine?.StateMachineType is null)
                {
                    continue;
                }

                var moveNext = AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
                if (moveNext is not null)
                {
                    return moveNext;
                }
            }
        }

#pragma warning restore S3011
        return null;
    }
}
