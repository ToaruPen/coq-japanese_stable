using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DynamicQuestExplicitConversationTextTranslator
{
    private static readonly Regex AcceptFindPattern = new(
        "^Yes\\. I will find (?<item>.+?) as you ask\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AcceptLocatePattern = new(
        "^Yes\\. I will locate (?<site>.+?) as you ask\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AcceptVerbPattern = new(
        "^Yes\\. I will (?<verb>open|close|enter|sleep in|sleep on|sit on|put something in|put something on|drink from|cook at|smoke from|pray at|desecrate) (?<item>.+?) as you ask\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AlreadyKnowSitePattern = new(
        "^I already know where (?<site>.+?) is\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FoundPattern = new(
        "^I've found (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex DontHavePattern = new(
        "^I don't have (?<item>.+?) yet\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LocatedPattern = new(
        "^I've located (?<site>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HaventLocatedPattern = new(
        "^I haven't located (?<site>.+?) yet\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex DidVerbPattern = new(
        "^I've (?<verb>opened|closed|entered|slept in|slept on|sat on|put something in|put something on|drunk from|cooked at|smoked from|prayed at|desecrated) (?<item>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HaventVerbPattern = new(
        "^I haven't (?<verb>opened|closed|entered|slept in|slept on|sat on|put something in|put something on|drunk from|cooked at|smoked from|prayed at|desecrated) (?<item>.+?) yet\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (TryTranslateCore(stripped, spans, original, out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslateCore(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        if (string.Equals(stripped, "No, I will not.", StringComparison.Ordinal))
        {
            translated = RestoreWhole("いや、断る。", spans, stripped.Length, source);
            return true;
        }

        var captureSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        if (TryTranslatePattern(AcceptFindPattern, stripped, spans, source, match =>
                "はい。頼まれたとおり" + TranslateCapture(match, captureSpans, "item") + "を探す。",
                out translated)
            || TryTranslatePattern(AcceptLocatePattern, stripped, spans, source, match =>
                "はい。頼まれたとおり" + TranslateCapture(match, captureSpans, "site") + "を特定する。",
                out translated)
            || TryTranslatePattern(AcceptVerbPattern, stripped, spans, source, match =>
                "はい。頼まれたとおり"
                + BuildVerbObjectPhrase(match.Groups["verb"].Value, TranslateCapture(match, captureSpans, "item")) + "。",
                out translated)
            || TryTranslatePattern(AlreadyKnowSitePattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "site") + "がどこにあるか既に知っている。",
                out translated)
            || TryTranslatePattern(FoundPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "item") + "を見つけた。",
                out translated)
            || TryTranslatePattern(DontHavePattern, stripped, spans, source, match =>
                "まだ" + TranslateCapture(match, captureSpans, "item") + "を持っていない。",
                out translated)
            || TryTranslatePattern(LocatedPattern, stripped, spans, source, match =>
                TranslateCapture(match, captureSpans, "site") + "を特定した。",
                out translated)
            || TryTranslatePattern(HaventLocatedPattern, stripped, spans, source, match =>
                "まだ" + TranslateCapture(match, captureSpans, "site") + "を特定していない。",
                out translated)
            || TryTranslatePattern(DidVerbPattern, stripped, spans, source, match =>
                BuildVerbObjectPastPhrase(PresentVerb(match.Groups["verb"].Value), TranslateCapture(match, captureSpans, "item")) + "。",
                out translated)
            || TryTranslatePattern(HaventVerbPattern, stripped, spans, source, match =>
                "まだ" + BuildVerbObjectNegativePhrase(PresentVerb(match.Groups["verb"].Value), TranslateCapture(match, captureSpans, "item")) + "。",
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

        translated = RestoreWhole(build(match), spans, stripped.Length, source);
        return true;
    }

    private static string TranslateCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var translated = DynamicQuestGeneratedQuestTextTranslator.TranslateCaptureVisible(group.Value);
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(translated, spans, group).Trim();
    }

    private static string RestoreWhole(string translated, IReadOnlyList<ColorSpan> spans, int sourceLength, string source)
    {
        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            sourceLength,
            source);
    }

    private static string PresentVerb(string source)
    {
        return source switch
        {
            "opened" => "open",
            "closed" => "close",
            "entered" => "enter",
            "slept in" => "sleep in",
            "slept on" => "sleep on",
            "sat on" => "sit on",
            "drunk from" => "drink from",
            "cooked at" => "cook at",
            "smoked from" => "smoke from",
            "prayed at" => "pray at",
            "desecrated" => "desecrate",
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

    private static string BuildVerbObjectPastPhrase(string verb, string item)
    {
        return verb switch
        {
            "open" => item + "を開けた",
            "close" => item + "を閉じた",
            "enter" => item + "に入った",
            "sleep in" or "sleep on" => item + "で眠った",
            "sit on" => item + "に座った",
            "put something in" => item + "に何かを入れた",
            "put something on" => item + "に何かを置いた",
            "drink from" => item + "から飲んだ",
            "cook at" => item + "で料理した",
            "smoke from" => item + "で喫煙した",
            "pray at" => item + "で祈った",
            "desecrate" => item + "を冒涜した",
            _ => verb + " " + item,
        };
    }

    private static string BuildVerbObjectNegativePhrase(string verb, string item)
    {
        return verb switch
        {
            "open" => item + "を開けていない",
            "close" => item + "を閉じていない",
            "enter" => item + "に入っていない",
            "sleep in" or "sleep on" => item + "で眠っていない",
            "sit on" => item + "に座っていない",
            "put something in" => item + "に何かを入れていない",
            "put something on" => item + "に何かを置いていない",
            "drink from" => item + "から飲んでいない",
            "cook at" => item + "で料理していない",
            "smoke from" => item + "で喫煙していない",
            "pray at" => item + "で祈っていない",
            "desecrate" => item + "を冒涜していない",
            _ => verb + " " + item,
        };
    }
}
