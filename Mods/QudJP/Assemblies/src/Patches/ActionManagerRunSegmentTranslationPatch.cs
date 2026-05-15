using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ActionManagerRunSegmentTranslationPatch
{
    private const string Context = nameof(ActionManagerRunSegmentTranslationPatch);

    private static readonly Regex PathToTargetPattern = new(
        "^You cannot find a path to (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PathTowardDirectionPattern = new(
        "^You cannot find a path toward the (?<direction>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoNearbyPattern = new(
        "^There are no (?<target>.+?) nearby\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AutoAttackNotHostilePattern = new(
        "^You will not auto-attack (?<target>.+?) because .+? not hostile to you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NavigateToTargetPattern = new(
        "^You can't find a way to navigate to (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex UnableToAttackPattern = new(
        "^You are unable to attack (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReachTargetPattern = new(
        "^You can't seem to find a way to reach (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.Core.ActionManager");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "RunSegment", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.RunSegment() target not found.", Context);
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

        var ownerFamily = family + "." + Context;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        return TryTranslatePopupPathToTarget(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslatePopupPathTowardDirection(source, stripped, route, ownerFamily, out translated)
            || TryTranslatePopupNoNearby(source, stripped, spans, route, ownerFamily, out translated);
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

    private static bool TryTranslatePopupPathToTarget(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = PathToTargetPattern.Match(stripped);
        if (!match.Success || string.Equals(match.Groups["target"].Value, "your destination", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = RestoreTarget(match, spans, "target") + "への経路が見つからない。";
        RecordPopup(route, family, "PathToTarget", source, translated);
        return true;
    }

    private static bool TryTranslatePopupPathTowardDirection(
        string source,
        string stripped,
        string route,
        string family,
        out string translated)
    {
        var match = PathTowardDirectionPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = TranslateDirection(match.Groups["direction"].Value) + "への経路が見つからない。";
        RecordPopup(route, family, "PathTowardDirection", source, translated);
        return true;
    }

    private static bool TryTranslatePopupNoNearby(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = NoNearbyPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = "近くに" + TranslateNearbyTarget(match, spans) + "はない。";
        RecordPopup(route, family, "NoNearby", source, translated);
        return true;
    }

    private static bool TryTranslateQueuedMessage(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        if (string.Equals(stripped, "You can't figure out how to safely reach the stairs from here.", StringComparison.Ordinal))
        {
            translated = "ここから階段へ安全に辿る経路が見つからない。";
            detail = "SafeReachStairs";
            return true;
        }

        if (string.Equals(stripped, "You cannot see your target.", StringComparison.Ordinal))
        {
            translated = "目標が見えない。";
            detail = "CannotSeeTarget";
            return true;
        }

        if (TryTranslateQueuedTargetMessage(source, stripped, spans, AutoAttackNotHostilePattern, "は敵対していないので自動攻撃しない。", "AutoAttackNotHostile", out translated, out detail)
            || TryTranslateQueuedTargetMessage(source, stripped, spans, NavigateToTargetPattern, "への移動経路が見つからない。", "NavigateToTarget", out translated, out detail)
            || TryTranslateQueuedTargetMessage(source, stripped, spans, UnableToAttackPattern, "を攻撃できない。", "UnableToAttack", out translated, out detail)
            || TryTranslateQueuedTargetMessage(source, stripped, spans, ReachTargetPattern, "へ到達する経路が見つからない。", "ReachTarget", out translated, out detail))
        {
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateQueuedTargetMessage(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Regex pattern,
        string suffix,
        string detailName,
        out string translated,
        out string detail)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreTarget(match, spans, "target") + suffix;
        detail = detailName;
        return true;
    }

    private static void RecordPopup(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + detail, source, translated);
    }

    private static string TranslateNearbyTarget(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var raw = match.Groups["target"].Value.Trim();
        return raw switch
        {
            "stairways" => "階段",
            "stairways leading upward" => "上り階段",
            "stairways leading downward" => "下り階段",
            _ => RestoreTarget(match, spans, "target"),
        };
    }

    private static string TranslateDirection(string direction)
    {
        return direction.Trim() switch
        {
            "north" => "北",
            "south" => "南",
            "east" => "東",
            "west" => "西",
            "northeast" => "北東",
            "northwest" => "北西",
            "southeast" => "南東",
            "southwest" => "南西",
            var value => value,
        };
    }

    private static string RestoreTarget(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        return StripLeadingEnglishArticlePreservingColors(restored);
    }

    private static string StripLeadingEnglishArticlePreservingColors(string source)
    {
        var direct = StringHelpers.StripLeadingEnglishArticle(source, includeCapitalizedDefiniteArticle: true);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        var withoutArticle = StringHelpers.StripLeadingEnglishArticle(
            visible,
            includeCapitalizedDefiniteArticle: true);
        if (string.Equals(withoutArticle, visible, StringComparison.Ordinal))
        {
            return source;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => withoutArticle);
    }
}
