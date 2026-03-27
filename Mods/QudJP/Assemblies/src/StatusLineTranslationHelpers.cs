using System;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class StatusLineTranslationHelpers
{
    private static readonly Regex HpLinePattern =
        new Regex("^HP:\\s*(?<current>\\d+)\\s*/\\s*(?<max>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HpStatusLinePattern =
        new Regex("^HP:\\s*(?<status>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LevelExpLinePattern =
        new Regex("^LVL:\\s*(?<level>\\d+)\\s+Exp:\\s*(?<current>\\d+)\\s*/\\s*(?<next>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BonusCapPattern =
        new Regex("^(?<stat>.+?) Bonus Cap:\\s*(?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WeaponClassPattern =
        new Regex("^Weapon Class:\\s*(?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslateCompareStatusLine(string source, string route, string family, out string translated)
    {
        if (WorldModsTextTranslator.TryTranslate(source, route, family, out translated))
        {
            return true;
        }

        var bonusCapMatch = BonusCapPattern.Match(source);
        if (bonusCapMatch.Success)
        {
            var translatedSuffix = StringHelpers.TranslateExactOrLowerAscii("Bonus Cap:");
            if (translatedSuffix is not null)
            {
                var translatedValue = bonusCapMatch.Groups["value"].Value;
                if (StringHelpers.TryGetTranslationExactOrLowerAscii(bonusCapMatch.Groups["value"].Value, out var valueTranslation))
                {
                    translatedValue = valueTranslation;
                }

                var rawStat = bonusCapMatch.Groups["stat"].Value;
                var statTranslated = StringHelpers.TryGetTranslationExactOrLowerAscii(rawStat, out var translatedStat);
                var statName = statTranslated ? translatedStat : rawStat;
                var separator = statTranslated ? "" : " ";

                translated = statName + separator + translatedSuffix + " " + translatedValue;
                DynamicTextObservability.RecordTransform(route, family, source, translated);
                return true;
            }
        }

        var weaponClassMatch = WeaponClassPattern.Match(source);
        if (weaponClassMatch.Success)
        {
            var translatedPrefix = StringHelpers.TranslateExactOrLowerAscii("Weapon Class:");
            if (translatedPrefix is not null)
            {
                var translatedValue = weaponClassMatch.Groups["value"].Value;
                if (StringHelpers.TryGetTranslationExactOrLowerAscii(weaponClassMatch.Groups["value"].Value, out var valueTranslation))
                {
                    translatedValue = valueTranslation;
                }

                translated = translatedPrefix + " " + translatedValue;
                DynamicTextObservability.RecordTransform(route, family, source, translated);
                return true;
            }
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslateCompareStatusSequence(string source, string route, string family, out string translated)
    {
        translated = source;
        var separator = ", ";
        if (source.IndexOf(separator, StringComparison.Ordinal) < 0)
        {
            separator = " ";
            if (source.IndexOf(separator, StringComparison.Ordinal) < 0)
            {
                return false;
            }
        }

        var options = string.Equals(separator, " ", StringComparison.Ordinal)
            ? StringSplitOptions.RemoveEmptyEntries
            : StringSplitOptions.None;
        var parts = source.Split(new[] { separator }, options);
        if (parts.Length < 2)
        {
            return false;
        }

        var translatedParts = new string[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            var translatedPart = StringHelpers.TranslateExactOrLowerAscii(parts[index]);
            if (translatedPart is null)
            {
                return false;
            }

            translatedParts[index] = translatedPart;
        }

        translated = string.Equals(separator, ", ", StringComparison.Ordinal)
            ? string.Join("、", translatedParts)
            : string.Join(" ", translatedParts);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    internal static bool TryTranslateActiveEffectsLine(string source, string route, string family, out string translated)
    {
        const string activeEffectsPrefix = "ACTIVE EFFECTS:";
        if (!source.StartsWith(activeEffectsPrefix, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var status = Translator.Translate(activeEffectsPrefix);
        if (string.Equals(status, activeEffectsPrefix, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        var tail = source.Substring(activeEffectsPrefix.Length).Trim();
        if (tail.Length == 0)
        {
            translated = status;
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        var parts = tail.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
        var translatedParts = new string[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            var translatedPart = StringHelpers.TranslateExactOrLowerAscii(parts[index]);
            if (translatedPart is null)
            {
                translated = source;
                return false;
            }

            translatedParts[index] = translatedPart;
        }

        translated = status + " " + string.Join("、", translatedParts);
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    internal static bool TryTranslateLevelExpLine(string source, string route, string family, out string translated)
    {
        var match = LevelExpLinePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var levelLabel = Translator.Translate("LVL");
        if (string.Equals(levelLabel, "LVL", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated =
            $"{levelLabel}: {match.Groups["level"].Value} Exp: {match.Groups["current"].Value} / {match.Groups["next"].Value}";
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }

    internal static bool TryTranslateHpLine(string source, string route, string family, out string translated)
    {
        var match = HpLinePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"HP: {match.Groups["current"].Value} / {match.Groups["max"].Value}";
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return true;
    }

    internal static bool TryTranslateHpStatusLine(string source, string route, string family, out string translated)
    {
        if (HpLinePattern.IsMatch(source))
        {
            translated = source;
            return false;
        }

        var match = HpStatusLinePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedStatus = StringHelpers.TranslateExactOrLowerAscii(match.Groups["status"].Value);
        if (translatedStatus is null)
        {
            translated = source;
            return false;
        }

        translated = $"HP: {translatedStatus}";
        DynamicTextObservability.RecordTransform(route, family, source, translated);
        return true;
    }
}
