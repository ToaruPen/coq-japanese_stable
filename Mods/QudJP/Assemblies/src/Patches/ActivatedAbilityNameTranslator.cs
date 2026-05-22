using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class ActivatedAbilityNameTranslator
{
    private const string SkillsAndPowersDictionaryFile = "ui-skillsandpowers.ja.json";

    private static readonly Regex ReleaseGasPattern =
        new Regex("^Release (?<gas>.+ Gas)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TightenPattern =
        new Regex("^Tighten (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DischargeChargePattern =
        new Regex("^Discharge \\[(?<count>\\d+) charge\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LaseChargesPattern =
        new Regex("^Lase \\((?<count>\\d+) charges\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TinkerTurretRemainingPattern =
        new Regex("^Tinker Turret\\s+\\[(?<count>\\d+) remaining\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static string TranslatePreservingColors(string source, string route, string family)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => TryTranslateVisibleName(visible, out var visibleTranslated)
                ? visibleTranslated
                : visible);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return translated;
    }

    internal static bool TryTranslateVisibleName(string source, out string translated)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, SkillsAndPowersDictionaryFile);
        if (scoped is not null)
        {
            translated = scoped;
            return true;
        }

        var releaseGasMatch = ReleaseGasPattern.Match(source);
        if (releaseGasMatch.Success
            && TryTranslateReleaseGasName(releaseGasMatch.Groups["gas"].Value, out translated))
        {
            return true;
        }

        var tightenMatch = TightenPattern.Match(source);
        if (tightenMatch.Success
            && TryTranslateTightenName(tightenMatch.Groups["target"].Value, out translated))
        {
            return true;
        }

        var dischargeMatch = DischargeChargePattern.Match(source);
        if (dischargeMatch.Success
            && TryTranslateBaseAbilityName("Discharge", out var discharge))
        {
            translated = discharge + " [" + dischargeMatch.Groups["count"].Value + "チャージ]";
            return true;
        }

        var laseMatch = LaseChargesPattern.Match(source);
        if (laseMatch.Success
            && TryTranslateBaseAbilityName("Lase", out var lase))
        {
            translated = lase + " (" + laseMatch.Groups["count"].Value + "チャージ)";
            return true;
        }

        var tinkerTurretMatch = TinkerTurretRemainingPattern.Match(source);
        if (tinkerTurretMatch.Success)
        {
            translated = "タレット製作 [残り" + tinkerTurretMatch.Groups["count"].Value + "]";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateReleaseGasName(string gasName, out string translated)
    {
        var generationName = gasName + " Generation";
        var translatedGenerationName = ChargenStructuredTextTranslator.Translate(generationName);
        if (string.Equals(translatedGenerationName, generationName, StringComparison.Ordinal))
        {
            translated = "Release " + gasName;
            return false;
        }

        translated = ToReleaseName(translatedGenerationName);
        return true;
    }

    private static string ToReleaseName(string translatedGenerationName)
    {
        const string generationSuffix = "生成";
        if (translatedGenerationName.EndsWith(generationSuffix, StringComparison.Ordinal))
        {
            return translatedGenerationName.Substring(0, translatedGenerationName.Length - generationSuffix.Length) + "放出";
        }

        return translatedGenerationName + "放出";
    }

    private static bool TryTranslateTightenName(string target, out string translated)
    {
        var translatedTarget = target;
        if (ContainsAsciiLetter(target) && !TryTranslateBaseAbilityName(target, out translatedTarget))
        {
            translated = "Tighten " + target;
            return false;
        }

        translated = translatedTarget + "を締め付ける";
        return true;
    }

    private static bool TryTranslateBaseAbilityName(string source, out string translated)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, SkillsAndPowersDictionaryFile);
        if (scoped is not null)
        {
            translated = scoped;
            return true;
        }

        if (StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsAsciiLetter(string source)
    {
        foreach (var character in source)
        {
            if ((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z'))
            {
                return true;
            }
        }

        return false;
    }
}
