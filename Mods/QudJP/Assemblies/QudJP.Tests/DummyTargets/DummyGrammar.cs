using System;
using System.Collections.Generic;
using System.Globalization;

namespace QudJP.Tests.DummyTargets;

internal static class DummyGrammar
{
    public static string A(string name, bool capitalize)
    {
        var article = name.Length > 0 && "aeiouAEIOU".IndexOf(name[0]) >= 0 ? "an" : "a";
        if (capitalize)
        {
            article = char.ToUpperInvariant(article[0]) + article.Substring(1);
        }

        return $"{article} {name}";
    }

    public static string Pluralize(string word)
    {
        return word + "s";
    }

    public static string MakePossessive(string word)
    {
        return word.EndsWith("s", System.StringComparison.Ordinal) ? word + "'" : word + "'s";
    }

    public static string MakeAndList(List<string> items)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        if (items.Count == 1)
        {
            return items[0];
        }

        if (items.Count == 2)
        {
            return items[0] + " and " + items[1];
        }

        return string.Join(", ", items.GetRange(0, items.Count - 1)) + ", and " + items[items.Count - 1];
    }

    public static string MakeOrList(List<string> items)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        if (items.Count == 1)
        {
            return items[0];
        }

        if (items.Count == 2)
        {
            return items[0] + " or " + items[1];
        }

        return string.Join(", ", items.GetRange(0, items.Count - 1)) + ", or " + items[items.Count - 1];
    }

    public static List<string> SplitOfSentenceList(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        var normalized = text
            .Replace(", and ", ", ")
            .Replace(" and ", ", ");

        var fragments = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(fragments.Length);
        for (var index = 0; index < fragments.Length; index++)
        {
            var trimmed = fragments[index].Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    public static string InitCaps(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (char.IsLetter(text[0]))
        {
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        return text;
    }

    public static string CardinalNumber(int number)
    {
        return number switch
        {
            0 => "zero",
            1 => "one",
            2 => "two",
            3 => "three",
            4 => "four",
            5 => "five",
            6 => "six",
            7 => "seven",
            8 => "eight",
            9 => "nine",
            10 => "ten",
            _ => number.ToString(CultureInfo.InvariantCulture),
        };
    }
}
