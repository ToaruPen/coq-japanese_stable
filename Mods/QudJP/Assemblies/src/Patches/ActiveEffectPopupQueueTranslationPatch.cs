using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class ActiveEffectPopupQueueTranslationPatch
{
    internal const string Family = "ActiveEffectPopupQueue";

    private const string Context = nameof(ActiveEffectPopupQueueTranslationPatch);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} required target parameter types not found.", Context);
            yield break;
        }

        var gameObjectParameters = new[] { gameObjectType };
        var eventParameters = new[] { eventType };
        var targetSpecs = new (string TypeName, string MethodName, Type[] Parameters)[]
        {
            ("XRL.World.Effects.IrisdualCallow", "Apply", gameObjectParameters),
            ("XRL.World.Effects.CookingDomainTongue_ThreeTongues_ProceduralCookingTriggeredAction", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Hobbled", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Terrified", "Apply", gameObjectParameters),
            ("XRL.World.Effects.GeometricHeal", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Trance", "Apply", gameObjectParameters),
            ("XRL.World.Effects.StingerPoisoned", "Apply", gameObjectParameters),
            ("XRL.World.Effects.FuriouslyConfused", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Confused", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Poisoned", "Apply", gameObjectParameters),
            ("XRL.World.Effects.PhasePoisoned", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Healing", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Dazed", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Paralyzed", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Poisoned", "FireEvent", eventParameters),
            ("XRL.World.Effects.PhasePoisoned", "FireEvent", eventParameters),
            ("XRL.World.Effects.AshPoison", "FireEvent", eventParameters),
            ("XRL.World.Effects.BasiliskPoison", "FireEvent", eventParameters),
            ("XRL.World.Effects.Cripple", "FireEvent", eventParameters),
            ("XRL.World.Effects.PoisonGasPoison", "FireEvent", eventParameters),
            ("XRL.World.Effects.Luminous", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Meditating", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Scintillating", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Suppressed", "Apply", gameObjectParameters),
            ("XRL.World.Effects.ShadeOil_Tonic", "Apply", gameObjectParameters),
            ("XRL.World.Effects.Asleep", "Remove", gameObjectParameters),
            ("XRL.World.Effects.ShadeOil_Tonic", "FireEvent", eventParameters),
            ("XRL.World.Effects.BrainBrineCurse", "FireEvent", eventParameters),
            ("XRL.World.Effects.SphynxSalt_Tonic", "Apply", gameObjectParameters),
        };

        foreach (var targetSpec in targetSpecs)
        {
            foreach (var method in ResolveTarget(targetSpec.TypeName, targetSpec.MethodName, targetSpec.Parameters))
            {
                yield return method;
            }
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

        if (!ActiveEffectPopupQueueTranslator.TryTranslateQueuedMessage(message, out var translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            "MessageQueue.AddPlayerMessage",
            Family + "." + detail,
            message,
            translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
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

        if (!ActiveEffectPopupQueueTranslator.TryTranslatePopupMessage(source, out translated, out var detail))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            route,
            Family + "." + detail,
            source,
            translated);
        return true;
    }

    private static IEnumerable<MethodBase> ResolveTarget(string typeName, string methodName, Type[] parameterTypes)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType is null)
        {
            Trace.TraceError("QudJP: {0} target type not found: {1}", Context, typeName);
            yield break;
        }

        var method = AccessTools.Method(targetType, methodName, parameterTypes);
        if (method is not null)
        {
            yield return method;
            yield break;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }
}

internal static class ActiveEffectPopupQueueTranslator
{
    private static readonly Regex IrisdualCallowRindPattern = new(
        "^(?<owner>.+?) rind softens while (?<subject>.+?) recrystallizes?!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ThreeTonguesPattern = new(
        "^A trio of tongues vegetate from (?<owner>.+?) (?<part>face|Face)!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HobbledPattern = new(
        "^(?<subject>.+?) (?:are|is) hobbled!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TerrifiedPattern = new(
        "^(?<subject>.+?) (?:are|is) overwhelmed with terror!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GeometricHealPattern = new(
        "^(?<subject>.+?) (?:begin|begins) healing\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrancePattern = new(
        "^(?<subject>.+?) (?:enter|enters) a trance!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StingerPoisonedPattern = new(
        "^(?<subject>.+?) (?:have|has) been poisoned!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FuriouslyConfusedPattern = new(
        "^(?<subject>.+?) (?:become|becomes) confused!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DazedPattern = new(
        "^(?<subject>.+?) (?:are|is) dazed\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ParalyzedPattern = new(
        "^(?<subject>.+?) (?:are|is) paralyzed!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoLongerPoisonedPattern = new(
        "^(?<subject>.+?) (?:are|is) no longer poisoned!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoLongerChokingPattern = new(
        "^(?<subject>.+?) (?:are|is) no longer choking!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoLongerCrippledPattern = new(
        "^(?<subject>.+?) (?:are|is) no longer crippled!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LessStiffPattern = new(
        "^(?<subject>.+?) (?:feel|feels) less stiff\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LuminousPattern = new(
        "^(?<subject>.+?) (?:start|starts) to glow\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MeditatingPattern = new(
        "^(?<subject>.+?) (?:begin|begins) meditating\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ScintillatingPattern = new(
        "^(?<subject>.+?) (?:start|starts) scintillating in prismatic hues!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SuppressedPattern = new(
        "^(?<subject>.+?) (?:are|is) suppressed!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShadeOilApplyPattern = new(
        "^(?<subject>.+?) (?:begin|begins) to flicker in and out of corporeality\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AsleepWakeUpPattern = new(
        "^(?<subject>.+?) (?:wake|wakes) up\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AsleepExitSleepModePattern = new(
        "^(?<subject>.+?) (?:exit|exits) sleep mode\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShadeOilPhasePromptPattern = new(
        "^(?<tonic>.+?) has been applied(?: by (?<applicator>.+?))?\\. Do you wish to phase out immediately\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShadeOilNamePattern = new(
        "shade oil",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static bool TryTranslateQueuedMessage(string source, out string translated, out string detail)
    {
        return TryTranslate(source, translatePopup: false, out translated, out detail);
    }

    internal static bool TryTranslatePopupMessage(string source, out string translated, out string detail)
    {
        return TryTranslate(source, translatePopup: true, out translated, out detail);
    }

    private static bool TryTranslate(string source, bool translatePopup, out string translated, out string detail)
    {
        if (string.IsNullOrEmpty(source) || MessageFrameTranslator.TryStripDirectTranslationMarker(source, out _))
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var matched = translatePopup
            ? TryTranslatePopupCore(source, stripped, spans, out translated, out detail)
            : TryTranslateQueuedCore(source, stripped, spans, out translated, out detail);
        if (!matched)
        {
            translated = source;
            detail = string.Empty;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateQueuedCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        _ = source;
        var match = IrisdualCallowRindPattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat(
                TranslatePossessiveActor(Restore(match, spans, "owner")),
                "外皮が柔らかくなり、",
                TranslateSubjectActor(Restore(match, spans, "subject")),
                "は再結晶化した！");
            detail = "IrisdualCallowRindSoftens";
            return true;
        }

        match = ThreeTonguesPattern.Match(stripped);
        if (match.Success)
        {
            translated = string.Concat(
                "3本の舌が",
                TranslatePossessiveActor(Restore(match, spans, "owner")),
                TranslateBodyPart(Restore(match, spans, "part")),
                "から生え出た！");
            detail = "ThreeTonguesVegetate";
            return true;
        }

        match = HobbledPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は足を引きずっている！";
            detail = "HobbledApply";
            return true;
        }

        match = TerrifiedPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は恐怖に圧倒された！";
            detail = "TerrifiedApply";
            return true;
        }

        match = GeometricHealPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は回復を始めた。";
            detail = "GeometricHealApply";
            return true;
        }

        match = TrancePattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "はトランス状態に入った！";
            detail = "TranceApply";
            return true;
        }

        match = StingerPoisonedPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は毒を受けた！";
            detail = "StingerPoisonedApply";
            return true;
        }

        match = FuriouslyConfusedPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は混乱した！";
            detail = "FuriouslyConfusedApply";
            return true;
        }

        match = DazedPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は朦朧としている。";
            detail = "DazedApply";
            return true;
        }

        match = ParalyzedPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は麻痺している！";
            detail = "ParalyzedApply";
            return true;
        }

        match = NoLongerPoisonedPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "はもう毒を受けていない！";
            detail = "NoLongerPoisonedFireEvent";
            return true;
        }

        match = NoLongerChokingPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は窒息から回復した！";
            detail = "NoLongerChokingFireEvent";
            return true;
        }

        match = NoLongerCrippledPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は損傷から回復した！";
            detail = "NoLongerCrippledFireEvent";
            return true;
        }

        match = LessStiffPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は体の硬さがほぐれた。";
            detail = "BasiliskPoisonLessStiff";
            return true;
        }

        match = LuminousPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は輝き始めた。";
            detail = "LuminousApply";
            return true;
        }

        match = MeditatingPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は瞑想を始めた。";
            detail = "MeditatingApply";
            return true;
        }

        match = ScintillatingPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は{{rainbow|虹色の色彩}}できらめき始めた！";
            detail = "ScintillatingApply";
            return true;
        }

        match = SuppressedPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は制圧された！";
            detail = "SuppressedApply";
            return true;
        }

        match = ShadeOilApplyPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は実体と非実体の間で揺らぎ始めた。";
            detail = "ShadeOilApply";
            return true;
        }

        match = AsleepWakeUpPattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "は目を覚ました。";
            detail = "AsleepWakeUp";
            return true;
        }

        match = AsleepExitSleepModePattern.Match(stripped);
        if (match.Success)
        {
            translated = TranslateSubjectActor(Restore(match, spans, "subject")) + "はスリープモードを終了した。";
            detail = "AsleepExitSleepMode";
            return true;
        }

        translated = source;
        detail = string.Empty;
        return false;
    }

    private static bool TryTranslatePopupCore(
        string source,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated,
        out string detail)
    {
        if (string.Equals(stripped, "You cannot do that on the world map.", StringComparison.Ordinal))
        {
            translated = "ワールドマップではそれはできない。";
            detail = "ShadeOilWorldMap";
            return true;
        }

        var match = ShadeOilPhasePromptPattern.Match(stripped);
        if (match.Success)
        {
            var applicator = match.Groups["applicator"];
            translated = applicator.Success
                ? string.Concat(
                    TranslateTonicName(Restore(match, spans, "tonic")),
                    "が",
                    TranslateApplicator(Restore(match, spans, "applicator")),
                    "によって適用された。すぐに位相をずらす？")
                : string.Concat(TranslateTonicName(Restore(match, spans, "tonic")), "が適用された。すぐに位相をずらす？");
            detail = "ShadeOilPhasePrompt";
            return true;
        }

        if (string.Equals(
                stripped,
                "You shake the water from your addled brain, but someone else's thoughts have already taken root.",
                StringComparison.Ordinal))
        {
            translated = "混乱した脳から水を振り払ったが、すでに誰か別の思考が根を下ろしている。";
            detail = "BrainBrineCurseRootedThoughts";
            return true;
        }

        if (string.Equals(stripped, "The clouds part in your mind and a ray of clarity strikes through.", StringComparison.Ordinal))
        {
            translated = "心の中で雲が割れ、明晰さの光が差し込む。";
            detail = "SphynxSaltClarity";
            return true;
        }

        return TryTranslateQueuedCore(source, stripped, spans, out translated, out detail);
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslatePossessiveActor(string source)
    {
        return NormalizeActor(StripTrailingEnglishPossessive(source)) + "の";
    }

    private static string TranslateSubjectActor(string source)
    {
        return NormalizeActor(source);
    }

    private static string NormalizeActor(string source)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(source).Trim();
        return visible switch
        {
            "you" or "your" => "あなた",
            "it" or "its" => "それ",
            "they" or "their" => "それら",
            _ => StringHelpers.StripLeadingEnglishArticle(
                source.Trim(),
                includeCapitalizedDefiniteArticle: true,
                includeCapitalizedIndefiniteArticle: true),
        };
    }

    private static string StripTrailingEnglishPossessive(string source)
    {
        var trimmed = source.Trim();
        if (trimmed.EndsWith("'s", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(0, trimmed.Length - 2).TrimEnd();
        }

        if (trimmed.EndsWith("'", StringComparison.Ordinal))
        {
            return trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
        }

        return trimmed;
    }

    private static string TranslateApplicator(string source)
    {
        var trimmed = source.Trim();
        if (trimmed.StartsWith("your ", StringComparison.OrdinalIgnoreCase))
        {
            return "あなたの" + trimmed.Substring(5).TrimStart();
        }

        if (trimmed.StartsWith("its ", StringComparison.OrdinalIgnoreCase))
        {
            return "それの" + trimmed.Substring(4).TrimStart();
        }

        if (trimmed.StartsWith("their ", StringComparison.OrdinalIgnoreCase))
        {
            return "それらの" + trimmed.Substring(6).TrimStart();
        }

        return StringHelpers.StripLeadingEnglishArticle(
            trimmed,
            includeCapitalizedDefiniteArticle: true,
            includeCapitalizedIndefiniteArticle: true);
    }

    private static string TranslateBodyPart(string source)
    {
        return string.Equals(source, "face", StringComparison.OrdinalIgnoreCase) ? "顔" : source;
    }

    private static string TranslateTonicName(string source)
    {
        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        return ShadeOilNamePattern.IsMatch(visible)
            ? ColorAwareTranslationComposer.TranslatePreservingColors(
                source,
                static visibleText => ShadeOilNamePattern.Replace(visibleText, "シェードオイル"))
            : source;
    }
}
