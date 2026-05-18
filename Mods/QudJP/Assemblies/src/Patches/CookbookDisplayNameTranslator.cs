using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class CookbookDisplayNameTranslator
{
    private static readonly IReadOnlyDictionary<string, string> CookingTerms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cooking"] = "料理",
            ["baking"] = "焼き料理",
            ["brewing"] = "醸造",
            ["roasting"] = "ロースト",
            ["stewing"] = "煮込み",
            ["searing"] = "焼きつけ",
            ["braising"] = "蒸し煮",
            ["boiling"] = "茹で料理",
            ["pickling"] = "漬物",
            ["fermenting"] = "発酵",
            ["frying"] = "揚げ物",
            ["broiling"] = "炙り焼き",
            ["steaming"] = "蒸し料理",
            ["grilling"] = "グリル",
        };

    private static readonly IReadOnlyDictionary<string, string> RecipeTerms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipes"] = "レシピ",
            ["dishes"] = "料理",
            ["meals"] = "食事",
            ["food"] = "食べ物",
            ["vittle"] = "食事",
            ["snacks"] = "軽食",
            ["cuisine"] = "料理",
            ["cooking"] = "料理",
            ["chow"] = "食事",
            ["grub"] = "食べ物",
            ["mess"] = "料理",
            ["victual"] = "食料",
            ["fare"] = "料理",
            ["courses"] = "コース料理",
            ["eats"] = "食べ物",
            ["servings"] = "食膳",
        };

    private static readonly IReadOnlyDictionary<string, string> CaptureTerms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Garden"] = "庭園",
            ["Astral"] = "アストラル",
            ["The Salt Roads"] = "塩の道",
            ["Glowfish"] = "グロウフィッシュ",
        };

    private static readonly string CookingTermsPattern = BuildAlternation(CookingTerms.Keys);
    private static readonly string RecipeTermsPattern = BuildAlternation(RecipeTerms.Keys);

    private static readonly Regex NounOfCookingPattern = new(
        "^The (?<noun>.+?) Of (?<cooking>" + CookingTermsPattern + ")$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CookingOfNounPattern = new(
        "^The (?<cooking>" + CookingTermsPattern + ") Of The (?<noun>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CookingPrefixPattern = new(
        "^(?<cooking>" + CookingTermsPattern + "): (?<title>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AdjectiveRecipesPattern = new(
        "^(?<adjective>.+?) (?<recipes>" + RecipeTermsPattern + ")$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CookingAdjectiveRecipesPattern = new(
        "^(?<cooking>" + CookingTermsPattern + ") (?<adjective>.+?) (?<recipes>" + RecipeTermsPattern + ")$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex FocusNounOfCookingPattern = new(
        "^(?<focus>.+?): The (?<noun>.+?) Of (?<cooking>" + CookingTermsPattern + ")$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex NounOfFocusPattern = new(
        "^The (?<noun>.+?) Of (?<focus>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CookingWithFocusPattern = new(
        "^(?<cooking>" + CookingTermsPattern + ") With (?<focus>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex FocusMarkovPattern = new(
        "^(?<focus>.+?): (?<title>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AdjectiveRecipesWithFocusPattern = new(
        "^(?<adjective>.+?) (?<recipes>" + RecipeTermsPattern + ") With (?<focus>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CookingAdjectiveRecipesWithFocusPattern = new(
        "^(?<cooking>" + CookingTermsPattern + ") (?<adjective>.+?) (?<recipes>" + RecipeTermsPattern + ") With (?<focus>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        if (TryStripDirectMarkerPreservingLeadingColor(source!, out translated))
        {
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (!TryTranslateCore(stripped, out var translatedCore))
        {
            translated = original;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            original);
        return true;
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        if (TryTranslatePattern(FocusNounOfCookingPattern, source, match =>
                Capture(match, "focus") + "：" + TranslateCooking(match, "cooking") + "の" + Capture(match, "noun"),
                out translated)
            || TryTranslatePattern(CookingOfNounPattern, source, match =>
                Capture(match, "noun") + "の" + TranslateCooking(match, "cooking"),
                out translated)
            || TryTranslatePattern(NounOfCookingPattern, source, match =>
                TranslateCooking(match, "cooking") + "の" + Capture(match, "noun"),
                out translated)
            || TryTranslatePattern(CookingWithFocusPattern, source, match =>
                Capture(match, "focus") + "を使った" + TranslateCooking(match, "cooking"),
                out translated)
            || TryTranslatePattern(CookingAdjectiveRecipesWithFocusPattern, source, match =>
                Capture(match, "focus") + "を使った" + Capture(match, "adjective") + "の"
                + TranslateCooking(match, "cooking") + TranslateRecipe(match, "recipes"),
                out translated)
            || TryTranslatePattern(AdjectiveRecipesWithFocusPattern, source, match =>
                Capture(match, "focus") + "を使った" + Capture(match, "adjective") + "の" + TranslateRecipe(match, "recipes"),
                out translated)
            || TryTranslatePattern(CookingAdjectiveRecipesPattern, source, match =>
                Capture(match, "adjective") + "の" + TranslateCooking(match, "cooking") + TranslateRecipe(match, "recipes"),
                out translated)
            || TryTranslatePattern(AdjectiveRecipesPattern, source, match =>
                Capture(match, "adjective") + "の" + TranslateRecipe(match, "recipes"),
                out translated)
            || TryTranslatePattern(CookingPrefixPattern, source, match =>
                TranslateCooking(match, "cooking") + "：" + Capture(match, "title"),
                out translated)
            || TryTranslatePattern(FocusMarkovPattern, source, match =>
                Capture(match, "focus") + "：" + Capture(match, "title"),
                out translated)
            || TryTranslatePattern(NounOfFocusPattern, source, match =>
                Capture(match, "focus") + "の" + Capture(match, "noun"),
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string source,
        Func<Match, string> build,
        out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = build(match);
        return true;
    }

    private static string TranslateCooking(Match match, string groupName)
    {
        return CookingTerms[match.Groups[groupName].Value];
    }

    private static string TranslateRecipe(Match match, string groupName)
    {
        return RecipeTerms[match.Groups[groupName].Value];
    }

    private static string Capture(Match match, string groupName)
    {
        var source = match.Groups[groupName].Value;
        return CaptureTerms.TryGetValue(source, out var translated) ? translated : source;
    }

    private static bool TryStripDirectMarkerPreservingLeadingColor(string source, out string stripped)
    {
        if (source.Length > 2 && source[0] == '&' && source[2] == MessageFrameTranslator.DirectTranslationMarker)
        {
            stripped = source.Substring(0, 2) + source.Substring(3);
            return true;
        }

        stripped = source;
        return false;
    }

    private static string BuildAlternation(IEnumerable<string> values)
    {
        return string.Join("|", values.Select(Regex.Escape));
    }
}
