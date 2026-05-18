using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class CookingIngredientFragmentTranslator
{
    private static readonly Regex MeasuredIngredientPattern = new(
        "^a (?<unit>pinch|dash|smidgen|sprinkle|nip|dram) of (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SomeIngredientPattern = new(
        "^some (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ArticleIngredientPattern = new(
        "^(?:a|an) (?<name>.+)$",
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source!, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var match = MeasuredIngredientPattern.Match(source!);
        if (match.Success
            && TryTranslateIngredientName(match.Groups["name"].Value, out var ingredient)
            && TryTranslateUnit(match.Groups["unit"].Value, out var unit))
        {
            translated = ingredient + unit;
            return true;
        }

        match = SomeIngredientPattern.Match(source!);
        if (match.Success && TryTranslateIngredientName(match.Groups["name"].Value, out ingredient))
        {
            translated = ingredient + "少々";
            return true;
        }

        match = ArticleIngredientPattern.Match(source!);
        if (match.Success && TryTranslateIngredientName(match.Groups["name"].Value, out ingredient))
        {
            translated = ingredient;
            return true;
        }

        translated = source!;
        return false;
    }

    private static bool TryTranslateIngredientName(string source, out string translated)
    {
        translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(CampfireRollIngredientsTranslationPatch));
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return true;
        }

        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source);
        if (scoped is not null)
        {
            translated = scoped;
            return true;
        }

        var lower = StringHelpers.LowerAscii(source);
        var direct = Translator.Translate(lower);
        if (!string.Equals(direct, lower, StringComparison.Ordinal))
        {
            translated = direct;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateUnit(string source, out string translated)
    {
        translated = source switch
        {
            "pinch" => "ひとつまみ",
            "dash" => "少量",
            "smidgen" => "ひとつまみ",
            "sprinkle" => "ひと振り",
            "nip" => "少量",
            "dram" => "1ドラム",
            _ => source,
        };
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }
}
