using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ItemModdingSifrahTranslationPatch
{
    private const string Context = nameof(ItemModdingSifrahTranslationPatch);

    private static readonly Regex FailurePattern = new(
        "^You abjectly failed to mod (?<target>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PartialSuccessPattern = new(
        "^Your work modding (?<target>.+) was passable\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SuccessPattern = new(
        "^Your work modding (?<target>.+) was solid and craftsmanlike\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CriticalSuccessPattern = new(
        "^Your work modding (?<target>.+) was outstanding\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.ItemModdingSifrah");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, gameObjectType, "ResultFailure");
        AddTarget(targets, targetType, gameObjectType, "ResultPartialSuccess");
        AddTarget(targets, targetType, gameObjectType, "ResultSuccess");
        AddTarget(targets, targetType, gameObjectType, "ResultCriticalSuccess");
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

        if (!TryTranslateCore(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, Type gameObjectType, string methodName)
    {
        var method = AccessTools.Method(targetType, methodName, new[] { gameObjectType });
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        return TryTranslatePattern(FailurePattern, source, target => target + "の改造に完全に失敗した。", out translated)
            || TryTranslatePattern(PartialSuccessPattern, source, target => target + "の改造作業はまずまずだった。", out translated)
            || TryTranslatePattern(SuccessPattern, source, target => target + "の改造作業は堅実で職人らしい仕上がりだった。", out translated)
            || TryTranslatePattern(CriticalSuccessPattern, source, target => target + "の改造作業は見事だった。", out translated);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<string, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var group = match.Groups["target"];
        var target = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(target),
            spans,
            stripped.Length,
            source);
        return true;
    }
}
