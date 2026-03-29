using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

public static class FactionsStatusScreenTranslationPatch
{
    private static readonly Regex VillageLabelPattern =
        new Regex("^The villagers of (?<name>[^.]+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex VillageNeutralPattern =
        new Regex("^The villagers of (?<name>.+) don't care about you, but aggressive ones will attack you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex VillageGossipPattern =
        new Regex("^The villagers of (?<name>.+) are interested in hearing gossip that's about them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DefiniteFactionPattern =
        new Regex("^The (?<name>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ReputationValuePattern =
        new Regex("^Reputation:\\s+(?<value>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GenericNeutralPattern =
        new Regex("^(?:The )?(?<name>.+?) (?<verb>don't|doesn't) care about you, but aggressive (?<group>ones|members) will attack you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretTradeAndGossipPattern =
        new Regex("^(?:The )?(?<name>.+?) (?<copula>is|are) interested in trading secrets about (?<topic>.+?)\\. They're also interested in hearing gossip that's about them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretGossipOnlyPattern =
        new Regex("^(?:The )?(?<name>.+?) (?<copula>is|are) interested in hearing gossip that's about them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const BindingFlags PublicInstanceFlags = BindingFlags.Instance | BindingFlags.Public;
    private const BindingFlags PublicStaticFlags = BindingFlags.Public | BindingFlags.Static;
    private static readonly Regex GenericDespisePattern =
        new Regex("^(?:The )?(?<name>.+?) (?<verb>despise|despises) you\\. Even docile (?<group>ones|members) will attack you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GenericDislikePattern =
        new Regex("^(?:The )?(?<name>.+?) (?<verb>dislike|dislikes) you, but docile (?<group>ones|members) won't attack you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GenericFavorPattern =
        new Regex("^(?:The )?(?<name>.+?) (?<verb>favor|favors) you\\. Aggressive (?<group>ones|members) won't attack you\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GenericReverePattern =
        new Regex("^(?:The )?(?<name>.+?) (?<verb>revere|reveres) you and (?<consider>consider|considers) you one of their own\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RankPattern =
        new Regex("^You hold the (?<term>.+?) of (?<rank>.+?) among them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PetPattern =
        new Regex("^(?:The )?(?<name>.+?) (?<allow>won't|will) usually let you pet them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretTradePattern =
        new Regex("^(?:The )?(?<name>.+?) (?<copula>is|are) interested in trading secrets about (?<topic>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretSharePattern =
        new Regex("^(?:The )?(?<name>.+?) (?<copula>is|are) interested in sharing secrets about (?<topic>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretLearnPattern =
        new Regex("^(?:The )?(?<name>.+?) (?<copula>is|are) interested in learning about (?<topic>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretAlsoTradeAndGossipPattern =
        new Regex("^They're also interested in trading secrets about (?<topic>.+?) and hearing gossip that's about them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretAlsoShareAndGossipPattern =
        new Regex("^They're also interested in sharing secrets about (?<topic>.+?) and hearing gossip that's about them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretAlsoLearnAndGossipPattern =
        new Regex("^They're also interested in learning about (?<topic>.+?) and hearing gossip that's about them\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretAlsoTradePattern =
        new Regex("^They're also interested in trading secrets about (?<topic>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretAlsoSharePattern =
        new Regex("^They're also interested in sharing secrets about (?<topic>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretAlsoLearnPattern =
        new Regex("^They're also interested in learning about (?<topic>.+?)\\.$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TopicLocationsOfPattern =
        new Regex("^(?:the )?locations? of (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TopicLocationsInPattern =
        new Regex("^locations? in (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TopicLocationsAroundPattern =
        new Regex("^locations? around (?<target>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TopicAllLocationsPattern =
        new Regex("^all (?<target>.+) locations$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LocalizedReputationValuePattern =
        new Regex("^評判:\\s+[-+]?\\d+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LocalizedVillageLabelPattern =
        new Regex("^.+の村人たち$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] EnglishFactionSentenceCues =
    {
        "don't care about you",
        "despise you",
        "dislike you",
        "favor you",
        "revere you",
        "consider you one of their own",
        "welcome in their holy places",
        "pet them",
        "interested in",
        "gossip that's about them",
    };
    private static readonly string[] LocalizedFactionSentenceMarkers =
    {
        "はあなたを",
        "はあなたに好意的",
        "はあなたを憎悪",
        "を自分たちの一員",
        "に関する秘密の取引に関心があり",
        "に関する秘密の共有に関心がある",
        "について知ることに関心がある",
        "自分たちに関するうわさ話に興味を示す",
        "彼らの聖地では歓迎されていない",
        "彼らの聖地で歓迎されている",
        "たいてい撫でさせてくれる",
        "たいてい撫でさせてくれない",
    };

#pragma warning disable CA2249
    private static bool ContainsChar(string source, char value) => source.IndexOf(value) >= 0;
#pragma warning restore CA2249

    internal static bool TryTranslateFactionText(string source, string context, out string translated)
    {
        translated = TranslateFactionText(source, context);
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    internal static string TranslateFactionText(string source, string context)
    {
        var currentContext = Translator.GetCurrentLogContext();
        if (string.Equals(
                ObservabilityHelpers.ExtractPrimaryContext(currentContext),
                ObservabilityHelpers.ExtractPrimaryContext(context),
                StringComparison.Ordinal))
        {
            return TranslateFactionTextWithoutContext(source);
        }

        using var _ = Translator.PushLogContext(context);
        return TranslateFactionTextWithoutContext(source);
    }

    internal static string TranslateFactionTextWithoutContext(string source)
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var translated = TranslateFactionTextCore(stripped);
        return spans.Count == 0
            ? translated
            : ColorAwareTranslationComposer.Restore(translated, spans);
    }

    internal static bool IsAlreadyLocalizedFactionText(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (string.Equals(source, "-", StringComparison.Ordinal)
            || LocalizedReputationValuePattern.IsMatch(source)
            || LocalizedVillageLabelPattern.IsMatch(source))
        {
            return true;
        }

        for (var index = 0; index < LocalizedFactionSentenceMarkers.Length; index++)
        {
            if (StringHelpers.ContainsOrdinal(source, LocalizedFactionSentenceMarkers[index]))
            {
                return true;
            }
        }

        return LooksLikeTerminalFactionLabel(source);
    }

    private static string TranslateFactionTextCore(string source)
    {
        if (IsAlreadyLocalizedFactionText(source))
        {
            return source;
        }

        if (UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(source, nameof(FactionsStatusScreenTranslationPatch)))
        {
            return source;
        }

#pragma warning disable CA2249
        if (source.IndexOf("\n\n", StringComparison.Ordinal) >= 0
            && TryTranslateParagraphSequence(source, out var paragraphTranslation))
        {
            return paragraphTranslation;
        }
#pragma warning restore CA2249

        if (TryTranslateExactKey(source, out var directTranslation))
        {
            return directTranslation;
        }

        if (TryTranslateTemplate(source, VillageNeutralPattern, "The villagers of {0} don't care about you, but aggressive ones will attack you.", out var translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, VillageGossipPattern, "The villagers of {0} are interested in hearing gossip that's about them.", out translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, VillageLabelPattern, "The villagers of {0}", out translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, GenericNeutralPattern, "The {0} don't care about you, but aggressive ones will attack you.", out translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, GenericDespisePattern, "The {0} despise you. Even docile ones will attack you.", out translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, GenericDislikePattern, "The {0} dislike you, but docile ones won't attack you.", out translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, GenericFavorPattern, "The {0} favor you. Aggressive ones won't attack you.", out translated))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, GenericReverePattern, "The {0} revere you and consider you one of their own.", out translated))
        {
            return translated;
        }

        if (TryTranslateRankText(source, out translated))
        {
            return translated;
        }

        if (TryTranslatePetText(source, out translated))
        {
            return translated;
        }

        if (TryTranslateSecretTradeAndGossip(source, out translated))
        {
            return translated;
        }

        if (TryTranslateSecretSentence(source, SecretTradePattern, "The {0} are interested in trading secrets about {1}.", out translated))
        {
            return translated;
        }

        if (TryTranslateSecretSentence(source, SecretSharePattern, "The {0} are interested in sharing secrets about {1}.", out translated))
        {
            return translated;
        }

        if (TryTranslateSecretSentence(source, SecretLearnPattern, "The {0} are interested in learning about {1}.", out translated))
        {
            return translated;
        }

        if (TryTranslateSecretGossipOnly(source, out translated))
        {
            return translated;
        }

        if (TryTranslateFollowupSentence(source, SecretAlsoTradeAndGossipPattern, "They're also interested in trading secrets about {0} and hearing gossip that's about them.", out translated))
        {
            return translated;
        }

        if (TryTranslateFollowupSentence(source, SecretAlsoShareAndGossipPattern, "They're also interested in sharing secrets about {0} and hearing gossip that's about them.", out translated))
        {
            return translated;
        }

        if (TryTranslateFollowupSentence(source, SecretAlsoLearnAndGossipPattern, "They're also interested in learning about {0} and hearing gossip that's about them.", out translated))
        {
            return translated;
        }

        if (TryTranslateFollowupSentence(source, SecretAlsoTradePattern, "They're also interested in trading secrets about {0}.", out translated))
        {
            return translated;
        }

        if (TryTranslateFollowupSentence(source, SecretAlsoSharePattern, "They're also interested in sharing secrets about {0}.", out translated))
        {
            return translated;
        }

        if (TryTranslateFollowupSentence(source, SecretAlsoLearnPattern, "They're also interested in learning about {0}.", out translated))
        {
            return translated;
        }

        if (TryTranslateSentenceSequence(source, out var sentenceTranslation))
        {
            return sentenceTranslation;
        }

        if (TryTranslateTemplate(source, DefiniteFactionPattern, "The {0}", out translated, translateGroup: true))
        {
            return translated;
        }

        if (TryTranslateTemplate(source, ReputationValuePattern, "Reputation: {0}", out translated, groupName: "value"))
        {
            return translated;
        }

        return source;
    }

    private static bool TryTranslateSecretTradeAndGossip(string source, out string translated)
    {
        var match = SecretTradeAndGossipPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = string.Equals(match.Groups["copula"].Value, "are", StringComparison.Ordinal)
            ? "The {0} are interested in trading secrets about {1}. They're also interested in hearing gossip that's about them."
            : "The {0} is interested in trading secrets about {1}. They're also interested in hearing gossip that's about them.";
        return TryTranslateTwoPlaceholderTemplate(
            match,
            templateKey,
            "name",
            "topic",
            out translated,
            translateFirstGroup: true,
            translateSecondGroup: true);
    }

    private static bool TryTranslateSecretGossipOnly(string source, out string translated)
    {
        var match = SecretGossipOnlyPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = string.Equals(match.Groups["copula"].Value, "are", StringComparison.Ordinal)
            ? "The {0} are interested in hearing gossip that's about them."
            : "The {0} is interested in hearing gossip that's about them.";
        return TryTranslateTemplateFromMatch(match, templateKey, out translated, translateGroup: true);
    }

    private static bool TryTranslateTemplate(string source, Regex pattern, string templateKey, out string translated, string groupName = "name", bool translateGroup = false)
    {
        var match = pattern.Match(source);
        return TryTranslateTemplateFromMatch(match, templateKey, out translated, groupName, translateGroup);
    }

    private static bool TryTranslateTemplateFromMatch(
        Match match,
        string templateKey,
        out string translated,
        string groupName = "name",
        bool translateGroup = false,
        bool translateGroupAsTopic = false)
    {
        if (!match.Success)
        {
            translated = match.Value;
            return false;
        }

        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = match.Value;
            return false;
        }

        var replacement = match.Groups[groupName].Value;
        if (translateGroup)
        {
            replacement = translateGroupAsTopic
                ? TranslateFactionTopic(replacement)
                : TranslateFactionGroup(replacement);
        }

        translated = translatedTemplate.Replace("{0}", replacement);
        DynamicTextObservability.RecordTransform(
            nameof(FactionsStatusScreenTranslationPatch),
            templateKey,
            match.Value,
            translated);
        return true;
    }

    private static bool TryTranslateTwoPlaceholderTemplate(
        Match match,
        string templateKey,
        string firstGroupName,
        string secondGroupName,
        out string translated,
        bool translateFirstGroup = false,
        bool translateSecondGroup = false)
    {
        if (!match.Success)
        {
            translated = match.Value;
            return false;
        }

        var translatedTemplate = Translator.Translate(templateKey);
        if (string.Equals(translatedTemplate, templateKey, StringComparison.Ordinal))
        {
            translated = match.Value;
            return false;
        }

        var first = ResolveTemplateGroup(match.Groups[firstGroupName].Value, firstGroupName, translateFirstGroup);
        var second = ResolveTemplateGroup(match.Groups[secondGroupName].Value, secondGroupName, translateSecondGroup);
        translated = translatedTemplate
            .Replace("{0}", first)
            .Replace("{1}", second);
        DynamicTextObservability.RecordTransform(
            nameof(FactionsStatusScreenTranslationPatch),
            templateKey,
            match.Value,
            translated);
        return true;
    }

    private static string ResolveTemplateGroup(string value, string groupName, bool translateGroup)
    {
        if (!translateGroup)
        {
            return value;
        }

        return string.Equals(groupName, "topic", StringComparison.Ordinal)
            ? TranslateFactionTopic(value)
            : TranslateFactionGroup(value);
    }

    private static string TranslateFactionGroup(string value)
    {
        if (UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
                value,
                nameof(FactionsStatusScreenTranslationPatch)))
        {
            return value;
        }

        return TryTranslateExactOrCaseFoldedKey(value, out var translated)
            ? translated
            : value;
    }

    private static string TranslateFactionTopic(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (UITextSkinTranslationPatch.IsAlreadyLocalizedDirectRouteTextForContext(
                value,
                nameof(FactionsStatusScreenTranslationPatch)))
        {
            return value;
        }

        if (TryTranslateExactOrCaseFoldedKey(value, out var direct))
        {
            return direct;
        }

        if (TryTranslateFactionTopicList(value, out var listTranslated))
        {
            return listTranslated;
        }

        if (TryTranslateFactionTopicFragment(value, out var fragmentTranslated))
        {
            return fragmentTranslated;
        }

        return value;
    }

    private static bool TryTranslateExactKey(string source, out string translated)
    {
        if (Translator.TryGetTranslation(source, out translated)
            && !string.Equals(source, translated, StringComparison.Ordinal))
        {
            DynamicTextObservability.RecordTransform(
                nameof(FactionsStatusScreenTranslationPatch),
                source,
                source,
                translated);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateExactOrCaseFoldedKey(string source, out string translated)
    {
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(source, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            nameof(FactionsStatusScreenTranslationPatch),
            source,
            source,
            translated);
        return true;
    }

    private static bool TryTranslateParagraphSequence(string source, out string translated)
    {
        var parts = source.Split(["\n\n"], StringSplitOptions.None);
        var changed = false;
        for (var index = 0; index < parts.Length; index++)
        {
            var part = TranslateFactionTextCore(parts[index]);
            changed |= !string.Equals(parts[index], part, StringComparison.Ordinal);
            parts[index] = part;
        }

        translated = changed ? string.Join("\n\n", parts) : source;
        return changed;
    }

    private static bool TryTranslateSentenceSequence(string source, out string translated)
    {
        translated = source;
        var splitIndex = source.IndexOf(". ", StringComparison.Ordinal);
        if (splitIndex < 0)
        {
            return false;
        }

        var first = source.Substring(0, splitIndex + 1);
        var second = source.Substring(splitIndex + 2);
        if (first.Length == 0 || second.Length == 0)
        {
            return false;
        }

        var translatedFirst = TranslateFactionTextCore(first);
        var translatedSecond = TranslateFactionTextCore(second);
        if (string.Equals(first, translatedFirst, StringComparison.Ordinal)
            && string.Equals(second, translatedSecond, StringComparison.Ordinal))
        {
            return false;
        }

        translated = translatedFirst + " " + translatedSecond;
        return true;
    }

    private static bool TryTranslateRankText(string source, out string translated)
    {
        var match = RankPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        return TryTranslateTwoPlaceholderTemplate(
            match,
            "You hold the {0} of {1} among them.",
            "term",
            "rank",
            out translated,
            translateSecondGroup: true);
    }

    private static bool TryTranslatePetText(string source, out string translated)
    {
        var match = PetPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = string.Equals(match.Groups["allow"].Value, "will", StringComparison.Ordinal)
            ? "The {0} will usually let you pet them."
            : "The {0} won't usually let you pet them.";
        return TryTranslateTemplateFromMatch(match, templateKey, out translated, translateGroup: true);
    }

    private static bool TryTranslateSecretSentence(string source, Regex pattern, string pluralTemplateKey, out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var templateKey = pluralTemplateKey;
        if (!string.Equals(match.Groups["copula"].Value, "are", StringComparison.Ordinal))
        {
            templateKey = pluralTemplateKey.Replace(" are ", " is ");
        }
        return TryTranslateTwoPlaceholderTemplate(
            match,
            templateKey,
            "name",
            "topic",
            out translated,
            translateFirstGroup: true,
            translateSecondGroup: true);
    }

    private static bool TryTranslateFollowupSentence(string source, Regex pattern, string templateKey, out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        return TryTranslateTemplateFromMatch(
            match,
            templateKey,
            out translated,
            groupName: "topic",
            translateGroup: true,
            translateGroupAsTopic: true);
    }

    private static bool TryTranslateFactionTopicList(string source, out string translated)
    {
        var parts = SplitFactionTopicList(source);
        if (parts.Count < 2)
        {
            translated = source;
            return false;
        }

        var changed = false;
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            var translatedPart = TranslateFactionTopic(part);
            changed |= !string.Equals(part, translatedPart, StringComparison.Ordinal);
            parts[index] = translatedPart;
        }

        if (!changed)
        {
            translated = source;
            return false;
        }

        translated = GrammarPatchHelpers.BuildJapaneseList(parts, "と");
        return true;
    }

    private static List<string> SplitFactionTopicList(string source)
    {
        var parts = new List<string>();
        var current = new StringBuilder(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            if (TryConsumeFactionTopicSeparator(source, ref index, current, parts))
            {
                continue;
            }

            current.Append(source[index]);
        }

        var last = current.ToString().Trim();
        if (last.Length > 0)
        {
            parts.Add(last);
        }

        return parts;
    }

    private static bool TryConsumeFactionTopicSeparator(string source, ref int index, StringBuilder current, List<string> parts)
    {
        if (source[index] == '、' || source[index] == 'と')
        {
            _ = TryFlushFactionTopicPart(current, parts);
            return true;
        }

        if (StartsWithAt(source, index, ", and "))
        {
            _ = TryFlushFactionTopicPart(current, parts);
            index += ", and ".Length - 1;
            return true;
        }

        if (StartsWithAt(source, index, " and "))
        {
            _ = TryFlushFactionTopicPart(current, parts);
            index += " and ".Length - 1;
            return true;
        }

        if (StartsWithAt(source, index, ", "))
        {
            _ = TryFlushFactionTopicPart(current, parts);
            index += ", ".Length - 1;
            return true;
        }

        return false;
    }

    private static bool TryFlushFactionTopicPart(StringBuilder current, List<string> parts)
    {
        var part = current.ToString().Trim();
        if (part.Length == 0)
        {
            return false;
        }

        parts.Add(part);
        current.Clear();
        return true;
    }

    private static bool StartsWithAt(string source, int index, string value)
    {
        return index + value.Length <= source.Length
            && string.Compare(source, index, value, 0, value.Length, StringComparison.Ordinal) == 0;
    }

    private static bool TryTranslateFactionTopicFragment(string source, out string translated)
    {
        if (TryTranslateTopicPattern(source, TopicLocationsOfPattern, "{0}の場所", out translated))
        {
            return true;
        }

        if (TryTranslateTopicPattern(source, TopicLocationsInPattern, "{0}の場所", out translated))
        {
            return true;
        }

        if (TryTranslateTopicPattern(source, TopicLocationsAroundPattern, "{0}周辺の場所", out translated))
        {
            return true;
        }

        if (TryTranslateTopicPattern(source, TopicAllLocationsPattern, "すべての{0}の場所", out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateTopicPattern(string source, Regex pattern, string template, out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var target = TranslateFactionTopicLeaf(match.Groups["target"].Value);
        translated = template.Replace("{0}", target);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static string TranslateFactionTopicLeaf(string value)
    {
        if (TryTranslateExactOrCaseFoldedKey(value, out var translated))
        {
            return translated;
        }

        var stripped = StringHelpers.StripLeadingDefiniteArticle(value, StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(stripped, value, StringComparison.Ordinal)
            && TryTranslateExactOrCaseFoldedKey(stripped, out translated))
        {
            return translated;
        }

        return value;
    }
    private static bool LooksLikeTerminalFactionLabel(string source)
    {
        if (source.StartsWith("The ", StringComparison.Ordinal)
            || source.StartsWith("Reputation:", StringComparison.Ordinal)
            || source.StartsWith("You ", StringComparison.Ordinal)
            || source.StartsWith("They're ", StringComparison.Ordinal))
        {
            return false;
        }

        if (ContainsChar(source, '\n')
            || ContainsChar(source, '.')
            || ContainsChar(source, ':'))
        {
            return false;
        }

        for (var index = 0; index < EnglishFactionSentenceCues.Length; index++)
        {
            if (StringHelpers.ContainsOrdinal(source, EnglishFactionSentenceCues[index]))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryGetLocalizedFactionSearchFragments(string? factionId, out IReadOnlyList<string> fragments)
    {
        fragments = Array.Empty<string>();
        if (string.IsNullOrEmpty(factionId))
        {
            return false;
        }

        var factionsType = AccessTools.TypeByName("XRL.World.Factions");
        var factionType = AccessTools.TypeByName("XRL.World.Faction");
        if (factionsType is null || factionType is null)
        {
            return false;
        }

        var getFaction = factionsType.GetMethod("Get", PublicStaticFlags, null, new[] { typeof(string) }, null);
        var getPreferredSecretDescription = factionType.GetMethod("GetPreferredSecretDescription", PublicStaticFlags, null, new[] { typeof(string) }, null);
        if (getFaction is null || getPreferredSecretDescription is null)
        {
            return false;
        }

        var faction = getFaction.Invoke(null, new object[] { factionId! });
        if (faction is null)
        {
            return false;
        }

        var methods = new[]
        {
            "GetFeelingText",
            "GetRankText",
            "GetPetText",
            "GetHolyPlaceText",
        };

        var localized = new List<string>();
        for (var index = 0; index < methods.Length; index++)
        {
            var method = faction.GetType().GetMethod(methods[index], PublicInstanceFlags);
            var source = method?.Invoke(faction, null) as string;
            AddLocalizedSearchFragment(localized, source);
        }

        AddLocalizedSearchFragment(localized, getPreferredSecretDescription.Invoke(null, new object[] { factionId! }) as string);
        fragments = localized;
        return localized.Count > 0;
    }

    internal static bool TryTranslateFactionLabelFromId(string source, string? factionId, out string translated)
    {
        translated = source;
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(factionId))
        {
            return false;
        }

        if (source.StartsWith("The ", StringComparison.Ordinal)
            && TryTranslateFactionLabelFallbackCandidate($"the {factionId}", out translated))
        {
            return true;
        }

        return TryTranslateFactionLabelFallbackCandidate(factionId!, out translated);
    }

    private static void AddLocalizedSearchFragment(List<string> localized, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var translated = IsAlreadyLocalizedFactionText(source!)
            ? source!
            : TranslateFactionTextWithoutContext(source!);
        var (stripped, _) = ColorAwareTranslationComposer.Strip(translated);
        if (!string.IsNullOrWhiteSpace(stripped)
            && !string.Equals(source, stripped, StringComparison.Ordinal)
            && !localized.Contains(stripped))
        {
            localized.Add(stripped);
        }
    }

    private static bool TryTranslateFactionLabelFallbackCandidate(string key, out string translated)
    {
        if (!StringHelpers.TryGetTranslationExactOrLowerAscii(key, out translated))
        {
            return false;
        }

        DynamicTextObservability.RecordTransform(
            nameof(FactionsStatusScreenTranslationPatch),
            $"FactionLabelFallback:{key}",
            key,
            translated);
        return true;
    }
}
