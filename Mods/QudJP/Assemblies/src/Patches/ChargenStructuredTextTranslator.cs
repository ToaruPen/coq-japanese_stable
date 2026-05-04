using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace QudJP.Patches;

internal static class ChargenStructuredTextTranslator
{
    private static readonly string ChargenSkillDictionaryFile =
        Path.Combine("Scoped", "ui-chargen-skill-context.ja.json");
    private static readonly object SyncRoot = new object();
    private static readonly Regex BulletLinePattern =
        new Regex(@"^(?<indent>\s*)(?<bullet>(?:\{\{[^{}]+\}\}|[\u00F9])\s*)?(?<content>.*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StatBonusPattern =
        new Regex(@"^(?<value>[+-]\d+)\s+(?<stat>Strength|Toughness|Willpower|Agility|Ego|Intelligence)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StatValuePattern =
        new Regex(@"^(?<stat>Strength|Toughness|Willpower|Agility|Ego|Intelligence):\s*(?<value>-?\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ResistanceBonusPattern =
        new Regex(@"^(?<value>[+-]\d+)\s+(?<kind>heat|cold)\s+resistance$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ReputationPattern =
        new Regex(@"^(?<value>[+-]\d+)\s+reputation\s+with\s+(?<faction>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BleedingSavePattern =
        new Regex(@"^(?<value>[+-]\d+)\s+to\s+saves\s+vs\.\s+bleeding$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CyberneticsSlotPattern =
        new Regex(@"^(?<name>.+?) (?<slotSegment>\((?<slot>Face|Body|Head|Back|Feet|Arm|Hands)\))$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PointsRemainingPattern =
        new Regex(@"^\s*Points Remaining:\s*(?<value>-?\d+)\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PointTokenPattern =
        new Regex(@"^\[(?<value>-?\d+)pts\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SelectionTokenPattern =
        new Regex(@"^\[(?: |■)\](?:\[(?<value>-?\d+)\])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BeakDescriptionPattern =
        new Regex(
            @"^Your face bears a sightly .+?\.\n\n\+1 Ego\nYou occasionally peck at your opponents\.\n\+200 reputation with \{\{w\|birds\}\}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StingerDescriptionPattern =
        new Regex(
            @"^You bear a tail with a stinger that delivers (?<adjective>confusing|paralyzing|poisonous) venom to your enemies\.",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex JapaneseCharacterPattern =
        new Regex("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AsciiLetterPattern =
        new Regex("[A-Za-z]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> StatNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Strength"] = "筋力",
        ["Toughness"] = "頑健",
        ["Willpower"] = "意志力",
        ["Agility"] = "敏捷",
        ["Ego"] = "自我",
        ["Intelligence"] = "知力",
    };

    // Slot names for the "name (Slot)" pattern from CyberneticsChoice.GetDescription().
    // Kept here instead of the dictionary to avoid false-positive hits on short English
    // words like "Back" or "Arm" in unrelated UI strings. Ordinal is safe because the
    // CyberneticsSlotPattern regex constrains the captured value to exactly these casings.
    private static readonly IReadOnlyDictionary<string, string> SlotNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Face"] = "顔",
        ["Body"] = "胴",
        ["Head"] = "頭",
        ["Back"] = "背中",
        ["Feet"] = "足",
        ["Arm"] = "腕",
        ["Hands"] = "手",
    };

    private static Dictionary<string, string>? subtypeDisplayNames;
    private static Dictionary<string, string>? mutationDisplayNames;
    private static Dictionary<string, string>? factionDisplayNames;

    internal static string Translate(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (TryTranslatePointsRemaining(source, out var translated))
        {
            DynamicTextObservability.RecordTransform(nameof(ChargenStructuredTextTranslator), "Chargen.PointsRemaining", source, translated);
            return translated;
        }

        if (TryTranslateCyberneticsSlot(source, out translated))
        {
            DynamicTextObservability.RecordTransform(nameof(ChargenStructuredTextTranslator), "Chargen.CyberneticsSlot", source, translated);
            return translated;
        }

        if (TryTranslateNameLabel(source, out translated))
        {
            DynamicTextObservability.RecordTransform(nameof(ChargenStructuredTextTranslator), "Chargen.NameLabel", source, translated);
            return translated;
        }

        if (TryTranslatePointToken(source, out translated))
        {
            DynamicTextObservability.RecordTransform(nameof(ChargenStructuredTextTranslator), "Chargen.PointToken", source, translated);
            return translated;
        }

        if (IsIgnorableUiToken(source))
        {
            return source;
        }

        if (TryTranslateExactLeaf(source, out translated))
        {
            DynamicTextObservability.RecordTransform(nameof(ChargenStructuredTextTranslator), "Chargen.ExactLeaf", source, translated);
            return translated;
        }

        if (TryTranslateKnownMutationDescriptionSource(source, out translated))
        {
            DynamicTextObservability.RecordTransform(nameof(ChargenStructuredTextTranslator), "Chargen.Mutation.LongDescription", source, translated);
            return translated;
        }

        if (!LooksLikeStructuredDescription(source))
        {
            return source;
        }

        translated = TranslateStructuredDescription(source);
        if (!string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(nameof(ChargenStructuredTextTranslator), "Chargen.StructuredDescription", source, translated);
        }

        return translated;
    }

    internal static void ResetForTests()
    {
        lock (SyncRoot)
        {
            subtypeDisplayNames = null;
            mutationDisplayNames = null;
            factionDisplayNames = null;
        }
    }

    internal static string TranslateMutationMenuDescription(string source)
    {
        const string variantSuffix = " [{{W|V}}]";
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (!source.EndsWith(variantSuffix, StringComparison.Ordinal))
        {
            return Translate(source);
        }

        var baseDescription = source.Substring(0, source.Length - variantSuffix.Length);
        var translatedBaseDescription = Translate(baseDescription);
        return string.Concat(translatedBaseDescription, variantSuffix);
    }

    internal static bool TryTranslateMutationLongDescription(string mutationName, out string translated)
    {
        if (string.IsNullOrWhiteSpace(mutationName))
        {
            translated = mutationName;
            return false;
        }

        var descriptionKey = string.Concat("mutation:", mutationName);
        var rankKey = string.Concat(descriptionKey, ":rank:1");
        var hasDescription = Translator.TryGetTranslation(descriptionKey, out var description)
            && !string.Equals(description, descriptionKey, StringComparison.Ordinal);
        var hasRank = TryGetMutationRankText(mutationName!, rankKey, out var rankText);

        if (!hasDescription && !hasRank)
        {
            translated = mutationName;
            return false;
        }

        if (hasDescription && hasRank)
        {
            translated = string.Concat(description, "\n\n", rankText);
        }
        else if (hasDescription)
        {
            translated = description;
        }
        else
        {
            translated = rankText;
        }
        return true;
    }

    private static bool TryTranslateKnownMutationDescriptionSource(string source, out string translated)
    {
        if (BeakDescriptionPattern.IsMatch(source))
        {
            return TryTranslateMutationLongDescription("Beak", out translated);
        }

        var stingerMatch = StingerDescriptionPattern.Match(source);
        if (stingerMatch.Success)
        {
            var mutationName = stingerMatch.Groups["adjective"].Value switch
            {
                "confusing" => "Stinger (Confusing Venom)",
                "paralyzing" => "Stinger (Paralyzing Venom)",
                "poisonous" => "Stinger (Poisoning Venom)",
                _ => string.Empty,
            };

            if (!string.IsNullOrEmpty(mutationName))
            {
                return TryTranslateMutationLongDescription(mutationName, out translated);
            }
        }

        translated = source;
        return false;
    }

    private static bool TryGetMutationRankText(string mutationName, string simpleRankKey, out string rankText)
    {
        if (Translator.TryGetTranslation(simpleRankKey, out rankText)
            && !string.Equals(rankText, simpleRankKey, StringComparison.Ordinal))
        {
            return true;
        }

        if (!TryGetStingerRankKey(mutationName, out var stingerRankKey))
        {
            rankText = simpleRankKey;
            return false;
        }

        return Translator.TryGetTranslation(stingerRankKey, out rankText)
            && !string.Equals(rankText, stingerRankKey, StringComparison.Ordinal);
    }

    private static bool TryGetStingerRankKey(string mutationName, out string rankKey)
    {
        var propertyName = mutationName switch
        {
            "Stinger (Confusing Venom)" => "Stinger Confusion",
            "Stinger (Paralyzing Venom)" => "Stinger Paralysis",
            "Stinger (Poisoning Venom)" => "Stinger Poison",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(propertyName))
        {
            rankKey = string.Empty;
            return false;
        }

        rankKey = string.Concat("mutation:", mutationName, ":", propertyName, ":rank:1");
        return true;
    }

#pragma warning disable CA1847
    private static bool LooksLikeStructuredDescription(string source)
    {
        return source.Contains("\n")
               || source.Contains("{{c|")
               || source.Contains("{{C|")
               || ContainsRawBulletMarker(source);
    }
#pragma warning restore CA1847

    private static string TranslateStructuredDescription(string source)
    {
        var lines = source.Split('\n');
        var translatedLines = new string[lines.Length];

        for (var index = 0; index < lines.Length; index++)
        {
            translatedLines[index] = TranslateLine(lines[index]);
        }

        return string.Join("\n", translatedLines);
    }

    private static string TranslateLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var match = BulletLinePattern.Match(line);
        if (!match.Success)
        {
            return TranslateLineContent(line);
        }

        var content = match.Groups["content"].Value;
        var translatedContent = TranslateLineContent(content);
        if (string.Equals(content, translatedContent, StringComparison.Ordinal))
        {
            return line;
        }

        return string.Concat(
            match.Groups["indent"].Value,
            match.Groups["bullet"].Value,
            translatedContent);
    }

    private static string TranslateLineContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        if (TryTranslateExactLeaf(content, out var translated))
        {
            return translated;
        }

        if (TryTranslateStatBonus(content, out translated))
        {
            return translated;
        }

        if (TryTranslateStatValue(content, out translated))
        {
            return translated;
        }

        if (TryTranslateResistanceBonus(content, out translated))
        {
            return translated;
        }

        if (TryTranslateBleedingSave(content, out translated))
        {
            return translated;
        }

        if (TryTranslateReputation(content, out translated))
        {
            return translated;
        }

        return content;
    }

    private static bool TryTranslateExactLeaf(string source, out string translated)
    {
        var scoped = ScopedDictionaryLookup.TranslateExactOrLowerAscii(source, ChargenSkillDictionaryFile);
        if (scoped is not null && !string.Equals(source, scoped, StringComparison.Ordinal))
        {
            translated = scoped;
            return true;
        }

        if (Translator.TryGetTranslation(source, out translated) && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            return true;
        }

        var subtypeNames = GetSubtypeDisplayNames();
        if (subtypeNames.TryGetValue(source, out var subtypeDisplayName))
        {
            translated = subtypeDisplayName;
            return true;
        }

        var mutationNames = GetMutationDisplayNames();
        if (mutationNames.TryGetValue(source, out var mutationDisplayName))
        {
            translated = mutationDisplayName;
            return true;
        }

        var factionNames = GetFactionDisplayNames();
        if (factionNames.TryGetValue(source, out var factionDisplayName))
        {
            translated = factionDisplayName;
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCyberneticsSlot(string source, out string translated)
    {
        translated = source;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = CyberneticsSlotPattern.Match(stripped);
        if (!match.Success)
        {
            return false;
        }

        var sourceName = match.Groups["name"].Value;
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(sourceName, out var translatedName)
            && !TryUseAlreadyLocalizedCyberneticsName(sourceName, out translatedName))
        {
            return false;
        }

        var sourceSlot = match.Groups["slot"].Value;
        if (!SlotNames.TryGetValue(sourceSlot, out var translatedSlot))
        {
            return false;
        }

        var translatedSlotSegment = string.Concat("（", translatedSlot, "）");
        var restoredName = spans.Count == 0
            ? translatedName
            : ColorAwareTranslationComposer.RestoreCapture(translatedName, spans, match.Groups["name"]);
        var restoredSlotSegment = spans.Count == 0
            ? translatedSlotSegment
            : ColorAwareTranslationComposer.RestoreCapture(translatedSlotSegment, spans, match.Groups["slotSegment"]);
        translated = string.Concat(restoredName, restoredSlotSegment);
        return true;
    }

    private static bool TryUseAlreadyLocalizedCyberneticsName(string sourceName, out string translatedName)
    {
        if (JapaneseCharacterPattern.IsMatch(sourceName) && !AsciiLetterPattern.IsMatch(sourceName))
        {
            translatedName = sourceName;
            return true;
        }

        translatedName = sourceName;
        return false;
    }

    private static bool TryTranslatePointsRemaining(string source, out string translated)
    {
        var match = PointsRemainingPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var label = Translator.Translate("Points Remaining:");
        if (string.Equals(label, "Points Remaining:", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = string.Concat(label.TrimEnd(), " ", match.Groups["value"].Value);
        return true;
    }

    private static bool TryTranslateNameLabel(string source, out string translated)
    {
        if (!string.Equals(source, "Name:", StringComparison.Ordinal)
            && !string.Equals(source, "Name: ", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = Translator.Translate("Name: ");
        if (string.Equals(translated, "Name: ", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translated.TrimEnd();
        return true;
    }

    private static bool TryTranslatePointToken(string source, out string translated)
    {
        var match = PointTokenPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = $"[{match.Groups["value"].Value}点]";
        return true;
    }

    private static bool IsIgnorableUiToken(string source)
    {
        return string.Equals(source, "[R]", StringComparison.Ordinal)
            || string.Equals(source, "[V]", StringComparison.Ordinal)
            || string.Equals(source, "[Delete]", StringComparison.Ordinal)
            || string.Equals(source, "[ ]", StringComparison.Ordinal)
            || string.Equals(source, "[■]", StringComparison.Ordinal)
            || SelectionTokenPattern.IsMatch(source)
            || source.StartsWith(">{{K|", StringComparison.Ordinal);
    }

    private static bool TryTranslateStatBonus(string source, out string translated)
    {
        var match = StatBonusPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var statName = match.Groups["stat"].Value;
        if (!StatNames.TryGetValue(statName, out var translatedStat))
        {
            translated = source;
            return false;
        }

        translated = string.Concat(translatedStat, " ", match.Groups["value"].Value);
        return true;
    }

    private static bool TryTranslateStatValue(string source, out string translated)
    {
        var match = StatValuePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var statName = match.Groups["stat"].Value;
        if (!StatNames.TryGetValue(statName, out var translatedStat))
        {
            translated = source;
            return false;
        }

        translated = string.Concat(translatedStat, ": ", match.Groups["value"].Value);
        return true;
    }

    private static bool TryTranslateResistanceBonus(string source, out string translated)
    {
        var match = ResistanceBonusPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var resistanceName = string.Equals(match.Groups["kind"].Value, "heat", StringComparison.OrdinalIgnoreCase)
            ? "熱耐性"
            : "冷気耐性";
        translated = string.Concat(resistanceName, " ", match.Groups["value"].Value);
        return true;
    }

    private static bool TryTranslateBleedingSave(string source, out string translated)
    {
        var match = BleedingSavePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = string.Concat("出血セーブ ", match.Groups["value"].Value);
        return true;
    }

    private static bool TryTranslateReputation(string source, out string translated)
    {
        var match = ReputationPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var factionKey = match.Groups["faction"].Value.Trim();
        if (!TryTranslateExactLeaf(factionKey, out var translatedFaction))
        {
            var strippedFactionKey = StringHelpers.StripLeadingDefiniteArticle(
                factionKey,
                StringComparison.OrdinalIgnoreCase);
            if (!TryTranslateExactLeaf(strippedFactionKey, out translatedFaction))
            {
                translated = source;
                return false;
            }
        }

        translated = string.Concat(translatedFaction, "との評判 ", match.Groups["value"].Value);
        return true;
    }

    private static bool ContainsRawBulletMarker(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] != '\u00F9')
            {
                continue;
            }

            if (index == 0 || source[index - 1] == '\n' || char.IsWhiteSpace(source[index - 1]))
            {
                return true;
            }
        }

        return false;
    }
    private static Dictionary<string, string> GetSubtypeDisplayNames()
    {
        lock (SyncRoot)
        {
            subtypeDisplayNames ??= LoadDisplayNameMap("Subtypes.jp.xml", "subtype");
            return subtypeDisplayNames;
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

            mutationDisplayNames = LoadDisplayNameMap("Mutations.jp.xml", "mutation");
            MergeDisplayNameMap(mutationDisplayNames, LoadDisplayNameMap("HiddenMutations.jp.xml", "mutation"));
            return mutationDisplayNames;
        }
    }

    private static Dictionary<string, string> GetFactionDisplayNames()
    {
        lock (SyncRoot)
        {
            factionDisplayNames ??= LoadDisplayNameMap("Factions.jp.xml", "faction");
            return factionDisplayNames;
        }
    }

    private static Dictionary<string, string> LoadDisplayNameMap(string relativePath, string elementName)
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

            foreach (var element in document.Root.Descendants(elementName))
            {
                var name = element.Attribute("Name")?.Value;
                var displayName = element.Attribute("DisplayName")?.Value;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                map[name!] = displayName!;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            Trace.TraceWarning(
                "QudJP: ChargenStructuredTextTranslator failed to load '{0}': {1}",
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
}
