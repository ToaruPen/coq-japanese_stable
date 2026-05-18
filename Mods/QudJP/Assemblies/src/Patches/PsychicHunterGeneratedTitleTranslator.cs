using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class PsychicHunterGeneratedTitleTranslator
{
    private static readonly Regex RankPrefixPattern = new(
        "^(?<modifier>.+?) \\*rank\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RankInPattern = new(
        "^\\*rank\\*-in-(?<modifier>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PsychicShapePattern = new(
        "^\\*rank\\* in the Psychic (?<shape>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RankOfPattern = new(
        "^\\*rank\\* of the (?<modifier>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RankHyphenPattern = new(
        "^\\*rank\\*-(?<modifier>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RankStampedPattern = new(
        "^\\*rank\\*, (?<stamp>.+?) in (?<material>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RankAndPattern = new(
        "^\\*rank\\* and (?<modifier>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RankBringerPattern = new(
        "^\\*rank\\* and bringer of (?<illness>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PtohTitlePattern = new(
        "^Ptoh's (?<title>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ExtradimensionalCreaturePattern = new(
        "^extradimensional (?<creature>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex EsperHunterPattern = new(
        "^esper (?<hunter>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TransdimensionalEntropistPattern = new(
        "^transdimensional (?<entropist>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex EsperFromCultPattern = new(
        "^esper from the (?<cult>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    internal static bool TryTranslateExpandedText(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        if (TryTranslateCommonPhrase(original, out translated)
            || TryTranslateSeekerRankFrame(original, out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    internal static bool TryTranslateTitle(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var normalized = TranslateRankAndCommonPhrases(source!);
        var match = PtohTitlePattern.Match(normalized);
        if (match.Success)
        {
            translated = "プトフの" + TranslateTitlePhrase(match.Groups["title"].Value);
            return true;
        }

        match = ExtradimensionalCreaturePattern.Match(normalized);
        if (match.Success)
        {
            translated = "異次元の" + TranslateTitlePhrase(match.Groups["creature"].Value);
            return true;
        }

        match = EsperFromCultPattern.Match(normalized);
        if (match.Success)
        {
            translated = TranslateTitlePhrase(match.Groups["cult"].Value) + "出身のエスパー";
            return true;
        }

        match = EsperHunterPattern.Match(normalized);
        if (match.Success)
        {
            translated = "エスパーの" + TranslateCommonPhrase(match.Groups["hunter"].Value);
            return true;
        }

        match = TransdimensionalEntropistPattern.Match(normalized);
        if (match.Success)
        {
            translated = "超次元の" + TranslateCommonPhrase(match.Groups["entropist"].Value);
            return true;
        }

        translated = normalized;
        return !string.Equals(source, normalized, StringComparison.Ordinal);
    }

    private static bool TryTranslateSeekerRankFrame(string source, out string translated)
    {
        var match = RankBringerPattern.Match(source);
        if (match.Success)
        {
            translated = "*rank*、" + TranslateTitlePhrase(match.Groups["illness"].Value) + "をもたらす者";
            return true;
        }

        match = RankPrefixPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateTitlePhrase(match.Groups["modifier"].Value) + "の*rank*";
            return true;
        }

        match = RankInPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateTitlePhrase(match.Groups["modifier"].Value) + "に属する*rank*";
            return true;
        }

        match = PsychicShapePattern.Match(source);
        if (match.Success)
        {
            translated = "サイキック" + TranslateTitlePhrase(match.Groups["shape"].Value) + "に属する*rank*";
            return true;
        }

        match = RankOfPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateTitlePhrase(match.Groups["modifier"].Value) + "の*rank*";
            return true;
        }

        match = RankHyphenPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateTitlePhrase(match.Groups["modifier"].Value) + "の*rank*";
            return true;
        }

        match = RankStampedPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateTitlePhrase(match.Groups["material"].Value) + "に" + TranslateTitlePhrase(match.Groups["stamp"].Value) + "された*rank*";
            return true;
        }

        match = RankAndPattern.Match(source);
        if (match.Success)
        {
            translated = "*rank*と" + TranslateTitlePhrase(match.Groups["modifier"].Value);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCommonPhrase(string source, out string translated)
    {
        translated = TranslateCommonPhrase(source);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static string TranslateRankAndCommonPhrases(string source)
    {
        return source
            .Replace("Osprey", "オスプレイ")
            .Replace("Harrier", "ハリアー")
            .Replace("Owl", "フクロウ")
            .Replace("Condor", "コンドル")
            .Replace("Strix", "ストリクス")
            .Replace("Rukh", "ルフ")
            .Replace("Eagle", "イーグル")
            .Replace("stalker", "追跡者")
            .Replace("assassin", "暗殺者")
            .Replace("entropist", "エントロピスト");
    }

    private static string TranslateCommonPhrase(string source)
    {
        return source switch
        {
            "stalker" => "追跡者",
            "assassin" => "暗殺者",
            "entropist" => "エントロピスト",
            "crimson" => "真紅",
            "cobalt" => "コバルト",
            "circle" => "円環",
            "sea" => "海",
            "ghost" => "幽鬼",
            "stamped" => "刻印",
            "silver" => "銀",
            "spouse" => "伴侶",
            "gout" => "痛風",
            "snapjaw" => "スナップジョー",
            _ => source,
        };
    }

    private static string TranslateTitlePhrase(string source)
    {
        if (ContainsTriadicSymbol(source))
        {
            return source;
        }

        var words = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return source;
        }

        var translated = new string[words.Length];
        for (var index = 0; index < words.Length; index++)
        {
            translated[index] = TranslateCommonPhrase(words[index]);
        }

        if (translated.Length == 2
            && string.Equals(translated[0], "真紅", StringComparison.Ordinal))
        {
            return translated[0] + "の" + translated[1];
        }

        return string.Concat(translated);
    }

    private static bool ContainsTriadicSymbol(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '∴')
            {
                return true;
            }
        }

        return false;
    }
}
