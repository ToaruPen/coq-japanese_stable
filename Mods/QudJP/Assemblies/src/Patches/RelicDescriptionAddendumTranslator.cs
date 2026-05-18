using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class RelicDescriptionAddendumTranslator
{
    private static readonly Regex StampedPattern = new(
        "\\b(?<be>It is|They are) (?<verb>stamped|painted|engraved|embossed|etched|adorned|carved) with (?<kind>tiny images of|fanciful depictions of|beautiful) (?<element>[^.]+)\\.",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EngravingPattern = new(
        "\\bThere's an engraving of (?<faction>.+?) being (?<state>lifted up on chairs|thrown into the air joyfully|venerated as idols|treated to a delightful feast|thrown off a cliff|humiliated at a banquet|launched into orbit|trapped in [^.]+)\\.",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source!, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var changed = false;
        translated = StampedPattern.Replace(source!, match =>
        {
            changed = true;
            return TranslateStamped(match);
        });
        translated = EngravingPattern.Replace(translated, match =>
        {
            changed = true;
            return TranslateEngraving(match);
        });

        return changed;
    }

    private static string TranslateStamped(Match match)
    {
        var element = TranslateElement(match.Groups["element"].Value);
        return match.Groups["kind"].Value switch
        {
            "tiny images of" => "それには" + AppendNo(element) + "小さな図像が刻まれている。",
            "fanciful depictions of" => "それには" + AppendNo(element) + "幻想的な描写が刻まれている。",
            "beautiful" => "それには美しい" + element + "が刻まれている。",
            _ => match.Value,
        };
    }

    private static string AppendNo(string source)
    {
        return source.EndsWith("の", StringComparison.Ordinal) ? source : source + "の";
    }

    private static string TranslateEngraving(Match match)
    {
        var faction = match.Groups["faction"].Value;
        var state = match.Groups["state"].Value;
        var translatedState = state switch
        {
            "lifted up on chairs" => "椅子に担ぎ上げられている",
            "thrown into the air joyfully" => "歓喜のうちに宙へ放り上げられている",
            "venerated as idols" => "偶像として崇敬されている",
            "treated to a delightful feast" => "楽しい饗宴でもてなされている",
            "thrown off a cliff" => "崖から投げ落とされている",
            "humiliated at a banquet" => "宴席で辱められている",
            "launched into orbit" => "軌道上へ打ち上げられている",
            _ when state.StartsWith("trapped in ", StringComparison.Ordinal) =>
                TranslateElement(state.Substring("trapped in ".Length)) + "に閉じ込められている",
            _ => state,
        };

        return faction + "が" + translatedState + "様子を描いた彫刻がある。";
    }

    private static string TranslateElement(string source)
    {
        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source);
        if (scoped is not null)
        {
            return scoped;
        }

        var lower = StringHelpers.LowerAscii(source);
        var direct = Translator.Translate(lower);
        return string.Equals(direct, lower, StringComparison.Ordinal) ? source : direct;
    }
}
