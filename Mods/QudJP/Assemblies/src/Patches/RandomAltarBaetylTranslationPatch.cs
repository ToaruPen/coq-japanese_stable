using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RandomAltarBaetylTranslationPatch
{
    private const string Context = nameof(RandomAltarBaetylTranslationPatch);

    private static readonly Regex RewardPopupPattern = new(
        "^I ACCEPT YOUR OFFERING!\\n\\nThe sparking baetyl gives you (?<reward>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var baetylType = AccessTools.TypeByName("XRL.World.Parts.RandomAltarBaetyl");
        if (baetylType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        var wantsSacrifice = AccessTools.Method(baetylType, "BaetylWantsSacrifice", []);
        if (wantsSacrifice is not null)
        {
            yield return wantsSacrifice;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.BaetylWantsSacrifice() not found.", Context);
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
        var match = RewardPopupPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"捧げ物を受け取った！\n\nsparking baetylは{RestoreReward(match, spans)}を授けた！",
            spans,
            stripped.Length,
            source);
        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + ".RandomAltarBaetylRewardPopup",
            source,
            translated);
        return true;
    }

    private static string RestoreReward(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var reward = match.Groups["reward"];
        return ColorAwareTranslationComposer.RestoreCapture(reward.Value, spans, reward).Trim();
    }
}
