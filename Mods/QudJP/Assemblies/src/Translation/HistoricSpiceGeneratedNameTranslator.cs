using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class HistoricSpiceGeneratedNameTranslator
{
    private static readonly Regex FestivalOfPattern =
        new Regex(
            "^(?<festival>[A-Z][A-Za-z]+) of the (?<subject>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingFestivalPattern =
        new Regex(
            "^(?<subject>.+) (?<festival>[A-Z][A-Za-z]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> FestivalWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "festival",
        "feast",
        "carnival",
        "jubilee",
        "holiday",
    };

    private static readonly HashSet<string> DishWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bread",
        "loaf",
        "slaw",
        "stew",
        "soup",
        "brisket",
        "borscht",
        "dip",
        "baklava",
        "compote",
        "hash",
        "porridge",
        "matz",
        "cookies",
        "yogurt",
        "goulash",
        "rice",
        "hummus",
        "knish",
        "broth",
        "kugel",
        "latkes",
        "schnitzel",
        "pancake",
        "roast",
        "shawarma",
        "flatbread",
        "meatballs",
        "pastry",
        "casserole",
        "cake",
        "dumpling",
        "doughnut",
        "tajine",
        "couscous",
        "dolma",
        "kebab",
        "fillet",
        "leaves",
        "bush",
        "shrubs",
        "grass",
        "root",
        "seeds",
        "thorns",
        "hay",
        "berries",
        "figs",
        "stems",
        "shoots",
        "bugs",
        "larvae",
        "bark",
        "scrap",
        "alloy",
        "wire",
        "diodes",
        "circuitry",
        "marrow",
        "bones",
        "clams",
        "mussels",
        "snails",
        "algae",
        "worms",
        "rocks",
        "gravel",
        "pebbles",
        "boulder",
        "humus",
        "rot",
        "corpse",
    };

    internal static bool TryTranslateCapture(string source, out string translated)
    {
        if (TryTranslateFestivalName(source, out translated)
            || TryTranslateDishName(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateFestivalName(string source, out string translated)
    {
        var ofMatch = FestivalOfPattern.Match(source);
        if (ofMatch.Success
            && IsFestivalWord(ofMatch.Groups["festival"].Value)
            && TryTranslateWord(ofMatch.Groups["festival"].Value, out var festival)
            && TryTranslateTitlePhrase(ofMatch.Groups["subject"].Value, out var subject))
        {
            translated = subject + "の" + festival;
            return true;
        }

        var trailingMatch = TrailingFestivalPattern.Match(source);
        if (trailingMatch.Success
            && IsFestivalWord(trailingMatch.Groups["festival"].Value)
            && TryTranslateWord(trailingMatch.Groups["festival"].Value, out festival)
            && TryTranslateTitlePhrase(trailingMatch.Groups["subject"].Value, out subject))
        {
            translated = subject + "の" + festival;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateDishName(string source, out string translated)
    {
        var words = SplitWords(source);
        if (words.Length < 2)
        {
            translated = source;
            return false;
        }

        var dishWordIndex = FindDishWordIndex(words);
        if (dishWordIndex < 0
            || !IsLikelyGeneratedDishName(words, dishWordIndex)
            || !TryTranslateWords(words, out var translatedWords))
        {
            translated = source;
            return false;
        }

        translated = string.Join("・", translatedWords);
        return true;
    }

    private static bool TryTranslateTitlePhrase(string source, out string translated)
    {
        var words = SplitWords(source);
        if (words.Length == 0 || !TryTranslateWords(words, out var translatedWords))
        {
            translated = source;
            return false;
        }

        translated = string.Concat(translatedWords);
        return true;
    }

    private static bool TryTranslateWords(string[] words, out string[] translatedWords)
    {
        translatedWords = new string[words.Length];
        for (var index = 0; index < words.Length; index++)
        {
            if (!TryTranslateWord(words[index], out translatedWords[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryTranslateWord(string source, out string translated)
    {
        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var lower = LowerAscii(source);
        var direct = Translator.Translate(lower);
        if (!string.Equals(direct, lower, StringComparison.Ordinal))
        {
            translated = direct;
            return true;
        }

        translated = source;
        return false;
    }

    private static int FindDishWordIndex(string[] words)
    {
        for (var index = 0; index < words.Length; index++)
        {
            if (DishWords.Contains(words[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsLikelyGeneratedDishName(string[] words, int dishWordIndex)
    {
        if (words.Length < 2 || words.Length > 5)
        {
            return false;
        }

        if (dishWordIndex != 0 && dishWordIndex != words.Length - 1)
        {
            return false;
        }

        for (var index = 0; index < words.Length; index++)
        {
            if (!IsTitleWord(words[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTitleWord(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var sawAsciiLetter = false;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z'))
            {
                if (!sawAsciiLetter)
                {
                    if (character < 'A' || character > 'Z')
                    {
                        return false;
                    }

                    sawAsciiLetter = true;
                    continue;
                }

                continue;
            }

            if (character != '\'' && character != '-')
            {
                return false;
            }
        }

        return sawAsciiLetter;
    }

    private static bool IsFestivalWord(string source) => FestivalWords.Contains(source);

    private static string[] SplitWords(string source) =>
        source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

    private static string LowerAscii(string source)
    {
        var buffer = source.ToCharArray();
        var changed = false;
        for (var index = 0; index < buffer.Length; index++)
        {
            var character = buffer[index];
            if (character < 'A' || character > 'Z')
            {
                continue;
            }

            buffer[index] = (char)(character + ('a' - 'A'));
            changed = true;
        }

        return changed ? new string(buffer) : source;
    }
}
