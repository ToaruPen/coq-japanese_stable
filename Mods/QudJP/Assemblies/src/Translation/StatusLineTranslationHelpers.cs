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
    private static readonly Regex RequiresPattern =
        new Regex("^Requires:\\s*(?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WeightPattern =
        new Regex("^Weight:\\s*(?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryTranslateCompareStatusLine(string source, string route, string family, out string translated)
    {
        if (WorldModsTextTranslator.TryTranslate(source, route, family, out translated))
        {
            return true;
        }

        var bonusCapMatch = BonusCapPattern.Match(source);
        if (bonusCapMatch.Success)
        {
            var translatedSuffix = StringHelpers.TranslateExactOrLowerAscii("Bonus Cap:", route);
            if (translatedSuffix is not null)
            {
                var translatedValue = bonusCapMatch.Groups["value"].Value;
                if (TryTranslateCompareStatusValue(translatedValue, route, out var valueTranslation))
                {
                    translatedValue = valueTranslation;
                }

                var rawStat = bonusCapMatch.Groups["stat"].Value;
                var translatedStat = StringHelpers.TranslateExactOrLowerAscii(rawStat, route);
                var statName = translatedStat ?? rawStat;
                var separator = translatedStat is not null ? "" : " ";

                translated = statName + separator + translatedSuffix + " " + translatedValue;
                DynamicTextObservability.RecordTransform(route, family, source, translated);
                return true;
            }
        }

        var weaponClassMatch = WeaponClassPattern.Match(source);
        if (weaponClassMatch.Success
            && TryTranslateLabeledValueLine(weaponClassMatch, "Weapon Class:", route, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        var requiresMatch = RequiresPattern.Match(source);
        if (requiresMatch.Success
            && TryTranslateLabeledValueLine(requiresMatch, "Requires:", route, out translated))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        var weightMatch = WeightPattern.Match(source);
        if (weightMatch.Success
            && TryTranslateLabeledValueLine(weightMatch, "Weight:", route, out translated, translateValue: false))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateLabeledValueLine(
        Match match,
        string label,
        string route,
        out string translated,
        bool translateValue = true)
    {
        var translatedLabel = StringHelpers.TranslateExactOrLowerAscii(label, route);
        if (translatedLabel is null)
        {
            translated = match.Value;
            return false;
        }

        var value = match.Groups["value"].Value;
        var translatedValue = value;
        if (translateValue && TryTranslateCompareStatusValue(value, route, out var valueTranslation))
        {
            translatedValue = valueTranslation;
        }

        translated = translatedLabel + " " + translatedValue;
        return true;
    }

    private static bool TryTranslateCompareStatusValue(string value, string route, out string translated)
    {
        var contextual = StringHelpers.TranslateExactOrLowerAscii(value, route);
        if (contextual is not null)
        {
            translated = contextual;
            return true;
        }

        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(value, "world-mods.ja.json");
        if (!string.IsNullOrEmpty(scoped) && !string.Equals(scoped, value, StringComparison.Ordinal))
        {
            translated = scoped!;
            return true;
        }

        translated = value;
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
            var translatedPart = StringHelpers.TranslateExactOrLowerAscii(parts[index], route);
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

        string status;
        using (Translator.PushLogContext(route))
        {
            status = Translator.Translate(activeEffectsPrefix);
        }
        if (string.Equals(status, activeEffectsPrefix, StringComparison.Ordinal))
        {
            // Translation unchanged; continue translating known effect fragments.
        }

        var tail = source.Substring(activeEffectsPrefix.Length).Trim();
        if (tail.Length == 0)
        {
            translated = status;
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                DynamicTextObservability.RecordTransform(route, family, source, translated);
                return true;
            }

            return false;
        }

        var parts = tail.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
        var translatedParts = new string[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            var translatedPart = StringHelpers.TranslateExactOrLowerAscii(parts[index], route);
            translatedParts[index] = translatedPart ?? parts[index];
        }

        translated = status + " " + string.Join("、", translatedParts);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

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

        string levelLabel;
        using (Translator.PushLogContext(route))
        {
            levelLabel = Translator.Translate("LVL");
        }
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

        var translatedStatus = StringHelpers.TranslateExactOrLowerAscii(match.Groups["status"].Value, route);
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
