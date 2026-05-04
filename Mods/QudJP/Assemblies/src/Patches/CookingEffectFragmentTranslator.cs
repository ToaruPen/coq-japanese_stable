using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace QudJP.Patches;

internal static class CookingEffectFragmentTranslator
{
    private static readonly object SyncRoot = new object();
    private static Dictionary<string, string>? mutationDisplayNames;

    private static readonly IReadOnlyDictionary<string, string> CookingAbilityNameOverrides =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Electromagnetic Pulse"] = "電磁パルス",
            ["Will Force"] = "意志の力",
            ["Burrowing Claws"] = "掘爪",
            ["Psychometry"] = "サイコメトリー",
            ["Burgeoning"] = "バージョニング",
            ["Quills"] = "棘",
            ["Sticky Tongue"] = "粘着舌",
            ["Intimidate"] = "威圧",
        };

    private static readonly IReadOnlyList<TranslationRule> Rules =
    [
        new(
            "ElectricDischarge",
            new Regex(
                "^@they release an electrical discharge per Electrical Generation at level (?<level>.+?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@they は電気生成レベル",
                RestoreVisible(match.Groups["level"], spans),
                "の放電を行う。")),
        new(
            "ElectricEmp",
            new Regex(
                "^@they release an electromagnetic pulse at level (?<level>.+?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@they はレベル",
                RestoreVisible(match.Groups["level"], spans),
                "の電磁パルスを放つ。")),
        new(
            "ElectricDamaged",
            new Regex(
                "^whenever @thisCreature take@s electric damage, there's (?<chance>.+?) chance$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@thisCreature が電撃ダメージを受けるたび、",
                NormalizeChance(RestoreVisible(match.Groups["chance"], spans)),
                "の確率で")),
        new(
            "TakeDamageChance",
            new Regex(
                "^whenever @thisCreature take@s damage, there's (?<chance>.+?) chance$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@thisCreature がダメージを受けるたび、",
                NormalizeChance(RestoreVisible(match.Groups["chance"], spans)),
                "の確率で")),
        new(
            "PhaseOnDamage",
            new Regex(
                "^whenever @thisCreature take@s damage, there's (?<chance>.+?) chance @they start phasing for (?<turns>.+?) turns\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@thisCreature がダメージを受けるたび、",
                NormalizeChance(RestoreVisible(match.Groups["chance"], spans)),
                "の確率で@they は",
                RestoreVisible(match.Groups["turns"], spans),
                "ターンのあいだフェイズアウトする。")),
        new(
            "TeleportOnAvoidableDamage",
            new Regex(
                "^Whenever @thisCreature take@s avoidable damage, there's (?<chance>.+?) chance @they teleport to a random space on the map instead\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@thisCreature が回避可能なダメージを受けると",
                NormalizeChance(RestoreVisible(match.Groups["chance"], spans)),
                "の確率で代わりにマップ内のランダムな地点へテレポートする。")),
        new(
            "TimedStatGain",
            new Regex(
                "^@they gain@s \\+(?<value>\\d+(?:-\\d+)?) (?<stat>Agility|AV|Strength) for (?<duration>\\d+(?:-\\d+)?) (?<unit>turns?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@they は",
                RestoreVisible(match.Groups["duration"], spans),
                TranslateDurationUnit(match.Groups["unit"].Value),
                "のあいだ",
                TranslateUnitStat(match.Groups["stat"].Value),
                "+",
                RestoreVisible(match.Groups["value"], spans),
                "を得る。")),
        new(
            "TimedResistanceGain",
            new Regex(
                "^@they gain (?<value>\\d+(?:-\\d+)?) (?<stat>cold resist|Cold Resist|Electric Resist|Heat Resist) for (?<duration>\\d+(?:-\\d+)?) (?<unit>turns?|hours?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@they は",
                RestoreVisible(match.Groups["duration"], spans),
                TranslateDurationUnit(match.Groups["unit"].Value),
                "のあいだ",
                TranslateUnitStat(match.Groups["stat"].Value),
                "+",
                RestoreVisible(match.Groups["value"], spans),
                "を得る。")),
        new(
            "ArmorPenetration",
            new Regex(
                "^whenever @thisCreature suffer@s (?<level>.+?)X or greater physical penetration,$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@thisCreature が",
                RestoreVisible(match.Groups["level"], spans),
                "倍以上の物理貫通を受けるたび、")),
        new(
            "ReflectOnce",
            new Regex(
                "^Reflect 100% damage the next time @they take damage\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (_, _) => "@they が次にダメージを受けたとき、そのダメージを100%反射する。"),
        new(
            "ReflectMany",
            new Regex(
                "^Reflect 100% damage the next (?<times>.+?) times @they take damage\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@they が次の",
                RestoreVisible(match.Groups["times"], spans),
                "回ダメージを受けたとき、そのダメージを100%反射する。")),
        new(
            "HpIncreaseDescription",
            new Regex(
                "^@they get \\+(?<percent>\\d+(?:-\\d+)?)% max HP for (?<hours>\\d+) hours?\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@they は",
                RestoreVisible(match.Groups["hours"], spans),
                "時間のあいだ最大HP+",
                RestoreVisible(match.Groups["percent"], spans),
                "%を得る。")),
        new(
            "HpIncreaseDetails",
            new Regex(
                "^\\+(?<percent>\\d+(?:-\\d+)?)% max HP$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "最大HP+",
                RestoreVisible(match.Groups["percent"], spans),
                "%")),
        new(
            "BasicHitPoints",
            new Regex(
                "^(?<value>[+-]\\d+(?:-\\d+)?)(?<percent>%)? hit points$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "HP",
                RestoreVisible(match.Groups["value"], spans),
                match.Groups["percent"].Value)),
        new(
            "BasicMoveSpeed",
            new Regex(
                "^(?<value>[+-]\\d+(?:-\\d+)?)(?<percent>%)? Move Speed$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "移動速度",
                RestoreVisible(match.Groups["value"], spans),
                match.Groups["percent"].Value)),
        new(
            "BasicToHit",
            new Regex(
                "^(?<value>[+-]\\d+(?:-\\d+)?) to hit$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "命中",
                RestoreVisible(match.Groups["value"], spans))),
        new(
            "BasicXpGained",
            new Regex(
                "^(?<value>[+-]\\d+(?:-\\d+)?)(?<percent>%)? XP gained$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "獲得XP",
                RestoreVisible(match.Groups["value"], spans),
                match.Groups["percent"].Value)),
        new(
            "UnitStat",
            new Regex(
                "^\\+(?<value>\\d+(?:-\\d+)?) (?<stat>Acid Resistance|Cold Resistance|Electric Resist(?:ance)?|Heat Resistance|AV|DV|Agility|Ego|Intelligence|MA|Quickness|Strength|Toughness|Willpower|STR)$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                TranslateUnitStat(match.Groups["stat"].Value),
                "+",
                RestoreVisible(match.Groups["value"], spans))),
        new(
            "NaturalHealingRate",
            new Regex(
                "^\\+(?<percent>\\d+(?:-\\d+)?)% to natural healing rate$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "自然治癒速度+",
                RestoreVisible(match.Groups["percent"], spans),
                "%")),
        new(
            "BleedingSaveBonus",
            new Regex(
                "^\\+(?<value>\\d+(?:-\\d+)?) to saves vs\\. bleeding$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "出血に対するセーヴ+",
                RestoreVisible(match.Groups["value"], spans))),
        new(
            "ReflectDamageBack",
            new Regex(
                "^Reflect (?<percent>\\d+(?:-\\d+)?)% damage back at @their attackers, rounded up\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "@their 攻撃者にダメージの",
                RestoreVisible(match.Groups["percent"], spans),
                "%を切り上げて反射する。")),
        new(
            "CanUseMutationWithBonus",
            new Regex(
                "^Can use (?<name>.+?) at level (?<level>\\d+(?:-\\d+)?)\\. If @they already have \\k<name>, it's enhanced by (?<bonus>\\d+(?:-\\d+)?) levels\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "〈",
                TranslateCapturedAbilityName(match.Groups["name"], spans),
                "〉をレベル",
                FormatRange(RestoreVisible(match.Groups["level"], spans)),
                "で使用できる。既に持つ場合、さらにレベル",
                FormatRange(RestoreVisible(match.Groups["bonus"], spans)),
                "強化される。")),
        new(
            "CanUseMutation",
            new Regex(
                "^Can use (?<name>.+?) at level (?<level>\\d+(?:-\\d+)?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "〈",
                TranslateCapturedAbilityName(match.Groups["name"], spans),
                "〉をレベル",
                FormatRange(RestoreVisible(match.Groups["level"], spans)),
                "で使用できる。")),
        new(
            "MutationLevelBonus",
            new Regex(
                "^\\+(?<level>\\d+(?:-\\d+)?) levels? to (?<name>.+?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "〈",
                TranslateCapturedAbilityName(match.Groups["name"], spans),
                "〉が+",
                FormatRange(RestoreVisible(match.Groups["level"], spans)),
                "レベル上昇する。")),
        new(
            "CanUseSkillWithBonus",
            new Regex(
                "^Can use (?<name>.+?)\\. If @they already have \\k<name>, gain a \\+(?<bonus>\\d+) bonus on the Ego roll when using \\k<name>\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "〈",
                TranslateCapturedAbilityName(match.Groups["name"], spans),
                "〉を使用できる。既に習得している場合は〈",
                TranslateCapturedAbilityName(match.Groups["name"], spans),
                "〉使用時の意志判定に+",
                RestoreVisible(match.Groups["bonus"], spans),
                "のボーナス。")),
        new(
            "CanUseSkill",
            new Regex(
                "^Can use (?<name>.+?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "〈",
                TranslateCapturedAbilityName(match.Groups["name"], spans),
                "〉を使用できる。")),
        new(
            "SkillEgoRollBonus",
            new Regex(
                "^\\+(?<bonus>\\d+) bonus on Ego roll when using (?<name>.+?)\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (match, spans) => string.Concat(
                "〈",
                TranslateCapturedAbilityName(match.Groups["name"], spans),
                "〉使用時の意志判定に+",
                RestoreVisible(match.Groups["bonus"], spans),
                "のボーナス。")),
        new(
            "NoEffect",
            new Regex(
                "^No effect\\.$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled),
            static (_, _) => "効果なし。"),
    ];

    internal static bool TryTranslate(string source, string route, string family, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        for (var index = 0; index < Rules.Count; index++)
        {
            var rule = Rules[index];
            var match = rule.Pattern.Match(stripped);
            if (!match.Success)
            {
                continue;
            }

            translated = rule.Build(match, spans);
            DynamicTextObservability.RecordTransform(route, family + "." + rule.Name, source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static string RestoreVisible(Group group, IReadOnlyList<ColorSpan> spans)
    {
        return ColorAwareTranslationComposer.RestoreCapture(group.Value, spans, group).Trim();
    }

    private static string NormalizeChance(string chance)
    {
        if (chance.StartsWith("an ", StringComparison.Ordinal))
        {
            return chance.Substring(3);
        }

        if (chance.StartsWith("a ", StringComparison.Ordinal))
        {
            return chance.Substring(2);
        }

        return chance;
    }

    private static string TranslateCapturedAbilityName(Group group, IReadOnlyList<ColorSpan> spans)
    {
        var sourceName = RestoreVisible(group, spans);
        var translatedName = TranslateAbilityName(sourceName);
        return spans.Count == 0
            ? translatedName
            : ColorAwareTranslationComposer.RestoreCapture(translatedName, spans, group);
    }

    private static string TranslateAbilityName(string sourceName)
    {
        if (CookingAbilityNameOverrides.TryGetValue(sourceName, out var cookingName))
        {
            return cookingName;
        }

        var mutationNames = GetMutationDisplayNames();
        if (mutationNames.TryGetValue(sourceName, out var mutationName))
        {
            return mutationName;
        }

        var scopedName = ScopedDictionaryLookup.TranslateExactOrLowerAscii(sourceName, "ui-skillsandpowers.ja.json");
        if (scopedName is not null && !string.Equals(sourceName, scopedName, StringComparison.Ordinal))
        {
            return scopedName;
        }

        return sourceName;
    }

    private static string FormatRange(string value)
    {
        return value.Replace("-", "～");
    }

    private static string TranslateUnitStat(string stat)
    {
        return stat switch
        {
            "Acid Resistance" => "酸耐性",
            "Cold Resist" or "cold resist" => "冷気耐性",
            "Cold Resistance" => "冷気耐性",
            "Electric Resist" or "Electric Resistance" => "電気耐性",
            "Heat Resistance" => "熱耐性",
            "Heat Resist" => "熱耐性",
            "Agility" => "敏捷",
            "Ego" => "エゴ",
            "Intelligence" => "知力",
            "Quickness" => "クイックネス",
            "Strength" or "STR" => "筋力",
            "Toughness" => "頑健",
            "Willpower" => "意志力",
            _ => stat,
        };
    }

    private static string TranslateDurationUnit(string unit)
    {
        return unit.StartsWith("hour", StringComparison.Ordinal) ? "時間" : "ターン";
    }

    internal static void ResetForTests()
    {
        lock (SyncRoot)
        {
            mutationDisplayNames = null;
        }
    }

    private static Dictionary<string, string> GetMutationDisplayNames()
    {
        lock (SyncRoot)
        {
            if (mutationDisplayNames is not null)
            {
                return mutationDisplayNames;
            }

            mutationDisplayNames = LoadMutationDisplayNameMap("Mutations.jp.xml");
            MergeDisplayNameMap(mutationDisplayNames, LoadMutationDisplayNameMap("HiddenMutations.jp.xml"));
            return mutationDisplayNames;
        }
    }

    private static Dictionary<string, string> LoadMutationDisplayNameMap(string relativePath)
    {
        var path = LocalizationAssetResolver.GetLocalizationPath(relativePath);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return map;
        }

        try
        {
            var document = XDocument.Load(path, LoadOptions.None);
            if (document.Root is null)
            {
                return map;
            }

            foreach (var element in document.Root.Descendants("mutation"))
            {
                var name = element.Attribute("Name")?.Value;
                var displayName = element.Attribute("DisplayName")?.Value;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(displayName))
                {
                    map[name!] = displayName!;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            Trace.TraceWarning(
                "QudJP: CookingEffectFragmentTranslator failed to load '{0}': {1}",
                relativePath,
                ex.Message);
        }

        return map;
    }

    private static void MergeDisplayNameMap(IDictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var pair in source)
        {
            target[pair.Key] = pair.Value;
        }
    }

    private sealed class TranslationRule
    {
        internal TranslationRule(string name, Regex pattern, Func<Match, IReadOnlyList<ColorSpan>, string> build)
        {
            Name = name;
            Pattern = pattern;
            Build = build;
        }

        internal string Name { get; }

        internal Regex Pattern { get; }

        internal Func<Match, IReadOnlyList<ColorSpan>, string> Build { get; }
    }
}
