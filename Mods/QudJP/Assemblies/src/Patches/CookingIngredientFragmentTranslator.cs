using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class CookingIngredientFragmentTranslator
{
    private static readonly Regex JapaneseCharacterPattern = new(
        "[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MeasuredIngredientPattern = new(
        "^(?:(?:a|an) )?(?<unit>pinch|dash|smidgen|sprinkle|nip|dram) of (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CountedMeasuredIngredientPattern = new(
        "^(?<count>\\d+|\\{\\{[A-Za-z]+\\|\\d+\\}\\}) (?<unit>servings?|drams?) of (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PossessiveBodyPartIngredientPattern = new(
        "^(?<owner>.+?)(?:'s|') (?<part>right hand|left hand|right foot|left foot|hand|foot|head|face|arm|leg|tail|wing|horn)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SomeIngredientPattern = new(
        "^some (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ArticleIngredientPattern = new(
        "^(?:a|an) (?<name>.+)$",
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source!, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var match = CountedMeasuredIngredientPattern.Match(source!);
        if (match.Success
            && TryTranslateIngredientName(match.Groups["name"].Value, out var countedIngredient)
            && TryTranslateCountedUnit(match.Groups["unit"].Value, out var countedUnit))
        {
            translated = countedIngredient + match.Groups["count"].Value + countedUnit;
            return true;
        }

        match = MeasuredIngredientPattern.Match(source!);
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

        if (TryTranslateIngredientName(source!, out ingredient))
        {
            translated = ingredient;
            return true;
        }

        translated = source!;
        return false;
    }

    private static bool TryTranslateIngredientName(string source, out string translated)
    {
        if (TryTranslatePossessiveBodyPartIngredientName(source, out translated))
        {
            return true;
        }

        if (TryTranslateColoredIngredientName(source, out translated))
        {
            return true;
        }

        if (TryPreserveAlreadyLocalizedColoredIngredientName(source, out translated))
        {
            return true;
        }

        return TryTranslateNonPossessiveIngredientName(source, out translated);
    }

    private static bool TryPreserveAlreadyLocalizedColoredIngredientName(string source, out string translated)
    {
        translated = source;
        if (!ColorAwareTranslationComposer.HasColorMarkup(source))
        {
            return false;
        }

        var visible = ColorAwareTranslationComposer.GetVisibleText(source);
        if (visible.StartsWith("some ", StringComparison.OrdinalIgnoreCase)
            || visible.StartsWith("a ", StringComparison.OrdinalIgnoreCase)
            || visible.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.Equals(visible, source, StringComparison.Ordinal)
            && JapaneseCharacterPattern.IsMatch(visible);
    }

    private static bool TryTranslateColoredIngredientName(string source, out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (string.Equals(stripped, source, StringComparison.Ordinal)
            || !TryTranslateNonPossessiveIngredientName(stripped, out var strippedTranslation))
        {
            translated = source;
            return false;
        }

        if (string.Equals(strippedTranslation, stripped, StringComparison.Ordinal))
        {
            translated = source;
            return true;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            strippedTranslation,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateNonPossessiveIngredientName(string source, out string translated)
    {
        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source);
        if (scoped is not null)
        {
            translated = scoped;
            return true;
        }

        translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(CampfireRollIngredientsTranslationPatch));
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return true;
        }

        if (HistorySpiceComponentLookup.TryTranslateTitlePhrase(source, out var titlePhrase))
        {
            translated = titlePhrase;
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

    private static bool TryTranslateCountedUnit(string source, out string translated)
    {
        if (source.StartsWith("serving", StringComparison.OrdinalIgnoreCase))
        {
            translated = "食分";
            return true;
        }

        if (source.StartsWith("dram", StringComparison.OrdinalIgnoreCase))
        {
            translated = "ドラム";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePossessiveBodyPartIngredientName(string source, out string translated)
    {
        translated = source;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = PossessiveBodyPartIngredientPattern.Match(stripped);
        if (!match.Success)
        {
            return false;
        }

        var ownerGroup = match.Groups["owner"];
        if (!TryTranslateNonPossessiveIngredientName(ownerGroup.Value, out var owner))
        {
            owner = ownerGroup.Value;
        }

        owner = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            owner,
            spans,
            ownerGroup);

        var partGroup = match.Groups["part"];
        if (!TryTranslateBodyPartName(partGroup.Value, out var part))
        {
            return false;
        }

        part = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            part,
            spans,
            partGroup);

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            owner + "の" + part,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static bool TryTranslateBodyPartName(string source, out string translated)
    {
        translated = source switch
        {
            "right hand" => "右手",
            "left hand" => "左手",
            "right foot" => "右足",
            "left foot" => "左足",
            "hand" => "手",
            "foot" => "足",
            "head" => "頭",
            "face" => "顔",
            "arm" => "腕",
            "leg" => "脚",
            "tail" => "尾",
            "wing" => "翼",
            "horn" => "角",
            _ => source,
        };
        return !string.Equals(translated, source, StringComparison.Ordinal);
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
