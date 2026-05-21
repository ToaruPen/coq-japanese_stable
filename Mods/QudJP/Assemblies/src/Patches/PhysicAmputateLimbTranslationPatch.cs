using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class PhysicAmputateLimbTranslationPatch
{
    private const string Context = nameof(PhysicAmputateLimbTranslationPatch);

    private static readonly Regex NoLimbsPattern = new(
        "^(?<subject>.+?) (?:has|have) no limbs\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoAmputatableLimbsPattern = new(
        "^(?<subject>.+?) (?:has|have) no limbs that can be amputated\\.?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotReachPattern = new(
        "^You cannot reach (?<target>.+?) to amputate (?<pronoun>.+?) limb\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RefusesPattern = new(
        "^(?<target>.+?) won't let you do that\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotAmputatePattern = new(
        "^You cannot amputate (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotBringSelfPattern = new(
        "^You cannot bring yourself to amputate your (?<limb>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CannotAmputateHoldingPattern = new(
        "^You cannot amputate the (?<limb>.+?) holding (?<weapon>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoReasonPattern = new(
        "^(?<target>.+?) (?:sees|see) no reason for you to amputate (?<pronoun>his|her|its|their) (?<limb>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PossessiveBodyPartPattern = new(
        "^(?<owner>.+?)(?:'s|') (?<part>right hand|left hand|right foot|left foot|right arm|left arm|hand|foot|head|face|arm|leg|tail|wing|horn|limbs)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PronounBodyPartPattern = new(
        "^(?<owner>your|his|her|its|their) (?<part>right hand|left hand|right foot|left foot|right arm|left arm|hand|foot|head|face|arm|leg|tail|wing|horn|limbs)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Skill.Physic_AmputateLimb");
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
            yield break;
        }

        Trace.TraceError("QudJP: {0}.FireEvent(Event) target not found.", Context);
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

        if (TryTranslateExactFailure(source, out translated, out var detail))
        {
            Record(route, family, detail, source, translated);
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateNoLimbs(source, stripped, spans, out translated, out detail)
            || TryTranslateCannotReach(source, stripped, spans, out translated, out detail)
            || TryTranslateRefuses(source, stripped, spans, out translated, out detail)
            || TryTranslateCannotBringSelf(source, stripped, spans, out translated, out detail)
            || TryTranslateCannotAmputateHolding(source, stripped, spans, out translated, out detail)
            || TryTranslateNoReason(source, stripped, spans, out translated, out detail)
            || TryTranslateCannotAmputate(source, stripped, spans, out translated, out detail))
        {
            Record(route, family, detail, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateExactFailure(string source, out string translated, out string detail)
    {
        translated = source;
        detail = string.Empty;

        switch (source)
        {
            case "You can't perform field amputations with hostiles nearby!":
                translated = "敵対者が近くにいると野外切断は行えない！";
                detail = "HostilesNearby";
                return true;
            case "You must have an axe or a weapon capable of dismemberment equipped in order to perform a field amputation.":
                translated = "野外切断を行うには、斧か切断可能な武器を装備していなければならない。";
                detail = "NeedDismemberingWeapon";
                return true;
            case "There is no one there for you to amputate their limb.":
                translated = "そこには四肢を切断できる相手がいない。";
                detail = "NoTarget";
                return true;
            default:
                return false;
        }
    }

    private static bool TryTranslateNoLimbs(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = NoAmputatableLimbsPattern.Match(stripped);
        if (match.Success)
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                $"{NormalizeSubject(RestoreCapture(match, spans, "subject"))}には切断できる四肢がない",
                spans,
                stripped.Length,
                source);
            detail = "NoAmputatableLimbs";
            return true;
        }

        match = NoLimbsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            $"{NormalizeSubject(RestoreCapture(match, spans, "subject"))}には四肢がない",
            spans,
            stripped.Length,
            source);
        detail = "NoLimbs";
        return true;
    }

    private static bool TryTranslateCannotReach(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = CannotReachPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreWhole(
            RestoreDisplayNameCapture(match, spans, "target") + "に手が届かず、四肢を切断できない。",
            stripped,
            spans,
            source);
        detail = "CannotReach";
        return true;
    }

    private static bool TryTranslateRefuses(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = RefusesPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreWhole(RestoreDisplayNameCapture(match, spans, "target") + "はそれを許さない。", stripped, spans, source);
        detail = "Refuses";
        return true;
    }

    private static bool TryTranslateCannotAmputate(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = CannotAmputatePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreWhole(RestoreBodyPartCapture(match, spans, "target") + "は切断できない。", stripped, spans, source);
        detail = "CannotAmputate";
        return true;
    }

    private static bool TryTranslateCannotBringSelf(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = CannotBringSelfPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreWhole(
            "自分の" + RestoreBodyPartCapture(match, spans, "limb") + "を切断する気にはなれない。",
            stripped,
            spans,
            source);
        detail = "CannotBringSelf";
        return true;
    }

    private static bool TryTranslateCannotAmputateHolding(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = CannotAmputateHoldingPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreWhole(
            RestoreDisplayNameCapture(match, spans, "weapon")
            + "を持っている"
            + RestoreBodyPartCapture(match, spans, "limb")
            + "は切断できない。",
            stripped,
            spans,
            source);
        detail = "CannotAmputateHolding";
        return true;
    }

    private static bool TryTranslateNoReason(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = NoReasonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = RestoreWhole(
            RestoreDisplayNameCapture(match, spans, "target")
            + "はあなたが"
            + RestoreBodyPartCapture(match, spans, "limb")
            + "を切断する理由がないと考えている。",
            stripped,
            spans,
            source);
        detail = "NoReason";
        return true;
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string RestoreDisplayNameCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        return DisplayNameCaptureTranslator.TranslatePreservingColors(RestoreCapture(match, spans, groupName), Context);
    }

    private static string RestoreBodyPartCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        return ColorAwareTranslationComposer.TranslatePreservingColors(restored, TranslateBodyPartPhrase);
    }

    private static string TranslateBodyPartPhrase(string source)
    {
        var pronounMatch = PronounBodyPartPattern.Match(source);
        if (pronounMatch.Success)
        {
            return TranslateOwnerPronoun(pronounMatch.Groups["owner"].Value)
                + TranslateBodyPartNameOrOriginal(pronounMatch.Groups["part"].Value);
        }

        var possessiveMatch = PossessiveBodyPartPattern.Match(source);
        if (possessiveMatch.Success)
        {
            var owner = DisplayNameCaptureTranslator.TranslatePreservingColors(
                possessiveMatch.Groups["owner"].Value.Trim(),
                Context);
            return owner + "の" + TranslateBodyPartNameOrOriginal(possessiveMatch.Groups["part"].Value);
        }

        return TranslateBodyPartNameOrOriginal(source);
    }

    private static string TranslateOwnerPronoun(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "YOUR" => "あなたの",
            "HIS" => "彼の",
            "HER" => "彼女の",
            "ITS" => "その",
            "THEIR" => "それらの",
            _ => source + " ",
        };
    }

    private static string TranslateBodyPartNameOrOriginal(string source)
    {
        return source switch
        {
            "right hand" => "右手",
            "left hand" => "左手",
            "right foot" => "右足",
            "left foot" => "左足",
            "right arm" => "右腕",
            "left arm" => "左腕",
            "hand" => "手",
            "foot" => "足",
            "head" => "頭",
            "face" => "顔",
            "arm" => "腕",
            "leg" => "脚",
            "tail" => "尾",
            "wing" => "翼",
            "horn" => "角",
            "limbs" => "四肢",
            _ => source,
        };
    }

    private static string NormalizeSubject(string value)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(value);
        var withoutArticle = StringHelpers.StripLeadingEnglishArticle(visible, includeCapitalizedDefiniteArticle: true);
        if (IsSecondPersonSubject(visible) || IsSecondPersonSubject(withoutArticle))
        {
            return ColorAwareTranslationComposer.TranslatePreservingColors(value, _ => "あなた");
        }

        var replacement = DisplayNameCaptureTranslator.TranslatePreservingColors(value, Context);
        return string.Equals(replacement, value, StringComparison.Ordinal)
            ? value
            : replacement;
    }

    private static bool IsSecondPersonSubject(string value)
    {
        return string.Equals(value, "You", StringComparison.OrdinalIgnoreCase);
    }

    private static string RestoreWhole(string translated, string stripped, IReadOnlyList<ColorSpan> spans, string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
    }

    private static void Record(string route, string family, string detail, string source, string translated)
    {
        DynamicTextObservability.RecordTransform(route, family + "." + Context + "." + detail, source, translated);
    }
}
