using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ITeleporterTranslationPatch
{
    private const string Context = nameof(ITeleporterTranslationPatch);

    private static readonly Regex ProtocolThinWorldThickWorldPattern = new(
        "^((?<subject>.+?) (?:is|are)) encoded with an imprint of the Thin World that has no meaning in the Thick World\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProtocolThinWorldPresentContextPattern = new(
        "^((?<subject>.+?) (?:is|are)) encoded with an imprint of the Thin World that has no meaning in your present context\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProtocolPresentContextPattern = new(
        "^((?<subject>.+?) (?:is|are)) encoded with an imprint that has no meaning in your present context\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RemotePocketDimensionPattern = new(
        "^((?<subject>.+?) (?:is|are)) encoded with the imprint of a remote pocket dimension, (?<plane>.+), that is inaccessible from your present vibrational plane\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RustedActivationButtonPattern = new(
        "^(?<subject>.+?)(?:'s|') activation button is rusted in place\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BrokenPattern = new(
        "^((?<subject>.+?) (?:is|are)) broken\\.\\.\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BootingPattern = new(
        "^((?<subject>.+?) (?:is|are)) still starting up\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PartialChargeShutdownPattern = new(
        "^(?<subject>.+?) hums? for a moment, then powers down\\. .+? (?:doesn't|don't) have enough charge to function\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoChargePattern = new(
        "^(?<subject>.+?) (?:doesn't|don't) have enough charge to function\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [ThreadStatic]
    private static string? directMarkerPassThroughText;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.ITeleporter");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.IEvent");
        if (targetType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "AttemptTeleport", [gameObjectType, eventType]);
        if (method is null)
        {
            Trace.TraceError("QudJP: {0}.AttemptTeleport(GameObject, IEvent) target not found.", Context);
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

        if (DoesVerbRouteTranslator.TryTranslateMarkedMessage(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Context + ".DoesVerb", source, translated);
            return true;
        }

        _ = family;

        var ownerFamily = "Popup.ProducerText." + Context;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        return TryTranslateProtocolThinWorldThickWorld(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateProtocolThinWorldPresentContext(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateProtocolPresentContext(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateRemotePocketDimension(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateRustedActivationButton(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateBroken(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateBooting(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslatePartialChargeShutdown(source, stripped, spans, route, ownerFamily, out translated)
            || TryTranslateNoCharge(source, stripped, spans, route, ownerFamily, out translated);
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

        if (!string.Equals(message, "You activate the recoiler.", StringComparison.Ordinal))
        {
            return false;
        }

        var source = message;
        const string translated = "リコイラーを起動した。";
        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Context + ".ActivateRecoiler",
            source,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    private static bool TryTranslateProtocolThinWorldThickWorld(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ProtocolThinWorldThickWorldPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreSubject(match, spans) + "はThick Worldでは意味を持たないThin Worldの刻印を帯びている。";
        RecordPopup(route, family, "ProtocolThinWorldThickWorld", source, translated);
        return true;
    }

    private static bool TryTranslateProtocolThinWorldPresentContext(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ProtocolThinWorldPresentContextPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreSubject(match, spans) + "は現在のコンテキストでは意味を持たないThin Worldの刻印を帯びている。";
        RecordPopup(route, family, "ProtocolThinWorldPresentContext", source, translated);
        return true;
    }

    private static bool TryTranslateProtocolPresentContext(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = ProtocolPresentContextPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreSubject(match, spans) + "は現在のコンテキストでは意味を持たない刻印を帯びている。";
        RecordPopup(route, family, "ProtocolPresentContext", source, translated);
        return true;
    }

    private static bool TryTranslateRemotePocketDimension(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = RemotePocketDimensionPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = string.Concat(
            RestoreSubject(match, spans),
            "は現在の振動面からは到達できない遠隔ポケット次元",
            RestoreCapture(match, spans, "plane"),
            "の刻印を帯びている。");
        RecordPopup(route, family, "RemotePocketDimension", source, translated);
        return true;
    }

    private static bool TryTranslateRustedActivationButton(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = RustedActivationButtonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreSubject(match, spans) + "の起動ボタンは錆びついて動かない。";
        RecordPopup(route, family, "RustedActivationButton", source, translated);
        return true;
    }

    private static bool TryTranslateBroken(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = BrokenPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreSubject(match, spans) + "は壊れている...";
        RecordPopup(route, family, "Broken", source, translated);
        return true;
    }

    private static bool TryTranslateBooting(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = BootingPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreSubject(match, spans) + "はまだ起動中だ。";
        RecordPopup(route, family, "Booting", source, translated);
        return true;
    }

    private static bool TryTranslatePartialChargeShutdown(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = PartialChargeShutdownPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreSubject(match, spans) + "は一瞬うなったあと停止した。機能するだけのチャージが足りない。";
        RecordPopup(route, family, "PartialChargeShutdown", source, translated);
        return true;
    }

    private static bool TryTranslateNoCharge(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string route,
        string family,
        out string translated)
    {
        var match = NoChargePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = RestoreSubject(match, spans) + "には機能するだけのチャージが足りない。";
        RecordPopup(route, family, "NoCharge", source, translated);
        return true;
    }

    private static string RestoreSubject(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["subject"];
        var cleaned = StringHelpers.StripLeadingEnglishArticle(
            group.Value.Trim(),
            includeCapitalizedDefiniteArticle: true);
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(cleaned, spans, group).Trim();
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
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
