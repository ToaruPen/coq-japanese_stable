using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class VillageLeaderConversationTranslator
{
    private static readonly Regex WardenPattern = new(
        "^(?<frame>Watch yourself|Live and drink|Stay out of trouble|I'm watching you), (?<traveler>adventurer|wanderer|traveler|drifter|nomad|friend)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex MayorWelcomeFirstPattern = new(
        "^Welcome to the village of (?<village>.+?), (?<traveler>adventurer|wanderer|traveler|drifter|nomad|friend)\\. (?<middle>.+?)\\. (?<tail>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex MayorTravelerFirstPattern = new(
        "^(?<traveler>adventurer|wanderer|traveler|drifter|nomad|friend), welcome to the village of (?<village>.+?)\\. (?<middle>.+?)\\. (?<tail>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ClanMiddlePattern = new(
        "^we are a (?<clan>people|clan|tribe|society) who (?<love>love|revere|honor|worship|cherish|venerate|esteem|treasure|pay homage to) (?<sacred>.+?) and (?<abhor>abhor|detest|denounce|scorn|dishonor) (?<profane>.+?)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
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
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (TryTranslateWarden(stripped, spans, original, out translated)
            || TryTranslateMayor(MayorWelcomeFirstPattern, stripped, spans, original, out translated)
            || TryTranslateMayor(MayorTravelerFirstPattern, stripped, spans, original, out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslateWarden(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = WardenPattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var traveler = TranslateTraveler(match.Groups["traveler"].Value);
        var core = match.Groups["frame"].Value switch
        {
            "Watch yourself" => traveler + "よ、身の振り方には気をつけろ。",
            "Live and drink" => "生きて飲め、" + traveler + "。",
            "Stay out of trouble" => "面倒は起こすな、" + traveler + "。",
            "I'm watching you" => "見張っているぞ、" + traveler + "。",
            _ => source,
        };

        translated = RestoreWhole(core, spans, stripped.Length, source);
        return !string.Equals(core, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateMayor(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success
            || !TryTranslateMayorMiddle(match.Groups["middle"].Value, spans, match.Groups["middle"].Index, out var middle)
            || !TryTranslateMayorTail(match.Groups["tail"].Value, out var tail))
        {
            translated = source;
            return false;
        }

        var village = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["village"].Value,
            spans,
            match.Groups["village"]).Trim();
        var traveler = TranslateTraveler(match.Groups["traveler"].Value);
        translated = RestoreWhole(
            traveler + "よ、" + village + "の村へようこそ。" + middle + "。" + tail + "。",
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateMayorMiddle(
        string middle,
        IReadOnlyList<ColorSpan> spans,
        int middleStart,
        out string translated)
    {
        if (string.Equals(
                middle,
                "Here you will find shade and vittle, along with other provisions to help you better scour the rust-caves for treasure",
                StringComparison.OrdinalIgnoreCase))
        {
            translated = "ここには日陰と食べ物があり、錆の洞窟で宝を探す助けになる備えもある";
            return true;
        }

        var match = ClanMiddlePattern.Match(middle);
        if (!match.Success)
        {
            translated = middle;
            return false;
        }

        var sacred = TranslateCapture(match, spans, middleStart, "sacred");
        var profane = TranslateCapture(match, spans, middleStart, "profane");
        translated = "われらは" + sacred + "を" + TranslateLove(match.Groups["love"].Value) + "、"
            + profane + "を" + TranslateAbhor(match.Groups["abhor"].Value) + TranslateClan(match.Groups["clan"].Value) + "だ";
        return true;
    }

    private static bool TryTranslateMayorTail(string tail, out string translated)
    {
        const string suffix = ", you may drink of our freshwater and quench your thirst";
        if (!tail.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            translated = tail;
            return false;
        }

        var prefix = tail.Substring(0, tail.Length - suffix.Length);
        var prefixJa = TranslateTailPrefix(prefix);
        if (prefixJa is null)
        {
            translated = tail;
            return false;
        }

        translated = prefixJa + "、われらの真水を飲み、渇きを癒してよい";
        return true;
    }

    private static string? TranslateTailPrefix(string prefix)
    {
        if (string.Equals(prefix, "above all else", StringComparison.OrdinalIgnoreCase))
        {
            return "何よりも";
        }

        if (string.Equals(prefix, "come what may", StringComparison.OrdinalIgnoreCase))
        {
            return "何があろうと";
        }

        if (string.Equals(prefix, "as long as you are respectful", StringComparison.OrdinalIgnoreCase))
        {
            return "敬意を払う限り";
        }

        return string.Equals(prefix, "per our custom", StringComparison.OrdinalIgnoreCase) ? "われらの習わしにより" : null;
    }

    private static string RestoreWhole(string core, IReadOnlyList<ColorSpan> spans, int strippedLength, string source) =>
        ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            core,
            spans,
            strippedLength,
            source);

    private static string TranslateTraveler(string source) => source switch
    {
        "adventurer" => "冒険者",
        "wanderer" => "放浪者",
        "traveler" => "旅人",
        "drifter" => "流れ者",
        "nomad" => "遊牧民",
        "friend" => "友",
        _ => source,
    };

    private static string TranslateCapture(Match match, IReadOnlyList<ColorSpan> spans, int offset, string groupName)
    {
        var group = match.Groups[groupName];
        var trimmed = group.Value.Trim();
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(trimmed);
        if (scoped is not null)
        {
            return RestoreCapture(scoped, spans, offset + group.Index, group.Length).Trim();
        }

        var articleless = StringHelpers.StripLeadingEnglishArticle(trimmed);
        scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(articleless);
        if (scoped is not null)
        {
            return RestoreCapture(scoped, spans, offset + group.Index, group.Length).Trim();
        }

        if (HistorySpiceComponentLookup.TryTranslateTitlePhrase(articleless, out var titlePhrase))
        {
            return RestoreCapture(titlePhrase, spans, offset + group.Index, group.Length).Trim();
        }

        return RestoreCapture(trimmed, spans, offset + group.Index, group.Length).Trim();
    }

    private static string RestoreCapture(string value, IReadOnlyList<ColorSpan> spans, int startIndex, int length)
    {
        if (spans.Count == 0)
        {
            return value;
        }

        var captureSpans = ColorCodePreserver.SliceSpans(spans, startIndex, length);
        captureSpans.AddRange(ColorCodePreserver.SliceAdjacentCaptureBoundarySpans(spans, startIndex, length));
        return ColorAwareTranslationComposer.Restore(value, captureSpans);
    }

    private static string TranslateClan(string source) => source switch
    {
        "people" => "民",
        "clan" => "氏族",
        "tribe" => "部族",
        "society" => "共同体",
        _ => source,
    };

    private static string TranslateLove(string source) => source switch
    {
        "love" => "愛し",
        "revere" => "崇敬し",
        "honor" => "敬い",
        "worship" => "崇拝し",
        "cherish" => "大切にし",
        "venerate" => "崇め",
        "esteem" => "尊び",
        "treasure" => "宝とし",
        "pay homage to" => "敬意を払い",
        _ => source,
    };

    private static string TranslateAbhor(string source) => source switch
    {
        "abhor" => "忌む",
        "detest" => "嫌悪する",
        "denounce" => "糾弾する",
        "scorn" => "軽蔑する",
        "dishonor" => "辱める",
        _ => source,
    };
}
