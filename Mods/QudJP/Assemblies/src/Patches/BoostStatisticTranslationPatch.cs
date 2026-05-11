using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class BoostStatisticTranslationPatch
{
    private const string Context = nameof(BoostStatisticTranslationPatch);

    private static readonly Regex ChangePattern = new(
        "^Your (?<stat>.+?) (?<verb>increases|decreases)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RestorePattern = new(
        "^Your (?<stat>.+?) returns to normal\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> StatisticNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Strength"] = "筋力",
        ["Agility"] = "敏捷",
        ["Toughness"] = "頑健",
        ["Intelligence"] = "知性",
        ["Willpower"] = "意志力",
        ["Ego"] = "自我",
    };

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.BoostStatistic");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "Apply", new[] { gameObjectType });
        AddTarget(targets, targetType, "Remove", new[] { gameObjectType });
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

        if (!TryTranslateCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform("MessageQueue.AddPlayerMessage", Context, message, translated);
        message = translated;
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

    private static bool TryTranslateCore(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        var changeMatch = ChangePattern.Match(stripped);
        if (changeMatch.Success)
        {
            var suffix = string.Equals(changeMatch.Groups["verb"].Value, "increases", StringComparison.Ordinal)
                ? "上昇した。"
                : "低下した。";
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                TranslateStatistic(changeMatch, spans, "stat") + "が" + suffix,
                spans,
                stripped.Length,
                source);
            return true;
        }

        var restoreMatch = RestorePattern.Match(stripped);
        if (restoreMatch.Success)
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                TranslateStatistic(restoreMatch, spans, "stat") + "が通常に戻った。",
                spans,
                stripped.Length,
                source);
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateStatistic(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var visible = group.Value.Trim();
        var translated = StatisticNames.TryGetValue(visible, out var mapped) ? mapped : visible;
        return ColorAwareTranslationComposer.RestoreCapture(translated, spans, group).Trim();
    }
}
