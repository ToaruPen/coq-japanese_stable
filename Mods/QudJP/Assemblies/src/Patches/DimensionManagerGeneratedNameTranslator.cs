using System;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DimensionManagerGeneratedNameTranslator
{
    private static readonly Regex RealmOfPattern = new(
        "^(?<kind>realm|sphere|zone|domain|dominion|orbit|plane|expanse|unit|quantity|radius|space|degree|stratum) of (?<thing>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex VoidOfPattern = new(
        "^(?<kind>void|nether|vacuum|nihility|nullity|abyss|chasm|fissure|gulf|gap|lacuna|womb|nothingness|schism) of (?<thing>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AdjectiveShapePattern = new(
        "^(?<adjective>vacuous|vacant|hollow|barren|pale|spotless|blank|empty|prosaic|dreary|dead|flat|inert|forsaken|colossal|vast|eternal|boundless|infinite) (?<shape>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CultOfPattern = new(
        "^(?<cult>cult|order|society|church|folk|brood|family|kith|people|clan|flock|sect|kinfolk|cabal|host) of \\*CultSymbol\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex LeadingThePattern = new(
        "^the (?<name>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryTranslateExpandedText(string? source, out string translated)
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (TryTranslateDimensionName(stripped, out var translatedCore)
            || TryTranslateCultForm(stripped, out translatedCore)
            || TryTranslateMutationCultForm(stripped, out translatedCore))
        {
            translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
                translatedCore,
                spans,
                stripped.Length,
                original);
            return true;
        }

        translated = original;
        return false;
    }

    internal static bool TryTranslateStoredName(string? source, out string translated)
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

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var match = LeadingThePattern.Match(original);
        var withoutArticle = match.Success ? match.Groups["name"].Value : original;
        if (TryTranslateExpandedText(withoutArticle, out translated))
        {
            return true;
        }

        if (match.Success)
        {
            translated = withoutArticle;
            return true;
        }

        translated = original;
        return false;
    }

    private static bool TryTranslateDimensionName(string source, out string translated)
    {
        var match = RealmOfPattern.Match(source);
        if (match.Success)
        {
            translated = match.Groups["thing"].Value + "の" + TranslateRealmKind(match.Groups["kind"].Value);
            return true;
        }

        match = VoidOfPattern.Match(source);
        if (match.Success)
        {
            translated = match.Groups["thing"].Value + "の" + TranslateVoidKind(match.Groups["kind"].Value);
            return true;
        }

        match = AdjectiveShapePattern.Match(source);
        if (match.Success)
        {
            translated = TranslateDimensionAdjective(match.Groups["adjective"].Value) + match.Groups["shape"].Value;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCultForm(string source, out string translated)
    {
        var match = CultOfPattern.Match(source);
        if (match.Success)
        {
            translated = "*CultSymbol*の" + TranslateCultKind(match.Groups["cult"].Value);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateMutationCultForm(string source, out string translated)
    {
        translated = source switch
        {
            "Charming *cult*" => "魅惑の*cult*",
            "*cult* and Love" => "愛と*cult*",
            "Verdant *cult*" => "新緑の*cult*",
            "*cult*, Aspect of Plants" => "植物の相としての*cult*",
            "All-Seeing *cult*" => "全視の*cult*",
            "*cult* and the Eye" => "眼と*cult*",
            "Entropic *cult*" => "エントロピーの*cult*",
            "*cult* and Entropy" => "エントロピーと*cult*",
            "Frigid *cult*" => "凍てつく*cult*",
            "*cult*, Aspect of Winter" => "冬の相としての*cult*",
            "Tyrannical *cult*" => "専制の*cult*",
            "Astral *cult*" => "アストラルの*cult*",
            "*cult* and Astral Projection" => "アストラル投射と*cult*",
            "Hidden *cult*" => "隠れた*cult*",
            "Lost *cult*" => "失われた*cult*",
            "Disjointed *cult*" => "分断された*cult*",
            "*cult*, the Divided" => "分かたれし*cult*",
            "Shining *cult*" => "輝く*cult*",
            "*cult* and Light" => "光と*cult*",
            "Unified *cult*" => "統一された*cult*",
            "*cult*, Aspect of Oneness" => "一体性の相としての*cult*",
            "Two-Faced *cult*" => "二面の*cult*",
            "*cult* and the Parallel" => "並行性と*cult*",
            "*cult* and the Before" => "過去と*cult*",
            "Oracular *cult*" => "予言の*cult*",
            "*cult*, Chrome and Brass" => "クロムと真鍮の*cult*",
            "*cult*, the Circuit-Maze" => "回路迷宮の*cult*",
            "Fevered *cult*" => "熱病の*cult*",
            "*cult*, Aspect of Fire" => "火の相としての*cult*",
            "traveling *cult*" => "旅する*cult*",
            "cosmic *cult*" => "宇宙の*cult*",
            "*cult*, the Door" => "扉たる*cult*",
            "violent *cult*" => "暴力の*cult*",
            "*cult*, Aspect of Inertia" => "慣性の相としての*cult*",
            "cerebral *cult*" => "脳髄の*cult*",
            "*cult* and the Mind" => "精神と*cult*",
            "vampiric *cult*" => "吸血の*cult*",
            "*cult*, the Succubus" => "サキュバスたる*cult*",
            "*cult*, the Mover" => "動かす者たる*cult*",
            "Long-Arm *cult*" => "長き腕の*cult*",
            "*cult*, the Here and There" => "こことあそこにある*cult*",
            "entangled *cult*" => "もつれた*cult*",
            "fickle *cult*" => "移り気な*cult*",
            "whimsical *cult*" => "気まぐれな*cult*",
            "*cult*, Now and Then" => "今とその時の*cult*",
            "Immortal *cult*" => "不朽の*cult*",
            "*cult*, the Many" => "多数なる*cult*",
            "popular *cult*" => "民衆の*cult*",
            _ => source,
        };
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static string TranslateRealmKind(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "realm" => "領域",
            "sphere" => "球界",
            "zone" => "帯域",
            "domain" => "領土",
            "dominion" => "支配圏",
            "orbit" => "軌道",
            "plane" => "平面",
            "expanse" => "広がり",
            "unit" => "単位",
            "quantity" => "量",
            "radius" => "半径",
            "space" => "空間",
            "degree" => "度",
            "stratum" => "層",
            _ => source,
        };
    }

    private static string TranslateVoidKind(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "void" => "虚空",
            "nether" => "冥界",
            "vacuum" => "真空",
            "nihility" => "虚無",
            "nullity" => "無",
            "abyss" => "深淵",
            "chasm" => "裂け目",
            "fissure" => "亀裂",
            "gulf" => "隔たり",
            "gap" => "間隙",
            "lacuna" => "欠落",
            "womb" => "胎内",
            "nothingness" => "無",
            "schism" => "分裂",
            _ => source,
        };
    }

    private static string TranslateDimensionAdjective(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "vacuous" => "空虚な",
            "vacant" => "空いた",
            "hollow" => "うつろな",
            "barren" => "不毛の",
            "pale" => "青白い",
            "spotless" => "無垢の",
            "blank" => "空白の",
            "empty" => "空の",
            "prosaic" => "散文的な",
            "dreary" => "陰鬱な",
            "dead" => "死んだ",
            "flat" => "平坦な",
            "inert" => "不活性の",
            "forsaken" => "見捨てられた",
            "colossal" => "巨大な",
            "vast" => "広大な",
            "eternal" => "永遠の",
            "boundless" => "果てしない",
            "infinite" => "無限の",
            _ => source,
        };
    }

    private static string TranslateCultKind(string source)
    {
        return StringHelpers.LowerAscii(source) switch
        {
            "cult" => "カルト",
            "order" => "教団",
            "society" => "結社",
            "church" => "教会",
            "folk" => "民",
            "brood" => "眷属",
            "family" => "一族",
            "kith" => "同族",
            "people" => "人々",
            "clan" => "氏族",
            "flock" => "群れ",
            "sect" => "宗派",
            "kinfolk" => "親族",
            "cabal" => "秘密結社",
            "host" => "軍勢",
            _ => source,
        };
    }
}
