using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class BedChairFragmentTranslator
{
    private static readonly TranslationRule[] BedRules =
    {
        new(
            "CannotSleepOn",
            new Regex("^You cannot sleep on (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "の上で眠れない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotSleepOnFragment",
            new Regex("^上で眠れない: (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "の上で眠れない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotReach",
            new Regex("^You cannot reach (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "に手が届かない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotReachFragment",
            new Regex("^手が届かない: (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "に手が届かない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "OutOfPhase",
            new Regex("^You are out of phase with (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "と位相がずれている" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "OutOfPhaseFragment",
            new Regex("^あなたは と位相がずれている。(?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "と位相がずれている" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "Broke",
            new Regex("^You think you broke (?<target>.+?)\\.\\.\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, _) => target + "を壊してしまった気がする。"),
        new(
            "BrokeFragment",
            new Regex("^\\s*を壊してしまった気がする。(?<target>.+?)\\.\\.\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, _) => target + "を壊してしまった気がする。"),
    };

    private static readonly TranslationRule[] ChairRules =
    {
        new(
            "CannotSitOn",
            new Regex("^You cannot sit on (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "に座れない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotSitOnFragment",
            new Regex("^座れない: (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "に座れない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotReach",
            new Regex("^You cannot reach (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "に手が届かない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotReachFragment",
            new Regex("^手が届かない: (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "に手が届かない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "OutOfPhase",
            new Regex("^You are out of phase with (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "と位相がずれている" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "OutOfPhaseFragment",
            new Regex("^あなたは と位相がずれている。(?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "と位相がずれている" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotUnequip",
            new Regex("^You cannot unequip (?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "を外せない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotUnequipFragment",
            new Regex("^\\s*を外せない。(?<target>.+?)(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "を外せない" + TranslateTerminal(match.Groups["end"].Value)),
        new(
            "CannotSetDown",
            new Regex("^You cannot set (?<target>.+?) down(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "を置けない" + TranslateTerminal(match.Groups["end"].Value, fallback: "！")),
        new(
            "CannotSetDownFragment",
            new Regex("^\\s*を設定できない。(?<target>.+?)(?:\\s+down)?(?<end>[.!?])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, match) => target + "を置けない" + TranslateTerminal(match.Groups["end"].Value, fallback: "！")),
        new(
            "Broke",
            new Regex("^You think you broke (?<target>.+?)\\.\\.\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, _) => target + "を壊してしまった気がする。"),
        new(
            "BrokeFragment",
            new Regex("^\\s*を壊してしまった気がする。(?<target>.+?)\\.\\.\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (target, _) => target + "を壊してしまった気がする。"),
    };

    internal static bool TryTranslateBedMessage(string source, string route, string family, out string translated)
    {
        return TryTranslate(source, BedRules, route, family, out translated);
    }

    internal static bool TryTranslateChairMessage(string source, string route, string family, out string translated)
    {
        return TryTranslate(source, ChairRules, route, family, out translated);
    }

    private static bool TryTranslate(
        string source,
        IReadOnlyList<TranslationRule> rules,
        string route,
        string family,
        out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        for (var index = 0; index < rules.Count; index++)
        {
            if (!TryApplyRule(stripped, spans, rules[index], out translated))
            {
                continue;
            }

            DynamicTextObservability.RecordTransform(route, family + "." + rules[index].Name, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryApplyRule(
        string source,
        IReadOnlyList<ColorSpan> spans,
        TranslationRule rule,
        out string translated)
    {
        var match = rule.Pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var targetGroup = match.Groups["target"];
        var visible = rule.Build(NormalizeTarget(targetGroup.Value), match);

        if (spans.Count == 0)
        {
            translated = visible;
            return true;
        }

        var boundarySpans = BuildMovedTargetBoundarySpans(spans, targetGroup, visible.Length);
        translated = boundarySpans.Count > 0
            ? ColorAwareTranslationComposer.Restore(visible, boundarySpans)
            : visible;
        return true;
    }

    private static List<ColorSpan> BuildMovedTargetBoundarySpans(
        IReadOnlyList<ColorSpan> spans,
        Group targetGroup,
        int translatedLength)
    {
        var boundarySpans = new List<ColorSpan>();
        var targetStart = targetGroup.Index;
        var targetEnd = targetGroup.Index + targetGroup.Length;

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            if (span.Index <= targetStart)
            {
                boundarySpans.Add(new ColorSpan(0, span.Token));
                continue;
            }

            if (span.Index >= targetEnd)
            {
                boundarySpans.Add(new ColorSpan(translatedLength, span.Token));
            }
        }

        return boundarySpans;
    }

    private static string NormalizeTarget(string target)
    {
        var trimmed = target.Trim();
        if (string.Equals(trimmed, "you", StringComparison.OrdinalIgnoreCase))
        {
            return "あなた";
        }

        if (trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("a ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            var separator = trimmed.IndexOf(' ');
            return separator >= 0 && separator < trimmed.Length - 1
                ? trimmed.Substring(separator + 1)
                : trimmed;
        }

        return trimmed;
    }

    private static string TranslateTerminal(string end, string fallback = "。")
    {
        return end switch
        {
            "!" => "！",
            "?" => "？",
            "." => "。",
            _ => fallback,
        };
    }

    private sealed class TranslationRule
    {
        internal TranslationRule(string name, Regex pattern, Func<string, Match, string> build)
        {
            Name = name;
            Pattern = pattern;
            Build = build;
        }

        internal string Name { get; }

        internal Regex Pattern { get; }

        internal Func<string, Match, string> Build { get; }
    }
}
