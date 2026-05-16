using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class CampfireNostrumsTranslationPatch
{
    private const string Context = nameof(CampfireNostrumsTranslationPatch);

    private static readonly Regex StaunchPassThroughPattern = new(
        "^You try to staunch the wounds of (?<target>.+?), but your limbs pass through .+\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StaunchCannotAffectPattern = new(
        "^You try to staunch the wounds of (?<target>.+?), but cannot affect .+\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StaunchPartialPattern = new(
        "^You staunch the wounds of (?<target>.+?), though some are too deep to treat\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StaunchFullPattern = new(
        "^You staunch the wounds of (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WoundsTooDeepPattern = new(
        "^(?<target>.+?) are too deep to treat\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NeitherBleedingPattern = new(
        "^Neither you nor (?<target>.+?) are bleeding\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NotBleedingPattern = new(
        "^You are not bleeding\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NoMedicinalIngredientsPattern = new(
        "^You have no medicinal ingredients with which to treat the poison coursing through (?<target>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PoisonPassThroughPattern = new(
        "^You try to cure the poison coursing through (?<target>.+?), but your limbs pass through .+\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PoisonCannotAffectPattern = new(
        "^You try to cure the poison coursing through (?<target>.+?), but cannot affect .+\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CurePoisonPattern = new(
        "^You cure the (?<poison>poison|poisons) coursing through (?<target>.+?) with a balm made from (?<ingredient>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PoisonIneffectivePattern = new(
        "^You try to cure the poison coursing through (?<target>.+?), but your cures are ineffective\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PoisonTooStrongYouAndTargetPattern = new(
        "^The poison affecting you and (?<target>.+?) is too strong to be cured by your nostrums\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PoisonTooStrongTargetsPattern = new(
        "^The poison affecting (?<targets>.+?) is too strong to be cured by your nostrums\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NeitherPoisonedPattern = new(
        "^Neither you nor (?<target>.+?) are poisoned\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ConditionNoMedicinalIngredientsPattern = new(
        "^You have no medicinal ingredients with which to treat (?<condition>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CureConditionPassThroughPattern = new(
        "^You try to cure (?<condition>.+?), but your limbs pass through .+\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex IllnessCannotAffectPattern = new(
        "^You try to (?<condition>.+?illness), but cannot affect .+\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CureConditionPattern = new(
        "^You cure (?<condition>.+?) with a balm made from (?<ingredient>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NeitherIllPattern = new(
        "^Neither you nor (?<target>.+?) are ill\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DiseaseAlreadyBoostedPattern = new(
        "^(?<target>.+?) already has boosted immunity from a nostrum\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DiseaseCannotAffectPattern = new(
        "^You try to (?<condition>.+?disease onset), but cannot affect .+\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BoostImmunityPattern = new(
        "^You boost (?<immunity>.+?) with a balm made from (?<ingredient>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BoostImmunityIneffectivePattern = new(
        "^You try to boost (?<immunity>.+?) with a balm made from (?<ingredient>.+?), but it is ineffective\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NeitherDiseaseOnsetPattern = new(
        "^Neither you nor (?<target>.+?) are suffering from the onset of a disease\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Campfire");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found.", Context);
            yield break;
        }

        foreach (var methodName in new[]
        {
            "NostrumsStopBleeding",
            "NostrumsTreatPoison",
            "NostrumsTreatIllness",
            "NostrumsTreatDiseaseOnset",
        })
        {
            var method = AccessTools.Method(targetType, methodName, Type.EmptyTypes);
            if (method is null)
            {
                Trace.TraceError("QudJP: {0}.Campfire.{1} target not found.", Context, methodName);
                continue;
            }

            yield return method;
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
        if (!TryTranslateCore(source, stripped, spans, out translated, out var detail))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "Popup.ProducerText." + Context + "." + detail, source, translated);
        return true;
    }

    private static bool TryTranslateCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            StaunchPassThroughPattern,
            "target",
            target => target + "の傷を止血しようとするが、手が体をすり抜ける。",
            "StaunchPassThrough",
            out translated,
            out detail))
        {
            return true;
        }

        if (NotBleedingPattern.IsMatch(stripped))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                "あなたは出血していない。",
                spans,
                stripped.Length,
                source);
            detail = "NotBleeding";
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            StaunchCannotAffectPattern,
            "target",
            target => target + "の傷を止血しようとするが、影響を与えられない。",
            "StaunchCannotAffect",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            StaunchPartialPattern,
            "target",
            target => target + "の傷を止血したが、深すぎて処置できないものもある。",
            "StaunchPartial",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            StaunchFullPattern,
            "target",
            target => target + "の傷を止血した。",
            "StaunchFull",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            WoundsTooDeepPattern,
            "target",
            target => target + "は深すぎて処置できない。",
            "WoundsTooDeep",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            NeitherBleedingPattern,
            "target",
            target => "あなたも" + target + "も出血していない。",
            "NeitherBleeding",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            NoMedicinalIngredientsPattern,
            "target",
            target => target + "を蝕む毒を治療する薬用素材がない。",
            "NoMedicinalIngredients",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            PoisonPassThroughPattern,
            "target",
            target => target + "を蝕む毒を治そうとするが、手が体をすり抜ける。",
            "PoisonPassThrough",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            PoisonCannotAffectPattern,
            "target",
            target => target + "を蝕む毒を治そうとするが、影響を与えられない。",
            "PoisonCannotAffect",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateCurePoison(source, stripped, spans, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            PoisonIneffectivePattern,
            "target",
            target => target + "を蝕む毒を治そうとするが、治療が効かない。",
            "PoisonIneffective",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            PoisonTooStrongYouAndTargetPattern,
            "target",
            target => "あなたと" + target + "にかかった毒は、薬では治せないほど強い。",
            "PoisonTooStrongYouAndTarget",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            PoisonTooStrongTargetsPattern,
            "targets",
            targets => targets + "にかかった毒は、薬では治せないほど強い。",
            "PoisonTooStrongTargets",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            NeitherPoisonedPattern,
            "target",
            target => "あなたも" + target + "も毒状態ではない。",
            "NeitherPoisoned",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateConditionNoMedicinalIngredients(source, stripped, spans, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateCureConditionPassThrough(source, stripped, spans, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            IllnessCannotAffectPattern,
            "condition",
            condition => condition + "を治そうとするが、影響を与えられない。",
            "IllnessCannotAffect",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateCureCondition(source, stripped, spans, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            NeitherIllPattern,
            "target",
            target => "あなたも" + target + "も病気ではない。",
            "NeitherIll",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            DiseaseAlreadyBoostedPattern,
            "target",
            target => target + "はすでに薬で免疫を高めている。",
            "DiseaseAlreadyBoosted",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            DiseaseCannotAffectPattern,
            "condition",
            condition => condition + "を治そうとするが、影響を与えられない。",
            "DiseaseCannotAffect",
            out translated,
            out detail))
        {
            return true;
        }

        if (TryTranslateBoostImmunity(source, stripped, spans, ineffective: false, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateBoostImmunity(source, stripped, spans, ineffective: true, out translated, out detail))
        {
            return true;
        }

        if (TryTranslateTargetPattern(
            source,
            stripped,
            spans,
            NeitherDiseaseOnsetPattern,
            "target",
            target => "あなたも" + target + "も病気の発症に苦しんでいない。",
            "NeitherDiseaseOnset",
            out translated,
            out detail))
        {
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslateTargetPattern(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        Regex pattern,
        string groupName,
        Func<string, string> translate,
        string candidateDetail,
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

        var target = RestoreCapture(match, spans, groupName).Trim();
        translated = RestoreWholeSourceBoundaryWrappers(
            translate(target),
            spans,
            stripped.Length,
            source);
        detail = candidateDetail;
        return true;
    }

    private static bool TryTranslateCurePoison(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = CurePoisonPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var poison = TranslatePoisonToken(match.Groups["poison"].Value);
        var target = RestoreCapture(match, spans, "target").Trim();
        var ingredient = RestoreCapture(match, spans, "ingredient").Trim();
        translated = RestoreWholeSourceBoundaryWrappers(
            ingredient + "で作った塗り薬で" + target + "を蝕む" + poison + "を治した。",
            spans,
            stripped.Length,
            source);
        detail = "CurePoison";
        return true;
    }

    private static string TranslatePoisonToken(string source)
    {
        if (string.Equals(source, "poison", StringComparison.Ordinal)
            || string.Equals(source, "poisons", StringComparison.Ordinal))
        {
            return "毒";
        }

        return source;
    }

    private static bool TryTranslateConditionNoMedicinalIngredients(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = ConditionNoMedicinalIngredientsPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var condition = RestoreCapture(match, spans, "condition").Trim();
        translated = RestoreWholeSourceBoundaryWrappers(
            condition + "を治療する薬用素材がない。",
            spans,
            stripped.Length,
            source);
        detail = IsIllnessCondition(condition)
            ? "IllnessNoMedicinalIngredients"
            : "DiseaseNoMedicinalIngredients";
        return true;
    }

    private static bool TryTranslateCureConditionPassThrough(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = CureConditionPassThroughPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var condition = RestoreCapture(match, spans, "condition").Trim();
        translated = RestoreWholeSourceBoundaryWrappers(
            condition + "を治そうとするが、手が体をすり抜ける。",
            spans,
            stripped.Length,
            source);
        detail = IsDiseaseOnsetCondition(condition) ? "DiseasePassThrough" : "IllnessPassThrough";
        return true;
    }

    private static bool TryTranslateCureCondition(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        var match = CureConditionPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var condition = RestoreCapture(match, spans, "condition").Trim();
        var ingredient = RestoreCapture(match, spans, "ingredient").Trim();
        translated = RestoreWholeSourceBoundaryWrappers(
            ingredient + "で作った塗り薬で" + condition + "を治した。",
            spans,
            stripped.Length,
            source);
        detail = IsIllnessCondition(condition) ? "CureIllness" : "CureDiseaseOnset";
        return true;
    }

    private static bool TryTranslateBoostImmunity(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        bool ineffective,
        out string translated,
        out string detail)
    {
        var pattern = ineffective ? BoostImmunityIneffectivePattern : BoostImmunityPattern;
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var immunity = RestoreCapture(match, spans, "immunity").Trim();
        var ingredient = RestoreCapture(match, spans, "ingredient").Trim();
        var suffix = ineffective ? "を高めようとするが、効果がない。" : "を高めた。";
        translated = RestoreWholeSourceBoundaryWrappers(
            ingredient + "で作った塗り薬で" + immunity + suffix,
            spans,
            stripped.Length,
            source);
        detail = ineffective ? "BoostImmunityIneffective" : "BoostImmunity";
        return true;
    }

    private static bool IsIllnessCondition(string condition)
    {
        return condition.EndsWith("'s illness", StringComparison.Ordinal);
    }

    private static bool IsDiseaseOnsetCondition(string condition)
    {
        return condition.EndsWith("'s diease onset", StringComparison.Ordinal)
            || condition.EndsWith("'s disease onset", StringComparison.Ordinal);
    }

    private static string RestoreCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group);
    }

    private static string RestoreWholeSourceBoundaryWrappers(
        string translated,
        IReadOnlyList<ColorSpan> spans,
        int sourceLength,
        string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            sourceLength,
            source);
    }
}
