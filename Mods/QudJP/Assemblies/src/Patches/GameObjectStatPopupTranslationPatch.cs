using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class GameObjectStatPopupTranslationPatch
{
    private const string Context = nameof(GameObjectStatPopupTranslationPatch);

    private static readonly string[] TargetMethodNames =
    [
        "GainSP",
        "GainEgo",
        "LoseEgo",
        "GainIntelligence",
        "GainWillpower",
    ];

    private static readonly Regex SkillPointGainPattern =
        new Regex(
            "^You gain (?<value>.+?) skill points!$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StatIncreasePattern =
        new Regex(
            "^Your (?<stat>Ego|Intelligence|Willpower) is increased by (?<value>.+?)!$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StatDecreasePattern =
        new Regex(
            "^Your (?<stat>Ego) is decreased by (?<value>.+?)!$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            return targets;
        }

        foreach (var methodName in TargetMethodNames)
        {
            var method = AccessTools.Method(gameObjectType, methodName, new[] { typeof(int), typeof(bool) });
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.{1}(int, bool) not found.", Context, methodName);
                continue;
            }

            targets.Add(method);
        }

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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateSkillPointGain(source, stripped, spans, route, family, out translated))
        {
            return true;
        }

        if (TryTranslateStatChange(source, stripped, spans, route, family, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateSkillPointGain(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = SkillPointGainPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var value = RestoreValue(match, spans);
        translated = RestoreWholeSourceBoundary(
            string.Concat("スキルポイントを", value, "獲得した！"),
            stripped,
            spans);
        DynamicTextObservability.RecordTransform(route, family + "." + Context + ".SkillPointGain", source, translated);
        return true;
    }

    private static bool TryTranslateStatChange(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = StatIncreasePattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateStatChange(source, stripped, spans, match, "増加した", route, family, "StatIncrease");
            return true;
        }

        match = StatDecreasePattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateStatChange(source, stripped, spans, match, "減少した", route, family, "StatDecrease");
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateStatChange(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Match match,
        string verb,
        string route,
        string family,
        string detail)
    {
        var translated = RestoreWholeSourceBoundary(
            string.Concat(TranslateStatName(match.Groups["stat"].Value), "が", RestoreValue(match, spans), verb, "！"),
            stripped,
            spans);
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
        return translated;
    }

    private static string RestoreValue(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["value"];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreWholeSourceBoundary(
        string translated,
        string stripped,
        IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length);
    }

    private static string TranslateStatName(string source)
    {
        return source switch
        {
            "Ego" => "自我",
            "Intelligence" => "知力",
            "Willpower" => "意志力",
            _ => source,
        };
    }
}
