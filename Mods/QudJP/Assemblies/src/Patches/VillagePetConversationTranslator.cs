using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class VillagePetConversationTranslator
{
    private static readonly Regex MultipleQuestionPattern = new(
        "^Why are there (?<pet>.+?) here\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SingularQuestionPattern = new(
        "^Why(?: is|'s) there (?<pet>.+?) here\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex NameShowedUpPattern = new(
        "^(?<name>.+?) just showed up one day and started (?<activity>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PronounShowedUpPattern = new(
        "^(?:It|They|He|She) just showed up one day and started (?<activity>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PronounBeenHerePattern = new(
        "^(?:It's|They've|He's|She's|It has|They have|He has|She has) been here for as long as I remember, (?<activity>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex NameBeenHerePattern = new(
        "^(?<name>.+?) (?<has>has|have) been here for as long as I remember, (?<activity>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex WhoKnowsPattern = new(
        "^(?<name>.+?)\\? Who knows\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AskYourselfPattern = new(
        "^Ask (?<them>.+?) yourself\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FindOrePattern = new(
        "^Perhaps (?<they1>.+?) thought (?<they2>.+?) could find (?<ore>.+?) here\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LoveOfPattern = new(
        "^Oh, (?<name>.+?)\\? I assume because of (?<their>.+?) love of (?<sacred>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly IReadOnlyDictionary<string, string> CaptureTerms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["glowfish"] = "グロウフィッシュ",
            ["albino ape"] = "アルビノ類人猿",
            ["gold"] = "金",
        };

    private static readonly IReadOnlyDictionary<string, string> StartedActivities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["singing"] = "歌い始めた",
            ["guarding the gate"] = "門を守り始めた",
        };

    private static readonly IReadOnlyDictionary<string, string> OngoingActivities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["breaking bread"] = "パンを分け合っている",
            ["barking"] = "吠えている",
        };

    internal static bool TryTranslateQuestion(string? source, out string translated)
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
        if (TryTranslatePattern(MultipleQuestionPattern, stripped, spans, original, match =>
                "なぜここに" + TranslateVisibleCapture(DropEnglishArticle(Restore(match, spans, "pet"))) + "がいるのだ？",
                out translated)
            || TryTranslatePattern(SingularQuestionPattern, stripped, spans, original, match =>
                "なぜここに" + TranslateVisibleCapture(DropEnglishArticle(Restore(match, spans, "pet"))) + "がいるのだ？",
                out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    internal static bool TryTranslateAnswer(string? source, out string translated)
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
        if (TryTranslatePattern(PronounShowedUpPattern, stripped, spans, original, match =>
                BuildStartedActivity(Restore(match, spans, "activity")) is { } activity
                    ? "ある日ふらりと現れ、" + activity + "んだ。"
                    : null,
                out translated)
            || TryTranslatePattern(NameShowedUpPattern, stripped, spans, original, match =>
                BuildStartedActivity(Restore(match, spans, "activity")) is { } activity
                    ? Restore(match, spans, "name") + "はある日ふらりと現れ、" + activity + "んだ。"
                    : null,
                out translated)
            || TryTranslatePattern(NameBeenHerePattern, stripped, spans, original, match =>
                BuildOngoingActivity(Restore(match, spans, "activity")) is { } activity
                    ? Restore(match, spans, "name") + "は私が覚えている限りずっとここにいて、" + activity + "。"
                    : null,
                out translated)
            || TryTranslatePattern(PronounBeenHerePattern, stripped, spans, original, match =>
                BuildOngoingActivity(Restore(match, spans, "activity")) is { } activity
                    ? "私が覚えている限りずっとここにいて、" + activity + "。"
                    : null,
                out translated)
            || TryTranslatePattern(WhoKnowsPattern, stripped, spans, original, match =>
                Restore(match, spans, "name") + "？ 誰にわかるものか。",
                out translated)
            || TryTranslatePattern(AskYourselfPattern, stripped, spans, original, _ => "直接聞いてみなさい。",
                out translated)
            || TryTranslatePattern(FindOrePattern, stripped, spans, original, match =>
                "おそらく、ここで" + TranslateVisibleCapture(Restore(match, spans, "ore")) + "を見つけられると思ったのだろう。",
                out translated)
            || TryTranslatePattern(LoveOfPattern, stripped, spans, original, match =>
                "ああ、" + Restore(match, spans, "name") + "か。" + TranslateCapture(match, spans, "sacred") + "への愛ゆえだと思う。",
                out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        Func<Match, string?> build,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var built = build(match);
        if (built is null)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            built,
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

    private static string TranslateCaptureVisible(string source)
    {
        var trimmed = source.Trim();
        var articleless = StringHelpers.StripLeadingEnglishArticle(trimmed);
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(trimmed);
        if (scoped is not null)
        {
            return scoped;
        }

        scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(articleless);
        if (scoped is not null)
        {
            return scoped;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(articleless, out var titlePhrase)
            ? titlePhrase
            : TranslateVisibleCapture(articleless);
    }

    private static string? BuildStartedActivity(string source)
    {
        return StartedActivities.TryGetValue(source.Trim(), out var translated)
            ? translated
            : null;
    }

    private static string? BuildOngoingActivity(string source)
    {
        return OngoingActivities.TryGetValue(source.Trim(), out var translated)
            ? translated
            : null;
    }

    private static string TranslateVisibleCapture(string source)
    {
        return CaptureTerms.TryGetValue(source.Trim(), out var translated)
            ? translated
            : source;
    }

    private static string DropEnglishArticle(string source)
    {
        var value = source.Trim();
        if (value.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(3).TrimStart();
        }

        if (value.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(2).TrimStart();
        }

        if (value.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(4).TrimStart();
        }

        return value;
    }
}
