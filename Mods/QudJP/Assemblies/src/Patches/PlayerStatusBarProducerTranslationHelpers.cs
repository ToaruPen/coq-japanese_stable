using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class PlayerStatusBarProducerTranslationHelpers
{
    private static readonly Regex CalendarStatusPattern =
        new Regex("^(?<time>.+?) (?<day>Ides|\\d{1,2}(?:st|nd|rd|th)) of (?<month>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OrdinalDayPattern =
        new Regex("^(?<day>\\d{1,2})(?:st|nd|rd|th)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static string TranslateStringDataValue(string fieldName, string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        return fieldName switch
        {
            "FoodWater" => TranslateFoodWater(source, route),
            "Zone" => TranslateExact(source, route, "PlayerStatusBar.Zone"),
            "ZoneOnly" => TranslateExact(source, route, "PlayerStatusBar.ZoneOnly"),
            "HPBar" => TranslateHpBar(source, route),
            "PlayerName" => TranslateExact(source, route, "PlayerStatusBar.PlayerName"),
            "Time" => TranslateCalendarStatus(source, route),
            "Temp" => TranslateExact(source, route, "PlayerStatusBar.Temp"),
            "Weight" => TranslateExact(source, route, "PlayerStatusBar.Weight"),
            _ => source,
        };
    }

    internal static string TranslateXpBarText(string source, string route)
    {
        return StatusLineTranslationHelpers.TryTranslateLevelExpLine(
            source,
            route,
            "PlayerStatusBar.LevelExp",
            out var translated)
            ? translated
            : source;
    }

    private static string TranslateFoodWater(string source, string route)
    {
        return StatusLineTranslationHelpers.TryTranslateCompareStatusSequence(
            source,
            route,
            "PlayerStatusBar.FoodWater",
            out var translated)
            ? translated
            : source;
    }

    private static string TranslateHpBar(string source, string route)
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TranslateHpBarVisible(visible, route));
        return translated;
    }

    private static string TranslateHpBarVisible(string source, string route)
    {
        if (StatusLineTranslationHelpers.TryTranslateHpLine(source, route, "PlayerStatusBar.HPBar", out var translated))
        {
            return translated;
        }

        return StatusLineTranslationHelpers.TryTranslateHpStatusLine(
            source,
            route,
            "PlayerStatusBar.HPBar",
            out translated)
            ? translated
            : source;
    }

    private static string TranslateExact(string source, string route, string family)
    {
        var translated = StringHelpers.TranslateExactOrLowerAscii(source);
        if (translated is null || string.Equals(translated, source, StringComparison.Ordinal))
        {
            return source;
        }

        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return translated;
    }

    private static string TranslateCalendarStatus(string source, string route)
    {
        var match = CalendarStatusPattern.Match(source);
        if (!match.Success)
        {
            return TranslateExact(source, route, "PlayerStatusBar.Time");
        }

        var translatedTime = TranslateExact(match.Groups["time"].Value, route, "PlayerStatusBar.TimeOfDay");
        var translatedMonth = TranslateExact(match.Groups["month"].Value, route, "PlayerStatusBar.Month");
        var translatedDay = TranslateCalendarDay(match.Groups["day"].Value);
        var translated = $"{translatedTime} {translatedMonth}{translatedDay}";

        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return source;
        }

        DynamicTextObservability.RecordTransform(route, "PlayerStatusBar.Time", source, translated);
        return translated;
    }

    private static string TranslateCalendarDay(string source)
    {
        if (string.Equals(source, "Ides", StringComparison.Ordinal))
        {
            return "15日";
        }

        var match = OrdinalDayPattern.Match(source);
        if (!match.Success)
        {
            return source;
        }

        return match.Groups["day"].Value + "日";
    }
}
