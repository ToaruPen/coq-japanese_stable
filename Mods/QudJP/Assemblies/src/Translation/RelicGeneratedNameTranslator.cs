using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class RelicGeneratedNameTranslator
{
    private const string ZoneDisplayDictionaryFile = "ui-zone-display.ja.json";
    private static readonly Dictionary<string, string> RelicSpecificWords = new(StringComparer.Ordinal)
    {
        ["aegis"] = "アイギス",
        ["band"] = "腕帯",
        ["becoming"] = "変容",
        ["boomstick"] = "ブームスティック",
        ["brand"] = "剣",
        ["breast"] = "胸甲",
        ["cannon"] = "大砲",
        ["cap"] = "帽子",
        ["chain"] = "鎖",
        ["chrome"] = "クロム",
        ["cleats"] = "スパイク付き",
        ["clogs"] = "木靴",
        ["cosh"] = "ブラックジャック",
        ["dirk"] = "ダーク",
        ["edge"] = "刃",
        ["enhancement"] = "強化体",
        ["fell"] = "伐斧",
        ["frill"] = "飾り",
        ["gaw"] = "珍品",
        ["gear"] = "歯車",
        ["glaive"] = "グレイブ",
        ["gloves"] = "手袋",
        ["guard"] = "護り",
        ["guise"] = "仮面",
        ["gun"] = "銃",
        ["hatchet"] = "手斧",
        ["helm"] = "兜",
        ["hew"] = "斫斧",
        ["hood"] = "フード",
        ["kris"] = "クリス",
        ["lid"] = "兜",
        ["link"] = "連環",
        ["long arm"] = "長銃",
        ["mail"] = "鎖帷子",
        ["mask"] = "面",
        ["mace"] = "メイス",
        ["mitts"] = "ミット",
        ["muffs"] = "マフ",
        ["orb"] = "宝珠",
        ["organ"] = "器官",
        ["pistol"] = "ピストル",
        ["point"] = "尖端",
        ["rifle"] = "ライフル",
        ["rod"] = "棍",
        ["shield"] = "盾",
        ["shank"] = "シャンク",
        ["shiv"] = "シヴ",
        ["sidearm"] = "サイドアーム",
        ["sneaks"] = "スニーカー",
        ["sphere"] = "球体",
        ["staff"] = "杖",
        ["toy"] = "玩具",
        ["veil"] = "ヴェール",
        ["vest"] = "ベスト",
        ["ward"] = "守り",
        ["ware"] = "ウェア",
        ["wire"] = "ワイヤー",
    };

    private static readonly Dictionary<string, string> BroadItemTypeWords = new(StringComparer.Ordinal)
    {
        ["atlas"] = "地図帳",
        ["bread"] = "パン",
        ["chow"] = "食事",
        ["codex"] = "写本",
        ["feed"] = "飼料",
        ["folio"] = "フォリオ",
        ["lexicon"] = "語彙録",
        ["meat"] = "肉",
        ["omnibus"] = "大全",
        ["opus"] = "作品",
        ["tome"] = "大冊",
        ["volume"] = "巻",
    };

    private static readonly Regex OfThePattern = new(
        "^(?<item>[A-Z][A-Za-z' -]+?) of the (?<descriptor>[A-Z][A-Za-z' -]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RegionPattern = new(
        "^(?<item>[A-Z][A-Za-z' -]+?) of (?<region>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PossessivePattern = new(
        "^(?<owner>[A-Z][A-Za-z' -]+?)(?:'s|') (?<descriptor>[A-Z][A-Za-z' -]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HyphenPairPattern = new(
        "^(?<left>[A-Z][A-Za-z']+)-(?<right>[A-Z][A-Za-z']+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex DescriptorPattern = new(
        "^(?<descriptor>[A-Z][A-Za-z' -]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated, bool includeBroadItemTypes = false)
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source!, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        if (TryTranslateWithoutLeadingArticle(original, includeBroadItemTypes, out translated))
        {
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (!TryTranslateCore(stripped, includeBroadItemTypes, out var translatedCore))
        {
            translated = original;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            original);
        return true;
    }

    private static bool TryTranslateWithoutLeadingArticle(
        string source,
        bool includeBroadItemTypes,
        out string translated)
    {
        if ((source.StartsWith("The ", StringComparison.Ordinal)
                || source.StartsWith("the ", StringComparison.Ordinal))
            && TryTranslate(source.Substring("The ".Length), out translated, includeBroadItemTypes))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCore(string source, bool includeBroadItemTypes, out string translated)
    {
        // Plain names are checked before color stripping; this second pass handles articles exposed only inside color wrappers.
        if ((source.StartsWith("The ", StringComparison.Ordinal)
                || source.StartsWith("the ", StringComparison.Ordinal))
            && TryTranslateCore(source.Substring("The ".Length), includeBroadItemTypes, out translated))
        {
            return true;
        }

        var ofMatch = OfThePattern.Match(source);
        if (ofMatch.Success
            && TryTranslateTitlePhrase(ofMatch.Groups["item"].Value, includeBroadItemTypes, out var item)
            && TryTranslateTitlePhrase(ofMatch.Groups["descriptor"].Value, includeBroadItemTypes, out var descriptor))
        {
            translated = descriptor + "の" + item;
            return true;
        }

        var regionMatch = RegionPattern.Match(source);
        var regionItem = regionMatch.Groups["item"].Value;
        var region = regionMatch.Groups["region"].Value;
        if (regionMatch.Success
            && (!region.StartsWith("the ", StringComparison.Ordinal) || ContainsSpace(regionItem))
            && TryTranslateTitlePhrase(regionItem, includeBroadItemTypes, out item))
        {
            translated = TranslateRegion(region) + "の" + item;
            return true;
        }

        var possessiveMatch = PossessivePattern.Match(source);
        if (possessiveMatch.Success
            && TryTranslateOwner(possessiveMatch.Groups["owner"].Value, out var owner)
            && TryTranslateTitlePhrase(possessiveMatch.Groups["descriptor"].Value, includeBroadItemTypes, out descriptor))
        {
            translated = owner + "の" + descriptor;
            return true;
        }

        var hyphenMatch = HyphenPairPattern.Match(source);
        if (hyphenMatch.Success
            && TryTranslateTitlePhrase(hyphenMatch.Groups["left"].Value, includeBroadItemTypes, out var left)
            && TryTranslateTitlePhrase(hyphenMatch.Groups["right"].Value, includeBroadItemTypes, out var right))
        {
            translated = left + "・" + right;
            return true;
        }

        var descriptorMatch = DescriptorPattern.Match(source);
        if (descriptorMatch.Success
            && TryTranslateTitlePhrase(descriptorMatch.Groups["descriptor"].Value, includeBroadItemTypes, out descriptor)
            && !string.Equals(descriptor, source, StringComparison.Ordinal))
        {
            translated = descriptor;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool ContainsSpace(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == ' ')
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTranslateOwner(string source, out string translated)
    {
        if (TryTranslateTitlePhrase(source, includeBroadItemTypes: false, out translated))
        {
            return true;
        }

        translated = source;
        return true;
    }

    private static string TranslateRegion(string source)
    {
        var trimmed = source.Trim();
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(trimmed);
        if (scoped is not null)
        {
            return scoped;
        }

        var articleless = StringHelpers.StripLeadingEnglishArticle(trimmed);
        scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(articleless);
        if (scoped is not null)
        {
            return scoped;
        }

        scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(articleless, ZoneDisplayDictionaryFile);
        if (scoped is not null)
        {
            return scoped;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(articleless, out var titlePhrase)
            ? titlePhrase
            : articleless;
    }

    private static bool TryTranslateTitlePhrase(string source, bool includeBroadItemTypes, out string translated)
    {
        if (TryTranslateExactComponent(source, includeBroadItemTypes, out translated))
        {
            return true;
        }

        var words = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            translated = source;
            return false;
        }

        var result = string.Empty;
        for (var index = 0; index < words.Length;)
        {
            if (!TryTranslateLongestTitlePhrasePart(words, index, includeBroadItemTypes, out var word, out var consumed))
            {
                translated = source;
                return false;
            }

            result += word;
            index += consumed;
        }

        translated = result;
        return true;
    }

    private static bool TryTranslateLongestTitlePhrasePart(
        string[] words,
        int startIndex,
        bool includeBroadItemTypes,
        out string translated,
        out int consumed)
    {
        for (var length = words.Length - startIndex; length > 1; length--)
        {
            if (TryTranslateExactComponent(string.Join(" ", words, startIndex, length), includeBroadItemTypes, out translated))
            {
                consumed = length;
                return true;
            }
        }

        if (TryTranslateHyphenatedWord(words[startIndex], includeBroadItemTypes, out translated))
        {
            consumed = 1;
            return true;
        }

        consumed = 0;
        translated = words[startIndex];
        return false;
    }

    private static bool TryTranslateHyphenatedWord(string source, bool includeBroadItemTypes, out string translated)
    {
        if (TryTranslateExactComponent(source, includeBroadItemTypes, out translated))
        {
            return true;
        }

        var parts = source.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            translated = source;
            return false;
        }

        var result = string.Empty;
        for (var index = 0; index < parts.Length; index++)
        {
            if (!TryTranslateWord(parts[index], includeBroadItemTypes, out var part))
            {
                translated = source;
                return false;
            }

            if (index > 0)
            {
                result += "・";
            }

            result += part;
        }

        translated = result;
        return true;
    }

    private static bool TryTranslateWord(string source, bool includeBroadItemTypes, out string translated)
    {
        if (TryTranslateExactComponent(source, includeBroadItemTypes, out translated))
        {
            return true;
        }

        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var lower = StringHelpers.LowerAscii(source);

        var direct = Translator.Translate(lower);
        if (!string.Equals(direct, lower, StringComparison.Ordinal))
        {
            translated = direct;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateExactComponent(string source, bool includeBroadItemTypes, out string translated)
    {
        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var lower = StringHelpers.LowerAscii(source);
        if (RelicSpecificWords.TryGetValue(lower, out var relicSpecific))
        {
            translated = relicSpecific;
            return true;
        }

        if (includeBroadItemTypes && BroadItemTypeWords.TryGetValue(lower, out var broadItemType))
        {
            translated = broadItemType;
            return true;
        }

        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(lower);
        if (scoped is not null)
        {
            translated = scoped;
            return true;
        }

        translated = source;
        return false;
    }
}
