using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP;

internal static class HistoricSpiceGeneratedNameTranslator
{
    private const string WorldGospelsDictionaryFile = "world-gospels.ja.json";

    private static readonly Regex FestivalOfPattern =
        new Regex(
            "^(?<festival>[A-Z][A-Za-z]+) of the (?<subject>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingFestivalPattern =
        new Regex(
            "^(?<subject>.+) (?<festival>[A-Z][A-Za-z]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FakedDeathCognomenPattern =
        new Regex(
            "^(?:the )?(?<adjective>[A-Z][A-Za-z'-]+) (?<ghost>[A-Z][A-Za-z'-]+)(?: of (?<place>.+))?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SultanateYearNamePattern =
        new Regex(
            "^Year of the (?<adjective>[A-Z][A-Za-z'-]+) (?<noun>[A-Z][A-Za-z'-]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BlessingOfItemPattern =
        new Regex(
            "^(?:the )?(?<blessing>[A-Za-z][A-Za-z'-]+) of (?<root>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PossessiveBlessingItemPattern =
        new Regex(
            "^(?<root>.+?)(?:'s|') (?<blessing>[A-Za-z][A-Za-z'-]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingBlessingItemPattern =
        new Regex(
            "^(?:the )?(?<root>.+) (?<blessing>[A-Za-z][A-Za-z'-]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CultOfTheRootPattern =
        new Regex(
            "^(?<kind>[A-Z][A-Za-z'-]+) of the (?<root>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CultOfRootPattern =
        new Regex(
            "^(?<kind>[A-Z][A-Za-z'-]+) of (?<root>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingCultKindPattern =
        new Regex(
            "^(?<root>.+) (?<kind>[A-Z][A-Za-z'-]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RootianCultKindPattern =
        new Regex(
            "^(?:(?<ordinal>[0-9]+(?:st|nd|rd|th)) )?(?<root>[A-Z][A-Za-z'-]+)ian (?<kind>[A-Z][A-Za-z'-]+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> CultKindWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cult",
        "order",
        "society",
        "church",
        "folk",
        "brood",
        "family",
        "kith",
        "people",
        "clan",
        "flock",
        "sect",
        "kinfolk",
        "cabal",
        "host",
    };

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
        "wafers",
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
        "meal",
        "paste",
        "mazebeard",
        "blaze",
        "flame",
        "char",
        "smoke",
        "honey",
        "rubbergum",
        "gum",
        "poultice",
        "anodyne",
        "oil",
        "tonic",
        "skulk",
        "tartbeard",
        "moss",
        "cracker",
        "tail",
        "scales",
        "scale",
        "dreams",
        "dream",
        "daydreams",
        "daydream",
        "petals",
        "petal",
        "vanta",
        "nectar",
        "greens",
        "flamebeard",
        "sleetbeard",
        "nullity",
        "nullbeard",
        "gallbeard",
        "dreambeard",
        "stillbeard",
        "yondercane",
        "air",
        "cane",
        "yuckwheat",
        "medicine",
        "stem",
        "ant",
        "gaster",
        "hoarshrooms",
        "hoarshroom",
        "mushrooms",
        "mushroom",
        "fungus",
        "light",
        "jerky",
        "meat",
        "flesh",
        "extremity",
        "limb",
        "appendage",
        "lagroot",
        "lag",
        "dust",
        "fiber",
        "water",
        "brineshroom",
        "pickles",
        "cucumber",
        "pickle",
        "gland",
        "memories",
        "psyche",
        "memory",
        "chips",
        "bop",
        "sponge",
        "cheek",
        "curd",
        "plasma",
        "sparks",
        "current",
        "electricity",
        "lightning",
        "electron",
        "volt",
        "nettles",
        "spines",
        "jam",
        "nettle",
        "thorn",
        "apples",
        "starapples",
        "bananas",
        "banana",
        "vinewafers",
        "freshwater",
        "voider",
        "glue",
        "draught",
        "elixir",
        "magma",
        "neutrons",
    };

    private static readonly HashSet<string> GhostWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ghost",
        "phantom",
        "shade",
        "spectre",
        "spirit",
        "wraith",
    };

    private static readonly HashSet<string> ItemBlessingWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "blessing",
        "gift",
        "joy",
        "victory",
        "rapture",
        "charm",
        "bliss",
        "pride",
        "wonder",
        "ecstasy",
        "jewel",
        "prize",
        "mirth",
        "solace",
        "triumph",
        "beauty",
        "grace",
        "glamor",
        "paragon",
        "dream",
        "lure",
        "promise",
        "boon",
        "nursling",
        "mite",
        "sprout",
        "urchin",
        "boy",
        "girl",
        "friend",
        "cohort",
        "cousin",
        "brother",
        "sister",
        "mother",
        "father",
        "comrade",
        "lover",
        "flame",
        "suitor",
        "foe",
        "rival",
        "star",
        "sun",
        "moon",
        "son",
        "daughter",
        "dear",
        "beloved",
        "pet",
        "flower",
    };

    internal static bool TryTranslateCapture(string source, out string translated)
    {
        if (TryTranslateDishName(source, out translated)
            || TryTranslateCommaSeparatedCognomens(source, out translated)
            || TryTranslateFakedDeathCognomen(source, out translated)
            || TryTranslateFestivalName(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslateSultanateYearName(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = SultanateYearNamePattern.Match(stripped);
        if (!match.Success
            || !HistorySpiceComponentLookup.TryTranslateWord(match.Groups["adjective"].Value, out var adjective)
            || !HistorySpiceComponentLookup.TryTranslateWord(match.Groups["noun"].Value, out var noun))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            adjective + noun + "の年",
            spans,
            stripped.Length,
            source);
        return true;
    }

    internal static bool TryTranslateHistoricItemName(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateHistoricItemNameCore(stripped, out var translatedCore))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translatedCore,
                spans,
                stripped.Length,
                source);
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslateSultanCultName(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateSultanCultNameCore(stripped, out var translatedCore))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translatedCore,
                spans,
                stripped.Length,
                source);
            return true;
        }

        translated = source;
        return false;
    }

    internal static bool TryTranslateRuinsSiteName(string source, out string translated)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return true;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (TryTranslateRuinsSiteNameCore(stripped, out var translatedCore))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translatedCore,
                spans,
                stripped.Length,
                source);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateHistoricItemNameCore(string source, out string translated) =>
        TryTranslateBlessingOfItem(source, out translated)
        || TryTranslatePossessiveBlessingItem(source, out translated)
        || TryTranslateTrailingBlessingItem(source, out translated);

    private static bool TryTranslateSultanCultNameCore(string source, out string translated) =>
        TryTranslateRootianCultName(source, out translated)
        || TryTranslateCultOfRootName(source, out translated)
        || TryTranslateTrailingCultName(source, out translated);

    private static bool TryTranslateRuinsSiteNameCore(string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrWhiteSpace(source)
            || string.Equals(source, "some forgotten ruins", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var working = source.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
            ? source.Substring("the ".Length)
            : source;
        var words = SplitWords(working);
        if (words.Length < 2)
        {
            return false;
        }

        if (words.Length >= 3
            && TryTranslateSiteModifier(words[0], out var firstModifier)
            && TryTranslateSiteModifier(words[1], out var secondModifier))
        {
            translated = firstModifier + "の" + secondModifier + ConcatWords(words, 2, words.Length);
            return true;
        }

        if (TryTranslateSiteModifier(words[0], out var leadingModifier))
        {
            translated = leadingModifier + "の" + ConcatWords(words, 1, words.Length);
            return true;
        }

        if (TryTranslateSiteModifier(words[words.Length - 1], out var trailingModifier))
        {
            translated = ConcatWords(words, 0, words.Length - 1) + trailingModifier;
            return true;
        }

        return false;
    }

    private static bool TryTranslateCommaSeparatedCognomens(string source, out string translated)
    {
        var parts = source.Split(new[] { ", " }, StringSplitOptions.None);
        if (parts.Length < 2)
        {
            translated = source;
            return false;
        }

        var translatedParts = new string[parts.Length];
        translatedParts[0] = parts[0];
        var changed = false;

        for (var index = 1; index < parts.Length; index++)
        {
            if (TryTranslateCognomenPart(parts[index], out var translatedPart))
            {
                translatedParts[index] = translatedPart;
                changed = true;
            }
            else
            {
                translatedParts[index] = parts[index];
            }
        }

        translated = changed ? string.Join("、", translatedParts) : source;
        return changed;
    }

    private static bool TryTranslateCognomenPart(string source, out string translated)
    {
        var exact = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source);
        if (exact is not null)
        {
            translated = exact;
            return true;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(source, out translated);
    }

    private static bool TryTranslateFakedDeathCognomen(string source, out string translated)
    {
        var match = FakedDeathCognomenPattern.Match(source);
        if (!match.Success
            || !GhostWords.Contains(match.Groups["ghost"].Value)
            || !HistorySpiceComponentLookup.TryTranslateWord(match.Groups["adjective"].Value, out var adjective)
            || !HistorySpiceComponentLookup.TryTranslateWord(match.Groups["ghost"].Value, out var ghost))
        {
            translated = source;
            return false;
        }

        translated = adjective + ghost;
        var place = match.Groups["place"].Value;
        if (place.Length > 0)
        {
            translated += "・" + place;
        }

        return true;
    }

    private static bool TryTranslateFestivalName(string source, out string translated)
    {
        var ofMatch = FestivalOfPattern.Match(source);
        if (ofMatch.Success
            && IsFestivalWord(ofMatch.Groups["festival"].Value)
            && HistorySpiceComponentLookup.TryTranslateWord(ofMatch.Groups["festival"].Value, out var festival)
            && HistorySpiceComponentLookup.TryTranslateTitlePhrase(ofMatch.Groups["subject"].Value, out var subject))
        {
            translated = subject + "の" + festival;
            return true;
        }

        var trailingMatch = TrailingFestivalPattern.Match(source);
        if (trailingMatch.Success
            && IsFestivalWord(trailingMatch.Groups["festival"].Value)
            && HistorySpiceComponentLookup.TryTranslateWord(trailingMatch.Groups["festival"].Value, out festival)
            && HistorySpiceComponentLookup.TryTranslateTitlePhrase(trailingMatch.Groups["subject"].Value, out subject))
        {
            translated = subject + "の" + festival;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateDishName(string source, out string translated)
    {
        if (TryTranslateDishPrepositionName(source, out translated)
            || TryTranslateDishSuffixName(source, out translated))
        {
            return true;
        }

        var words = SplitWords(source);
        if (words.Length < 2)
        {
            translated = source;
            return false;
        }

        var dishWordIndex = FindDishWordIndex(words);
        if (dishWordIndex < 0
            || !IsLikelyGeneratedDishName(words, dishWordIndex)
            || !TryTranslateDishWords(words, out translated))
        {
            translated = source;
            return false;
        }

        return true;
    }

    private static bool TryTranslateDishPrepositionName(string source, out string translated)
    {
        var words = SplitRecipeWords(source);
        if (words.Length < 3)
        {
            translated = source;
            return false;
        }

        if (TryTranslateDishOfName(words, out translated))
        {
            return true;
        }

        for (var index = 0; index < words.Length; index++)
        {
            if (!TryMatchDishPreposition(words, index, out var consumed, out var prepositionKind))
            {
                continue;
            }

            if (index == 0 || index + consumed >= words.Length)
            {
                continue;
            }

            if (!TryTranslateDishList(words, 0, index, out var leftItems)
                || !TryTranslateDishList(words, index + consumed, words.Length, out var rightItems))
            {
                continue;
            }

            var leftIsDish = IsDishLikePhrase(words, 0, index);
            var rightIsDish = IsDishLikePhrase(words, index + consumed, words.Length);
            if (!leftIsDish && !rightIsDish)
            {
                continue;
            }

            var leftPhrase = JoinDishItems(leftItems);
            var rightPhrase = JoinDishItems(rightItems);
            if (prepositionKind == DishPrepositionKind.With
                && (leftItems.Count > 1 || rightItems.Count > 1))
            {
                translated = rightItems.Count == 1 && rightIsDish
                    ? rightPhrase + "：" + leftPhrase + "入り"
                    : leftPhrase + "：" + rightPhrase + "入り";
                return true;
            }

            var suffix = DishPrepositionSuffix(prepositionKind);
            translated = rightItems.Count == 1 && rightIsDish
                ? leftPhrase + suffix + rightPhrase
                : leftPhrase + "：" + rightPhrase + suffix;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateDishOfName(string[] words, out string translated)
    {
        for (var index = 1; index < words.Length - 1; index++)
        {
            if (!IsWord(words, index, "of"))
            {
                continue;
            }

            if (!TryTranslateDishList(words, 0, index, out var leftItems)
                || !TryTranslateDishList(words, index + 1, words.Length, out var rightItems))
            {
                continue;
            }

            var leftIsDish = IsDishLikePhrase(words, 0, index);
            var rightIsDish = IsDishLikePhrase(words, index + 1, words.Length);
            if (!leftIsDish && !rightIsDish)
            {
                continue;
            }

            var leftPhrase = JoinDishItems(leftItems);
            var rightPhrase = JoinDishItems(rightItems);
            translated = leftIsDish
                ? rightPhrase + "入り" + leftPhrase
                : leftPhrase + "入り" + rightPhrase;
            return true;
        }

        translated = string.Empty;
        return false;
    }

    private static bool TryTranslateDishSuffixName(string source, out string translated)
    {
        var words = SplitWords(source);
        if (words.Length < 2)
        {
            translated = source;
            return false;
        }

        var hyphenIndex = words[0].IndexOf('-');
        if (hyphenIndex <= 0 || hyphenIndex >= words[0].Length - 1)
        {
            translated = source;
            return false;
        }

        var modifier = words[0].Substring(0, hyphenIndex);
        var suffix = words[0].Substring(hyphenIndex + 1);
        if (!TryTranslateIngredientPhrase(new[] { modifier }, out var translatedModifier)
            || !TryTranslateDishSuffix(suffix, out var translatedSuffix)
            || !IsDishLikePhrase(words, 1, words.Length)
            || !TryTranslateIngredientPhrase(words, 1, words.Length, out var translatedDish))
        {
            translated = source;
            return false;
        }

        translated = translatedModifier + translatedSuffix + translatedDish;
        return true;
    }

    private static bool TryTranslateBlessingOfItem(string source, out string translated)
    {
        var match = BlessingOfItemPattern.Match(source);
        if (!match.Success || !TryTranslateItemBlessing(match.Groups["blessing"].Value, out var blessing))
        {
            translated = source;
            return false;
        }

        translated = TranslateHistoricItemRoot(match.Groups["root"].Value) + "の" + blessing;
        return true;
    }

    private static bool TryTranslatePossessiveBlessingItem(string source, out string translated)
    {
        var match = PossessiveBlessingItemPattern.Match(source);
        if (!match.Success || !TryTranslateItemBlessing(match.Groups["blessing"].Value, out var blessing))
        {
            translated = source;
            return false;
        }

        translated = TranslateHistoricItemRoot(match.Groups["root"].Value) + "の" + blessing;
        return true;
    }

    private static bool TryTranslateTrailingBlessingItem(string source, out string translated)
    {
        var match = TrailingBlessingItemPattern.Match(source);
        if (!match.Success || !TryTranslateItemBlessing(match.Groups["blessing"].Value, out var blessing))
        {
            translated = source;
            return false;
        }

        translated = TranslateHistoricItemRoot(match.Groups["root"].Value) + "の" + blessing;
        return true;
    }

    private static bool TryTranslateCultOfRootName(string source, out string translated)
    {
        var match = CultOfTheRootPattern.Match(source);
        if (!match.Success)
        {
            match = CultOfRootPattern.Match(source);
        }

        if (!match.Success || !TryTranslateCultKind(match.Groups["kind"].Value, out var kind))
        {
            translated = source;
            return false;
        }

        translated = TranslateHistoricItemRoot(match.Groups["root"].Value) + "の" + kind;
        return true;
    }

    private static bool TryTranslateTrailingCultName(string source, out string translated)
    {
        var match = TrailingCultKindPattern.Match(source);
        if (!match.Success || !TryTranslateCultKind(match.Groups["kind"].Value, out var kind))
        {
            translated = source;
            return false;
        }

        translated = TranslateHistoricItemRoot(match.Groups["root"].Value) + "の" + kind;
        return true;
    }

    private static bool TryTranslateRootianCultName(string source, out string translated)
    {
        var match = RootianCultKindPattern.Match(source);
        if (!match.Success || !TryTranslateCultKind(match.Groups["kind"].Value, out var kind))
        {
            translated = source;
            return false;
        }

        var ordinal = match.Groups["ordinal"].Value;
        var root = TranslateHistoricItemRoot(match.Groups["root"].Value);
        translated = (ordinal.Length > 0 ? ordinal + " " : string.Empty)
            + root
            + "派の"
            + kind;
        return true;
    }

    private static bool TryTranslateItemBlessing(string source, out string translated)
    {
        if (!ItemBlessingWords.Contains(source))
        {
            translated = source;
            return false;
        }

        if (HistorySpiceComponentLookup.TryTranslateWord(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            return true;
        }

        var worldGospel = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, WorldGospelsDictionaryFile);
        if (worldGospel is not null)
        {
            translated = worldGospel;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCultKind(string source, out string translated)
    {
        if (!CultKindWords.Contains(source))
        {
            translated = source;
            return false;
        }

        if (HistorySpiceComponentLookup.TryTranslateWord(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            return true;
        }

        var worldGospel = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, WorldGospelsDictionaryFile);
        if (worldGospel is not null)
        {
            translated = worldGospel;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateSiteModifier(string source, out string translated)
    {
        if (HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source) is { } exact)
        {
            translated = exact;
            return true;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(source, out translated);
    }

    private static string TranslateHistoricItemRoot(string source)
    {
        if (HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source) is { } exact)
        {
            return exact;
        }

        if (HistorySpiceComponentLookup.TryTranslateTitlePhrase(source, out var titlePhrase))
        {
            return titlePhrase;
        }

        using var _ = Translator.PushMissingKeyLoggingSuppression(true);
        var lower = StringHelpers.LowerAscii(source);
        var direct = Translator.Translate(lower);
        return string.Equals(direct, lower, StringComparison.Ordinal) ? source : direct;
    }

    private static bool TryTranslateDishWords(string[] words, out string translated)
    {
        var translatedChunks = new List<string>();
        var index = 0;
        while (index < words.Length)
        {
            if (!TryTranslateLongestDishChunk(words, index, out var translatedChunk, out var consumed))
            {
                translated = string.Empty;
                return false;
            }

            translatedChunks.Add(translatedChunk);
            index += consumed;
        }

        translated = string.Concat(translatedChunks);
        return true;
    }

    private static bool TryTranslateLongestDishChunk(
        string[] words,
        int startIndex,
        out string translated,
        out int consumed)
    {
        for (var length = words.Length - startIndex; length > 1; length--)
        {
            var phrase = string.Join(" ", words, startIndex, length);
            var exact = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(phrase);
            if (exact is not null)
            {
                translated = exact;
                consumed = length;
                return true;
            }
        }

        if (HistorySpiceComponentLookup.TryTranslateWord(words[startIndex], out translated))
        {
            consumed = 1;
            return true;
        }

        consumed = 0;
        return false;
    }

    private static bool TryTranslateDishList(
        string[] words,
        int start,
        int end,
        out List<string> translatedItems)
    {
        translatedItems = new List<string>();
        var itemStart = start;
        for (var index = start; index <= end; index++)
        {
            if (index < end && !IsDishListSeparator(words[index]))
            {
                continue;
            }

            if (itemStart == index)
            {
                itemStart = index + 1;
                continue;
            }

            if (!TryTranslateIngredientPhrase(words, itemStart, index, out var translatedItem))
            {
                return false;
            }

            translatedItems.Add(translatedItem);
            itemStart = index + 1;
        }

        return translatedItems.Count > 0;
    }

    private static bool TryTranslateIngredientPhrase(string[] words, out string translated) =>
        TryTranslateIngredientPhrase(words, 0, words.Length, out translated);

    private static bool TryTranslateIngredientPhrase(
        string[] words,
        int start,
        int end,
        out string translated)
    {
        var translatedChunks = new List<string>();
        var index = start;
        while (index < end)
        {
            if (!TryTranslateLongestDishChunk(words, index, out var translatedChunk, out var consumed)
                || index + consumed > end)
            {
                translated = string.Empty;
                return false;
            }

            translatedChunks.Add(translatedChunk);
            index += consumed;
        }

        translated = string.Concat(translatedChunks);
        return true;
    }

    private static bool TryTranslateDishSuffix(string source, out string translated)
    {
        if (string.Equals(source, "cured", StringComparison.OrdinalIgnoreCase))
        {
            translated = "漬け";
            return true;
        }

        if (string.Equals(source, "rubbed", StringComparison.OrdinalIgnoreCase))
        {
            translated = "まぶし";
            return true;
        }

        if (HistorySpiceComponentLookup.TryTranslateWord(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private enum DishPrepositionKind
    {
        With,
        In,
        OnTopOf,
        Over,
    }

    private static bool TryMatchDishPreposition(
        string[] words,
        int start,
        out int consumed,
        out DishPrepositionKind kind)
    {
        if (IsWord(words, start, "inside") && IsWord(words, start + 1, "of"))
        {
            consumed = 2;
            kind = DishPrepositionKind.In;
            return true;
        }

        if (IsWord(words, start, "on") && IsWord(words, start + 1, "top") && IsWord(words, start + 2, "of"))
        {
            consumed = 3;
            kind = DishPrepositionKind.OnTopOf;
            return true;
        }

        if (IsWord(words, start, "with"))
        {
            consumed = 1;
            kind = DishPrepositionKind.With;
            return true;
        }

        if (IsWord(words, start, "in"))
        {
            consumed = 1;
            kind = DishPrepositionKind.In;
            return true;
        }

        if (IsWord(words, start, "over"))
        {
            consumed = 1;
            kind = DishPrepositionKind.Over;
            return true;
        }

        consumed = 0;
        kind = DishPrepositionKind.With;
        return false;
    }

    private static string DishPrepositionSuffix(DishPrepositionKind kind)
    {
        switch (kind)
        {
            case DishPrepositionKind.OnTopOf:
                return "のせ";
            case DishPrepositionKind.Over:
                return "がけ";
            default:
                return "入り";
        }
    }

    private static bool IsDishLikePhrase(string[] words, int start, int end)
    {
        if (start >= end)
        {
            return false;
        }

        return DishWords.Contains(words[start]) || DishWords.Contains(words[end - 1]);
    }

    private static bool IsDishListSeparator(string source) =>
        string.Equals(source, "and", StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, ",", StringComparison.Ordinal);

    private static bool IsWord(string[] words, int index, string expected) =>
        index >= 0
        && index < words.Length
        && string.Equals(words[index], expected, StringComparison.OrdinalIgnoreCase);

    private static string JoinDishItems(List<string> items)
    {
        if (items.Count == 1)
        {
            return items[0];
        }

        if (items.Count == 2)
        {
            return items[0] + "と" + items[1];
        }

        return string.Join("、", items);
    }

    private static int FindDishWordIndex(string[] words)
    {
        if (words.Length == 0)
        {
            return -1;
        }

        if (DishWords.Contains(words[0]))
        {
            return 0;
        }

        var lastIndex = words.Length - 1;
        if (DishWords.Contains(words[lastIndex]))
        {
            return lastIndex;
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
            if (index > 0 && index < words.Length - 1 && IsDishConnectorWord(words[index]))
            {
                continue;
            }

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

    private static bool IsDishConnectorWord(string source) =>
        string.Equals(source, "of", StringComparison.OrdinalIgnoreCase);

    private static bool IsFestivalWord(string source) => FestivalWords.Contains(source);

    private static string[] SplitWords(string source) =>
        source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

    private static string[] SplitRecipeWords(string source) =>
        source.Replace(",", " ,").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

    private static string ConcatWords(string[] words, int start, int end)
    {
        return string.Join(" ", words, start, end - start);
    }

}
