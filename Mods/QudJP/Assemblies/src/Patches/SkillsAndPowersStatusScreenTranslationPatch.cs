using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SkillsAndPowersStatusScreenTranslationPatch
{
    private static readonly Regex SkillPointsPattern =
        new Regex("^Skill Points \\(SP\\): (?<rest>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LearnedPattern =
        new Regex("^Learned \\[(?<owned>\\d+)\\/(?<limit>\\d+)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StartingCostPattern =
        new Regex("^Starting Cost \\[(?<cost>\\d+) sp\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StartingCostRankPattern =
        new Regex("^Starting Cost \\[(?<cost>\\d+) sp\\] \\[(?<rank>\\d+)\\/(?<max>\\d+)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RequirementBlockPattern =
        new Regex("^:: (?<cost>\\d+) SP ::\\n:: (?<requirement>\\d+) (?<attribute>Strength|Toughness|Willpower|Agility|Ego|Intelligence) ::\\n?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SkillLinePattern =
        new Regex("^(?<indent>\\s*):(?<name>.+?) \\[(?<cost>\\d+)sp\\] (?<requirement>\\d+) (?<attribute>Strength|Toughness|Willpower|Agility|Ego|Intelligence)(?:, (?<prereq>.+))?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PrefixedSkillNamePattern =
        new Regex("^(?<indent>\\s*):(?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = GameTypeResolver.FindType("Qud.UI.SkillsAndPowersStatusScreen", "SkillsAndPowersStatusScreen");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: SkillsAndPowersStatusScreenTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "UpdateViewFromData", Type.EmptyTypes);
        if (method is null)
        {
            Trace.TraceError("QudJP: SkillsAndPowersStatusScreenTranslationPatch.UpdateViewFromData not found.");
        }

        return method;
    }

    public static void Postfix(object? ___spText)
    {
        try
        {
            UITextSkinTemplateTranslator.TranslateSinglePlaceholderText(
                ___spText,
                SkillPointsPattern,
                "Skill Points (SP): {val}",
                "{val}",
                nameof(SkillsAndPowersStatusScreenTranslationPatch));
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: SkillsAndPowersStatusScreenTranslationPatch.Postfix failed: {0}", ex);
        }
    }

    internal static bool TryTranslateText(string source, string route, out string translated)
    {
        if (TryTranslateSkillPoints(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateLearned(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateStartingCost(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateRequirementBlock(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateSkillLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslatePrefixedSkillName(source, route, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateSkillPoints(string source, string route, out string translated)
    {
        var match = SkillPointsPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var template = Translator.Translate("Skill Points (SP): {val}");
        if (string.Equals(template, "Skill Points (SP): {val}", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = template.Replace("{val}", match.Groups["rest"].Value);
        DynamicTextObservability.RecordTransform(route, "Skill Points (SP): {val}", source, translated);
        return true;
    }

    private static bool TryTranslateLearned(string source, string route, out string translated)
    {
        var match = LearnedPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var label = Translator.Translate("Learned");
        if (string.Equals(label, "Learned", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = $"{label} [{match.Groups["owned"].Value}/{match.Groups["limit"].Value}]";
        DynamicTextObservability.RecordTransform(route, "Learned[{owned}/{limit}]", source, translated);
        return true;
    }

    private static bool TryTranslateStartingCost(string source, string route, out string translated)
    {
        var rankedMatch = StartingCostRankPattern.Match(source);
        if (rankedMatch.Success)
        {
            var translatedPrefix = TranslateStartingCostPrefix(rankedMatch.Groups["cost"].Value);
            if (translatedPrefix is null)
            {
                translated = source;
                return false;
            }

            translated = $"{translatedPrefix} [{rankedMatch.Groups["rank"].Value}/{rankedMatch.Groups["max"].Value}]";
            DynamicTextObservability.RecordTransform(route, "Starting Cost [{val} sp] [{rank}/{max}]", source, translated);
            return true;
        }

        var match = StartingCostPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var prefix = TranslateStartingCostPrefix(match.Groups["cost"].Value);
        if (prefix is null)
        {
            translated = source;
            return false;
        }

        translated = prefix;
        DynamicTextObservability.RecordTransform(route, "Starting Cost [{val} sp]", source, translated);
        return true;
    }

    private static string? TranslateStartingCostPrefix(string cost)
    {
        var template = Translator.Translate("Starting Cost [{val} sp]");
        if (string.Equals(template, "Starting Cost [{val} sp]", StringComparison.Ordinal))
        {
            return null;
        }

        return template.Replace("{val}", cost);
    }

    private static bool TryTranslateRequirementBlock(string source, string route, out string translated)
    {
        var match = RequirementBlockPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var attribute = TranslateLeaf(match.Groups["attribute"].Value);
        translated = $":: {match.Groups["cost"].Value} SP ::\n:: {match.Groups["requirement"].Value} {attribute} ::";
        DynamicTextObservability.RecordTransform(route, "SkillRequirementBlock", source, translated);
        return true;
    }

    private static bool TryTranslateSkillLine(string source, string route, out string translated)
    {
        var match = SkillLinePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var name = TranslateLeaf(match.Groups["name"].Value);
        var attribute = TranslateLeaf(match.Groups["attribute"].Value);
        var changed = !string.Equals(name, match.Groups["name"].Value, StringComparison.Ordinal)
            || !string.Equals(attribute, match.Groups["attribute"].Value, StringComparison.Ordinal);
        var translatedLine = $"{name} [{match.Groups["cost"].Value}sp] {match.Groups["requirement"].Value} {attribute}";
        if (match.Groups["prereq"].Success)
        {
            var prereq = TranslateLeaf(match.Groups["prereq"].Value);
            changed |= !string.Equals(prereq, match.Groups["prereq"].Value, StringComparison.Ordinal);
            translatedLine += $", {prereq}";
        }

        if (!changed)
        {
            translated = source;
            return false;
        }

        translated = $"{match.Groups["indent"].Value}:{translatedLine}";
        DynamicTextObservability.RecordTransform(route, "SkillLine", source, translated);
        return true;
    }

    private static bool TryTranslatePrefixedSkillName(string source, string route, out string translated)
    {
        var match = PrefixedSkillNamePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedName = TranslateLeaf(match.Groups["name"].Value);
        if (string.Equals(translatedName, match.Groups["name"].Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = $"{match.Groups["indent"].Value}:{translatedName}";
        DynamicTextObservability.RecordTransform(route, "SkillNameLine", source, translated);
        return true;
    }

    private static string TranslateLeaf(string source)
    {
        var direct = Translator.Translate(source);
        return string.Equals(direct, source, StringComparison.Ordinal) ? source : direct;
    }
}
