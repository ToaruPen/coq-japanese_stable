using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class SettlementFarmNameTranslator
{
    private static readonly Regex SecludedFarmPattern = new(
        "^(?:a |an )?(?<modifier>secluded|quiet|small|remote) (?<type>pig|starapple) farm$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex OfPattern = new(
        "^the (?<kind>Farm|Ranch|Pasture|Orchard|Grove|Shire|End|Hedge|Furrow|Hearth|Hold|Reach) of (?<owner>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex OwnerKindPattern = new(
        "^(?:the )?(?<owner>.+?) (?<kind>Farm|Ranch|Pasture|Orchard|Grove|Shire|End|Hedge|Furrow|Hearth|Hold|Reach)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PrefixKindPattern = new(
        "^(?<prefix>Mud|Pig|Snout|Fruit|Apple|Red|Sweet)(?<kind>Shire|End|Hedge|Furrow|Hearth|Hold|Reach)$",
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

        var match = SecludedFarmPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateModifier(match.Groups["modifier"].Value)
                + TranslateFarmType(match.Groups["type"].Value)
                + "農場";
            return true;
        }

        match = PrefixKindPattern.Match(source);
        if (match.Success)
        {
            translated = TranslatePrefix(match.Groups["prefix"].Value) + TranslateKind(match.Groups["kind"].Value);
            return true;
        }

        match = OfPattern.Match(source);
        if (match.Success)
        {
            translated = match.Groups["owner"].Value + "の" + TranslateKind(match.Groups["kind"].Value);
            return true;
        }

        match = OwnerKindPattern.Match(source);
        if (match.Success)
        {
            translated = NormalizePossessiveOwner(match.Groups["owner"].Value) + "の" + TranslateKind(match.Groups["kind"].Value);
            return true;
        }

        translated = source!;
        return false;
    }

    private static string NormalizePossessiveOwner(string source)
    {
        return source.EndsWith("'s", StringComparison.Ordinal)
            ? source.Substring(0, source.Length - 2)
            : source;
    }

    private static string TranslateModifier(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "secluded" => "人里離れた",
            "quiet" => "静かな",
            "small" => "小さな",
            "remote" => "辺境の",
            _ => source,
        };
    }

    private static string TranslateFarmType(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "pig" => "豚",
            "starapple" => "スターアップル",
            _ => source,
        };
    }

    private static string TranslatePrefix(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "mud" => "泥",
            "pig" => "豚",
            "snout" => "鼻面",
            "fruit" => "果実",
            "apple" => "リンゴ",
            "red" => "赤",
            "sweet" => "甘味",
            _ => source,
        };
    }

    private static string TranslateKind(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "farm" => "農場",
            "ranch" => "牧場",
            "pasture" => "放牧地",
            "orchard" => "果樹園",
            "grove" => "木立",
            "shire" => "村郡",
            "end" => "果て",
            "hedge" => "垣根",
            "furrow" => "畝",
            "hearth" => "炉辺",
            "hold" => "砦",
            "reach" => "辺境",
            _ => source,
        };
    }
}
