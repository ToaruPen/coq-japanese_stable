using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LongBladesCoreTranslationPatch
{
    private const string Context = nameof(LongBladesCoreTranslationPatch);

    private static readonly Regex GuardDownPattern = new(
        "^(?<count>\\d+) turns? remains? until your guard is down\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AggressiveLungeBlockedPattern = new(
        "^You can't aggressively lunge through (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LungeInterruptedPattern = new(
        "^(?<actor>.+?)(?:'s|') lunge is interrupted\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PlayerLungePassesThroughPattern = new(
        "^Your lunge passes through (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ActorLungePassesThroughPattern = new(
        "^(?<actor>.+?)(?:'s|') lunge passes through (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AggressiveSwipeObserverPattern = new(
        "^(?<actor>.+?) aggressively\\s*(?:swipes|swipe) .+? blade in the air\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DefensiveSwipeObserverPattern = new(
        "^(?<actor>.+?)\\s*(?:swipes|swipe) .+? blade in the air, pushing .+? foes backward\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.LongBladesCore");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "FireEvent", [eventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.FireEvent(Event) target not found.", Context);
            yield break;
        }

        yield return method;
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
            if (!OwnerTranslationScope.IsActive(activeDepth))
            {
                directMarkerPassThroughText = null;
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
        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (PopupShowTranslationPatch.TryTranslateDirectMarkedOwnerPopup(source, ref directMarkerPassThroughText, out translated))
        {
            return true;
        }

        _ = family;

        var ownerFamily = "Popup.ProducerText." + Context;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        return TryTranslateAggressiveLungeBlocked(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslatePlayerLungePassesThrough(source, stripped, spans, route, ownerFamily, out translated);
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

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(message);
        if (!TryTranslateQueuedMessage(message, stripped, spans, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateAggressiveLungeBlocked(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = AggressiveLungeBlockedPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreCapture(match, spans, "target") + "を通り抜けて攻勢ランジすることはできない。";
        RecordPopup(route, family, "AggressiveLungeBlocked", source, translated);
        return true;
    }

    private static bool TryTranslatePlayerLungePassesThrough(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = PlayerLungePassesThroughPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = "あなたのランジは" + RestoreCapture(match, spans, "target") + "をすり抜けた。";
        RecordPopup(route, family, "PlayerLungePassesThrough", source, translated);
        return true;
    }

    private static bool TryTranslateQueuedMessage(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        if (TryTranslateGuardDown(source, stripped, out translated, out detail)
            || TryTranslateLungeInterrupted(source, stripped, spans, out translated, out detail)
            || TryTranslateActorLungePassesThrough(source, stripped, spans, out translated, out detail)
            || TryTranslateAggressiveSwipePlayer(source, out translated, out detail)
            || TryTranslateAggressiveSwipeObserver(source, stripped, spans, out translated, out detail)
            || TryTranslateDefensiveSwipePlayer(source, out translated, out detail)
            || TryTranslateDefensiveSwipeObserver(source, stripped, spans, out translated, out detail)
            || TryTranslateExactQueue(source, out translated, out detail))
        {
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateGuardDown(string source, string stripped, out string translated, out string detail)
    {
        var match = GuardDownPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = "ガードが下がるまであと" + match.Groups["count"].Value + "ターン。";
        detail = "GuardDownCountdown";
        return true;
    }

    private static bool TryTranslateLungeInterrupted(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = LungeInterruptedPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestorePossessiveOwner(match, spans, "actor") + "のランジは中断された。";
        detail = "ActorLungeInterrupted";
        return true;
    }

    private static bool TryTranslateActorLungePassesThrough(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = ActorLungePassesThroughPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = string.Concat(
            RestorePossessiveOwner(match, spans, "actor"),
            "のランジは",
            RestoreCapture(match, spans, "target"),
            "をすり抜けた。");
        detail = "ActorLungePassesThrough";
        return true;
    }

    private static bool TryTranslateAggressiveSwipePlayer(string source, out string translated, out string detail)
    {
        if (!string.Equals(source, "You aggressively swipe your blade in the air.", StringComparison.Ordinal))
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = "刃を空中で荒々しく振り払った。";
        detail = "AggressiveSwipePlayer";
        return true;
    }

    private static bool TryTranslateAggressiveSwipeObserver(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = AggressiveSwipeObserverPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreCapture(match, spans, "actor") + "は刃を空中で荒々しく振り払った。";
        detail = "AggressiveSwipeObserver";
        return true;
    }

    private static bool TryTranslateDefensiveSwipePlayer(string source, out string translated, out string detail)
    {
        if (!string.Equals(source, "You swipe your blade in the air, pushing your enemies backward.", StringComparison.Ordinal))
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = "刃を空中で薙ぎ払い、敵を後退させた。";
        detail = "DefensiveSwipePlayer";
        return true;
    }

    private static bool TryTranslateDefensiveSwipeObserver(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = DefensiveSwipeObserverPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreCapture(match, spans, "actor") + "は刃を空中で薙ぎ払い、敵を後退させた。";
        detail = "DefensiveSwipeObserver";
        return true;
    }

    private static bool TryTranslateExactQueue(string source, out string translated, out string detail)
    {
        switch (source)
        {
            case "You must be in a long blade stance to use that ability.":
                translated = "ロングブレードの型に入っていないとそのアビリティは使えない。";
                detail = "StanceRequired";
                return true;
            case "{{G|En garde!}}":
                translated = "{{G|構えよ！}}";
                detail = "EnGarde";
                return true;
            default:
                translated = source;
                detail = string.Empty;
                return false;
        }
    }

    private static string RestorePossessiveOwner(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return StripLeadingArticle(RestoreCapture(match, spans, groupName));
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string StripLeadingArticle(string source)
    {
        return StringHelpers.StripLeadingEnglishArticle(source, includeCapitalizedDefiniteArticle: true);
    }

    private static void RecordPopup(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(
            route,
            family + "." + detail,
            source,
            translated);
    }
}
