using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class WindupTranslationPatch
{
    private const string Context = nameof(WindupTranslationPatch);
    private static readonly Regex PlayerUnresponsivePattern = new(
        "^You try to wind (?<target>.+?), but (?<pronoun>.+?) (?:is|are) unresponsive\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ObserverUnresponsivePattern = new(
        "^(?:The |the |A |a |An |an )?(?<actor>.+?) tries? to wind (?<target>.+?), but (?<pronoun>.+?) (?:is|are) unresponsive\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlayerWindPattern = new(
        "^You wind (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ObserverWindPattern = new(
        "^(?:The |the |A |a |An |an )?(?<actor>.+?) winds (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var eventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        if (eventType is null)
        {
            Trace.TraceError("QudJP: {0} InventoryActionEvent type not found.", Context);
            return targets;
        }

        var targetType = AccessTools.TypeByName("XRL.World.Parts.Windup");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        var method = AccessTools.Method(targetType, "HandleEvent", new[] { eventType });
        if (method is not null)
        {
            targets.Add(method);
            return targets;
        }

        Trace.TraceError("QudJP: {0}.{1}.HandleEvent target not found.", Context, targetType.FullName);
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

        if (!TryTranslateWindupMessage(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        _ = route;
        _ = family;
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (TryTranslateWindupMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform("Popup.Show", Context, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateWindupMessage(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        return TryTranslatePlayerUnresponsive(source, out translated)
            || TryTranslateObserverUnresponsive(source, out translated)
            || TryTranslatePlayerWind(source, out translated)
            || TryTranslateObserverWind(source, out translated);
    }

    private static bool TryTranslatePlayerUnresponsive(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PlayerUnresponsivePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "target")}を巻こうとしたが、反応しない。";
        return true;
    }

    private static bool TryTranslateObserverUnresponsive(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ObserverUnresponsivePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "actor")}は{RestoreCapture(match, spans, "target")}を巻こうとしたが、反応しなかった。";
        return true;
    }

    private static bool TryTranslatePlayerWind(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PlayerWindPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "target")}を巻いた。";
        return true;
    }

    private static bool TryTranslateObserverWind(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = ObserverWindPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"{RestoreCapture(match, spans, "actor")}は{RestoreCapture(match, spans, "target")}を巻いた。";
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
