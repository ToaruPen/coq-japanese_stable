using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsStasisEntanglerTranslationPatch
{
    private const string Context = nameof(CyberneticsStasisEntanglerTranslationPatch);
    private static readonly Regex AllAroundPattern = new(
        "^(?<fields>.+?) appear all around\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SeveralNearbyPattern = new(
        "^Several (?<fields>.+?) appear nearby\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var zoneType = AccessTools.TypeByName("XRL.World.Zone");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var targetType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsStasisEntangler");
        if (zoneType is null || gameObjectType is null || targetType is null)
        {
            Trace.TraceError("QudJP: {0} target, Zone, or GameObject type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(
            targetType,
            "DeployToCells",
            new[] { zoneType, gameObjectType, gameObjectType, typeof(int), typeof(int) });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.DeployToCells target not found.", Context, targetType.FullName);
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

        if (!TryTranslateDeployMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateDeployMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = AllAroundPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"{RestoreCapture(match, spans, "fields")}が周囲一帯に出現した。";
            return true;
        }

        match = SeveralNearbyPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"いくつかの{RestoreCapture(match, spans, "fields")}が近くに出現した。";
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
