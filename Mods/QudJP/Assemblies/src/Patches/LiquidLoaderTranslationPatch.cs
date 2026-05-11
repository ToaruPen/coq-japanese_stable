using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LiquidLoaderTranslationPatch
{
    private const string Context = nameof(LiquidLoaderTranslationPatch);

    private static readonly Regex AlreadyFullPattern = new(
        "^(?<loader>.+?) (?:is|are) already full(?: of (?<liquid>.+?))?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoLiquidForPattern = new(
        "^You have no (?<liquid>.+?) for (?<loader>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DumpPattern = new(
        "^You dump the (?<liquid>.+?) out of (?<loader>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FillPattern = new(
        "^You (?<partial>partially )?fill (?<loader>.+?) with (?<liquid>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoLiquidToSupplyPattern = new(
        "^You have no (?<liquid>.+?) to supply (?<host>.+?) with\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoRoomPattern = new(
        "^(?<host>.+?) (?:has|have) no room for more (?<liquid>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var commandReloadEventType = AccessTools.TypeByName("XRL.World.CommandReloadEvent");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (commandReloadEventType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target event types not found.", Context);
            return targets;
        }

        AddLoaderTargets(targets, "XRL.World.Parts.BioAmmoLoader", commandReloadEventType, eventType);
        AddLoaderTargets(targets, "XRL.World.Parts.LiquidAmmoLoader", commandReloadEventType, eventType);
        AddLoaderTargets(targets, "XRL.World.Parts.ModLiquidCooled", commandReloadEventType, eventType);
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

        DynamicTextObservability.RecordTransform(Context, "LiquidLoader.Queue", message, translated);
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

    private static void AddLoaderTargets(
        List<MethodBase> targets,
        string typeName,
        Type commandReloadEventType,
        Type eventType)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            return;
        }

        AddTarget(targets, targetType, "HandleEvent", new[] { commandReloadEventType });
        AddTarget(targets, targetType, "FireEvent", new[] { eventType });
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

    private static bool TryTranslateQueuedCore(string source, out string translated)
    {
        return TryTranslatePattern(AlreadyFullPattern, source, TranslateAlreadyFull, out translated)
            || TryTranslatePattern(
                NoLiquidForPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "loader")}用の{Restore(match, spans, "liquid")}がない。",
                out translated)
            || TryTranslatePattern(
                DumpPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "loader")}から{Restore(match, spans, "liquid")}を捨てた。",
                out translated)
            || TryTranslatePattern(FillPattern, source, TranslateFill, out translated);
    }

    private static bool TryTranslatePopupCore(string source, out string translated)
    {
        return TryTranslatePattern(
            NoLiquidToSupplyPattern,
            source,
            (match, spans) => $"{Restore(match, spans, "host")}に供給する{Restore(match, spans, "liquid")}がない。",
            out translated)
            || TryTranslatePattern(
                NoRoomPattern,
                source,
                (match, spans) => $"{Restore(match, spans, "host")}にはこれ以上{Restore(match, spans, "liquid")}を入れる余地がない。",
                out translated);
    }

    private static string TranslateAlreadyFull(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var loader = Restore(match, spans, "loader");
        var liquidGroup = match.Groups["liquid"];
        if (liquidGroup.Success)
        {
            return $"{loader}はすでに{ColorAwareTranslationComposer.RestoreCapture(liquidGroup.Value, spans, liquidGroup).Trim()}で満杯だ。";
        }

        return $"{loader}はすでに満杯だ。";
    }

    private static string TranslateFill(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var loader = Restore(match, spans, "loader");
        var liquid = Restore(match, spans, "liquid");
        return match.Groups["partial"].Success
            ? $"{loader}を{liquid}で部分的に満たした。"
            : $"{loader}を{liquid}で満たした。";
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
