using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class RealityStabilizedEventTranslationPatch
{
    private const string Context = nameof(RealityStabilizedEventTranslationPatch);

    private static readonly Regex PsychicWhiffPattern = new(
        "^You feel a psychic whiff as (?<actor>.+?) pushes? past resistance in the structure of spacetime\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PsychicThudPattern = new(
        "^You feel a psychic thud as (?<actor>.+?) pushes? against the structure of spacetime and fails? to break through\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WincePattern = new(
        "^(?<actor>.+?) winces?\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShowerSparksPattern = new(
        "^(?<device>.+?) showers? sparks everywhere\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmitSparksPattern = new(
        "^(?<device>.+?) emits? a shower of sparks!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OptionToContestSifrahPattern = new(
        "^(?<intro>A normality lattice prevents you from altering spacetime in (?:(?:both your local region and the local region you're trying to interact with)|(?:the|that) local region)\\.) You can try to push through at some risk\\. Your feeling is that success would be (?<difficulty>almost impossible|challenging|moderately difficult|easy|very easy)\\. Do you want to try\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // OptionToContestChancePattern keeps the color-control suffix in <percentToken>\d+%(?:[A-Za-z])?
    // from markup like "{{R|20%}}R"; ColorAwareTranslationComposer strips the wrapper but leaves the suffix.
    private static readonly Regex OptionToContestChancePattern = new(
        "^(?<intro>A normality lattice prevents you from altering spacetime in (?:(?:both your local region and the local region you're trying to interact with)|(?:the|that) local region)\\.) You can try to push through at some risk\\. You estimate (?<estimate>less than a|about a) (?<percentToken>\\d+%(?:[A-Za-z])?) chance of success\\. Do you want to try\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string NormalityLatticePopup =
        "You try to push through the normality lattice, but it snaps back into place.";

    private const string NormalityLatticeWinceSuffix = " You wince in pain.";

    private const string NormalityLatticeTranslation =
        "あなたはノーマリティ格子を押し通ろうとしたが、それは跳ね返って元に戻った。";

    private const string NormalityLatticeWinceTranslation = "あなたは痛みに顔をしかめた。";

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Effects.RealityStabilized");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var eventType = AccessTools.TypeByName("XRL.World.Event");
        if (targetType is null || gameObjectType is null || eventType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "TryContest", new[] { gameObjectType, typeof(int), typeof(int) });
        AddTarget(targets, targetType, "OptionToContest", new[] { gameObjectType, typeof(int), typeof(bool) });
        AddTarget(targets, targetType, "FailedToContest", new[] { gameObjectType });
        AddTarget(targets, targetType, "ShortCircuitDevice", new[] { gameObjectType, gameObjectType, eventType });
        return targets;
    }

    public static void Prefix(out int __state)
    {
        try
        {
            __state = activeDepth;
            activeDepth++;
        }
        catch (Exception ex)
        {
            __state = 0;
            Trace.TraceError("QudJP: {0}.Prefix failed: {1}", Context, ex);
        }
    }

    public static Exception? Finalizer(Exception? __exception, int __state)
    {
        try
        {
            activeDepth = __state;
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

        if (activeDepth <= 0
            || string.IsNullOrEmpty(message)
            || MessageFrameTranslator.TryStripDirectTranslationMarker(message, out _))
        {
            return false;
        }

        if (!TryTranslateQueuedCore(message, out var translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(Context, "RealityStabilized.Queue", message, translated);
        message = MessageFrameTranslator.MarkDirectTranslation(translated);
        return true;
    }

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        if (activeDepth <= 0 || string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        if (TryTranslateOptionToContestPopup(source, out translated)
            || TryTranslateNormalityLatticePopup(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
            return true;
        }

        if (!TryTranslatePattern(EmitSparksPattern, source, device => $"{device}が火花の雨を放った！", out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
        return true;
    }

    private static bool TryTranslateOptionToContestPopup(string source, out string translated)
    {
        if (TryTranslatePattern(
                OptionToContestSifrahPattern,
                source,
                (match, spans) =>
                    TranslateOptionToContestIntro(Restore(match, spans, "intro"))
                    + "危険を冒して押し通ることはできる。成功は"
                    + TranslateOptionToContestDifficulty(Restore(match, spans, "difficulty"))
                    + "だと感じる。試しますか？",
                out translated)
            || TryTranslatePattern(
                OptionToContestChancePattern,
                source,
                (match, spans) =>
                {
                    var chance = Restore(match, spans, "percentToken");
                    var estimate = Restore(match, spans, "estimate") == "less than a"
                        ? chance + "未満"
                        : "約" + chance;
                    return TranslateOptionToContestIntro(Restore(match, spans, "intro"))
                        + "危険を冒して押し通ることはできる。成功率は"
                        + estimate
                        + "と見積もっている。試しますか？";
                },
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateNormalityLatticePopup(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        string? result = null;

        if (string.Equals(stripped, NormalityLatticePopup, StringComparison.Ordinal))
        {
            result = NormalityLatticeTranslation;
        }
        else if (string.Equals(stripped, NormalityLatticePopup + NormalityLatticeWinceSuffix, StringComparison.Ordinal))
        {
            result = NormalityLatticeTranslation + NormalityLatticeWinceTranslation;
        }

        if (result is null)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            result,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static void AddTarget(List<MethodBase> targets, Type targetType, string methodName, Type[] parameters)
    {
        var method = AccessTools.Method(targetType, methodName, parameters);
        if (method is not null)
        {
            targets.Add(method);
            return;
        }

        Trace.TraceError("QudJP: {0}.{1}.{2} target not found.", Context, targetType.FullName, methodName);
    }

    private static bool TryTranslateQueuedCore(string source, out string translated)
    {
        return TryTranslatePattern(
            PsychicWhiffPattern,
            source,
            actor => $"{actor}が時空構造の抵抗を押し通る、精神的なかすかな感触を覚えた。",
            out translated)
            || TryTranslatePattern(
                PsychicThudPattern,
                source,
                actor => $"{TranslateFailedContestActor(actor)}が時空構造を押して突破に失敗した、精神的な鈍い衝撃を感じた。",
                out translated)
            || TryTranslatePattern(
                WincePattern,
                source,
                actor => $"{actor}が顔をしかめた。",
                out translated)
            || TryTranslatePattern(
                ShowerSparksPattern,
                source,
                device => $"{device}があたり一面に火花を散らした。",
                out translated);
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<string, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var groupName = match.Groups["actor"].Success ? "actor" : "device";
        var group = match.Groups[groupName];
        var value = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(value),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> translate,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translate(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslateOptionToContestIntro(string intro)
    {
        return intro switch
        {
            "A normality lattice prevents you from altering spacetime in both your local region and the local region you're trying to interact with." =>
                "ノーマリティ格子により、あなたは自分の局所領域と干渉しようとしている局所領域の両方で時空を変えられない。",
            "A normality lattice prevents you from altering spacetime in the local region." =>
                "ノーマリティ格子により、あなたはこの局所領域で時空を変えられない。",
            "A normality lattice prevents you from altering spacetime in that local region." =>
                "ノーマリティ格子により、あなたはその局所領域で時空を変えられない。",
            _ => intro,
        };
    }

    private static string TranslateOptionToContestDifficulty(string difficulty)
    {
        return difficulty switch
        {
            "almost impossible" => "ほぼ不可能",
            "challenging" => "困難",
            "moderately difficult" => "やや困難",
            "easy" => "簡単",
            "very easy" => "とても簡単",
            _ => difficulty,
        };
    }

    private static string TranslateFailedContestActor(string actor)
    {
        return string.Equals(actor, "someone", StringComparison.Ordinal) ? "誰か" : actor;
    }
}
