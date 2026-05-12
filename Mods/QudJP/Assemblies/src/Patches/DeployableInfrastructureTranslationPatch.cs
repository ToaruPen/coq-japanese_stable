using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class DeployableInfrastructureTranslationPatch
{
    private const string Context = nameof(DeployableInfrastructureTranslationPatch);
    private const string DoesVerbDetail = "DoesVerb";
    private const string NoUsefulWayDetail = "NoUsefulWay";

    private static readonly Regex NoUsefulWayPattern = new(
        "^There is no useful way to (?<verb>deploy) (?<target>.+?) there\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.DeployableInfrastructure");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var cellType = AccessTools.TypeByName("XRL.World.Cell");
        if (targetType is null || gameObjectType is null || cellType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target types.", Context);
            yield break;
        }

        var attemptDeploy = AccessTools.Method(targetType, "AttemptDeploy", [gameObjectType]);
        if (attemptDeploy is not null)
        {
            yield return attemptDeploy;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.AttemptDeploy(GameObject) not found.", Context);
        }

        var deployOne = AccessTools.Method(
            targetType,
            "DeployOne",
            [gameObjectType, cellType, typeof(bool), typeof(bool)]);
        if (deployOne is not null)
        {
            yield return deployOne;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.DeployOne(GameObject, Cell, bool, bool) not found.", Context);
        }
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || !DoesVerbRouteTranslator.TryTranslatePlainSentence(message, out var translated))
        {
            return false;
        }

        translated = MessageFrameTranslator.MarkDirectTranslation(translated);
        DynamicTextObservability.RecordTransform(Context, "DeployableInfrastructure.DoesVerb", message, translated);
        message = translated;
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        translated = source;
        try
        {
            if (activeDepth <= 0 || string.IsNullOrEmpty(source))
            {
                return false;
            }

            if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
            {
                translated = markedText;
                return true;
            }

            if (TryTranslateNoUsefulWayPopup(source, out translated))
            {
                DynamicTextObservability.RecordTransform(
                    route,
                    family + "." + Context + "." + NoUsefulWayDetail,
                    source,
                    translated);
                return true;
            }

            if (!DoesVerbRouteTranslator.TryTranslatePlainSentence(source, out translated))
            {
                return false;
            }

            DynamicTextObservability.RecordTransform(
                route,
                family + "." + Context + "." + DoesVerbDetail,
                source,
                translated);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: {0}.TryTranslatePopupMessage failed: {1}", Context, ex);
            translated = source;
            return false;
        }
    }

    private static bool TryTranslateNoUsefulWayPopup(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = NoUsefulWayPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = ColorAwareTranslationComposer.RestoreCapture(
            match.Groups["target"].Value,
            spans,
            match.Groups["target"]).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"ここで{target}を展開する有用な方法はない。",
            spans,
            stripped.Length,
            source);
        return true;
    }
}
