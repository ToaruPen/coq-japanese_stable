using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DynamicQuestConversationTextTranslator
{
    private const string RewardTail =
        " Our village owes you a debt. For now, please choose a reward from our stockpile as payment for your service.";

    private const string RecoilerTail =
        " You've proven =player.reflexive= a friend to our village. Take this recoiler and return whenever your throat is dry.";

    private static readonly Regex OurThanksRewardPattern = new(
        "^Our thanks, (?<traveler>.+?)\\." + Regex.Escape(RewardTail) + "$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ThankYouRewardPattern = new(
        "^Thank you for your service, (?<traveler>.+?)\\." + Regex.Escape(RewardTail) + "$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex YouHaveThanksRewardPattern = new(
        "^(?<traveler>.+?), you have our thanks\\." + Regex.Escape(RewardTail) + "$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex OurThanksRecoilerPattern = new(
        "^Our thanks, (?<traveler>.+?)\\." + Regex.Escape(RecoilerTail) + "$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ThankYouRecoilerPattern = new(
        "^Thank you for your service, (?<traveler>.+?)\\." + Regex.Escape(RecoilerTail) + "$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex YouHaveThanksRecoilerPattern = new(
        "^(?<traveler>.+?), you have our thanks\\." + Regex.Escape(RecoilerTail) + "$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private const string SpeakSignpostPrefix = "話す相手：";

    private const string FindSignpostPrefix = "探す相手：";

    private const string DirectionAlternation =
        "northeast|northwest|southeast|southwest|north|south|east|west";

    private const string LocationPhraseAlternation =
        "to the (?:" + DirectionAlternation + ")|here|somewhere|above|below";

    private static readonly Regex LocationPhrasePattern = new(
        "(?:also )?(?<location>" + LocationPhraseAlternation + ")",
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
        if (TryTranslateIntro(stripped, spans, original, out translated)
            || TryTranslatePattern(OurThanksRewardPattern, stripped, spans, original, match =>
                RestoreTraveler(match, spans) + "よ、感謝する。われらの村はあなたに借りがある。今は奉仕への報酬として、備蓄から褒美を選んでほしい。",
                out translated)
            || TryTranslatePattern(ThankYouRewardPattern, stripped, spans, original, match =>
                "奉仕に感謝する、" + RestoreTraveler(match, spans) + "。われらの村はあなたに借りがある。今は奉仕への報酬として、備蓄から褒美を選んでほしい。",
                out translated)
            || TryTranslatePattern(YouHaveThanksRewardPattern, stripped, spans, original, match =>
                RestoreTraveler(match, spans) + "よ、感謝する。われらの村はあなたに借りがある。今は奉仕への報酬として、備蓄から褒美を選んでほしい。",
                out translated)
            || TryTranslatePattern(OurThanksRecoilerPattern, stripped, spans, original, match =>
                RestoreTraveler(match, spans) + "よ、感謝する。あなたは=player.reflexive=をわれらの村の友だと示した。このリコイラーを受け取り、喉が渇いたときはいつでも戻ってきてほしい。",
                out translated)
            || TryTranslatePattern(ThankYouRecoilerPattern, stripped, spans, original, match =>
                "奉仕に感謝する、" + RestoreTraveler(match, spans) + "。あなたは=player.reflexive=をわれらの村の友だと示した。このリコイラーを受け取り、喉が渇いたときはいつでも戻ってきてほしい。",
                out translated)
            || TryTranslatePattern(YouHaveThanksRecoilerPattern, stripped, spans, original, match =>
                RestoreTraveler(match, spans) + "よ、感謝する。あなたは=player.reflexive=をわれらの村の友だと示した。このリコイラーを受け取り、喉が渇いたときはいつでも戻ってきてほしい。",
                out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslateIntro(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        var translatedCore = stripped switch
        {
            "I'm looking for work." => "仕事を探している。",
            "Do you have work that needs doing?" => "何かやるべき仕事はあるか？",
            "My services are available if you have work to offer." => "仕事があるなら、力を貸せる。",
            "Is there work around here?" => "この辺りに仕事はあるか？",
            "Speak to " => SpeakSignpostPrefix,
            "Talk to " => SpeakSignpostPrefix,
            "Find " => FindSignpostPrefix,
            _ => null,
        };

        if (TryTranslateSignpostDirectionList(stripped, spans, out var signpostDirectionList))
        {
            translatedCore = signpostDirectionList;
        }

        if (translatedCore is null)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateSignpostDirectionList(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        out string translated)
    {
        if (!TryGetSignpostPrefix(stripped, out var prefix, out var listStart)
            || !stripped.EndsWith(".", StringComparison.Ordinal))
        {
            translated = stripped;
            return false;
        }

        var list = stripped.Substring(listStart, stripped.Length - listStart - 1);
        var translatedEntries = new List<string>();
        var index = 0;
        while (index < list.Length)
        {
            if (!TryReadSignpostEntry(list, spans, listStart, index, out var translatedEntry, out var nextIndex))
            {
                translated = stripped;
                return false;
            }

            translatedEntries.Add(translatedEntry);
            index = nextIndex;
            if (index == list.Length)
            {
                break;
            }

            if (StartsWithAt(list, ", or ", index))
            {
                index += ", or ".Length;
            }
            else if (StartsWithAt(list, " or ", index))
            {
                index += " or ".Length;
            }
            else if (StartsWithAt(list, ", ", index))
            {
                index += ", ".Length;
            }
            else
            {
                translated = stripped;
                return false;
            }
        }

        if (translatedEntries.Count == 0)
        {
            translated = stripped;
            return false;
        }

        translated = string.Join("、または", translatedEntries)
            + (prefix == SpeakSignpostPrefix ? "と話す。" : "を探す。");
        return true;
    }

    private static bool TryReadSignpostEntry(
        string list,
        IReadOnlyList<ColorSpan> spans,
        int listStart,
        int index,
        out string translated,
        out int nextIndex)
    {
        var commaIndex = list.IndexOf(", ", index, StringComparison.Ordinal);
        var orIndex = list.IndexOf(" or ", index, StringComparison.Ordinal);
        if (orIndex >= 0 && (commaIndex < 0 || orIndex < commaIndex))
        {
            return TryBuildBareSignpostEntry(list, spans, listStart, index, orIndex, out translated, out nextIndex);
        }

        if (commaIndex < 0)
        {
            return TryBuildBareSignpostEntry(list, spans, listStart, index, list.Length, out translated, out nextIndex);
        }

        var locationStart = commaIndex + ", ".Length;
        var locationMatch = LocationPhrasePattern.Match(list, locationStart);
        if (locationMatch.Success
            && locationMatch.Index == locationStart
            && IsSignpostEntryBoundary(list, locationMatch.Index + locationMatch.Length))
        {
            var target = RestoreSignpostTarget(list, spans, listStart, index, commaIndex - index);
            translated = TranslateLocationPhrase(locationMatch.Groups["location"].Value, target);
            nextIndex = locationMatch.Index + locationMatch.Length;
            return true;
        }

        return TryBuildBareSignpostEntry(list, spans, listStart, index, commaIndex, out translated, out nextIndex);
    }

    private static bool TryBuildBareSignpostEntry(
        string list,
        IReadOnlyList<ColorSpan> spans,
        int listStart,
        int targetStart,
        int targetEnd,
        out string translated,
        out int nextIndex)
    {
        var targetLength = targetEnd - targetStart;
        if (targetLength <= 0)
        {
            translated = string.Empty;
            nextIndex = targetStart;
            return false;
        }

        translated = RestoreSignpostTarget(list, spans, listStart, targetStart, targetLength);
        nextIndex = targetEnd;
        return true;
    }

    private static string RestoreSignpostTarget(
        string list,
        IReadOnlyList<ColorSpan> spans,
        int listStart,
        int targetStart,
        int targetLength)
    {
        return ColorAwareTranslationComposer.RestoreSlice(
            list.Substring(targetStart, targetLength),
            spans,
            listStart + targetStart,
            targetLength);
    }

    private static bool IsSignpostEntryBoundary(string source, int index)
    {
        return index == source.Length
            || StartsWithAt(source, ", or ", index)
            || StartsWithAt(source, " or ", index)
            || StartsWithAt(source, ", ", index);
    }

    private static bool TryGetSignpostPrefix(string source, out string prefix, out int listStart)
    {
        if (source.StartsWith(SpeakSignpostPrefix, StringComparison.Ordinal))
        {
            prefix = SpeakSignpostPrefix;
            listStart = SpeakSignpostPrefix.Length;
            return true;
        }

        if (source.StartsWith(FindSignpostPrefix, StringComparison.Ordinal))
        {
            prefix = FindSignpostPrefix;
            listStart = FindSignpostPrefix.Length;
            return true;
        }

        prefix = string.Empty;
        listStart = 0;
        return false;
    }

    private static bool StartsWithAt(string source, string value, int index)
    {
        return index >= 0
            && index + value.Length <= source.Length
            && string.CompareOrdinal(source, index, value, 0, value.Length) == 0;
    }

    private static string TranslateDirection(string source)
    {
        return source switch
        {
            "north" => "北",
            "south" => "南",
            "east" => "東",
            "west" => "西",
            "northeast" => "北東",
            "northwest" => "北西",
            "southeast" => "南東",
            "southwest" => "南西",
            _ => source,
        };
    }

    private static string TranslateLocationPhrase(string source, string target)
    {
        return source switch
        {
            "here" => "ここにいる" + target,
            "somewhere" => "どこかにいる" + target,
            "above" => "上方にいる" + target,
            "below" => "下方にいる" + target,
            _ when source.StartsWith("to the ", StringComparison.Ordinal) =>
                TranslateDirection(source.Substring("to the ".Length)) + "にいる" + target,
            _ => source + "にいる" + target,
        };
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

    private static string RestoreTraveler(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["traveler"];
        var translated = TranslateTraveler(group.Value);
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(translated, spans, group).Trim();
    }

    private static string TranslateTraveler(string source)
    {
        return StringHelpers.LowerAscii(source.Trim()) switch
        {
            "adventurer" => "冒険者",
            "traveler" => "旅人",
            "nomad" => "遊牧民",
            "wanderer" => "放浪者",
            "drifter" => "漂泊者",
            "friend" => "友",
            _ => source.Trim(),
        };
    }
}
