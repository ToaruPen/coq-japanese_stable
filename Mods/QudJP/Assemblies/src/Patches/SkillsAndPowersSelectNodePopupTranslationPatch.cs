using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SkillsAndPowersSelectNodePopupTranslationPatch
{
    private const string Context = nameof(SkillsAndPowersSelectNodePopupTranslationPatch);
    private const string AlreadyHaveDetail = "AlreadyHave";
    private const string InitiationRequiredDetail = "InitiationRequired";
    private const string NotEnoughSkillPointsDetail = "NotEnoughSkillPoints";

    private static readonly Regex AlreadyHavePattern = new(
        "^You already have that (?<kind>skill|power)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InitiationRequiredPattern = new(
        "^You must be initiated into this (?<kind>skill|power) in order to learn it\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NotEnoughSkillPointsPattern = new(
        "^You don't have enough skill points to buy that (?<kind>skill|power)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoImplementationPattern = new(
        "^No implementation for (?<type>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BuyConfirmationPattern = new(
        "^Are you sure you want to buy (?<name>.+?) for (?<cost>.+?)\\s*sp\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.UI.SkillsAndPowersScreen");
        var spNodeType = AccessTools.TypeByName("XRL.UI.SPNode");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        if (targetType is null || spNodeType is null || gameObjectType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            yield break;
        }

        var method = AccessTools.Method(targetType, "SelectNode", [spNodeType, gameObjectType]);
        if (method is not null)
        {
            yield return method;
        }
        else
        {
            Trace.TraceError("QudJP: {0}.SelectNode(SPNode, GameObject) target not found.", Context);
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
        if (TryTranslateKindPattern(AlreadyHavePattern, stripped, spans, source, AlreadyHaveDetail, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateKindPattern(
                InitiationRequiredPattern,
                stripped,
                spans,
                source,
                InitiationRequiredDetail,
                out translated,
                out detail))
        {
            return true;
        }

        if (TryTranslateKindPattern(
                NotEnoughSkillPointsPattern,
                stripped,
                spans,
                source,
                NotEnoughSkillPointsDetail,
                out translated,
                out detail))
        {
            return true;
        }

        var match = NoImplementationPattern.Match(stripped);
        if (match.Success)
        {
            detail = "NoImplementation";
            translated = RestoreWhole(
                ColorAwareTranslationComposer.MarkupAwareRestoreCapture(match.Groups["type"].Value, spans, match.Groups["type"]).Trim()
                + "の実装がない。",
                stripped,
                spans,
                source);
            return true;
        }

        match = BuyConfirmationPattern.Match(stripped);
        if (match.Success)
        {
            detail = "BuyConfirmation";
            var rawName = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
                match.Groups["name"].Value,
                spans,
                match.Groups["name"]).Trim();
            var name = SkillsAndPowersStatusScreenTranslationPatch.TryTranslateExactLeafPreservingColors(
                rawName,
                Context + ".BuyConfirmationName",
                recordTransform: false).translated;
            var cost = match.Groups["cost"].Value.Trim();
            translated = RestoreWhole(name + "を{{C|" + cost + "}}SPで購入しますか？", stripped, spans, source);
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateKindPattern(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        string detailWhenMatched,
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

        detail = detailWhenMatched;
        var kind = TranslateKind(match.Groups["kind"].Value);
        translated = detailWhenMatched switch
        {
            AlreadyHaveDetail => "その" + kind + "はすでに習得している。",
            InitiationRequiredDetail => "この" + kind + "を習得するには入門している必要がある。",
            _ => "その" + kind + "を購入するにはスキルポイントが足りない！",
        };
        translated = RestoreWhole(translated, stripped, spans, source);
        return true;
    }

    private static string TranslateKind(string kind)
    {
        return string.Equals(kind, "power", StringComparison.Ordinal) ? "パワー" : "スキル";
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
