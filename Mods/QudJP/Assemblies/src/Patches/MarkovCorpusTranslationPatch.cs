using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class MarkovCorpusTranslationPatch
{
    internal const string VanillaCorpusName = "LibraryCorpus.json";

    private const string JapaneseCorpusRelativePath = "Corpus/LibraryCorpus.ja.json";
    private const int MaxSeedRetryAttempts = 32;

    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex JapaneseCharacterPattern = new("[\\p{IsHiragana}\\p{IsKatakana}\\p{IsCJKUnifiedIdeographs}]", RegexOptions.Compiled);

    private static string? corpusPathOverride;

    [DataContract]
    private sealed class JapaneseCorpusDocument
    {
        [DataMember(Name = "order")]
        public int Order { get; set; } = 2;

        [DataMember(Name = "sentences")]
        public string[]? Sentences { get; set; }
    }

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = ResolveGameType("XRL.World.Parts.MarkovBook", "MarkovBook");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: MarkovCorpusTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "EnsureCorpusLoaded", new[] { typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: MarkovCorpusTranslationPatch.EnsureCorpusLoaded(string) not found.");
        }

        return method;
    }

    [HarmonyPrefix]
    public static bool Prefix(string Corpus)
    {
        try
        {
            if (!ShouldUseJapaneseCorpus(Corpus))
            {
                return true;
            }

            return !EnsureJapaneseCorpusLoaded(Corpus);
        }
        catch (Exception ex)
        {
            Trace.TraceError("QudJP: MarkovCorpusTranslationPatch.Prefix failed: {0}", ex);
            return true;
        }
    }

    internal static bool ShouldUseJapaneseCorpus(string corpus)
    {
        return string.Equals(corpus, VanillaCorpusName, StringComparison.Ordinal);
    }

    internal static void SetCorpusPathForTests(string? path)
    {
        corpusPathOverride = path;
    }

    internal static void ResetForTests()
    {
        corpusPathOverride = null;
    }

    internal static string ResolveJapaneseCorpusPath()
    {
        return !string.IsNullOrWhiteSpace(corpusPathOverride)
            ? Path.GetFullPath(corpusPathOverride)
            : LocalizationAssetResolver.GetLocalizationPath(JapaneseCorpusRelativePath);
    }

    internal static (int Order, string CorpusText) LoadJapaneseCorpusSource()
    {
        var path = ResolveJapaneseCorpusPath();
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Japanese Markov corpus file not found.", path);
        }

        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(JapaneseCorpusDocument));
        if (serializer.ReadObject(stream) is not JapaneseCorpusDocument document)
        {
            throw new InvalidDataException($"Could not deserialize Japanese Markov corpus at '{path}'.");
        }

        if (document.Sentences is null || document.Sentences.Length == 0)
        {
            throw new InvalidDataException($"Japanese Markov corpus at '{path}' does not contain any sentences.");
        }

        var normalizedSentences = new string[document.Sentences.Length];
        var normalizedCount = 0;
        for (var index = 0; index < document.Sentences.Length; index++)
        {
            var normalized = NormalizeSentence(document.Sentences[index]);
            if (string.IsNullOrEmpty(normalized))
            {
                continue;
            }

            normalizedSentences[normalizedCount++] = normalized;
        }

        if (normalizedCount == 0)
        {
            throw new InvalidDataException($"Japanese Markov corpus at '{path}' contains only empty sentences.");
        }

        var sentences = new string[normalizedCount];
        Array.Copy(normalizedSentences, sentences, normalizedCount);

        var order = document.Order > 0 ? document.Order : 2;
        return (order, string.Join(" ", sentences));
    }

    internal static object BuildChainData(string corpusText, int order)
    {
        var markovChainType = ResolveGameType("XRL.MarkovChain", "MarkovChain");
        if (markovChainType is null)
        {
            throw new InvalidOperationException("QudJP: XRL.MarkovChain type not found.");
        }

        var buildChainMethod = AccessTools.Method(markovChainType, "BuildChain", new[] { typeof(string), typeof(int) });
        if (buildChainMethod is null)
        {
            throw new MissingMethodException(markovChainType.FullName, "BuildChain");
        }

        var data = buildChainMethod.Invoke(null, new object[] { corpusText, order });
        return data ?? throw new InvalidOperationException("QudJP: MarkovChain.BuildChain returned null.");
    }

    internal static string GenerateSentence(object chainData, string? seed = null)
    {
        var markovChainType = ResolveGameType("XRL.MarkovChain", "MarkovChain");
        if (markovChainType is null)
        {
            throw new InvalidOperationException("QudJP: XRL.MarkovChain type not found.");
        }

        var generateSentenceMethod = AccessTools.Method(markovChainType, "GenerateSentence");
        if (generateSentenceMethod is null)
        {
            throw new MissingMethodException(markovChainType.FullName, "GenerateSentence");
        }

        var effectiveSeed = seed;
        if (effectiveSeed is null)
        {
            var discoveredSeed = FindJapaneseSeed(chainData);
            if (discoveredSeed is null)
            {
                Trace.TraceWarning(
                    $"QudJP: MarkovCorpusTranslationPatch.GenerateSentence could not find a Japanese seed for chain data type '{chainData.GetType().FullName}'. Falling back to retry loop.");
            }

            effectiveSeed = discoveredSeed;
        }

        var maxAttempts = string.IsNullOrEmpty(effectiveSeed) ? MaxSeedRetryAttempts : 1;
        string? normalized = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var result = generateSentenceMethod.Invoke(null, new object?[] { chainData, effectiveSeed }) as string;
            normalized = NormalizeSentence(result ?? throw new InvalidOperationException("QudJP: MarkovChain.GenerateSentence returned null."));
            if (!string.IsNullOrEmpty(effectiveSeed) || ContainsJapaneseCharacters(normalized))
            {
                return normalized;
            }
        }

        return normalized ?? string.Empty;
    }

    private static string? FindJapaneseSeed(object chainData)
    {
        var openingWordsField = AccessTools.Field(chainData.GetType(), "OpeningWords");
        if (openingWordsField?.GetValue(chainData) is IList openingWords)
        {
            foreach (var openingWord in openingWords)
            {
                if (openingWord is string candidate && ContainsJapaneseCharacters(candidate))
                {
                    return candidate;
                }
            }
        }

        var chainField = AccessTools.Field(chainData.GetType(), "Chain");
        if (chainField?.GetValue(chainData) is IDictionary chain)
        {
            foreach (DictionaryEntry entry in chain)
            {
                if (entry.Key is string candidate && ContainsJapaneseCharacters(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    internal static int GetOpeningWordCount(object chainData)
    {
        var openingWordsField = AccessTools.Field(chainData.GetType(), "OpeningWords");
        if (openingWordsField?.GetValue(chainData) is not IList openingWords)
        {
            throw new InvalidOperationException("QudJP: MarkovChainData.OpeningWords not accessible.");
        }

        return openingWords.Count;
    }

    internal static int GetTransitionCount(object chainData)
    {
        var markovChainType = ResolveGameType("XRL.MarkovChain", "MarkovChain");
        if (markovChainType is null)
        {
            throw new InvalidOperationException("QudJP: XRL.MarkovChain type not found.");
        }

        var countMethod = AccessTools.Method(markovChainType, "Count");
        if (countMethod is null)
        {
            throw new MissingMethodException(markovChainType.FullName, "Count");
        }

        var result = countMethod.Invoke(null, new[] { chainData });
        return result is int count ? count : throw new InvalidOperationException("QudJP: MarkovChain.Count returned a non-int result.");
    }

    internal static bool ContainsJapaneseCharacters(string text)
    {
        return !string.IsNullOrEmpty(text) && JapaneseCharacterPattern.IsMatch(text);
    }

    internal static IDictionary GetCorpusCacheForTests()
    {
        var markovBookType = ResolveGameType("XRL.World.Parts.MarkovBook", "MarkovBook");
        if (markovBookType is null)
        {
            throw new InvalidOperationException("QudJP: XRL.World.Parts.MarkovBook type not found.");
        }

        return GetCorpusCache(markovBookType);
    }

    internal static bool EnsureJapaneseCorpusLoaded(string corpus)
    {
        var markovBookType = ResolveGameType("XRL.World.Parts.MarkovBook", "MarkovBook");
        if (markovBookType is null)
        {
            throw new InvalidOperationException("QudJP: XRL.World.Parts.MarkovBook type not found.");
        }

        var corpusCache = GetCorpusCache(markovBookType);
        if (corpusCache.Contains(corpus))
        {
            return true;
        }

        var (order, corpusText) = LoadJapaneseCorpusSource();
        var chainData = BuildChainData(corpusText, order);
        corpusCache[corpus] = chainData;

        try
        {
            var postprocessMethod = AccessTools.Method(markovBookType, "PostprocessLoadedCorpus");
            if (postprocessMethod is null)
            {
                throw new MissingMethodException(markovBookType.FullName, "PostprocessLoadedCorpus");
            }

            _ = postprocessMethod.Invoke(null, new[] { chainData });
        }
        catch
        {
            // Rollback cache entry on postprocess failure to allow English fallback
            corpusCache.Remove(corpus);
            throw;
        }

        return true;
    }

    private static IDictionary GetCorpusCache(Type markovBookType)
    {
        var corpusField = AccessTools.Field(markovBookType, "CorpusData");
        if (corpusField?.GetValue(null) is not IDictionary corpusCache)
        {
            throw new InvalidOperationException("QudJP: MarkovBook.CorpusData cache not accessible.");
        }

        return corpusCache;
    }

    private static string NormalizeSentence(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var normalized = WhitespacePattern.Replace(source, " ").Trim();
        if (normalized.EndsWith("。", StringComparison.Ordinal))
        {
            return normalized.Remove(normalized.Length - 1) + ".";
        }

        if (!normalized.EndsWith(".", StringComparison.Ordinal))
        {
            return normalized + ".";
        }

        return normalized;
    }

    private static Type? ResolveGameType(string fullTypeName, string simpleTypeName)
    {
        var assemblyQualifiedType = Type.GetType(fullTypeName + ", Assembly-CSharp", throwOnError: false);
        if (assemblyQualifiedType is not null)
        {
            return assemblyQualifiedType;
        }

        Trace.TraceWarning(
            "QudJP: MarkovCorpusTranslationPatch could not resolve '{0}' from Assembly-CSharp directly. Falling back to loaded-assembly search for '{1}'.",
            fullTypeName,
            simpleTypeName);
        return GameTypeResolver.FindType(fullTypeName, simpleTypeName);
    }
}
