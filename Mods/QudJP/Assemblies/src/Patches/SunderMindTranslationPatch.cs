using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SunderMindTranslationPatch
{
    private const string Context = nameof(SunderMindTranslationPatch);

    private static readonly Regex CancelWithTargetPattern = new(
        "^Your concentration slips and the channel between you and (?<target>.+) dissipates into aether\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BeginQueuePattern = new(
        "^You burrow a channel through the psychic aether to (?<target>.+) and begin to sunder (?<possessive>.+) mind!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BeginPopupPattern = new(
        "^(?<source>.+?) (?<direction>.*?)(?:burrow|burrows) a channel through the psychic aether and\\s*(?:begin|begins) to sunder your mind!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PenetrationFailurePattern = new(
        "^Your attack fails to penetrate (?<defenses>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HeadExplodesPattern = new(
        "^(?:(?<owner>.+?)'s|Your) head explodes!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BlastNoDamageMessagePattern = new(
        "^(?<attacker>You|.+?) (?<verb>sunder|sunders) (?<target>.+?) mind(?<multiplier>\\(x\\d+\\)) for (?<damage>.+?) damage!$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NoseBeginsToBleedPattern = new(
        "^(?:(?<owner>.+?)'s|Your) nose begins to bleed\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CoreBeginsToLeakPattern = new(
        "^(?:(?<owner>.+?)'s|Your) core begins to leak\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BrainBeginsToHemorrhagePattern = new(
        "^(?:(?<owner>.+?)'s|Your) brain begins to hemorrhage\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ThreadStatic]
    private static int activeDepth;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        var targetType = AccessTools.TypeByName("XRL.World.Parts.Mutation.SunderMind");
        var gameObjectType = AccessTools.TypeByName("XRL.World.GameObject");
        var memberOfPsychicBattleType = AccessTools.TypeByName("XRL.World.Effects.MemberOfPsychicBattle");
        if (targetType is null || gameObjectType is null || memberOfPsychicBattleType is null)
        {
            Trace.TraceError("QudJP: {0} target types not found.", Context);
            return targets;
        }

        AddTarget(targets, targetType, "CancelSunder", Type.EmptyTypes);
        AddTarget(targets, targetType, "BeginSunder", new[] { gameObjectType });
        AddTarget(targets, targetType, "PenetrationFailure", new[] { gameObjectType });
        AddTarget(targets, targetType, "Tick", Type.EmptyTypes);
        AddTarget(targets, targetType, "Nosebleed", new[] { gameObjectType, memberOfPsychicBattleType });
        return targets;
    }

    public static void Prefix()
    {
        try
        {
            activeDepth++;
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
            if (activeDepth > 0)
            {
                activeDepth--;
            }
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

        DynamicTextObservability.RecordTransform(Context, "SunderMind.Queue", message, translated);
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

        if (!TryTranslateBeginPopup(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(route, family + "." + Context, source, translated);
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
            CancelWithTargetPattern,
            source,
            (match, spans) => $"集中が途切れ、あなたと{Restore(match, spans, "target")}の間の回路が霊気へ霧散した。",
            out translated)
            || TryTranslateExact(
                source,
                "Your concentration slips and the channel dissipates.",
                "集中が途切れ、回路が霧散した。",
                out translated)
            || TryTranslatePattern(
            BeginQueuePattern,
            source,
            (match, spans) =>
                $"精神の霊界に穿ち{Restore(match, spans, "target")}へ通路を掘り、{TranslateMindOwner(Restore(match, spans, "possessive"))}精神を破壊し始めた！",
            out translated)
            || TryTranslatePattern(
                PenetrationFailurePattern,
                source,
                (match, spans) => $"{Restore(match, spans, "defenses")}を突破できなかった。",
                out translated)
            || TryTranslatePattern(
                BlastNoDamageMessagePattern,
                source,
                (match, spans) =>
                {
                    var attacker = match.Groups["attacker"].Value;
                    var subject = attacker == "You" ? "あなた" : RestoreWithoutWholeSourceBoundary(match, spans, "attacker");
                    var mindOwner = TranslateMindOwner(Restore(match, spans, "target"));
                    return $"{subject}は{mindOwner}精神を{Restore(match, spans, "multiplier")}破壊し、{Restore(match, spans, "damage")}ダメージを与えた！";
                },
                out translated)
            || TryTranslatePattern(
                HeadExplodesPattern,
                source,
                (match, spans) => $"{RestorePossessiveOwner(match, spans)}の頭が爆発した！",
                out translated)
            || TryTranslatePattern(
                NoseBeginsToBleedPattern,
                source,
                (match, spans) => $"{RestorePossessiveOwner(match, spans)}の鼻血が出始めた。",
                out translated)
            || TryTranslatePattern(
                CoreBeginsToLeakPattern,
                source,
                (match, spans) => $"{RestorePossessiveOwner(match, spans)}のコアが漏れ始めた。",
                out translated)
            || TryTranslatePattern(
                BrainBeginsToHemorrhagePattern,
                source,
                (match, spans) => $"{RestorePossessiveOwner(match, spans)}の脳が出血し始めた。",
                out translated);
    }

    private static bool TryTranslateBeginPopup(string source, out string translated)
    {
        return TryTranslatePattern(
            BeginPopupPattern,
            source,
            (match, spans) =>
            {
                var sourceName = Restore(match, spans, "source");
                var direction = Restore(match, spans, "direction");
                var separator = direction.Length == 0 ? " " : $" {direction} ";
                return $"{sourceName}{separator}精神の霊界に通路を掘り、あなたの精神を破壊し始めた！";
            },
            out translated);
    }

    private static bool TryTranslateExact(string source, string expected, string replacement, out string translated)
    {
        if (!string.Equals(source, expected, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = replacement;
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

    private static string RestoreWithoutWholeSourceBoundary(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var innerSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, match.Value.Length);
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, innerSpans, group).Trim();
    }

    private static string RestorePossessiveOwner(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return match.Groups["owner"].Success ? Restore(match, spans, "owner") : "あなた";
    }

    private static string TranslateMindOwner(string source)
    {
        switch (source.ToUpperInvariant())
        {
            case "YOUR":
            case "YOURS":
                return "あなたの";
            case "ITS":
                return "その";
            case "THEIR":
            case "THEIRS":
                return "彼らの";
            case "HIS":
                return "彼の";
            case "HER":
            case "HERS":
                return "彼女の";
        }

        return source.EndsWith("'s", StringComparison.Ordinal)
            ? source.Substring(0, source.Length - 2) + "の"
            : source + "の";
    }
}
