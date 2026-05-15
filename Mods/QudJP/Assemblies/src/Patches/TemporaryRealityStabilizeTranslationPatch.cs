using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class TemporaryRealityStabilizeTranslationPatch
{
    private const string Context = nameof(TemporaryRealityStabilizeTranslationPatch);
    private static readonly Regex WorldlinePattern = new(
        "^(?:The |the |A |a |An |an )?(?<owner>.+?)(?:'s|s') worldline through spacetime snaps back to its canonical path, and (?<vanisher>.+?) vanishes?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.RealityStabilizeEvent");
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Temporary");
        if (eventType is null || targetType is null)
        {
            Trace.TraceError("QudJP: {0} target or RealityStabilizeEvent type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", new[] { eventType });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.HandleEvent(RealityStabilizeEvent) target not found.", Context, targetType.FullName);
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

        if (!TryTranslateWorldlineMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateWorldlineMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = WorldlinePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var owner = RestoreCapture(match, spans, "owner");
        translated = $"{owner}の時空を通る世界線が本来の経路へ戻り、{owner}は消滅した。";
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
