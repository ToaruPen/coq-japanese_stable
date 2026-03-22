using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace QudJP;

internal static class MessageFrameTranslator
{
    private static readonly object SyncRoot = new object();
    private static readonly Regex PlaceholderPattern =
        new Regex(@"\{(?<index>\d+)\}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static LoadedVerbDictionary? loadedDictionary;
    private static string? dictionaryPathOverride;
    private static int loadInvocationCount;

    internal const char DirectTranslationMarker = '\x01';

    internal static int LoadInvocationCount => Volatile.Read(ref loadInvocationCount);

    internal static void SetDictionaryPathForTests(string? filePath)
    {
        lock (SyncRoot)
        {
            dictionaryPathOverride = filePath;
            loadedDictionary = null;
            Interlocked.Exchange(ref loadInvocationCount, 0);
        }
    }

    internal static void ResetForTests()
    {
        SetDictionaryPathForTests(null);
    }

    internal static string MarkDirectTranslation(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return DirectTranslationMarker.ToString();
        }

        return source[0] == DirectTranslationMarker
            ? source
            : DirectTranslationMarker + source;
    }

    internal static bool TryStripDirectTranslationMarker(string? source, out string stripped)
    {
        if (source is not null && source.Length > 0 && source[0] == DirectTranslationMarker)
        {
            stripped = source.Substring(startIndex: 1);
            return true;
        }

        stripped = source ?? string.Empty;
        return false;
    }

    internal static bool TryStripDirectTranslationMarker(ref string message)
    {
        if (!TryStripDirectTranslationMarker(message, out var stripped))
        {
            return false;
        }

        message = stripped;
        return true;
    }

    internal static bool TryTranslateXDidY(
        string? subject,
        string verb,
        string? extra,
        string? endMark,
        out string translated)
    {
        if (verb is null)
        {
            throw new ArgumentNullException(nameof(verb));
        }

        if (!TryResolveVerbOnlyOrTailPredicate(
                verb,
                NormalizeFragment(extra),
                objectSlotCount: 0,
                Array.Empty<string>(),
                out var predicate))
        {
            translated = string.Empty;
            return false;
        }

        translated = BuildSentence(subject, predicate, endMark);
        return true;
    }

    internal static bool TryTranslateXDidYToZ(
        string? subject,
        string verb,
        string? preposition,
        string objectText,
        string? extra,
        string? endMark,
        out string translated)
    {
        if (verb is null)
        {
            throw new ArgumentNullException(nameof(verb));
        }

        if (objectText is null)
        {
            throw new ArgumentNullException(nameof(objectText));
        }

        var normalizedTail = BuildSingleObjectTail(preposition, extra);
        if (TryResolveVerbOnlyOrTailPredicate(
                verb,
                normalizedTail,
                objectSlotCount: 1,
                new[] { objectText },
                out var predicate))
        {
            translated = BuildSentence(subject, predicate, endMark);
            return true;
        }

        if (string.IsNullOrWhiteSpace(extra)
            && TryResolveTier1Verb(verb, out var verbText)
            && TryBuildObjectPhrase(preposition, objectText, out var objectPhrase))
        {
            translated = BuildSentence(subject, objectPhrase + verbText, endMark);
            return true;
        }

        translated = string.Empty;
        return false;
    }

    internal static bool TryTranslateWDidXToYWithZ(
        string? subject,
        string verb,
        string? directPreposition,
        string directObject,
        string? indirectPreposition,
        string indirectObject,
        string? extra,
        string? endMark,
        out string translated)
    {
        if (verb is null)
        {
            throw new ArgumentNullException(nameof(verb));
        }

        if (directObject is null)
        {
            throw new ArgumentNullException(nameof(directObject));
        }

        if (indirectObject is null)
        {
            throw new ArgumentNullException(nameof(indirectObject));
        }

        var normalizedTail = BuildDoubleObjectTail(directPreposition, indirectPreposition, extra);
        if (TryResolveVerbOnlyOrTailPredicate(
                verb,
                normalizedTail,
                objectSlotCount: 2,
                new[] { directObject, indirectObject },
                out var predicate))
        {
            translated = BuildSentence(subject, predicate, endMark);
            return true;
        }

        if (string.IsNullOrWhiteSpace(extra)
            && TryResolveTier1Verb(verb, out var verbText)
            && TryBuildObjectPhrase(directPreposition, directObject, out var directPhrase)
            && TryBuildObjectPhrase(indirectPreposition, indirectObject, out var indirectPhrase))
        {
            translated = BuildSentence(subject, directPhrase + indirectPhrase + verbText, endMark);
            return true;
        }

        translated = string.Empty;
        return false;
    }

    private static bool TryResolveVerbOnlyOrTailPredicate(
        string verb,
        string? normalizedTail,
        int objectSlotCount,
        IReadOnlyList<string> objectValues,
        out string predicate)
    {
        if (string.IsNullOrWhiteSpace(normalizedTail))
        {
            return TryResolveTier1Verb(verb, out predicate);
        }

        var normalizedTailValue = normalizedTail!;
        if (TryResolveExactPair(verb, normalizedTailValue, objectValues, out predicate))
        {
            return true;
        }

        if (TryResolveTemplate(verb, normalizedTailValue, objectSlotCount, objectValues, out predicate))
        {
            return true;
        }

        predicate = string.Empty;
        return false;
    }

    private static bool TryResolveTier1Verb(string verb, out string predicate)
    {
        var dictionary = GetLoadedDictionary();
        return dictionary.Tier1.TryGetValue(verb, out predicate!);
    }

    private static bool TryResolveExactPair(
        string verb,
        string normalizedTail,
        IReadOnlyList<string> objectValues,
        out string predicate)
    {
        var dictionary = GetLoadedDictionary();
        if (!dictionary.Tier2.TryGetValue(BuildPairKey(verb, normalizedTail), out var template))
        {
            predicate = string.Empty;
            return false;
        }

        predicate = ApplyPlaceholderValues(template, CreateReplacementValues(objectValues, staticValues: null));
        return true;
    }

    private static bool TryResolveTemplate(
        string verb,
        string normalizedTail,
        int objectSlotCount,
        IReadOnlyList<string> objectValues,
        out string predicate)
    {
        var dictionary = GetLoadedDictionary();
        for (var index = 0; index < dictionary.Tier3.Count; index++)
        {
            var definition = dictionary.Tier3[index];
            if (!string.Equals(definition.Verb, verb, StringComparison.Ordinal))
            {
                continue;
            }

            var regex = CreateTemplateRegex(definition.Extra, objectSlotCount);
            var match = regex.Match(normalizedTail);
            if (!match.Success)
            {
                continue;
            }

            var replacementValues = CreateReplacementValues(
                objectValues,
                CollectTemplateCaptures(match, objectSlotCount, definition.Extra));
            predicate = ApplyPlaceholderValues(definition.Text, replacementValues);
            return true;
        }

        predicate = string.Empty;
        return false;
    }

    private static IReadOnlyDictionary<int, string> CreateReplacementValues(
        IReadOnlyList<string> objectValues,
        IReadOnlyDictionary<int, string>? staticValues)
    {
        var values = new Dictionary<int, string>();
        for (var index = 0; index < objectValues.Count; index++)
        {
            values[index] = objectValues[index];
        }

        if (staticValues is not null)
        {
            foreach (var pair in staticValues)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return values;
    }

    private static Dictionary<int, string> CollectTemplateCaptures(
        Match match,
        int objectSlotCount,
        string pattern)
    {
        var captures = new Dictionary<int, string>();
        var placeholderMatches = PlaceholderPattern.Matches(pattern);
        for (var index = 0; index < placeholderMatches.Count; index++)
        {
            var placeholderIndex = ParsePlaceholderIndex(placeholderMatches[index]);
            if (placeholderIndex < objectSlotCount)
            {
                continue;
            }

            captures[placeholderIndex] = match.Groups[GetTemplateGroupName(placeholderIndex)].Value;
        }

        return captures;
    }

    private static Regex CreateTemplateRegex(string pattern, int objectSlotCount)
    {
        var builder = new StringBuilder();
        builder.Append('^');

        var lastIndex = 0;
        var placeholderMatches = PlaceholderPattern.Matches(pattern);
        for (var index = 0; index < placeholderMatches.Count; index++)
        {
            var placeholder = placeholderMatches[index];
            var placeholderIndex = ParsePlaceholderIndex(placeholder);

            builder.Append(Regex.Escape(pattern.Substring(lastIndex, placeholder.Index - lastIndex)));
            if (placeholderIndex < objectSlotCount)
            {
                builder.Append(Regex.Escape(placeholder.Value));
            }
            else
            {
                builder.Append("(?<")
                    .Append(GetTemplateGroupName(placeholderIndex))
                    .Append(">.+?)");
            }

            lastIndex = placeholder.Index + placeholder.Length;
        }

        builder.Append(Regex.Escape(pattern.Substring(lastIndex)));
        builder.Append('$');

        return new Regex(
            builder.ToString(),
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    }

    private static string GetTemplateGroupName(int index)
    {
        return "p" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static int ParsePlaceholderIndex(Capture capture)
    {
        var value = capture.Value;
        var indexText = value.Remove(startIndex: value.Length - 1, count: 1).Remove(startIndex: 0, count: 1);
        return int.Parse(
            indexText,
            CultureInfo.InvariantCulture);
    }

    private static string ApplyPlaceholderValues(string template, IReadOnlyDictionary<int, string> values)
    {
        return PlaceholderPattern.Replace(
            template,
            match =>
            {
                var index = ParsePlaceholderIndex(match);
                return values.TryGetValue(index, out var value)
                    ? value
                    : match.Value;
            });
    }

    private static string BuildSentence(string? subject, string predicate, string? endMark)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(subject))
        {
            var trimmedSubject = subject!.Trim();
            builder.Append(trimmedSubject);
            if (!EndsWithJapaneseParticle(trimmedSubject))
            {
                builder.Append('は');
            }
        }

        builder.Append(predicate);

        if (!EndsWithSentencePunctuation(builder))
        {
            builder.Append(TranslateEndMark(endMark));
        }

        return builder.ToString();
    }

    private static string TranslateEndMark(string? endMark)
    {
        return endMark switch
        {
            "!" => "！",
            "?" => "？",
            "." or null or "" => "。",
            _ => endMark,
        };
    }

    private static bool EndsWithJapaneseParticle(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var last = source[source.Length - 1];
        return last is 'は' or 'が' or 'を' or 'に' or 'へ' or 'と' or 'で' or 'の' or 'も' or 'や' or 'か';
    }

    private static bool EndsWithSentencePunctuation(StringBuilder builder)
    {
        for (var index = builder.Length - 1; index >= 0; index--)
        {
            if (char.IsWhiteSpace(builder[index]))
            {
                continue;
            }

            return builder[index] is '。' or '！' or '？' or '.' or '!' or '?';
        }

        return false;
    }

    private static string BuildSingleObjectTail(string? preposition, string? extra)
    {
        var builder = new StringBuilder();
        AppendWithSpaceIfNeeded(builder, NormalizeFragment(preposition));
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append("{0}");
        AppendWithSpaceIfNeeded(builder, NormalizeFragment(extra));
        return builder.ToString();
    }

    private static string BuildDoubleObjectTail(string? directPreposition, string? indirectPreposition, string? extra)
    {
        var builder = new StringBuilder();
        AppendWithSpaceIfNeeded(builder, NormalizeFragment(directPreposition));
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append("{0}");
        AppendWithSpaceIfNeeded(builder, NormalizeFragment(indirectPreposition));
        builder.Append(' ');
        builder.Append("{1}");
        AppendWithSpaceIfNeeded(builder, NormalizeFragment(extra));
        return builder.ToString();
    }

    private static bool TryBuildObjectPhrase(string? preposition, string objectText, out string phrase)
    {
        if (!TryTranslatePreposition(preposition, out var suffix))
        {
            phrase = string.Empty;
            return false;
        }

        phrase = objectText + suffix;
        return true;
    }

    private static bool TryTranslatePreposition(string? preposition, out string suffix)
    {
        var normalized = NormalizeFragment(preposition);
        if (string.IsNullOrEmpty(normalized))
        {
            suffix = "を";
            return true;
        }

        switch (normalized)
        {
            case "against":
            case "at":
            case "over":
                suffix = "を";
                return true;
            case "to":
            case "toward":
            case "in":
            case "into":
            case "on":
            case "onto":
                suffix = "に";
                return true;
            case "down on":
                suffix = "の上に";
                return true;
            case "with":
                suffix = "で";
                return true;
            case "from":
            case "off":
            case "out of":
                suffix = "から";
                return true;
            case "under":
                suffix = "の下に";
                return true;
        }

        var normalizedText = normalized!;
        if (Translator.TryGetTranslation(normalizedText, out var translated)
            && !string.Equals(translated, normalizedText, StringComparison.Ordinal))
        {
            suffix = translated;
            return true;
        }

        suffix = string.Empty;
        return false;
    }

    private static void AppendWithSpaceIfNeeded(StringBuilder builder, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textValue = text!;
        if (builder.Length > 0
            && !textValue.StartsWith(",", StringComparison.Ordinal)
            && !textValue.StartsWith(";", StringComparison.Ordinal)
            && !textValue.StartsWith(".", StringComparison.Ordinal))
        {
            builder.Append(' ');
        }

        builder.Append(textValue);
    }

    private static string? NormalizeFragment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value!.Trim();
    }

    private static LoadedVerbDictionary GetLoadedDictionary()
    {
        var cached = Volatile.Read(ref loadedDictionary);
        if (cached is not null)
        {
            return cached;
        }

        lock (SyncRoot)
        {
            if (loadedDictionary is null)
            {
                loadedDictionary = LoadDictionary();
            }

            return loadedDictionary;
        }
    }

    private static LoadedVerbDictionary LoadDictionary()
    {
        Interlocked.Increment(ref loadInvocationCount);

        var path = ResolveDictionaryPath();
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"QudJP: message-frame verb dictionary file not found: {path}", path);
        }

        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(VerbDictionaryDocument));
        var document = serializer.ReadObject(stream) as VerbDictionaryDocument;
        if (document is null)
        {
            throw new InvalidDataException($"QudJP: failed to deserialize message-frame verb dictionary: {path}");
        }

        var tier1 = new Dictionary<string, string>(StringComparer.Ordinal);
        var tier2 = new Dictionary<string, string>(StringComparer.Ordinal);
        var tier3 = new List<VerbTemplateDefinition>();

        if (document.Tier1 is not null)
        {
            for (var index = 0; index < document.Tier1.Count; index++)
            {
                var entry = document.Tier1[index];
                if (entry is null || string.IsNullOrWhiteSpace(entry.Verb) || entry.Text is null)
                {
                    throw new InvalidDataException($"QudJP: malformed tier1 verb entry at index {index} in '{path}'.");
                }

                tier1[entry.Verb!] = entry.Text;
            }
        }

        if (document.Tier2 is not null)
        {
            for (var index = 0; index < document.Tier2.Count; index++)
            {
                var entry = document.Tier2[index];
                if (entry is null || string.IsNullOrWhiteSpace(entry.Verb) || entry.Extra is null || entry.Text is null)
                {
                    throw new InvalidDataException($"QudJP: malformed tier2 verb entry at index {index} in '{path}'.");
                }

                tier2[BuildPairKey(entry.Verb!, entry.Extra)] = entry.Text;
            }
        }

        if (document.Tier3 is not null)
        {
            for (var index = 0; index < document.Tier3.Count; index++)
            {
                var entry = document.Tier3[index];
                if (entry is null || string.IsNullOrWhiteSpace(entry.Verb) || entry.Extra is null || entry.Text is null)
                {
                    throw new InvalidDataException($"QudJP: malformed tier3 verb entry at index {index} in '{path}'.");
                }

                tier3.Add(new VerbTemplateDefinition(entry.Verb!, entry.Extra, entry.Text));
            }
        }

        return new LoadedVerbDictionary(tier1, tier2, tier3);
    }

    private static string ResolveDictionaryPath()
    {
        if (!string.IsNullOrWhiteSpace(dictionaryPathOverride))
        {
            return Path.GetFullPath(dictionaryPathOverride);
        }

        return LocalizationAssetResolver.GetLocalizationPath("Dictionaries/verbs.ja.json");
    }

    private static string BuildPairKey(string verb, string extra)
    {
        return verb + '\u001f' + extra;
    }

    [DataContract]
    private sealed class VerbDictionaryDocument
    {
        [DataMember(Name = "tier1")]
        public List<VerbOnlyEntry>? Tier1 { get; set; }

        [DataMember(Name = "tier2")]
        public List<VerbTailEntry>? Tier2 { get; set; }

        [DataMember(Name = "tier3")]
        public List<VerbTailEntry>? Tier3 { get; set; }
    }

    [DataContract]
    private sealed class VerbOnlyEntry
    {
        [DataMember(Name = "verb")]
        public string? Verb { get; set; }

        [DataMember(Name = "text")]
        public string? Text { get; set; }
    }

    [DataContract]
    private sealed class VerbTailEntry
    {
        [DataMember(Name = "verb")]
        public string? Verb { get; set; }

        [DataMember(Name = "extra")]
        public string? Extra { get; set; }

        [DataMember(Name = "text")]
        public string? Text { get; set; }
    }

    private sealed class LoadedVerbDictionary
    {
        public LoadedVerbDictionary(
            Dictionary<string, string> tier1,
            Dictionary<string, string> tier2,
            List<VerbTemplateDefinition> tier3)
        {
            Tier1 = tier1;
            Tier2 = tier2;
            Tier3 = tier3;
        }

        public Dictionary<string, string> Tier1 { get; }

        public Dictionary<string, string> Tier2 { get; }

        public List<VerbTemplateDefinition> Tier3 { get; }
    }

    private sealed class VerbTemplateDefinition
    {
        public VerbTemplateDefinition(string verb, string extra, string text)
        {
            Verb = verb;
            Extra = extra;
            Text = text;
        }

        public string Verb { get; }

        public string Extra { get; }

        public string Text { get; }
    }
}
