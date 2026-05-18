using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class EaterCryptPlaqueTextTranslator
{
    private const string NullSeedMarker = "*shortMarkov*";

    private static readonly Regex CryptIntroPattern = new(
        "^(?<intro>Here Rests|Here Lies|Inside Lies|Inside Rests|Here Rest|Here Lie|Inside Lie|Inside Rest|Sheltered Here under Gjaus is|Sheltered Here under Gjaus are)(?: (?<cognomen>.*?))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FamilyTitleOfPattern = new(
        "^The (?<term>family|Family|clan|brood|tribe|kith|kinfolk|children|folk|progeny) of (?<family>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FamilyTitleTrailingPattern = new(
        "^The (?<family>.+?) (?<term>family|Family|clan|brood|tribe|kith|kinfolk|children|folk|progeny)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CognomenPattern = new(
        "^the (?:(?<adjective>.+?) )?(?<role>.+?) of$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex WisdomSeedPattern = new(
        "^(?<term>wisdom|knowledge|enlightenment) (?<seed>\\*markovSeed:is\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex TutorKnowledgePattern = new(
        "^Only the (?<term>wise|shrewd|learned|erudite|controversial|cerebral|profound|methodical) know (?<seed>\\*markovSeed:what\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ThreeTermSeedPattern = new(
        "^(?<first>wisdom|knowledge|enlightenment|bravery|ferocity|honor|courage|valor|violence|wardenship|protection|bloodshed|war|combat|might), (?<second>quills|inkwells|scrolls|swords|guns|maces|axes|iron gauntlets|skulls|hammers|pummels|bones|helmets|breastplates|gauntlets|boots), and (?<seed>\\*markovSeed:a\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex SeedActionPattern = new(
        "^(?<verb>Question|Fear|Quiver at|Tremble before|Dread|Shun|Be in awe of|Bless|Thank|Exalt|Give thanks for|Praise|Honor) (?<seed>\\*markovSeed:(?:the|for)\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex WarriorIdealPattern = new(
        "^(?<term>bravery|ferocity|honor|courage|valor|violence|wardenship|protection|bloodshed|war|combat|might) (?<seed>\\*markovSeed:is\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ApproachFoePattern = new(
        "^(?<verb>approach|meet|begin|match|come at|surround|threaten|accost) (?<foe>death|dying|the void|mortality|misfortune|battle|foes|adversaries|enemies) (?<seed>\\*markovSeed:with\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PriestIdealPattern = new(
        "^(?<term>godliness|god|divinity|virtue|piety|Gjaus|faith|holiness) (?<seed>\\*markovSeed:is\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex LoveNounPattern = new(
        "^(?<verb>love|revere|honor|worship|cherish|venerate|esteem|treasure|pay homage to) the (?<noun>.+), (?<seed>\\*markovSeed:for,so,because\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PrayerSeedPattern = new(
        "^(?<verb>voice|utter|say|sound) a (?<prayer>prayer|blessing) (?<seed>\\*markovSeed:for\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex RoyalOurPattern = new(
        "^Our (?<noun>.+?) (?<seed>\\*markovSeed:is\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex RoyalWePattern = new(
        "^We (?<seed>\\*markovSeed:are,do,see,feel,know,have,say,go,take\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HarkPattern = new(
        "^(?<hark>Hark|Attend|Attention|Pay heed|Adventurer)! (?<seed>\\*shortMarkov\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var original = source!;
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(original, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (TryTranslatePattern(CryptIntroPattern, stripped, spans, original, TranslateCryptIntro, out translated)
            || TryTranslatePattern(FamilyTitleOfPattern, stripped, spans, original, TranslateFamilyTitle, out translated)
            || TryTranslatePattern(FamilyTitleTrailingPattern, stripped, spans, original, TranslateFamilyTitle, out translated)
            || TryTranslateFamilyWords(stripped, spans, original, out translated)
            || TryTranslatePattern(CognomenPattern, stripped, spans, original, TranslateCognomen, out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static string TranslateCryptIntro(Match match, IReadOnlyList<ColorSpan> spans)
    {
        _ = spans;
        var cognomen = TranslateCryptIntroCognomen(Restore(match, "cognomen"));
        var intro = match.Groups["intro"].Value switch
        {
            "Here Rests" or "Here Lies" or "Inside Lies" or "Inside Rests" => "ここに眠る",
            "Here Rest" or "Here Lie" or "Inside Lie" or "Inside Rest" => "ここに眠る",
            "Sheltered Here under Gjaus is" or "Sheltered Here under Gjaus are" => "ジャウスの庇護の下、ここに眠る",
            _ => string.Empty,
        };
        return string.IsNullOrWhiteSpace(cognomen)
            ? intro
            : intro + " " + cognomen;
    }

    private static string TranslateFamilyTitle(Match match, IReadOnlyList<ColorSpan> spans)
    {
        _ = spans;
        return Restore(match, "family") + "の" + TranslateFamilyTerm(match.Groups["term"].Value);
    }

    private static string TranslateCognomen(Match match, IReadOnlyList<ColorSpan> spans)
    {
        _ = spans;
        var role = TranslateFamilyTerm(match.Groups["role"].Value);
        var adjective = TranslateFamilyTerm(Restore(match, "adjective"));
        return string.IsNullOrWhiteSpace(adjective)
            ? role + "の"
            : JoinAdjectiveRole(adjective, role) + "の";
    }

    private static bool TryTranslateFamilyWords(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        out string translated)
    {
        if (TryTranslatePattern(WisdomSeedPattern, stripped, spans, source, static (match, _) =>
                TranslateTerm(match.Groups["term"].Value) + "、" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(TutorKnowledgePattern, stripped, spans, source, static (match, _) =>
                TranslateTerm(match.Groups["term"].Value) + "者だけが" + NullSeedMarker + "を知る",
                out translated)
            || TryTranslatePattern(ThreeTermSeedPattern, stripped, spans, source, static (match, _) =>
                TranslateTerm(match.Groups["first"].Value) + "、" + TranslateTerm(match.Groups["second"].Value) + "、そして" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(SeedActionPattern, stripped, spans, source, static (match, _) =>
                NullSeedMarker + "を" + TranslateAction(match.Groups["verb"].Value),
                out translated)
            || TryTranslatePattern(WarriorIdealPattern, stripped, spans, source, static (match, _) =>
                TranslateTerm(match.Groups["term"].Value) + "、" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(ApproachFoePattern, stripped, spans, source, static (match, _) =>
                TranslateFoe(match.Groups["foe"].Value) + "に" + TranslateApproach(match.Groups["verb"].Value) + "、" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(PriestIdealPattern, stripped, spans, source, static (match, _) =>
                TranslateTerm(match.Groups["term"].Value) + "、" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(LoveNounPattern, stripped, spans, source, static (match, _) =>
                TranslateNoun(match.Groups["noun"].Value) + "を" + TranslateLove(match.Groups["verb"].Value) + "、" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(PrayerSeedPattern, stripped, spans, source, static (match, _) =>
                TranslatePrayer(match.Groups["prayer"].Value) + "を唱えよ、" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(RoyalOurPattern, stripped, spans, source, static (match, _) =>
                "われらの" + TranslateNoun(match.Groups["noun"].Value) + "、" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(RoyalWePattern, stripped, spans, source, static (match, _) =>
                "われらは、" + NullSeedMarker,
                out translated)
            || TryTranslatePattern(HarkPattern, stripped, spans, source, static (match, _) =>
                TranslateHark(match.Groups["hark"].Value) + "！ " + match.Groups["seed"].Value,
                out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePattern(
        Regex pattern,
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        string source,
        Func<Match, IReadOnlyList<ColorSpan>, string> build,
        out string translated)
    {
        var match = pattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            build(match, spans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string TranslateFamilyTerm(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var translated = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source);
        if (translated is not null)
        {
            return translated;
        }

        return source.ToUpperInvariant() switch
        {
            "FAMILY" => "家",
            "CLAN" => "氏族",
            "BROOD" => "一族",
            "TRIBE" => "部族",
            "KITH" => "同族",
            "KINFOLK" => "一族の者たち",
            "CHILDREN" => "子ら",
            "FOLK" => "民",
            "PROGENY" => "子孫",
            "TUTORS" => "師",
            "LEARNED" => "学識ある",
            "KINDRED" => "同族",
            _ => source,
        };
    }

    private static string TranslateCryptIntroCognomen(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var match = CognomenPattern.Match(source);
        return match.Success ? TranslateCognomen(match, []) : source;
    }

    private static string JoinAdjectiveRole(string adjective, string role)
    {
        return adjective.EndsWith("ある", StringComparison.Ordinal)
            ? adjective + role
            : adjective + "な" + role;
    }

    private static string Restore(Match match, string groupName)
    {
        var group = match.Groups[groupName];
        return group.Success ? group.Value.Trim() : string.Empty;
    }

    private static string TranslateTerm(string term)
    {
        return term.ToUpperInvariant() switch
        {
            "WISDOM" => "知恵",
            "KNOWLEDGE" => "知識",
            "ENLIGHTENMENT" => "啓悟",
            "WISE" => "賢き",
            "SHREWD" => "抜け目なき",
            "LEARNED" => "学識ある",
            "ERUDITE" => "博識なる",
            "CONTROVERSIAL" => "物議を醸す",
            "CEREBRAL" => "知的な",
            "PROFOUND" => "深遠なる",
            "METHODICAL" => "緻密なる",
            "QUILLS" => "羽ペン",
            "INKWELLS" => "インク壺",
            "SCROLLS" => "巻物",
            "BRAVERY" => "勇敢",
            "FEROCITY" => "獰猛さ",
            "HONOR" => "名誉",
            "COURAGE" => "勇気",
            "VALOR" => "武勇",
            "VIOLENCE" => "暴力",
            "WARDENSHIP" => "守護",
            "PROTECTION" => "庇護",
            "BLOODSHED" => "流血",
            "WAR" => "戦",
            "COMBAT" => "戦闘",
            "MIGHT" => "力",
            "SWORDS" => "剣",
            "GUNS" => "銃",
            "MACES" => "メイス",
            "AXES" => "斧",
            "IRON GAUNTLETS" => "鉄の籠手",
            "SKULLS" => "頭蓋",
            "HAMMERS" => "槌",
            "PUMMELS" => "殴打",
            "BONES" => "骨",
            "HELMETS" => "兜",
            "BREASTPLATES" => "胸当て",
            "GAUNTLETS" => "籠手",
            "BOOTS" => "靴",
            "GODLINESS" => "信心",
            "GOD" => "神",
            "DIVINITY" => "神性",
            "VIRTUE" => "徳",
            "PIETY" => "敬虔",
            "GJAUS" => "ジャウス",
            "FAITH" => "信仰",
            "HOLINESS" => "聖性",
            _ => term,
        };
    }

    private static string TranslateAction(string action)
    {
        return action.ToUpperInvariant() switch
        {
            "QUESTION" => "問え",
            "FEAR" => "恐れよ",
            "QUIVER AT" => "前に震えよ",
            "TREMBLE BEFORE" => "前におののけ",
            "DREAD" => "恐怖せよ",
            "SHUN" => "避けよ",
            "BE IN AWE OF" => "畏敬せよ",
            "BLESS" => "祝福せよ",
            "THANK" => "感謝せよ",
            "EXALT" => "称えよ",
            "GIVE THANKS FOR" => "感謝を捧げよ",
            "PRAISE" => "賛美せよ",
            "HONOR" => "讃えよ",
            _ => action,
        };
    }

    private static string TranslateApproach(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "APPROACH" or "MEET" or "COME AT" or "ACCOST" => "立ち向かえ",
            "BEGIN" => "始めよ",
            "MATCH" => "相対せよ",
            "SURROUND" => "包囲せよ",
            "THREATEN" => "脅かせ",
            _ => source,
        };
    }

    private static string TranslateFoe(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "DEATH" => "死",
            "DYING" => "死にゆくもの",
            "THE VOID" => "虚無",
            "MORTALITY" => "死すべき定め",
            "MISFORTUNE" => "不運",
            "BATTLE" => "戦い",
            "FOES" => "敵",
            "ADVERSARIES" => "敵対者",
            "ENEMIES" => "敵",
            _ => source,
        };
    }

    private static string TranslateLove(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "LOVE" or "CHERISH" or "TREASURE" => "愛せよ",
            "REVERE" or "VENERATE" or "PAY HOMAGE TO" => "崇敬せよ",
            "HONOR" or "ESTEEM" => "讃えよ",
            "WORSHIP" => "崇拝せよ",
            _ => source,
        };
    }

    private static string TranslatePrayer(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "PRAYER" => "祈り",
            "BLESSING" => "祝福",
            _ => source,
        };
    }

    private static string TranslateNoun(string source)
    {
        if (string.Equals(source, "sultan", StringComparison.OrdinalIgnoreCase))
        {
            return "スルタン";
        }

        var translated = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(source);
        return translated is null ? source : translated;
    }

    private static string TranslateHark(string hark)
    {
        return hark.ToUpperInvariant() switch
        {
            "HARK" or "ATTEND" or "ATTENTION" or "PAY HEED" => "聞け",
            "ADVENTURER" => "冒険者よ",
            _ => hark,
        };
    }
}
