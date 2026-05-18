using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class CookingMealDescriptionTranslator
{
    private static readonly Regex TossPattern = new(
        "^You toss (?<ingredients>.+?) into a pot and stir\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex GatherAnythingPattern = new(
        "^You gather whatever you can find for your meal: (?<ingredients>.+?)\\.\\n\\nYou toss them in a pot and stir\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RummagePattern = new(
        "^Rummaging over your surroundings, you find these ingredients: (?<ingredients>.+?)\\.\\n\\nYou toss them in a pot and stir\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FixingsPattern = new(
        "^You gather some fixings: (?<ingredients>.+?)\\.\\n\\nYou toss them in a pot and stir\\.$",
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
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (TryTranslatePattern(TossPattern, stripped, spans, original, match =>
                RestoreIngredients(match, spans) + "を鍋に放り込み、かき混ぜた。", out translated)
            || TryTranslatePattern(GatherAnythingPattern, stripped, spans, original, match =>
                "食事に使えそうなものをかき集めた: " + RestoreIngredients(match, spans) + "\n\nそれらを鍋に放り込み、かき混ぜた。", out translated)
            || TryTranslatePattern(RummagePattern, stripped, spans, original, match =>
                "周囲を探り、次の材料を見つけた: " + RestoreIngredients(match, spans) + "\n\nそれらを鍋に放り込み、かき混ぜた。", out translated)
            || TryTranslatePattern(FixingsPattern, stripped, spans, original, match =>
                "いくつかの具材を集めた: " + RestoreIngredients(match, spans) + "\n\nそれらを鍋に放り込み、かき混ぜた。", out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        Func<Match, string> build,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedCore = build(match);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string RestoreIngredients(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["ingredients"];
        var restored = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(group.Value, spans, group).Trim();
        return TranslateIngredientList(restored);
    }

    private static string TranslateIngredientList(string source)
    {
        var normalized = source.Replace(", and ", "\u001f").Replace(", ", "\u001e");
        var pieces = normalized.Split(new[] { '\u001f', '\u001e' }, StringSplitOptions.None);
        if (pieces.Length == 1)
        {
            return TranslateIngredientFragment(source);
        }

        var separators = new List<char>();
        for (var index = 0; index < normalized.Length; index++)
        {
            if (normalized[index] is '\u001f' or '\u001e')
            {
                separators.Add(normalized[index]);
            }
        }

        var translated = new List<string>(pieces.Length);
        for (var index = 0; index < pieces.Length; index++)
        {
            translated.Add(TranslateIngredientFragment(pieces[index]));
        }

        var result = translated[0];
        for (var index = 0; index < separators.Count; index++)
        {
            result += separators[index] == '\u001f' ? "と" : "、";
            result += translated[index + 1];
        }

        return result;
    }

    private static string TranslateIngredientFragment(string source)
    {
        var trimmed = source.Trim();
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(trimmed);
        if (!CookingIngredientFragmentTranslator.TryTranslate(stripped, out var translated))
        {
            translated = TranslateIngredientName(stripped);
        }

        return ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translated,
            spans,
            stripped.Length,
            trimmed);
    }

    private static string TranslateIngredientName(string source)
    {
        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source);
        if (scoped is not null)
        {
            return scoped;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(source, out var titlePhrase)
            ? titlePhrase
            : source;
    }
}
