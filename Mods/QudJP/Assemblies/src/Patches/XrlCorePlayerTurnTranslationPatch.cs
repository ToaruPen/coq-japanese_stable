using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class XrlCorePlayerTurnTranslationPatch
{
    private const string Context = nameof(XrlCorePlayerTurnTranslationPatch);

    private static readonly Regex InvalidInventoryObjectPattern = new(
        "^Invalid inventory object: (?<target>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HpWarningPattern = new(
        "^\\{\\{R\\|Your health has dropped below \\{\\{C\\|(?<value>\\d+)%\\}\\}!\\}\\}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InvalidWaitTurnsPattern = new(
        "^(?<count>-?\\d+) is not a valid number of turns to wait\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AutoattackNonHostilePattern = new(
        "^You do not autoattack (?<target>.+?) because .+ not hostile to you\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FleePathPattern = new(
        "^You can't find a way to flee from (?<target>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReachPathPattern = new(
        "^You can't find a way to reach (?<target>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.Core.XRLCore");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "PlayerTurn", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.PlayerTurn target not found.", Context);
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
        return TryTranslateHpWarning(source, route, ownerFamily, out translated)
            || TryTranslateAutoattackNonHostile(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateFleePath(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateReachPath(source, stripped, spans, route, ownerFamily, out translated);
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

    private static bool TryTranslatePopupTemplate(
        Regex pattern,
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        string detail,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = translate(match, spans);
        DynamicTextObservability.RecordTransform(route, family + "." + detail, source, translated);
        return true;
    }

    private static bool TryTranslateHpWarning(string source, string route, string family, out string translated)
    {
        var match = HpWarningPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = "{{R|HPが{{C|" + match.Groups["value"].Value + "%}}を下回った！}}";
        DynamicTextObservability.RecordTransform(route, family + ".HpWarning", source, translated);
        return true;
    }

    private static bool TryTranslateAutoattackNonHostile(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        return TryTranslatePopupTemplate(
            AutoattackNonHostilePattern,
            source,
            stripped,
            spans,
            route,
            family,
            "AutoattackNonHostile",
            static (match, matchSpans) => RestoreCapture(match, matchSpans, "target") + "は敵対していないため自動攻撃しない。",
            out translated);
    }

    private static bool TryTranslateFleePath(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        return TryTranslatePopupTemplate(
            FleePathPattern,
            source,
            stripped,
            spans,
            route,
            family,
            "FleePath",
            static (match, matchSpans) => RestoreCapture(match, matchSpans, "target") + "から逃げる経路が見つからない。",
            out translated);
    }

    private static bool TryTranslateReachPath(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        return TryTranslatePopupTemplate(
            ReachPathPattern,
            source,
            stripped,
            spans,
            route,
            family,
            "ReachPath",
            static (match, matchSpans) => RestoreCapture(match, matchSpans, "target") + "に到達する経路が見つからない。",
            out translated);
    }

    private static bool TryTranslateQueuedMessage(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        if (TryTranslateInvalidInventoryObject(source, stripped, spans, out translated, out detail)
            || TryTranslateInvalidWaitTurns(source, stripped, out translated, out detail)
            || TryTranslateExactQueue(source, out translated, out detail))
        {
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateInvalidInventoryObject(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = InvalidInventoryObjectPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = "無効なインベントリオブジェクト: " + RestoreCapture(match, spans, "target");
        detail = "InvalidInventoryObject";
        return true;
    }

    private static bool TryTranslateInvalidWaitTurns(string source, string stripped, out string translated, out string detail)
    {
        var match = InvalidWaitTurnsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = match.Groups["count"].Value + "は待機ターン数として無効だ。";
        detail = "InvalidWaitTurns";
        return true;
    }

    private static bool TryTranslateExactQueue(string source, out string translated, out string detail)
    {
        switch (source)
        {
            case "You don't see any hostiles nearby.":
                translated = "付近に敵対者はいない。";
                detail = "NoNearbyHostiles";
                return true;
            case "Set Terse messages":
                translated = "簡潔なメッセージに設定した。";
                detail = "SetTerseMessages";
                return true;
            case "Set Verbose messages":
                translated = "詳細なメッセージに設定した。";
                detail = "SetVerboseMessages";
                return true;
            default:
                translated = source;
                detail = string.Empty;
                return false;
        }
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }
}
