using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

internal static class CharacterStatusScreenTextTranslator
{
    private static readonly Regex ComparisonLabelOpenerSuffixPattern =
        new Regex("\\{\\{[^|]+\\|$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AttributePointsPattern =
        new Regex("^Attribute Points: (?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MutationPointsPattern =
        new Regex("^Mutation Points: (?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MutationRankPattern =
        new Regex("^RANK (?<level>\\d+)/(?<max>\\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MutationTypePattern =
        new Regex("^\\[(?<kind>Morphotype|Physical Defect|Mental Defect|Physical Mutation|Mental Mutation)\\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MutationListLinePattern =
        new Regex("^(?<name>.+?) \\((?<level>\\d+)\\)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StatusSummaryPattern =
        new Regex("^Level: (?<level>\\d+) ¯ HP: (?<hpCurrent>\\d+)\\/(?<hpMax>\\d+) ¯ XP: (?<xpCurrent>\\d+)\\/(?<xpMax>\\d+) ¯ Weight: (?<weight>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] CharacterArchetypes =
    {
        "Mutated Human",
        "True Kin",
    };

    private static readonly string[] HighlightHelpLabels =
    {
        "Strength",
        "Agility",
        "Toughness",
        "Intelligence",
        "Willpower",
        "Ego",
        "Armor Value (AV)",
        "Dodge Value (DV)",
        "mental armor (MA)",
        "Quickness",
        "Move Speed",
        "Temperature",
        "Acid Resist",
        "Cold Resist",
        "Electrical Resist",
        "Heat Resist",
        "Compute Power (CP)",
    };

    internal static bool TryTranslateUiText(string source, string route, out string translated)
    {
        if (TryTranslatePointLabel(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateStatusSummary(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateCompactStatusLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateCompareStatusLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateActiveEffectsLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateExactLookup(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateMutationRank(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateMutationType(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateMutationListLine(source, route, out translated))
        {
            return true;
        }

        if (TryTranslateAttributeHelp(source, route, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateCompactStatusLine(string source, string route, out string translated)
    {
        if (StatusLineTranslationHelpers.TryTranslateLevelExpLine(source, route, "CharacterStatus.CompactLevelExp", out translated))
        {
            return true;
        }

        return StatusLineTranslationHelpers.TryTranslateHpLine(source, route, "CharacterStatus.CompactHp", out translated);
    }

    private static bool TryTranslateCompareStatusLine(string source, string route, out string translated)
    {
        return StatusLineTranslationHelpers.TryTranslateCompareStatusLine(source, route, "CharacterStatus.CompareStatus", out translated);
    }

    private static bool TryTranslateActiveEffectsLine(string source, string route, out string translated)
    {
        return StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(source, route, "CharacterStatus.ActiveEffects", out translated);
    }

    private static bool TryTranslatePointLabel(string source, string route, out string translated)
    {
        if (TryTranslateSinglePlaceholderLabel(source, route, AttributePointsPattern, "Attribute Points: {0}", out translated))
        {
            return true;
        }

        return TryTranslateSinglePlaceholderLabel(source, route, MutationPointsPattern, "Mutation Points: {0}", out translated);
    }

    private static bool TryTranslateSinglePlaceholderLabel(string source, string route, Regex pattern, string templateKey, out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = translatedTemplate.Replace("{0}", match.Groups["value"].Value);
        DynamicTextObservability.RecordTransform(route, templateKey, source, translated);
        return true;
    }

    private static bool TryTranslateStatusSummary(string source, string route, out string translated)
    {
        var match = StatusSummaryPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var levelLabel = Translator.Translate("LVL");
        var weightLabel = Translator.Translate("Weight");
        if (string.Equals(levelLabel, "LVL", StringComparison.Ordinal)
            || string.Equals(weightLabel, "Weight", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = string.Concat(
            levelLabel,
            ": ",
            match.Groups["level"].Value,
            " ¯ ",
            "HP",
            ": ",
            match.Groups["hpCurrent"].Value,
            "/",
            match.Groups["hpMax"].Value,
            " ¯ ",
            "XP",
            ": ",
            match.Groups["xpCurrent"].Value,
            "/",
            match.Groups["xpMax"].Value,
            " ¯ ",
            weightLabel,
            ": ",
            match.Groups["weight"].Value);
        DynamicTextObservability.RecordTransform(route, "CharacterStatus.StatusSummary", source, translated);
        return true;
    }

    internal static bool TryTranslateMutationDetails(object mutation, string source, string route, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var mutationName = GetStableMutationDictionaryName(mutation);
        if (string.IsNullOrWhiteSpace(mutationName))
        {
            return false;
        }

        var description = GetMutationDictionaryValue($"mutation:{mutationName}");
        var level = GetIntMemberValue(mutation, "Level");
        var currentRank = level.HasValue
            ? GetMutationDictionaryValue($"mutation:{mutationName}:rank:{level.Value}")
            : null;
        var nextRank = level.HasValue
            ? GetMutationDictionaryValue($"mutation:{mutationName}:rank:{level.Value + 1}")
            : null;

#pragma warning disable CA2249
        if (source.IndexOf("This rank", StringComparison.Ordinal) >= 0
            || source.IndexOf("Next rank", StringComparison.Ordinal) >= 0)
#pragma warning restore CA2249
        {
            return TryTranslateRankComparisonDetails(source, route, description, currentRank, nextRank, out translated);
        }

        return TryTranslateSimpleDetails(source, route, description, currentRank, out translated);
    }

    internal static bool ShouldSuppressMissingKeyLogging(string source)
    {
#pragma warning disable CA2249
        return source.IndexOf("\n\n", StringComparison.Ordinal) >= 0
            || source.IndexOf("This rank", StringComparison.Ordinal) >= 0
            || source.IndexOf("Next rank", StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
    }

    private static bool TryTranslateExactLookup(string source, string route, out string translated)
    {
        if (Translator.TryGetTranslation(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(route, "CharacterStatus.ExactLookup", source, translated);
            return true;
        }

        var translatedLeaf = ChargenStructuredTextTranslator.Translate(source);
        if (!string.Equals(source, translatedLeaf, StringComparison.Ordinal))
        {
            translated = translatedLeaf;
            DynamicTextObservability.RecordTransform(route, "CharacterStatus.ExactLeaf", source, translated);
            return true;
        }

        if (TryTranslateArchetypeCallingTitle(source, route, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateArchetypeCallingTitle(string source, string route, out string translated)
    {
        for (var index = 0; index < CharacterArchetypes.Length; index++)
        {
            var archetype = CharacterArchetypes[index];
            var prefix = archetype + " ";
            if (!source.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var translatedArchetype = ChargenStructuredTextTranslator.Translate(archetype);
            var translatedCalling = ChargenStructuredTextTranslator.Translate(source.Substring(prefix.Length));
            if (string.Equals(translatedArchetype, archetype, StringComparison.Ordinal)
                && string.Equals(translatedCalling, source.Substring(prefix.Length), StringComparison.Ordinal))
            {
                break;
            }

            translated = translatedArchetype + " " + translatedCalling;
            DynamicTextObservability.RecordTransform(route, "CharacterStatus.ArchetypeCalling", source, translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateMutationRank(string source, string route, out string translated)
    {
        var match = MutationRankPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var rankLabel = Translator.Translate("RANK");
        if (string.Equals(rankLabel, "RANK", StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = $"{rankLabel} {match.Groups["level"].Value}/{match.Groups["max"].Value}";
        DynamicTextObservability.RecordTransform(route, "CharacterStatus.Rank", source, translated);
        return true;
    }

    private static bool TryTranslateMutationType(string source, string route, out string translated)
    {
        var match = MutationTypePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedKind = Translator.Translate(match.Groups["kind"].Value);
        if (string.Equals(translatedKind, match.Groups["kind"].Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = $"[{translatedKind}]";
        DynamicTextObservability.RecordTransform(route, "CharacterStatus.MutationType", source, translated);
        return true;
    }

    private static bool TryTranslateMutationListLine(string source, string route, out string translated)
    {
        var match = MutationListLinePattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var translatedName = ChargenStructuredTextTranslator.Translate(match.Groups["name"].Value);
        if (string.Equals(translatedName, match.Groups["name"].Value, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        translated = $"{translatedName} ({match.Groups["level"].Value})";
        DynamicTextObservability.RecordTransform(route, "CharacterStatus.MutationLine", source, translated);
        return true;
    }

    private static bool TryTranslateAttributeHelp(string source, string route, out string translated)
    {
        var normalizedKey = NormalizeAttributeHelpKey(source);
        if (normalizedKey is null
            || !Translator.TryGetTranslation(normalizedKey, out translated)
            || string.Equals(normalizedKey, translated, StringComparison.Ordinal))
        {
            translated = source;
            return false;
        }

        DynamicTextObservability.RecordTransform(route, "CharacterStatus.AttributeHelp", source, translated);
        return true;
    }

    private static string? NormalizeAttributeHelpKey(string source)
    {
        for (var index = 0; index < HighlightHelpLabels.Length; index++)
        {
            var label = HighlightHelpLabels[index];
            var prefix = $"Your {label}";
            if (!source.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var builder = new StringBuilder();
            builder.Append("Your {{W|");
            builder.Append(label);
            builder.Append("}}");
#pragma warning disable CA1846
            builder.Append(source.Substring(prefix.Length, source.Length - prefix.Length));
#pragma warning restore CA1846
            return builder.ToString();
        }

        return null;
    }

    private static bool TryTranslateSimpleDetails(string source, string route, string? description, string? rankText, out string translated)
    {
        if (string.IsNullOrEmpty(description) && string.IsNullOrEmpty(rankText))
        {
            translated = source;
            return false;
        }

        if (!string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(rankText))
        {
            translated = string.Concat(description, "\n\n", rankText);
        }
        else
        {
            translated = description ?? rankText ?? source;
        }

        DynamicTextObservability.RecordTransform(route, "CharacterStatus.MutationDetails", source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool TryTranslateRankComparisonDetails(
        string source,
        string route,
        string? description,
        string? currentRank,
        string? nextRank,
        out string translated)
    {
#pragma warning disable CA2249
        var hasCurrentRankSection = source.IndexOf("This rank", StringComparison.Ordinal) >= 0;
        var hasNextRankSection = source.IndexOf("Next rank", StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
        var hasDescriptionSection = RequiresComparisonDescription(source, hasCurrentRankSection, hasNextRankSection);
        var thisRankLabel = hasCurrentRankSection ? Translator.Translate("This rank") : null;
        var nextRankLabel = hasNextRankSection ? Translator.Translate("Next rank") : null;
        if ((!hasCurrentRankSection && !hasNextRankSection)
            || (hasDescriptionSection && string.IsNullOrEmpty(description))
            || (hasCurrentRankSection
                && (string.IsNullOrEmpty(currentRank)
                    || string.Equals(thisRankLabel, "This rank", StringComparison.Ordinal)))
            || (hasNextRankSection
                && (string.IsNullOrEmpty(nextRank)
                    || string.Equals(nextRankLabel, "Next rank", StringComparison.Ordinal))))
        {
            translated = source;
            return false;
        }

        var builder = new StringBuilder();
        if (hasDescriptionSection)
        {
            builder.Append(description);
        }

        if (hasCurrentRankSection)
        {
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append("{{w|");
            builder.Append(thisRankLabel);
            builder.Append("}}:\n");
            builder.Append(currentRank);
        }

        if (hasNextRankSection)
        {
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append("{{w|");
            builder.Append(nextRankLabel);
            builder.Append("}}:\n");
            builder.Append(nextRank);
        }

        translated = builder.Length == 0 ? source : builder.ToString();
        DynamicTextObservability.RecordTransform(route, "CharacterStatus.MutationDetailComparison", source, translated);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool RequiresComparisonDescription(string source, bool hasCurrentRankSection, bool hasNextRankSection)
    {
        var firstLabelIndex = GetFirstComparisonLabelIndex(source, hasCurrentRankSection, hasNextRankSection);
        if (firstLabelIndex <= 0)
        {
            return false;
        }

        var prefix = source.Substring(0, firstLabelIndex).TrimEnd();
        if (prefix.Length == 0)
        {
            return false;
        }

        prefix = ComparisonLabelOpenerSuffixPattern.Replace(prefix, string.Empty).TrimEnd();
        prefix = prefix.TrimEnd(':');
        return !string.IsNullOrWhiteSpace(prefix);
    }

    private static int GetFirstComparisonLabelIndex(string source, bool hasCurrentRankSection, bool hasNextRankSection)
    {
        var currentIndex = hasCurrentRankSection
            ? source.IndexOf("This rank", StringComparison.Ordinal)
            : -1;
        var nextIndex = hasNextRankSection
            ? source.IndexOf("Next rank", StringComparison.Ordinal)
            : -1;

        if (currentIndex < 0)
        {
            return nextIndex;
        }

        if (nextIndex < 0)
        {
            return currentIndex;
        }

        return Math.Min(currentIndex, nextIndex);
    }

    private static string? GetMutationDictionaryValue(string key)
    {
        return Translator.TryGetTranslation(key, out var translated)
            && !string.Equals(translated, key, StringComparison.Ordinal)
            ? translated
            : null;
    }

    private static string? GetStableMutationDictionaryName(object mutation)
    {
        var mutationEntry = GetObjectMethodValue(mutation, "GetMutationEntry");
        if (mutationEntry is null)
        {
            Trace.TraceWarning(
                "QudJP: CharacterStatusScreenTextTranslator could not resolve GetMutationEntry() for mutation details on type '{0}'.",
                mutation.GetType().FullName);
            return null;
        }

        var entryName = GetStringMemberValue(mutationEntry, "Name");
        if (!string.IsNullOrWhiteSpace(entryName))
        {
            return entryName;
        }

        Trace.TraceWarning(
            "QudJP: CharacterStatusScreenTextTranslator received a mutation entry without a stable Name for type '{0}'.",
            mutation.GetType().FullName);
        return null;
    }

    private static object? GetObjectMethodValue(object instance, string methodName)
    {
        var method = AccessTools.Method(instance.GetType(), methodName, Type.EmptyTypes);
        return method?.Invoke(instance, null);
    }

    private static string? GetStringMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead && property.PropertyType == typeof(string))
        {
            return property.GetValue(instance) as string;
        }

        var field = AccessTools.Field(type, memberName);
        return field?.FieldType == typeof(string)
            ? field.GetValue(instance) as string
            : null;
    }

    private static int? GetIntMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = AccessTools.Property(type, memberName);
        if (property is not null && property.CanRead && property.PropertyType == typeof(int))
        {
            if (property.GetValue(instance) is int propertyValue)
            {
                return propertyValue;
            }

            return null;
        }

        var field = AccessTools.Field(type, memberName);
        if (field?.FieldType != typeof(int))
        {
            return null;
        }

        if (field.GetValue(instance) is int fieldValue)
        {
            return fieldValue;
        }

        return null;
    }
}
