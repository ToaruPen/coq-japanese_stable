using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class MerchantAdvertisementTextTranslator
{
    private static readonly Regex BookTitlePattern = new(
        "^advertisement for (?<merchant>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ComeToPattern = new(
        "^Come to (?<workshop>.+?) for the highest quality wares\\.\\n\\nLocated (?<location>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FinestGoodsPattern = new(
        "^The finest goods at (?<workshop>.+?)\\.\\n\\nTravel (?<location>.+)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ComePattern = new(
        "^Come!\\n\\n(?<location>.+)\\.$",
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
        var match = ComeToPattern.Match(original);
        if (match.Success)
        {
            translated = match.Groups["workshop"].Value + "へどうぞ。最高品質の商品を取りそろえています。\n\n所在地：" + match.Groups["location"].Value + "。";
            return true;
        }

        match = FinestGoodsPattern.Match(original);
        if (match.Success)
        {
            translated = "最高の商品は" + match.Groups["workshop"].Value + "で。\n\n道順：" + match.Groups["location"].Value + "。";
            return true;
        }

        match = ComePattern.Match(original);
        if (match.Success)
        {
            translated = "お越しください！\n\n" + match.Groups["location"].Value + "。";
            return true;
        }

        translated = original;
        return false;
    }

    internal static bool TryTranslateBookTitle(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var sanitized = MessageFrameTranslator.StripAllDirectTranslationMarkers(source);
        var match = BookTitlePattern.Match(sanitized);
        if (match.Success)
        {
            translated = match.Groups["merchant"].Value + "の広告";
            return true;
        }

        translated = sanitized;
        return false;
    }
}
