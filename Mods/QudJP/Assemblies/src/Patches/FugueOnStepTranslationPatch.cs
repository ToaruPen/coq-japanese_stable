using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class FugueOnStepTranslationPatch
{
    private const string Context = nameof(FugueOnStepTranslationPatch);
    private static readonly Regex PlayerStepPattern = new(
        "^You step on (?<target>.+?) and vibrate through spacetime\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ObserverStepPattern = new(
        "^(?:The |the |A |a |An |an )?(?<subject>.+?) steps? on (?<target>.+?) and ?vibrates? through spacetime\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var targetType = AccessTools.TypeByName("XRL.World.Parts.FugueOnStep");
        if (gameObjectType is null || targetType is null)
        {
            Trace.TraceError("QudJP: {0} target or GameObject type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "Activate", new[] { gameObjectType });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.Activate(GameObject) target not found.", Context, targetType.FullName);
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

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(message, out var markedText))
        {
            message = markedText;
            return true;
        }

        if (!TryTranslateStepMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateStepMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PlayerStepPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"{RestoreCapture(match, spans, "target")}を踏み、時空を震わせた。";
            return true;
        }

        match = ObserverStepPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"{RestoreCapture(match, spans, "subject")}は{RestoreCapture(match, spans, "target")}を踏み、時空を震わせた。";
            return true;
        }

        translated = source;
        return false;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
