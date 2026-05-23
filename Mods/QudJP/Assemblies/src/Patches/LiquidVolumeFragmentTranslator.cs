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
            "OwnershipPour",
            new Regex(
                "^(?<target>.+?) (?:is|are) not owned by you\\. Are you sure you want to pour from (?<object>.+?)\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => BuildOwnershipQuestion(
                match.Groups["target"],
                spans,
                static target => string.Concat(target, "はあなたの所有物ではない。本当にそこから注ぎますか？"))),
        new(
            "OwnershipTake",
            new Regex(
                "^(?<target>.+?) (?:is|are) not owned by you\\. Are you sure you want to take from (?<object>.+?)\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => BuildOwnershipQuestion(
                match.Groups["target"],
                spans,
                static target => string.Concat(target, "はあなたの所有物ではない。本当にそこから取りますか？"))),
        new(
            "OwnershipCollect",
            new Regex(
                "^(?<target>.+?) (?:is|are) not owned by you\\. Are you sure you want to collect from (?<object>.+?)\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => BuildOwnershipQuestion(
                match.Groups["target"],
                spans,
                static target => string.Concat(target, "はあなたの所有物ではない。本当にそこから集めますか？"))),
        new(
            "OwnershipUseLiquid",
            new Regex(
                "^(?<target>.+?) (?:is|are) not owned by you\\. Are you sure you want to use (?<liquid>.+?) from (?<object>.+?)\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "はあなたの所有物ではない。",
                RestoreVisible(match.Groups["liquid"], spans),
                "を本当にそこから使いますか？")),
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
        new(
            "DrainConfirm",
            new Regex(
                "^Are you sure you want to drain (?<target>.+?)\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "を本当に排出しますか？")),
        new(
            "CollectConfirm",
            new Regex(
                "^You are able to collect (?<amount>\\d+) drams? of (?<liquid>.+?)\\. Are you sure you want to\\?$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                RestoreVisible(match.Groups["liquid"], spans),
                "を",
                match.Groups["amount"].Value,
                "ドラム集められる。本当にそうしますか？")),
        new(
            "NoAvailableCollectionContainer",
            new Regex(
                "^You have nowhere available to collect that\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (_, _) => "それを集められる容器がない。"),
        new(
            "CannotDoForSomeReason",
            new Regex(
                "^You cannot do that for some reason\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (_, _) => "何らかの理由でそれはできない。"),
        new(
            "AutoCollectPureLiquidOnly",
            new Regex(
                "^Auto collection only works on unsealed containers with pure liquids\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (_, _) => "自動収集は、密閉されていない純粋な液体入りの容器でのみ機能する。"),
        new(
            "AutoCollectUnknownLiquid",
            new Regex(
                "^It isn't clear what kind of liquid would be appropriate for (?<target>.+?) to collect\\. Pour a pure liquid into it, and then enable auto-collect\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateTarget(match.Groups["target"], spans),
                "がどの種類の液体を集めるのに適しているか不明だ。純粋な液体を注いでから、自動収集を有効にする。")),
        new(
            "HowManyDrams",
            new Regex(
                "^How many drams\\? \\(max=(?<max>\\d+)\\)$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, _) => string.Concat("何ドラム？(最大=", match.Groups["max"].Value, ")")),
        new(
            "CollectMessage",
            new Regex(
                "^You collect (?<amount>\\d+) drams? of (?<liquid>.+?)(?:(?: (?<openDirection>to the north|to the south|to the east|to the west|to the northeast|to the northwest|to the southeast|to the southwest|nearby|above|below|here|somewhere))|(?: from (?<source>.+?) (?<sourceDirection>to the north|to the south|to the east|to the west|to the northeast|to the northwest|to the southeast|to the southwest|nearby|above|below|here|somewhere)))?(?: in (?<storage>.+?))?\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            BuildCollectMessage),
        new(
            "PourOutSelf",
            new Regex(
                "^(?<amount>\\d+) drams? of (?<liquid>.+?) pours out all over you!$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => BuildPourOutMessage(
                match.Groups["amount"],
                match.Groups["liquid"],
                "あなた",
                spans)),
        new(
            "PourOutActor",
            new Regex(
                "^(?<amount>\\d+) drams? of (?<liquid>.+?) pours out all over (?<target>.+?)!$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => BuildPourOutMessage(
                match.Groups["amount"],
                match.Groups["liquid"],
                TranslateTarget(match.Groups["target"], spans),
                spans)),
        new(
            "Fizzy",
            new Regex(
                "^It's fizzy\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (_, _) => "シュワシュワしている。"),
    ];

    internal static bool TryTranslatePopupMessage(string source, string route, string family, out string translated)
    {
        return TryTranslate(source, route, family, out translated);
    }

    internal static bool TryTranslateQueuedMessage(string source, string route, string family, out string translated)
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

    private static string BuildPourOutMessage(
        Group amountGroup,
        Group liquidGroup,
        string target,
        IReadOnlyList<ColorSpan> spans)
    {
        return string.Concat(
            RestoreVisible(liquidGroup, spans),
            ' ',
            amountGroup.Value,
            "ドラムが",
            target,
            "の全身にかかった！");
    }

    private static string BuildCollectMessage(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var prefix = BuildCollectLocationPrefix(match, spans);
        var storageSuffix = match.Groups["storage"].Success
            ? string.Concat("（", StripPossessivePrefixPreservingColors(RestoreVisible(match.Groups["storage"], spans)), "に入れた）")
            : string.Empty;

        return string.Concat(
            prefix,
            RestoreVisible(match.Groups["liquid"], spans),
            "を",
            match.Groups["amount"].Value,
            "ドラム集めた",
            storageSuffix,
            "。");
    }

    private static string BuildCollectLocationPrefix(Match match, IReadOnlyList<ColorSpan> spans)
    {
        if (match.Groups["source"].Success)
        {
            var source = TranslateTarget(match.Groups["source"], spans);
            if (TryTranslateDirection(match.Groups["sourceDirection"].Value, out var sourceDirection))
            {
                return string.Concat(source, "（", sourceDirection, "）から");
            }

            return source + "から";
        }

        return match.Groups["openDirection"].Success
            && TryTranslateDirection(match.Groups["openDirection"].Value, out var openDirection)
            ? openDirection + "で"
            : string.Empty;
    }

    private static string TranslateTarget(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var restored = RestoreVisible(group, spans);
        var normalized = NormalizeTarget(group.Value);
        return string.Equals(normalized, group.Value.Trim(), StringComparison.Ordinal)
            ? restored
            : TranslateNormalizedTarget(group, spans, restored, normalized);
    }

    private static string TranslateNormalizedTarget(
        Group group,
        IReadOnlyList<ColorSpan> spans,
        string restored,
        string normalized)
    {
        var articleStripped = StringHelpers.StripLeadingEnglishArticle(
            group.Value.Trim(),
            includeCapitalizedDefiniteArticle: true);
        if (string.Equals(normalized, articleStripped, StringComparison.Ordinal))
        {
            return StripLeadingEnglishArticlePreservingColors(restored);
        }

        return ColorAwareTranslationComposer.RestoreCapture(normalized, spans, group).Trim();
    }

    private static string RestoreVisible(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var restored = ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group);
        return restored.Trim();
    }

    private static string StripLeadingEnglishArticlePreservingColors(string source)
    {
        var direct = StringHelpers.StripLeadingEnglishArticle(source, includeCapitalizedDefiniteArticle: true);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        var withoutArticle = StringHelpers.StripLeadingEnglishArticle(
            visible,
            includeCapitalizedDefiniteArticle: true);
        if (string.Equals(withoutArticle, visible, StringComparison.Ordinal))
        {
            return source;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => withoutArticle);
    }

    private static string StripPossessivePrefixPreservingColors(string source)
    {
        var direct = StripPossessivePrefix(source);
        if (!string.Equals(direct, source, StringComparison.Ordinal))
        {
            return direct;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        var withoutPossessive = StripPossessivePrefix(visible);
        if (string.Equals(withoutPossessive, visible, StringComparison.Ordinal))
        {
            return source;
        }

        return ColorAwareTranslationComposer.TranslatePreservingColors(source, _ => withoutPossessive);
    }

    private static string StripPossessivePrefix(string source)
    {
        var trimmed = source.Trim();
        if (trimmed.StartsWith("your ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring("your ".Length);
        }

        if (trimmed.StartsWith("its ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring("its ".Length);
        }

        return source;
    }

    private static bool TryTranslateDirection(string source, out string translated)
    {
        return QudJP.DirectionPhraseTranslator.TryTranslateNounStem(source, out translated);
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
