using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using QudJP;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SkillsAndPowersStatusScreenTranslationPatch
{
    private static readonly string SkillNameDictionaryFile =
        Path.Combine("Scoped", "ui-skillsandpowers-skill-names.ja.json");
    private static readonly IReadOnlyDictionary<string, string> AttributeRequirementAbbreviations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strength"] = "STR",
            ["Toughness"] = "TOU",
            ["Willpower"] = "WIL",
            ["Agility"] = "AGI",
            ["Ego"] = "EGO",
            ["Intelligence"] = "INT",
        };
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
        new Regex("^(?<indent>\\s*)(?<colon>:?)(?<name>.+?) (?<costBlock>\\[(?<cost>\\d+)sp\\]) (?<requirement>\\d+) (?<attribute>Strength|Toughness|Willpower|Agility|Ego|Intelligence)(?:, (?<prereq>.+))?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PrefixedSkillNamePattern =
        new Regex("^(?<indent>\\s*):(?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BracketedLeafPattern =
        new Regex("^\\[(?<inner>.+)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GeneratedDetailStatLinePattern =
        new Regex("^(?<label>Duration|Range|Area|Radius|Cooldown|Cooldown Remaining Turns): (?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CooldownAdjustmentPattern =
        new Regex("^Cooldown (?<direction>reduced|increased) by (?<amount>.+?) due to (?<reason>.+)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CooldownFloorPattern =
        new Regex("^Cooldown cannot be reduced below (?<amount>.+?) rounds\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

        if (TryTranslateSkillNameList(source, out translated))
        {
            DynamicTextObservability.RecordTransform(route, "SkillNameList", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    internal static (bool changed, string translated) TryTranslateExactLeafPreservingColors(
        string source,
        string route,
        bool recordTransform)
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateLeaf);
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return (false, source);
        }

        if (recordTransform)
        {
            DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.ExactLeaf", source, translated);
        }

        return (true, translated);
    }

    internal static (bool changed, string translated) TryTranslateDetailText(
        string source,
        string route,
        bool recordTransform)
    {
        return TryTranslateLineCollection(source, route, "SkillsAndPowers.DetailText", TryTranslateDetailLinePreservingColors, recordTransform);
    }

    internal static (bool changed, string translated) TryTranslateLearnedStatusText(
        string source,
        string route,
        bool recordTransform)
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            static visible => StringHelpers.TranslateExactOrLowerAsciiFallback(visible));
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return (false, source);
        }

        if (recordTransform)
        {
            DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.LearnedStatus", source, translated);
        }

        return (true, translated);
    }

    internal static (bool changed, string translated) TryTranslateRequirementsOwnerText(
        string source,
        string route,
        bool recordTransform)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = RequirementBlockPattern.Match(stripped);
        if (!match.Success)
        {
            return (false, source);
        }

        var cost = ColorAwareTranslationComposer.RestoreCapture(match.Groups["cost"].Value, spans, match.Groups["cost"]);
        var translated = $":: {cost} SP ::\n:: {match.Groups["requirement"].Value} {TranslateAttributeRequirement(match.Groups["attribute"].Value)} ::";
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return (false, source);
        }

        if (recordTransform)
        {
            DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.Requirements", source, translated);
        }

        return (true, translated);
    }

    internal static (bool changed, string translated) TryTranslateRequiredSkillsOwnerText(
        string source,
        string route,
        bool recordTransform)
    {
        return TryTranslateLineCollection(source, route, "SkillsAndPowers.RequiredSkills", TryTranslateStructuredLinePreservingColors, recordTransform);
    }

    internal static string TranslateLeaf(string source)
    {
        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        var coreLength = source.Length - leadingLength - trailingLength;
        if (coreLength <= 0)
        {
            return source;
        }

        var prefix = leadingLength == 0 ? string.Empty : source.Substring(0, leadingLength);
        var suffix = trailingLength == 0 ? string.Empty : source.Substring(source.Length - trailingLength);
        var core = source.Substring(leadingLength, coreLength);
        var translatedCore = TranslateLeafCore(core);
        return string.Equals(translatedCore, core, StringComparison.Ordinal)
            ? source
            : prefix + translatedCore + suffix;
    }

    private static string TranslateLeafCore(string source)
    {
        var normalizedSource = string.Equals(source, "REQUIRED SKILLS", StringComparison.Ordinal)
            ? "Required Skills"
            : source;
        var direct = TranslateDictionaryLeaf(normalizedSource);
        if (!string.Equals(direct, normalizedSource, StringComparison.Ordinal))
        {
            return direct;
        }

        var bracketedMatch = BracketedLeafPattern.Match(source);
        if (bracketedMatch.Success)
        {
            var inner = bracketedMatch.Groups["inner"].Value;
            var translatedInner = TranslateLeafCore(inner);
            if (!string.Equals(translatedInner, inner, StringComparison.Ordinal))
            {
                return "[" + translatedInner + "]";
            }
        }

        return source;
    }

    private static string TranslateDictionaryLeaf(string source)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, SkillNameDictionaryFile);
        if (scoped is not null && !string.Equals(scoped, source, StringComparison.Ordinal))
        {
            return scoped;
        }

        var direct = Translator.Translate(source);
        return string.Equals(direct, source, StringComparison.Ordinal) ? source : direct;
    }

    internal static string TranslateAttributeRequirement(string source)
    {
        return AttributeRequirementAbbreviations.TryGetValue(source, out var abbreviation)
            ? abbreviation
            : source;
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

        var attribute = TranslateAttributeRequirement(match.Groups["attribute"].Value);
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
        var attribute = TranslateAttributeRequirement(match.Groups["attribute"].Value);
        var changed = !string.Equals(name, match.Groups["name"].Value, StringComparison.Ordinal)
            || !string.Equals(attribute, match.Groups["attribute"].Value, StringComparison.Ordinal);
        var translatedLine = $"{name} {match.Groups["costBlock"].Value} {match.Groups["requirement"].Value} {attribute}";
        if (match.Groups["prereq"].Success)
        {
            var prereq = TranslateSkillNameListOrLeaf(match.Groups["prereq"].Value);
            changed |= !string.Equals(prereq, match.Groups["prereq"].Value, StringComparison.Ordinal);
            translatedLine += $", {prereq}";
        }

        if (!changed)
        {
            translated = source;
            return false;
        }

        translated = $"{match.Groups["indent"].Value}{match.Groups["colon"].Value}{translatedLine}";
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

    internal static (bool changed, string translated) TryTranslateStructuredLinePreservingColors(
        string source,
        string route,
        bool recordTransform)
    {
        if (TryTranslateSkillLinePreservingCaptureColors(source, route, recordTransform, out var captureColored))
        {
            return (true, captureColored);
        }

        if (TryTranslateSkillNameListPreservingColors(source, out var listTranslated))
        {
            if (recordTransform)
            {
                DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.SkillNameList.CaptureColors", source, listTranslated);
            }

            return (true, listTranslated);
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            source,
            visible => TryTranslateText(visible, route, out var structured) ? structured : TranslateLeaf(visible));
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return (false, source);
        }

        if (recordTransform)
        {
            DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.StructuredLine", source, translated);
        }

        return (true, translated);
    }

    private static (bool changed, string translated) TryTranslateDetailLinePreservingColors(
        string source,
        string route,
        bool recordTransform)
    {
        var exact = TryTranslateExactLeafPreservingColors(source, route, recordTransform);
        if (exact.changed)
        {
            return exact;
        }

        if (TryTranslateGeneratedDetailStatLine(source, route, recordTransform, out var translatedStatLine))
        {
            return (true, translatedStatLine);
        }

        if (TryTranslateCooldownAdjustmentLine(source, route, recordTransform, out var translatedCooldownLine))
        {
            return (true, translatedCooldownLine);
        }

        return (false, source);
    }

    private static bool TryTranslateGeneratedDetailStatLine(
        string source,
        string route,
        bool recordTransform,
        out string translated)
    {
        translated = source;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = GeneratedDetailStatLinePattern.Match(stripped);
        if (!match.Success)
        {
            return false;
        }

        var rawLabel = match.Groups["label"].Value;
        var translatedLabel = TranslateGeneratedDetailLabel(rawLabel);
        var label = ColorAwareTranslationComposer.RestoreCapture(
            translatedLabel,
            spans,
            match.Groups["label"]);
        var value = ColorAwareTranslationComposer.RestoreCapture(match.Groups["value"].Value, spans, match.Groups["value"]);
        var translatedValue = ColorAwareTranslationComposer.TranslatePreservingColors(value, TranslateGeneratedDetailValue);
        translated = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            label + ": " + translatedValue,
            spans,
            match.Groups[0]);
        if (recordTransform)
        {
            DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.GeneratedDetailStatLine", source, translated);
        }

        return true;
    }

    private static bool TryTranslateCooldownAdjustmentLine(
        string source,
        string route,
        bool recordTransform,
        out string translated)
    {
        translated = source;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var adjustmentMatch = CooldownAdjustmentPattern.Match(stripped);
        if (adjustmentMatch.Success)
        {
            var amount = ColorAwareTranslationComposer.RestoreCapture(
                adjustmentMatch.Groups["amount"].Value,
                spans,
                adjustmentMatch.Groups["amount"]);
            var rawReason = ColorAwareTranslationComposer.RestoreCapture(
                adjustmentMatch.Groups["reason"].Value,
                spans,
                adjustmentMatch.Groups["reason"]);
            var reason = ColorAwareTranslationComposer.TranslatePreservingColors(rawReason, TranslateCooldownReason);
            translated = string.Equals(adjustmentMatch.Groups["direction"].Value, "reduced", StringComparison.Ordinal)
                ? $"クールダウンが{amount}短縮（{reason}による）。"
                : $"クールダウンが{amount}増加（{reason}による）。";
            if (recordTransform)
            {
                DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.CooldownAdjustment", source, translated);
            }

            return true;
        }

        var floorMatch = CooldownFloorPattern.Match(stripped);
        if (!floorMatch.Success)
        {
            return false;
        }

        var floorAmount = ColorAwareTranslationComposer.RestoreCapture(
            floorMatch.Groups["amount"].Value,
            spans,
            floorMatch.Groups["amount"]);
        translated = $"クールダウンは{floorAmount}ラウンド未満には短縮されない。";
        if (recordTransform)
        {
            DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.CooldownFloor", source, translated);
        }

        return true;
    }

    private static string TranslateGeneratedDetailLabel(string label)
    {
        return label switch
        {
            "Duration" => "持続時間",
            "Range" => "射程",
            "Area" => "効果範囲",
            "Radius" => "半径",
            "Cooldown" => "クールダウン",
            "Cooldown Remaining Turns" => "クールダウン残りターン",
            _ => label,
        };
    }

    private static string TranslateGeneratedDetailValue(string value)
    {
        var translated = value;
        translated = translated.Replace("centered around yourself", "自分中心");
        translated = translated.Replace("around self", "自分中心");
        translated = translated.Replace("move actions", "移動アクション");
        translated = translated.Replace("rounds", "ラウンド");
        translated = translated.Replace("round", "ラウンド");
        translated = translated.Replace("turns", "ターン");
        translated = translated.Replace("turn", "ターン");
        translated = translated.Replace("squares", "マス");
        translated = translated.Replace("square", "マス");
        translated = translated.Replace("sight", "視界");
        return translated;
    }

    private static string TranslateCooldownReason(string reason)
    {
        var translated = TranslateLeaf(reason);
        if (!string.Equals(translated, reason, StringComparison.Ordinal))
        {
            return translated;
        }

        return reason switch
        {
            "high Strength" => "高い筋力",
            "high Toughness" => "高い耐久力",
            "high Willpower" => "高い意志力",
            "high Agility" => "高い敏捷性",
            "high Ego" => "高い自我",
            "high Intelligence" => "高い知性",
            "low Strength" => "低い筋力",
            "low Toughness" => "低い耐久力",
            "low Willpower" => "低い意志力",
            "low Agility" => "低い敏捷性",
            "low Ego" => "低い自我",
            "low Intelligence" => "低い知性",
            _ => reason,
        };
    }

    private static bool TryTranslateSkillLinePreservingCaptureColors(
        string source,
        string route,
        bool recordTransform,
        out string translated)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = SkillLinePattern.Match(stripped);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var colon = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            match.Groups["colon"].Value,
            spans,
            match.Groups["colon"]);
        var name = RestoreTranslatedCapture(match, spans, "name", TranslateLeaf);
        var costBlock = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["costBlock"].Value,
            spans,
            match.Groups["costBlock"]).Trim();
        var requirement = ColorAwareTranslationComposer.MarkupAwareRestoreCapture(
            match.Groups["requirement"].Value,
            spans,
            match.Groups["requirement"]).Trim();
        var attribute = RestoreTranslatedCapture(match, spans, "attribute", TranslateAttributeRequirement);

        var translatedVisible = $"{match.Groups["indent"].Value}{colon}{name} {costBlock} {requirement} {attribute}";
        if (match.Groups["prereq"].Success)
        {
            var prereq = RestoreTranslatedCapture(match, spans, "prereq", TranslateSkillNameListOrLeaf);
            translatedVisible += $", {prereq}";
        }

        var changed = !string.Equals(translatedVisible, stripped, StringComparison.Ordinal);
        if (!changed)
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedVisible,
            spans,
            stripped.Length,
            source);
        if (recordTransform)
        {
            DynamicTextObservability.RecordTransform(route, "SkillsAndPowers.SkillLine.CaptureColors", source, translated);
        }

        return true;
    }

    private static string RestoreTranslatedCapture(
        Match match,
        IReadOnlyList<ColorSpan> spans,
        string groupName,
        Func<string, string> translate)
    {
        var group = match.Groups[groupName];
        var translated = translate(group.Value);
        return ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(translated, spans, group).Trim();
    }

    private static string TranslateSkillNameListOrLeaf(string source)
    {
        return TryTranslateSkillNameList(source, out var translated)
            ? translated
            : TranslateLeaf(source);
    }

    private static bool TryTranslateSkillNameList(string source, out string translated)
    {
        const string Separator = ", ";
        if (source.IndexOf(Separator, StringComparison.Ordinal) < 0)
        {
            translated = source;
            return false;
        }

        var parts = source.Split(new[] { Separator }, StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Length == 0)
            {
                translated = source;
                return false;
            }

            var translatedPart = TranslateLeaf(parts[index]);
            changed |= !string.Equals(translatedPart, parts[index], StringComparison.Ordinal);
            parts[index] = translatedPart;
        }

        translated = string.Join(Separator, parts);
        return changed;
    }

    private static bool TryTranslateSkillNameListPreservingColors(string source, out string translated)
    {
        const string Separator = ", ";
        if (source.IndexOf(Separator, StringComparison.Ordinal) < 0)
        {
            translated = source;
            return false;
        }

        var parts = source.Split(new[] { Separator }, StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Length == 0)
            {
                translated = source;
                return false;
            }

            parts[index] = TranslateSkillNameListPartPreservingColors(parts[index], out var partChanged);
            changed |= partChanged;
        }

        translated = string.Join(Separator, parts);
        return changed;
    }

    private static string TranslateSkillNameListPartPreservingColors(string source, out bool changed)
    {
        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var withoutMarker))
        {
            var translatedWithoutMarker = ColorAwareTranslationComposer.TranslatePreservingColors(withoutMarker, TranslateLeaf);
            changed = !string.Equals(withoutMarker, translatedWithoutMarker, StringComparison.Ordinal);
            return changed
                ? MessageFrameTranslator.MarkDirectTranslation(translatedWithoutMarker)
                : source;
        }

        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(source, TranslateLeaf);
        changed = !string.Equals(source, translated, StringComparison.Ordinal);
        return translated;
    }

    private static (bool changed, string translated) TryTranslateLineCollection(
        string source,
        string route,
        string family,
        Func<string, string, bool, (bool changed, string translated)> lineTranslator,
        bool recordTransform)
    {
        var lines = source.Split(new[] { '\n' }, StringSplitOptions.None);
        if (lines.Length == 1)
        {
            return lineTranslator(source, route, recordTransform);
        }

        var translatedLines = new string[lines.Length];
        var changed = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var lineResult = lineTranslator(lines[index], route, false);
            translatedLines[index] = lineResult.translated;
            changed |= lineResult.changed;
        }

        if (!changed)
        {
            return (false, source);
        }

        var translated = string.Join("\n", translatedLines);
        if (recordTransform)
        {
            DynamicTextObservability.RecordTransform(route, family, source, translated);
        }

        return (true, translated);
    }
}
