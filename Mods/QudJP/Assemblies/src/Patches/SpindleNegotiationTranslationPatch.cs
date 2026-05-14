using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SpindleNegotiationTranslationPatch
{
    private const string Context = nameof(SpindleNegotiationTranslationPatch);

    private static readonly Regex DelegateGratitudePattern = new(
        "^The delegate for (?<faction>.+?) says, 'Live and drink, (?<address>.+?)\\. We won't forget this\\.'$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DelegateGivesHeirloomPattern = new(
        "^The delegate for (?<faction>.+?) gives you (?<heirloom>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DelegateBetrayedPattern = new(
        "^The delegate for (?<faction>.+?) says, 'Betrayer! May you choke on your own spittle! We won't forget this\\.'$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ChaosSpielAccusationPattern = new(
        "^You yell, 'I cannot believe (?<subject>.+?) don't despise (?<target>.+?) for (?<reason>.+?)\\.'$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ChaosSpielOpinionChangedPattern = new(
        "^Due to your revelation, (?<subject>.+?) change their opinion of (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CouncilConvenesPattern = new(
        "^The council will be convened! Come back in (?<days>\\d+) (?<unit>day|days)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.SpindleNegotiation");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "FireEvent", [eventType]);
        if (method is not null)
        {
            yield return method;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.FireEvent(Event) target not found.", Context);
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
        _ = family;

        if (!OwnerTranslationScope.IsActive(activeDepth) || string.IsNullOrEmpty(source))
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
        if (!TryTranslateStripped(stripped, spans, source, out translated, out var detail))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
        return true;
    }

    private static bool TryTranslateStripped(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated,
        out string detail)
    {
        var match = DelegateGratitudePattern.Match(stripped);
        if (match.Success)
        {
            detail = "DelegateGratitude";
            translated = RestoreWhole(
                Restore(match, spans, "faction") + "の代表は言う。「生きて水を飲め、"
                + Restore(match, spans, "address") + "。私たちはこのことを忘れない。」",
                stripped,
                spans,
                source);
            return true;
        }

        match = DelegateGivesHeirloomPattern.Match(stripped);
        if (match.Success)
        {
            detail = "DelegateGivesHeirloom";
            translated = RestoreWhole(
                Restore(match, spans, "faction") + "の代表はあなたに" + Restore(match, spans, "heirloom") + "をくれた！",
                stripped,
                spans,
                source);
            return true;
        }

        match = DelegateBetrayedPattern.Match(stripped);
        if (match.Success)
        {
            detail = "DelegateBetrayed";
            translated = RestoreWhole(
                Restore(match, spans, "faction")
                + "の代表は言う。「裏切り者め！自分の唾で窒息するがいい！私たちはこのことを忘れない。」",
                stripped,
                spans,
                source);
            return true;
        }

        match = ChaosSpielAccusationPattern.Match(stripped);
        if (match.Success)
        {
            detail = "ChaosSpielAccusation";
            translated = RestoreWhole(
                "あなたは叫ぶ。「" + Restore(match, spans, "subject")
                + "が" + Restore(match, spans, "reason")
                + "のことで" + Restore(match, spans, "target")
                + "を軽蔑していないなんて信じられない。」",
                stripped,
                spans,
                source);
            return true;
        }

        match = ChaosSpielOpinionChangedPattern.Match(stripped);
        if (match.Success)
        {
            detail = "ChaosSpielOpinionChanged";
            translated = RestoreWhole(
                "あなたの暴露により、" + Restore(match, spans, "subject")
                + "は" + Restore(match, spans, "target") + "への評価を変えた。",
                stripped,
                spans,
                source);
            return true;
        }

        match = CouncilConvenesPattern.Match(stripped);
        if (match.Success)
        {
            detail = "CouncilConvenes";
            translated = RestoreWhole(
                "評議会は招集される！" + match.Groups["days"].Value + "日後に戻ってこい。",
                stripped,
                spans,
                source);
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreWhole(
        string translated,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }
}
