using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DynamicQuestGeneratedQuestTextTranslator
{
    private static readonly string[] CaptureDictionaryFiles =
    [
        "Scoped/historyspice-common.ja.json",
        "ui-zone-display.ja.json",
    ];

    private static readonly (string FileName, string Context)[] ContextualCaptureDictionaries =
    [
        ("ui-journal-chronology.ja.json", "Qud.UI.Qud.API.JournalAPI"),
        ("ui-journal.ja.json", "Qud.UI.XRL.UI.JournalScreen"),
        ("ui-journal.ja.json", "Qud.UI.Qud.UI.JournalLineData"),
        ("world-factions.ja.json", "Faction.Name"),
    ];

    private static readonly Regex HelpingFindTitlePattern = new(
        "^(?<helping>Helping|Assisting|Aiding) (?<giver>.+?) to Find (?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HelpingTitlePattern = new(
        "^(?<helping>Helping|Assisting|Aiding) (?<giver>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SanctityTitlePattern = new(
        "^The (?<concept>sanctity|divinity|purity|goodness|wisdom|virtue) of (?<sacred>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex FindNamePattern = new(
        "^Find (?<target>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex VisitNamePattern = new(
        "^Visit (?<target>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LocateNamePattern = new(
        "^Locate (?<target>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RecoverItemNamePattern = new(
        "^Recover (?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ReturnItemNamePattern = new(
        "^Return (?<item>.+?) to (?<target>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ReturnNamePattern = new(
        "^Return to (?<target>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex VerbItemNamePattern = new(
        "^(?<verb>Open|Close|Enter|Sleep In|Sleep On|Sit On|Put Something In|Put Something On|Drink From|Cook At|Smoke From|Pray At|Desecrate) (?<item>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LocateAtTextPattern = new(
        "^Locate (?<item>.+?) at (?<target>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LocateWithinTextPattern = new(
        "^Locate (?<site>.+?), located within (?<max>[0-9]+) parasangs of (?<landmark>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LocateNextToTextPattern = new(
        "^Locate (?<site>.+?), located next to (?<landmark>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LocateDirectionTextPattern = new(
        "^Locate (?<site>.+?), located (?<range>[0-9]+-[0-9]+) parasangs (?<direction>north|south|east|west) of (?<landmark>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LocatePathTextPattern = new(
        "^Locate (?<site>.+?), located (?<direction>north|south|east|west) along the (?<path>.+?) that runs through (?<landmark>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ReturnItemTextPattern = new(
        "^Return (?<item>.+?) to (?<target>.+?) and speak (?:with|to) (?<giver>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ReturnTextPattern = new(
        "^Return to (?<target>.+?) and speak to (?<giver>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TravelVerbTextPattern = new(
        "^Travel to (?<target>.+?) and (?<verb>open|close|enter|sleep in|sleep on|sit on|put something in|put something on|drink from|cook at|smoke from|pray at|desecrate) (?<item>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TravelHistoricalSiteTextPattern = new(
        "^Travel to the historical site of (?<target>.+?)(?:\\.)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RecoverAtTextPattern = new(
        "^Recover (?<item>.+?) at (?<target>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex GiftRelicNamePattern = new(
        "^(?<quality>.+?), the Gift of (?<domain>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex OfPhrasePattern = new(
        "^(?<head>.+?) of (?<tail>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

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
        if (TryTranslateStepText(stripped, spans, original, out translated)
            || TryTranslateTitleOrName(stripped, spans, original, out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslateTitleOrName(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var captureSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        if (TryTranslatePattern(HelpingFindTitlePattern, stripped, spans, source, match =>
                Restore(match, captureSpans, "giver") + "が" + TranslateCapture(match, captureSpans, "item") + "を探すのを助ける",
                out translated)
            || TryTranslatePattern(HelpingTitlePattern, stripped, spans, source, match =>
                Restore(match, captureSpans, "giver") + "を助ける",
                out translated)
            || TryTranslatePattern(SanctityTitlePattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "sacred") + "の" + TranslateConcept(match.Groups["concept"].Value),
                out translated)
            || TryTranslatePattern(FindNamePattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "を探す",
                out translated)
            || TryTranslatePattern(VisitNamePattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "を訪問",
                out translated)
            || TryTranslatePattern(LocateNamePattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "を見つける",
                out translated)
            || TryTranslatePattern(RecoverItemNamePattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "item") + "を取り戻す",
                out translated)
            || TryTranslatePattern(ReturnItemNamePattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "item") + "を" + TranslateCapture(match, captureSpans, "target") + "へ返す",
                out translated)
            || TryTranslatePattern(ReturnNamePattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "へ戻る",
                out translated)
            || TryTranslatePattern(VerbItemNamePattern, stripped, spans, source, match =>
                BuildVerbObjectPhrase(StringHelpers.LowerAscii(match.Groups["verb"].Value), TranslateCapture(match, captureSpans, "item")),
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateStepText(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var captureSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        if (TryTranslatePattern(LocateAtTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "で" + TranslateCapture(match, captureSpans, "item") + "を見つける。",
                out translated)
            || TryTranslatePattern(LocateWithinTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "site") + "を見つける。" + TranslateCapture(match, captureSpans, "landmark") + "から"
                + match.Groups["max"].Value + "パラサング以内にある。",
                out translated)
            || TryTranslatePattern(LocateNextToTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "site") + "を見つける。" + TranslateCapture(match, captureSpans, "landmark") + "の隣にある。",
                out translated)
            || TryTranslatePattern(LocateDirectionTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "site") + "を見つける。" + TranslateCapture(match, captureSpans, "landmark") + "から"
                + match.Groups["range"].Value + "パラサング" + TranslateDirection(match.Groups["direction"].Value) + "にある。",
                out translated)
            || TryTranslatePattern(LocatePathTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "site") + "を見つける。" + TranslateCapture(match, captureSpans, "landmark") + "を通る"
                + TranslateCapture(match, captureSpans, "path") + "に沿って" + TranslateDirection(match.Groups["direction"].Value) + "にある。",
                out translated)
            || TryTranslatePattern(ReturnItemTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "item") + "を" + TranslateCapture(match, captureSpans, "target") + "へ返し、"
                + Restore(match, captureSpans, "giver") + "と話す。",
                out translated)
            || TryTranslatePattern(ReturnTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "へ戻り、" + Restore(match, captureSpans, "giver") + "と話す。",
                out translated)
            || TryTranslatePattern(TravelVerbTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "へ行き、"
                + BuildVerbObjectPhrase(match.Groups["verb"].Value, TranslateCapture(match, captureSpans, "item")) + "。",
                out translated)
            || TryTranslatePattern(TravelHistoricalSiteTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "の史跡へ向かう。",
                out translated)
            || TryTranslatePattern(RecoverAtTextPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "target") + "で" + TranslateCapture(match, captureSpans, "item") + "を取り戻す。",
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        Func<Match, string> build,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            build(match),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string Restore(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
    }

    private static string TranslateCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var translated = TranslateCaptureVisible(group.Value);
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(translated, spans, group).Trim();
    }

    internal static string TranslateCaptureVisible(string source)
    {
        var trimmed = source.Trim();
        if (TryTranslateGiftRelicName(trimmed, out var giftRelic))
        {
            return giftRelic;
        }

        if (TryTranslateCommaSeparatedCapture(trimmed, out var commaSeparated))
        {
            return commaSeparated;
        }

        if (TryTranslateOfPhrase(trimmed, out var ofPhrase))
        {
            return ofPhrase;
        }

        if (TryTranslateCompactSuffixCapture(trimmed, out var compactSuffix))
        {
            return compactSuffix;
        }

        if (TryTranslateSpecialCapture(trimmed, out var special))
        {
            return special;
        }

        var stripped = StringHelpers.StripLeadingEnglishArticle(trimmed);
        if (TryTranslateSpecialCapture(stripped, out special))
        {
            return special;
        }

        if (TryTranslateKnownCapture(trimmed, out var exact))
        {
            return exact;
        }

        if (TryTranslateKnownCapture(stripped, out exact))
        {
            return exact;
        }

        if (TryTranslateSpaceSeparatedCapture(stripped, out var spaceSeparated))
        {
            return spaceSeparated;
        }

        if (ContainsJapaneseCharacters(stripped))
        {
            return stripped;
        }

        if (MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(trimmed, nameof(DynamicQuestGeneratedQuestTextTranslator), out var zone)
            && !string.Equals(zone, trimmed, StringComparison.Ordinal))
        {
            return zone;
        }

        if (MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(stripped, nameof(DynamicQuestGeneratedQuestTextTranslator), out zone)
            && !string.Equals(zone, stripped, StringComparison.Ordinal))
        {
            return zone;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(stripped, out var titlePhrase)
            ? titlePhrase
            : stripped;
    }

    private static bool ContainsJapaneseCharacters(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= '\u3040' && character <= '\u30ff')
                || (character >= '\u3400' && character <= '\u9fff'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTranslateGiftRelicName(string source, out string translated)
    {
        var match = GiftRelicNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = TranslateCaptureVisible(match.Groups["domain"].Value) + "の"
            + TranslateCaptureVisible(match.Groups["quality"].Value) + "賜物";
        return true;
    }

    private static bool TryTranslateCommaSeparatedCapture(string source, out string translated)
    {
        var parts = source.Split(new[] { ", " }, StringSplitOptions.None);
        if (parts.Length <= 1)
        {
            translated = source;
            return false;
        }

        var changed = false;
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            var translatedPart = TranslateCaptureVisible(part);
            if (!string.Equals(translatedPart, part, StringComparison.Ordinal))
            {
                changed = true;
            }

            parts[index] = translatedPart;
        }

        translated = string.Join(", ", parts);
        return changed;
    }

    private static bool TryTranslateOfPhrase(string source, out string translated)
    {
        var match = OfPhrasePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var head = TranslateCaptureVisible(match.Groups["head"].Value);
        var tail = TranslateCaptureVisible(match.Groups["tail"].Value);
        if (string.Equals(head, match.Groups["head"].Value, StringComparison.Ordinal)
            && string.Equals(tail, match.Groups["tail"].Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = tail + "の" + head;
        return true;
    }

    private static bool TryTranslateCompactSuffixCapture(string source, out string translated)
    {
        const string WoeSuffix = "woe";
        if (source.Length <= WoeSuffix.Length
            || !source.EndsWith(WoeSuffix, StringComparison.OrdinalIgnoreCase)
            || !HistorySpiceComponentLookup.TryTranslateWord(WoeSuffix, out var translatedSuffix))
        {
            translated = source;
            return false;
        }

        var root = source.Substring(0, source.Length - WoeSuffix.Length);
        if (root.Length == 0)
        {
            translated = source;
            return false;
        }

        translated = TranslateCaptureVisible(root) + "の" + translatedSuffix;
        return true;
    }

    private static bool TryTranslateSpaceSeparatedCapture(string source, out string translated)
    {
        var words = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1)
        {
            translated = source;
            return false;
        }

        var changed = false;
        for (var index = 0; index < words.Length; index++)
        {
            var word = words[index];
            var translatedWord = TranslateCaptureVisible(word);
            if (!string.Equals(translatedWord, word, StringComparison.Ordinal))
            {
                changed = true;
            }

            words[index] = translatedWord;
        }

        translated = string.Concat(words);
        return changed;
    }

    private static bool TryTranslateSpecialCapture(string source, out string translated)
    {
        translated = StringHelpers.LowerAscii(source) switch
        {
            "joppa" => "ジョッパ",
            "the six day stilt" or "six day stilt" => "六日のスティルト",
            "the mechanimists" or "mechanimists" => "メカニマス教団",
            "rust wells" => "錆の井戸",
            "hidden archive" => "隠された文書庫",
            "salt shrine" => "塩の祠",
            "the sanctity of salt" or "sanctity of salt" => "塩の聖性",
            "tending hearths" => "炉の世話",
            "breaking bread" => "パンを分け合っている",
            "cooking" => "料理",
            "patience" => "忍耐",
            "sacred vessel" => "聖なる器",
            "scheme" => "計画",
            "rusted relic" => "錆びた遺物",
            "relic" => "遺物",
            _ => source,
        };
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateKnownCapture(string source, out string translated)
    {
        var exact = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, CaptureDictionaryFiles);
        if (exact is not null)
        {
            translated = exact;
            return true;
        }

        for (var index = 0; index < ContextualCaptureDictionaries.Length; index++)
        {
            var (fileName, context) = ContextualCaptureDictionaries[index];
            exact = ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly(source, context, fileName);
            if (exact is not null)
            {
                translated = exact;
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static string TranslateConcept(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "sanctity" => "聖性",
            "divinity" => "神性",
            "purity" => "純粋さ",
            "goodness" => "善性",
            "wisdom" => "叡智",
            "virtue" => "徳",
            _ => source,
        };
    }

    private static string TranslateDirection(string source)
    {
        return source switch
        {
            "north" => "北",
            "south" => "南",
            "east" => "東",
            "west" => "西",
            _ => source,
        };
    }

    private static string BuildVerbObjectPhrase(string verb, string item)
    {
        return verb switch
        {
            "open" => item + "を開ける",
            "close" => item + "を閉じる",
            "enter" => item + "に入る",
            "sleep in" or "sleep on" => item + "で眠る",
            "sit on" => item + "に座る",
            "put something in" => item + "に何かを入れる",
            "put something on" => item + "に何かを置く",
            "drink from" => item + "から飲む",
            "cook at" => item + "で料理する",
            "smoke from" => item + "で喫煙する",
            "pray at" => item + "で祈る",
            "desecrate" => item + "を冒涜する",
            _ => verb + " " + item,
        };
    }
}
