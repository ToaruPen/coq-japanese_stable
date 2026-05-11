using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsMedassistModuleTranslationPatch
{
    private const string Context = nameof(CyberneticsMedassistModuleTranslationPatch);

    private static readonly Regex SlotTonicPattern = new(
        "^You slot (?<item>.+?) into (?<module>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EjectTonicPattern = new(
        "^You eject (?<item>.+?) from (?<module>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InjectPattern = new(
        "^Your (?<module>.+?) injects? you with (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsMedassistModule");
        var inventoryActionEventType = AccessTools.TypeByName("XRL.World.InventoryActionEvent");
        var damageType = AccessTools.TypeByName("XRL.World.Damage");
        if (targetType is null || inventoryActionEventType is null || damageType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "HandleEvent", new[] { inventoryActionEventType });
        AddTarget(targets, targetType, "AttemptMedicalAssistance", new[] { damageType });
        return targets;
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
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        if (!TryTranslateQueuedCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "CyberneticsMedassistModule.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!TryTranslatePopupCore(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslatePopupCore(string source, out string translated)
    {
        return TryTranslatePattern(
            SlotTonicPattern,
            source,
            (match, spans) => $"{Restore(match, spans, "item")}を{Restore(match, spans, "module")}に装填した。",
            out translated)
            || TryTranslatePattern(
                EjectTonicPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "item")}を{Restore(match, spans, "module")}から排出した。",
                out translated);
    }

    private static bool TryTranslateQueuedCore(string source, out string translated)
    {
        if (TryTranslatePattern(
                InjectPattern,
                source,
                (match, spans) => $"あなたの{Restore(match, spans, "module")}が{Restore(match, spans, "item")}を注射した。",
                out translated))
        {
            return true;
        }

        translated = source == "The injection fails." ? "注射は失敗に終わった。" : string.Empty;
        return translated.Length > 0;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }
}
