using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class AutomatedExternalDefibrillatorTranslationPatch
{
    private const string Context = nameof(AutomatedExternalDefibrillatorTranslationPatch);

    private static readonly Regex NoSkillPattern = new(
        "^You don't know how to use (?<device>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StatusPattern = new(
        "^(?<device>.+?) (?:is|are) (?<status>disabled by electromagnetic pulse|unpowered|unfueled|disabled by fuel contamination|switched off|still warming up|nonfunctional)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoTargetPattern = new(
        "^There is no one there to use (?<device>.+?) on\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoUsableTargetPattern = new(
        "^There is no one there you can use (?<device>.+?) on\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TargetConfirmationPattern = new(
        "^(?<target>.+?) (?:is|are) not in cardiac arrest\\. Do you want to use (?<device>.+?) on (?<pronoun>.+?) anyway\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SelfConfirmationPattern = new(
        "^You are not in cardiac arrest\\. Do you want to use (?<device>.+?) on (?<pronoun>.+?) anyway\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.AutomatedExternalDefibrillator");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (targetType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target types.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "AttemptDefibrillate", new[] { gameObjectType, eventType });
        if (method is not null)
        {
            yield return method;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.AttemptDefibrillate(GameObject,IEvent) target not found.", Context);
        }
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

        if (!TryTranslateMessage(message, nameof(CombatAndLogMessageQueuePatch), "MessageQueue", out var translated))
        {
            return false;
        }

        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        return TryTranslateMessage(source, route, family, out translated);
    }

    private static bool TryTranslateMessage(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (!OwnerTranslationScope.IsActive(activeDepth))
        {
            translated = source;
            return false;
        }

        if (!TryTranslateCore(source, out translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
        return true;
    }

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        return TryTranslatePattern(
            NoSkillPattern,
            source,
            (match, spans) => $"あなたは{RestoreDisplayName(match, spans, "device")}の使い方を知らない。",
            "Defibrillator.NoSkill",
            out translated,
            out detail)
            || TryTranslatePattern(
                StatusPattern,
                source,
                (match, spans) =>
                    $"{RestoreDisplayName(match, spans, "device")}は{TranslateStatus(Restore(match, spans, "status"))}。",
                "Defibrillator.Status",
                out translated,
                out detail)
            || TryTranslatePattern(
                NoTargetPattern,
                source,
                (match, spans) => $"そこには{RestoreDisplayName(match, spans, "device")}を使う相手がいない。",
                "Defibrillator.NoTarget",
                out translated,
                out detail)
            || TryTranslatePattern(
                NoUsableTargetPattern,
                source,
                (match, spans) => $"そこには{RestoreDisplayName(match, spans, "device")}を使える相手がいない。",
                "Defibrillator.NoUsableTarget",
                out translated,
                out detail)
            || TryTranslatePattern(
                SelfConfirmationPattern,
                source,
                (match, spans) =>
                    $"あなたは心停止状態ではない。それでも{RestoreDisplayName(match, spans, "device")}を{TranslateSelfPronoun(Restore(match, spans, "pronoun"))}に使いますか？",
                "Defibrillator.SelfConfirm",
                out translated,
                out detail)
            || TryTranslatePattern(
                TargetConfirmationPattern,
                source,
                (match, spans) =>
                    $"{RestoreDisplayName(match, spans, "target")}は心停止状態ではない。それでも{RestoreDisplayName(match, spans, "device")}を使いますか？",
                "Defibrillator.TargetConfirm",
                out translated,
                out detail);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        string patternDetail,
        out string translated,
        out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        detail = patternDetail;
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreDisplayName(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return DisplayNameCaptureTranslator.TranslatePreservingColors(Restore(match, spans, groupName), Context);
    }

    private static string TranslateStatus(string source)
    {
        return ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateStatusVisible);
    }

    private static string TranslateStatusVisible(string source)
    {
        return source switch
        {
            "disabled by electromagnetic pulse" => "EMPで無力化されている",
            "unpowered" => "無電力だ",
            "unfueled" => "燃料切れだ",
            "disabled by fuel contamination" => "燃料汚染で機能停止している",
            "switched off" => "電源が切れている",
            "still warming up" => "まだ起動準備中だ",
            "nonfunctional" => "機能していない",
            _ => source,
        };
    }

    private static string TranslateSelfPronoun(string source)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        return string.Equals(stripped, "yourself", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stripped, "itself", StringComparison.OrdinalIgnoreCase)
                ? ColorAwareTranslationComposer.Restore("自分自身", spans)
                : source;
    }
}
