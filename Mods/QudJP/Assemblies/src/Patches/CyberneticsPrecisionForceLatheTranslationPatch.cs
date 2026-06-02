using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CyberneticsPrecisionForceLatheTranslationPatch
{
    private const string Context = nameof(CyberneticsPrecisionForceLatheTranslationPatch);

    private static readonly Regex NoHoldSlotPattern = new(
        "^You have no place available to hold (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StatusFailurePattern = new(
        "^(?:The |the |[Aa]n? )?(?<subject>.+?) (?:is|are) (?<status>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.CyberneticsPrecisionForceLathe");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (targetType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} failed to resolve target types.", Context);
            yield break;
        }

        var method = AccessTools.Method(
            targetType,
            "ActivatePrecisionForceLathe",
            [gameObjectType, gameObjectType, eventType]);
        if (method is not null)
        {
            yield return method;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.ActivatePrecisionForceLathe target not found.", Context);
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

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        return TryTranslateMessage(source, route, family + "." + Context, stripDirectMarker: true, out translated);
    }

    internal static bool TryTranslateQueuedMessage(ref string message, string? color)
    {
        _ = color;

        if (!TryTranslateMessage(
                message,
                nameof(CombatAndLogMessageQueuePatch),
                "MessageQueue." + Context,
                stripDirectMarker: false,
                out var translated))
        {
            return false;
        }

        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateMessage(
        string source,
        string route,
        string family,
        bool stripDirectMarker,
        out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return stripDirectMarker;
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

        DynamicTextObservability.RecordTransform(route, family + "." + detail, source, translated);
        return true;
    }

    private static bool TryTranslateCore(string source, out string translated, out string detail)
    {
        return TryTranslatePattern(
            NoHoldSlotPattern,
            source,
            (match, spans) =>
            {
                var item = Restore(match, spans, "item");
                var (strippedItem, _) = ColorAwareTranslationComposer.Strip(item);
                return string.Equals(
                        strippedItem,
                        "the result",
                        StringComparison.Ordinal)
                    ? "結果を保持できる空き部位がない。"
                    : $"{TranslateDisplayName(item)}を保持できる空き部位がない。";
            },
            "NoHoldSlot",
                out translated,
                out detail)
            || TryTranslateStatusFailure(source, out translated, out detail);
    }

    private static bool TryTranslateStatusFailure(string source, out string translated, out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = StatusFailurePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var status = Restore(match, spans, "status");
        var translatedStatus = TranslateStatusPhrase(status);
        if (string.Equals(translatedStatus, status, StringComparison.Ordinal))
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{TranslateDisplayName(Restore(match, spans, "subject"))}は{translatedStatus}。",
            spans,
            stripped.Length,
            source);
        detail = "StatusFailure";
        return true;
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

    private static string TranslateDisplayName(string source)
    {
        return DisplayNameCaptureTranslator.TranslatePreservingColors(source, Context);
    }

    private static string TranslateStatusPhrase(string source)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var translated = stripped switch
        {
            "disabled by electromagnetic pulse" => "電磁パルスで無効化されている",
            "unpowered" => "電源が入っていない",
            "unfueled" => "燃料がない",
            "disabled by fuel contamination" => "燃料汚染で無効化されている",
            "switched off" => "スイッチが切れている",
            "still warming up" => "まだウォームアップ中",
            "nonfunctional" => "機能していない",
            _ => source,
        };

        return string.Equals(translated, source, StringComparison.Ordinal)
            ? source
            : ColorAwareTranslationComposer.Restore(translated, spans);
    }
}
