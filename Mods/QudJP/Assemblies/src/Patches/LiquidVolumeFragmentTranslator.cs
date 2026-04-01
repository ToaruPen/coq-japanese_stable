using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class LiquidVolumeFragmentTranslator
{
    private static readonly IReadOnlyList<TranslationRule> Rules =
    [
        new(
            "InteractionBlocked",
            new Regex(
                "^You cannot seem to interact with (?<target>.+?) in any way\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "にはどうやっても干渉できないようだ。")),
        new(
            "OwnershipDrink",
            new Regex(
                "^(?<target>.+?) (?:is|are) not owned by you\\. Are you sure you want to drink from (?<object>.+?)\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => BuildOwnershipQuestion(
                match.Groups["target"],
                spans,
                static target => string.Concat(target, "はあなたの所有物ではない。本当にそこから飲みますか？"))),
        new(
            "OwnershipDrain",
            new Regex(
                "^(?<target>.+?) (?:is|are) not owned by you\\. Are you sure you want to drain (?<object>.+?)\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => BuildOwnershipQuestion(
                match.Groups["target"],
                spans,
                static target => string.Concat(target, "はあなたの所有物ではない。本当に排出しますか？"))),
        new(
            "OwnershipFill",
            new Regex(
                "^(?<target>.+?) (?:is|are) not owned by you\\. Are you sure you want to fill (?<object>.+?)\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => BuildOwnershipQuestion(
                match.Groups["target"],
                spans,
                static target => string.Concat(target, "はあなたの所有物ではない。本当に満たしますか？"))),
        new(
            "NowStatus",
            new Regex(
                "^You are now (?<status>.+)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "あなたは今、",
                RestoreVisible(match.Groups["status"], spans),
                "。")),
        new(
            "NoDrain",
            new Regex(
                "^(?<target>.+?) (?:have|has) no drain\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "には排出口がない。")),
        new(
            "Sealed",
            new Regex(
                "^(?<target>.+?) (?:is|are) sealed\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "は密閉されている。")),
        new(
            "Empty",
            new Regex(
                "^(?<target>.+?) (?:is|are) empty\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "は空だ。")),
        new(
            "PourIntoSelf",
            new Regex(
                "^You can't pour from a container into (?<target>.+?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "に容器から注ぐことはできない。")),
        new(
            "EmptyFirst",
            new Regex(
                "^Do you want to empty (?<target>.+?) first\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "を先に空にしますか？")),
    ];

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        return TryTranslate(source, route, family, out translated);
    }

    private static bool TryTranslate(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
            }
            else
            {
                translated = source;
            }

            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        for (var index = 0; index < Rules.Count; index++)
        {
            var rule = Rules[index];
            var match = rule.Pattern.Match(stripped);
            if (!match.Success)
            {
                continue;
            }

            translated = rule.Build(match, spans);
            DynamicTextObservability.RecordTransform(route, family + "." + rule.Name, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static string BuildOwnershipQuestion(Group targetGroup, IReadOnlyList<ColorSpan> spans, Func<string, string> build)
    {
        var target = TranslateTarget(targetGroup, spans);
        return build(target);
    }

    private static string TranslateTarget(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var restored = RestoreVisible(group, spans);
        var normalized = NormalizeTarget(group.Value);
        return string.Equals(normalized, group.Value.Trim(), StringComparison.Ordinal)
            ? restored
            : ColorAwareTranslationComposer.RestoreCapture(normalized, spans, group).Trim();
    }

    private static string RestoreVisible(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group);
        return restored.Trim();
    }

    private static string NormalizeTarget(string target)
    {
        var trimmed = target.Trim();
        if (string.Equals(trimmed, "you", StringComparison.OrdinalIgnoreCase))
        {
            return "あなた";
        }

        if (string.Equals(trimmed, "yourself", StringComparison.OrdinalIgnoreCase))
        {
            return "自分";
        }

        if (string.Equals(trimmed, "itself", StringComparison.OrdinalIgnoreCase))
        {
            return "それ自身";
        }

        return StringHelpers.StripLeadingEnglishArticle(trimmed, includeCapitalizedDefiniteArticle: true);
    }

    private sealed class TranslationRule
    {
        internal TranslationRule(string name, Regex pattern, Func<Match, IReadOnlyList<ColorSpan>, string> build)
        {
            Name = name;
            Pattern = pattern;
            Build = build;
        }

        internal string Name { get; }

        internal Regex Pattern { get; }

        internal Func<Match, IReadOnlyList<ColorSpan>, string> Build { get; }
    }
}
