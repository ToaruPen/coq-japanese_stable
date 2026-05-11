using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class HackingSifrahResultTranslationPatch
{
    private const string Context = nameof(HackingSifrahResultTranslationPatch);

    private static readonly Regex HackSuccessPattern = new Regex(
        "^You hack (.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FindBitsPattern = new Regex(
        "^You hack (.+) and find tinkering bits <\\{\\{\\|(.+)\\}\\}> in (.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FindStuckItemPattern = new Regex(
        "^You hack (.+) and find (.+) stuck in (.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HackingProgressOpenPattern = new Regex(
        "^You feel like you're making progress on hacking (.+) open\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HackingProgressPattern = new Regex(
        "^You feel like you're making progress on hacking (.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HackingFailurePattern = new Regex(
        "^You cannot seem to work out how to hack (.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HackingCriticalFailurePattern = new Regex(
        "^Your attempt to hack (.+) has gone very wrong\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PowerConsumptionPattern = new Regex(
        "^You hack (.+), and find a way to reduce (.+) power consumption in the process!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LicensePointPattern = new Regex(
        "^In the course of the hack, you are able to insert instructions into (.+) granting you an extra (.+)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AlertLightsPattern = new Regex(
        "^The hack fails, and alert lights on (.+) begin pulsing (rhythmically|urgently)\\.\\.\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        AddTargetsForType(targets, "XRL.World.Parts.Door",
            "HackingResultSuccess",
            "HackingResultExceptionalSuccess",
            "HackingResultPartialSuccess",
            "HackingResultFailure",
            "HackingResultCriticalFailure");
        AddTargetsForType(targets, "XRL.World.Parts.PowerSwitch",
            "HackingResultSuccess",
            "HackingResultExceptionalSuccess",
            "HackingResultPartialSuccess",
            "HackingResultFailure",
            "HackingResultCriticalFailure");
        AddTargetsForType(targets, "XRL.World.Parts.TemplarPhylactery",
            "HackingResultSuccess",
            "HackingResultExceptionalSuccess",
            "HackingResultPartialSuccess",
            "HackingResultFailure",
            "HackingResultCriticalFailure");
        AddTargetsForType(targets, "XRL.World.Parts.CyberneticsTerminal2",
            "HackingResultExceptionalSuccess",
            "HackingResultFailure",
            "HackingResultCriticalFailure");
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
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static void AddTargetsForType(List<MethodBase> targets, string typeName, params string[] methodNames)
    {
        var targetType = AccessTools.TypeByName(typeName);
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var hackingSifrahType = AccessTools.TypeByName("XRL.World.HackingSifrah");
        if (targetType is null || gameObjectType is null || hackingSifrahType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found for {1}.", Context, typeName);
            return;
        }

        var parameters = new[] { gameObjectType, gameObjectType, hackingSifrahType };
        for (var index = 0; index < methodNames.Length; index++)
        {
            var method = AccessTools.Method(targetType, methodNames[index], parameters);
            if (method is not null)
            {
                targets.Add(method);
                continue;
            }

            Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, typeName, methodNames[index]);
        }
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        return TryTranslateMatch(HackSuccessPattern, source, m => $"{m.Groups[1].Value}をハックした。", out translated)
            || TryTranslateMatch(FindBitsPattern, source, TranslateFindBits, out translated)
            || TryTranslateMatch(FindStuckItemPattern, source, TranslateFindStuckItem, out translated)
            || TryTranslateMatch(
                HackingProgressOpenPattern,
                source,
                m => $"{m.Groups[1].Value}を開くハックが進んでいる気がする。",
                out translated)
            || TryTranslateMatch(
                HackingProgressPattern,
                source,
                m => $"{m.Groups[1].Value}のハックが進んでいる気がする。",
                out translated)
            || TryTranslateMatch(
                HackingFailurePattern,
                source,
                m => $"{m.Groups[1].Value}をハックする方法がわからない。",
                out translated)
            || TryTranslateMatch(
                HackingCriticalFailurePattern,
                source,
                m => $"{m.Groups[1].Value}のハックはひどく失敗した。",
                out translated)
            || TryTranslateMatch(
                PowerConsumptionPattern,
                source,
                m => $"{m.Groups[1].Value}をハックし、その過程で{m.Groups[2].Value}の電力消費を減らす方法を見つけた！",
                out translated)
            || TryTranslateMatch(
                LicensePointPattern,
                source,
                m => $"ハックの過程で{m.Groups[1].Value}に命令を挿入し、追加の{m.Groups[2].Value}を得た！",
                out translated)
            || TryTranslateMatch(AlertLightsPattern, source, TranslateAlertLights, out translated);
    }

    private static bool TryTranslateMatch(Regex pattern, string source, Func<Match, string> translate, out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = translate(match);
        return true;
    }

    private static string TranslateFindBits(Match match)
    {
        return $"{match.Groups[1].Value}をハックし、{match.Groups[3].Value}の中に修理ビット<{{{{|{match.Groups[2].Value}}}}}>を見つけた！";
    }

    private static string TranslateFindStuckItem(Match match)
    {
        return $"{match.Groups[1].Value}をハックし、{match.Groups[3].Value}の中に挟まっている{match.Groups[2].Value}を見つけた！";
    }

    private static string TranslateAlertLights(Match match)
    {
        var adverb = string.Equals(match.Groups[2].Value, "urgently", StringComparison.Ordinal)
            ? "緊急に"
            : "規則的に";
        return $"ハックは失敗し、{match.Groups[1].Value}の警告灯が{adverb}点滅し始めた...";
    }
}
