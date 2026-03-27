using System;

namespace QudJP.Patches;

internal static class PlayerStatusBarProducerTranslationHelpers
{
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
            "Time" => TranslateExact(source, route, "PlayerStatusBar.Time"),
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
}
