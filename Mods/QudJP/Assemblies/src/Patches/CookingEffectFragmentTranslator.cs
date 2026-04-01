using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class CookingEffectFragmentTranslator
{
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
