using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class LevelerTranslationPatch
{
    private const string Context = nameof(LevelerTranslationPatch);

    private static readonly Regex BuyMutationPromptPattern = new(
        "^Your genome enters an excited state! Would you like to spend (?<points>.+?) mutation points to buy (?<term>.+?) before rapidly mutating\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RapidAdvancementPattern = new(
        "^You have rapidly advanced (?<mutation>.+?) by (?<amount>.+?) ranks to rank (?<rank>.+?)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoPhysicalMutationPattern = new(
        "^You have no physical (?<term>.+?) to rapidly advance!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var levelerType = AccessTools.TypeByName("XRL.World.Parts.Leveler");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (levelerType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var rapidAdvancement = AccessTools.Method(levelerType, "RapidAdvancement", [typeof(int), gameObjectType]);
        if (rapidAdvancement is not null)
        {
            yield return rapidAdvancement;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.RapidAdvancement(int, GameObject) not found.", Context);
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

        if (!TryTranslate(source, out translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            "Popup.ProducerText." + Context + "." + detail,
            source,
            translated);
        return true;
    }

    private static bool TryTranslate(string source, out string translated, out string detail)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);

        var buyMatch = BuyMutationPromptPattern.Match(stripped);
        if (buyMatch.Success)
        {
            var term = RestoreArticleStrippedCapture(buyMatch, spans, "term");
            translated = RestoreWhole(
                $"ゲノムが励起状態に入った！急速変異する前に{RestoreCapture(buyMatch, spans, "points")}変異ポイントを消費して{term}を購入しますか？",
                spans,
                stripped.Length,
                source);
            detail = "LevelerBuyMutationPrompt";
            return true;
        }

        var advancementMatch = RapidAdvancementPattern.Match(stripped);
        if (advancementMatch.Success)
        {
            translated = RestoreWhole(
                $"{RestoreCapture(advancementMatch, spans, "mutation")}を"
                + $"{RestoreCapture(advancementMatch, spans, "amount")}ランク急速に成長させ、"
                + $"ランク{RestoreCapture(advancementMatch, spans, "rank")}に到達した！",
                spans,
                stripped.Length,
                source);
            detail = "LevelerRapidAdvancement";
            return true;
        }

        var noPhysicalMatch = NoPhysicalMutationPattern.Match(stripped);
        if (noPhysicalMatch.Success)
        {
            translated = RestoreWhole(
                $"急速に成長させられる身体的{RestoreCapture(noPhysicalMatch, spans, "term")}がない！",
                spans,
                stripped.Length,
                source);
            detail = "LevelerNoPhysicalMutations";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static string RestoreWhole(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int strippedLength,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            strippedLength,
            source);
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreArticleStrippedCapture(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string groupName)
    {
        var group = match.Groups[groupName];
        var stripped = StringHelpers.StripLeadingEnglishArticle(group.Value);
        return ColorAwareTranslationComposer.RestoreCapture(stripped, spans, group).Trim();
    }
}
