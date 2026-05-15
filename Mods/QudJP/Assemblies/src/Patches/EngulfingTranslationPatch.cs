using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class EngulfingTranslationPatch
{
    private const string Context = nameof(EngulfingTranslationPatch);
    private static readonly Regex EngulfYouFailPattern = new(
        "^(?:The |the |A |a |An |an )?(?<actor>.+?) (?:tries|try) to engulf you, but (?:fails|fail)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EngulfTargetFailPattern = new(
        "^(?:The |the |A |a |An |an )?(?<actor>.+?) (?:tries|try) to engulf (?<target>.+?), but (?:fails|fail)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Engulfing");
        if (gameObjectType is null || eventType is null || targetType is null)
        {
            Trace.TraceError("QudJP: {0} target, GameObject, or Event type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "Engulf", new[] { gameObjectType, eventType });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.Engulf target not found.", Context, targetType.FullName);
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

        if (!TryTranslateEngulfingMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateEngulfingMessage(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = EngulfYouFailPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"{RestoreCapture(match, spans, "actor")}はあなたを飲み込もうとしたが、失敗した。";
            return true;
        }

        match = EngulfTargetFailPattern.Match(stripped);
        if (match.Success)
        {
            translated = $"{RestoreCapture(match, spans, "actor")}は{RestoreCapture(match, spans, "target")}を飲み込もうとしたが、失敗した。";
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
