using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class VillageTerrainRevealDescriptionTranslator
{
    private static readonly string PeoplePattern = BuildAlternation(
        "people",
        "folk",
        "communities",
        "kindred",
        "families",
        "kin");

    private static readonly string GatherPattern = BuildAlternation(
        "come together",
        "habitate together",
        "live together",
        "assemble",
        "cluster",
        "gather");

    private static readonly string ReverencePattern = BuildAlternation(
        "deification",
        "reverence",
        "adoration",
        "devotion",
        "worship",
        "piety",
        "honor",
        "love",
        "awe");

    private static readonly string ProfanePattern = BuildAlternation(
        "blaspheme",
        "profane",
        "violate",
        "scorn",
        "mock");

    private static readonly string GatheringPattern = BuildAlternation(
        "congregation",
        "settlement",
        "gathering",
        "conclave",
        "society",
        "flock",
        "band");

    private static readonly string KinPattern = BuildAlternation(
        "kinsfolk",
        "kindred",
        "families",
        "people",
        "tribe",
        "clan",
        "folk",
        "kind",
        "kin");

    private static readonly Regex TerrainFirstReverencePattern = new(
        "^(?<terrain>.+?), (?<people>" + PeoplePattern + ") (?<gather>" + GatherPattern + ") in (?<reverence>" + ReverencePattern + ") of (?<sacred>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PeopleFirstReverencePattern = new(
        "^(?<people>" + PeoplePattern + ") (?<gather>" + GatherPattern + ") (?<terrain>.+?) in (?<reverence>" + ReverencePattern + ") of (?<sacred>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex TerrainFirstProfanePattern = new(
        "^(?<terrain>.+?), (?<people>" + PeoplePattern + ") (?<gather>" + GatherPattern + ") to (?<profane>" + ProfanePattern + ") (?<profaneThing>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PeopleFirstProfanePattern = new(
        "^(?<people>" + PeoplePattern + ") (?<gather>" + GatherPattern + ") (?<terrain>.+?) to (?<profane>" + ProfanePattern + ") (?<profaneThing>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex TerrainFirstFactionPattern = new(
        "^(?<terrain>.+?), there's a (?<gathering>" + GatheringPattern + ") of (?<faction>.+?) and their (?<kin>" + KinPattern + ")\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex FactionFirstPattern = new(
        "^there's a (?<gathering>" + GatheringPattern + ") of (?<faction>.+?) and their (?<kin>" + KinPattern + ") (?<terrain>.+?)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly IReadOnlyDictionary<string, string> CaptureTerms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Glow"] = "輝き",
            ["the Glow"] = "輝き",
            ["Mechanimists"] = "メカニマス教団",
            ["the Mechanimists"] = "メカニマス教団",
        };

    private static readonly IReadOnlyDictionary<string, string> TerrainFragments =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["over the flats"] = "平地の上で",
            ["neath gusts of poisoned salt"] = "毒塩の突風の下で",
            ["buried under the crescent dunes"] = "三日月砂丘の下に埋もれて",
            ["shrouded in motes of fireflies"] = "蛍の微光に包まれて",
            ["tangled in brineweed"] = "塩水草に絡まれて",
            ["in the still marsh waters"] = "静かな沼沢の水の中で",
            ["deep within an earth-cleft"] = "大地の裂け目の奥深くで",
            ["under the labyrinthine shade"] = "迷宮じみた陰の下で",
            ["nestled in the crumbling shale"] = "崩れゆく頁岩に抱かれて",
            ["nestled between the salt-spangled hills"] = "塩きらめく丘の間に抱かれて",
            ["in a riven valley"] = "裂けた谷で",
            ["lit by the high salt sun"] = "高き塩の太陽に照らされて",
            ["over the prismatic loam"] = "虹色のロームの上で",
            ["on a meadow of honeysuckle"] = "スイカズラの草地で",
            ["through the petal-strewn steppe"] = "花びら散るステップを抜けて",
            ["hidden by the choking canopy"] = "息詰まる天蓋に隠されて",
            ["root-strangled to the earth"] = "根に締めつけられて大地に伏し",
            ["echoing the hoots of apes against its chrome walls"] = "クロムの壁に猿の叫びを響かせて",
            ["wreathed around the root maze"] = "根の迷路に巻きつかれて",
            ["inside a hollowed-out nimbus beam"] = "くり抜かれたニンバス梁の内側で",
            ["flooded by Svy's gushes"] = "スヴィの奔流に浸されて",
            ["flooded by the river's gushes"] = "川の奔流に浸されて",
            ["flooded by Opal's gushes"] = "オパールの奔流に浸されて",
            ["flooded by Yonth's gushes"] = "ヨンスの奔流に浸されて",
            ["flooded by alluvial gushes"] = "沖積の奔流に浸されて",
            ["rotted by the salt water"] = "塩水に腐食されて",
            ["under the chrome carcasses of giants"] = "巨人たちのクロムの骸の下で",
            ["inside a nebula of spores"] = "胞子の星雲の中で",
            ["through brooks of primordial soup"] = "原始のスープの小川を抜けて",
            ["neath a kaleidoscope of shrooms"] = "万華鏡めいた茸の下で",
            ["blooming with algae"] = "藻に咲き覆われて",
            ["threaded with fingers of coral"] = "珊瑚の指に縫い通されて",
            ["through a porous bed of sponge"] = "多孔質の海綿床を抜けて",
            ["under the polyp-strewn trellis"] = "ポリプ散る格子棚の下で",
            ["pleated to the labyrinth of coral"] = "珊瑚の迷宮へと折り重なって",
            ["through the crystal labyrinth"] = "結晶の迷宮を抜けて",
            ["moated by pools of warm static"] = "温かな静電気の池に囲まれて",
            ["on a black marble glade"] = "黒大理石の空き地で",
            ["surrounded by the hum"] = "低い唸りに囲まれて",
            ["under a chrome arch"] = "クロムのアーチの下で",
            ["through knots of braided rust"] = "編まれた錆の結び目を抜けて",
            ["entombed in a sarcophagus of circuitry"] = "回路の石棺に葬られて",
            ["under a crysteel balustrade"] = "クリスタル鋼の手すりの下で",
            ["atop the star orchid cornices"] = "星蘭のコーニスの上で",
            ["behind a cryptic freize"] = "謎めいたフリーズの背後で",
            ["atop a high fastness"] = "高き要塞の上で",
            ["looking out over the poisoned jungle"] = "毒の密林を見渡して",
            ["chiseled into the limestone"] = "石灰岩に刻み込まれて",
        };

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
        if (TryTranslatePattern(TerrainFirstReverencePattern, stripped, spans, original, BuildTerrainFirstReverence, out translated)
            || TryTranslatePattern(PeopleFirstReverencePattern, stripped, spans, original, BuildPeopleFirstReverence, out translated)
            || TryTranslatePattern(TerrainFirstProfanePattern, stripped, spans, original, BuildTerrainFirstProfane, out translated)
            || TryTranslatePattern(PeopleFirstProfanePattern, stripped, spans, original, BuildPeopleFirstProfane, out translated)
            || TryTranslatePattern(TerrainFirstFactionPattern, stripped, spans, original, BuildTerrainFirstFaction, out translated)
            || TryTranslatePattern(FactionFirstPattern, stripped, spans, original, BuildFactionFirst, out translated))
        {
            return true;
        }

        translated = original;
        return false;
    }

    private static string BuildTerrainFirstReverence(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return TranslateTerrain(match, spans) + "、"
            + TranslateComponent(match, "people") + "が" + TranslateCapture(match, spans, "sacred") + "を"
            + TranslateComponent(match, "reverence") + "して" + TranslateComponent(match, "gather") + "。";
    }

    private static string BuildPeopleFirstReverence(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return TranslateComponent(match, "people") + "が" + TranslateCapture(match, spans, "sacred") + "を"
            + TranslateComponent(match, "reverence") + "して" + TranslateTerrain(match, spans) + TranslateComponent(match, "gather") + "。";
    }

    private static string BuildTerrainFirstProfane(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return TranslateTerrain(match, spans) + "、"
            + TranslateComponent(match, "people") + "が" + TranslateCapture(match, spans, "profaneThing") + "を"
            + TranslateProfaneAction(match) + "ために" + TranslateComponent(match, "gather") + "。";
    }

    private static string BuildPeopleFirstProfane(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return TranslateComponent(match, "people") + "が" + TranslateCapture(match, spans, "profaneThing") + "を"
            + TranslateProfaneAction(match) + "ために" + TranslateTerrain(match, spans) + TranslateComponent(match, "gather") + "。";
    }

    private static string BuildTerrainFirstFaction(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return TranslateTerrain(match, spans) + "、" + TranslateCapture(match, spans, "faction") + "とその"
            + TranslateFactionKin(match) + "の" + TranslateFactionGathering(match) + "がいる。";
    }

    private static string BuildFactionFirst(Match match, IReadOnlyList<ColorSpan> spans)
    {
        return TranslateTerrain(match, spans) + "、" + TranslateCapture(match, spans, "faction") + "とその"
            + TranslateFactionKin(match) + "の" + TranslateFactionGathering(match) + "がいる。";
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

        var captureSpans = ColorAwareTranslationComposer.WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length);
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            build(match, captureSpans),
            spans,
            stripped.Length,
            source);
        return true;
    }

    private static string TranslateTerrain(Match match, IReadOnlyList<ColorSpan> spans)
    {
        var group = match.Groups["terrain"];
        var terrain = group.Value;
        var translatedFragments = new List<string>();
        var index = 0;
        while (index < terrain.Length)
        {
            var separatorIndex = terrain.IndexOf(" and ", index, StringComparison.Ordinal);
            var fragmentEnd = separatorIndex < 0 ? terrain.Length : separatorIndex;
            var fragment = terrain.Substring(index, fragmentEnd - index);
            var removeTerminal = separatorIndex >= 0;
            translatedFragments.Add(TranslateTerrainFragment(fragment, spans, group.Index + index, fragment.Length, removeTerminal));
            if (separatorIndex < 0)
            {
                break;
            }

            index = separatorIndex + " and ".Length;
        }

        if (translatedFragments.Count == 1)
        {
            return translatedFragments[0];
        }

        return string.Join("と", translatedFragments);
    }

    private static string TranslateTerrainFragment(
        string stripped,
        IReadOnlyList<ColorSpan> spans,
        int sourceStart,
        int sourceLength,
        bool removeTerminal)
    {
        if (TerrainFragments.TryGetValue(stripped.Trim(), out var translated))
        {
            var normalized = removeTerminal ? RemoveTerminalDe(translated) : translated;
            return ColorAwareTranslationComposer.RestoreSlice(normalized, spans, sourceStart, sourceLength).Trim();
        }

        return ColorAwareTranslationComposer.RestoreSlice(stripped, spans, sourceStart, sourceLength).Trim();
    }

    private static string RemoveTerminalDe(string source)
    {
        return source.EndsWith("で", StringComparison.Ordinal)
            ? source.Substring(0, source.Length - 1)
            : source;
    }

    private static string TranslateComponent(Match match, string groupName)
    {
        var value = match.Groups[groupName].Value;
        var translated = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(value);
        if (translated is not null)
        {
            return translated;
        }

        Trace.TraceWarning(
            "QudJP: {0} missing HistorySpice component translation for '{1}'.",
            nameof(VillageTerrainRevealDescriptionTranslator),
            value);
        return value;
    }

    private static string TranslateProfaneAction(Match match)
    {
        return StringHelpers.LowerAscii(match.Groups["profane"].Value) switch
        {
            "profane" => "冒涜する",
            "mock" => "嘲る",
            "scorn" => "侮る",
            "violate" => "踏みにじる",
            "blaspheme" => "冒瀆する",
            _ => TranslateComponent(match, "profane"),
        };
    }

    private static string TranslateFactionKin(Match match)
    {
        return StringHelpers.LowerAscii(match.Groups["kin"].Value) switch
        {
            "kin" or "kinsfolk" or "kindred" or "families" or "people" or "tribe" or "clan" or "folk" or "kind" => "同胞",
            _ => TranslateComponent(match, "kin"),
        };
    }

    private static string TranslateFactionGathering(Match match)
    {
        return StringHelpers.LowerAscii(match.Groups["gathering"].Value) switch
        {
            "flock" or "band" or "congregation" or "settlement" or "gathering" or "conclave" or "society" => "一団",
            _ => TranslateComponent(match, "gathering"),
        };
    }

    private static string TranslateCapture(Match match, IReadOnlyList<ColorSpan> spans, string groupName)
    {
        var group = match.Groups[groupName];
        var translated = TranslateCaptureVisible(group.Value);
        return ColorAwareTranslationComposer.MarkupAwareRestoreCapture(translated, spans, group).Trim();
    }

    private static string TranslateCaptureVisible(string source)
    {
        var trimmed = source.Trim();
        if (CaptureTerms.TryGetValue(trimmed, out var captureTerm))
        {
            return captureTerm;
        }

        var scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(trimmed);
        if (scoped is not null)
        {
            return scoped;
        }

        var articleless = StringHelpers.StripLeadingEnglishArticle(trimmed);
        if (CaptureTerms.TryGetValue(articleless, out captureTerm))
        {
            return captureTerm;
        }

        scoped = HistorySpiceComponentLookup.TranslateExactOrLowerAscii(articleless);
        if (scoped is not null)
        {
            return scoped;
        }

        return HistorySpiceComponentLookup.TryTranslateTitlePhrase(articleless, out var titlePhrase)
            ? titlePhrase
            : articleless;
    }

    private static string BuildAlternation(params string[] values)
    {
        return string.Join("|", values.OrderByDescending(static value => value.Length).Select(Regex.Escape));
    }
}
