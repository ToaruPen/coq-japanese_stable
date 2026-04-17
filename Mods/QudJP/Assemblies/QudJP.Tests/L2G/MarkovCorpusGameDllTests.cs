#if HAS_GAME_DLL
using System.Collections;
using System.Reflection;
using QudJP.Patches;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
[NonParallelizable]
public sealed class MarkovCorpusGameDllTests
{
    private string localizationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        localizationRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        MarkovCorpusTranslationPatch.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        MarkovCorpusTranslationPatch.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [Test]
    public void BuildChainData_ProducesExpectedProductionCorpusMetrics()
    {
        var (order, corpusText) = MarkovCorpusTranslationPatch.LoadJapaneseCorpusSource();
        var chainData = MarkovCorpusTranslationPatch.BuildChainData(corpusText, order);

        Assert.Multiple(() =>
        {
            Assert.That(order, Is.EqualTo(2));
            Assert.That(MarkovCorpusTranslationPatch.GetOpeningWordCount(chainData), Is.GreaterThan(5000));
            Assert.That(MarkovCorpusTranslationPatch.GetTransitionCount(chainData), Is.GreaterThan(100000));
        });
    }

    [Test]
    public void GenerateSentence_ProducesValidJapaneseOutput()
    {
        var (order, corpusText) = MarkovCorpusTranslationPatch.LoadJapaneseCorpusSource();
        var chainData = MarkovCorpusTranslationPatch.BuildChainData(corpusText, order);

        const int sampleSize = 200;
        var sentences = Enumerable.Range(0, sampleSize)
            .Select(_ => MarkovCorpusTranslationPatch.GenerateSentence(chainData).TrimEnd())
            .ToArray();
        var uniqueCount = sentences.Distinct().Count();
        TestContext.WriteLine($"Generated {uniqueCount} unique sentences out of {sampleSize} samples.");

        Assert.Multiple(() =>
        {
            Assert.That(sentences.All(s => !string.IsNullOrEmpty(s)), Is.True, "All generated sentences should be non-empty.");
            Assert.That(sentences.All(s => MarkovCorpusTranslationPatch.ContainsJapaneseCharacters(s)), Is.True, "All generated sentences should contain Japanese characters.");
            Assert.That(sentences.All(s => s.EndsWith(".", StringComparison.Ordinal)), Is.True, "All generated sentences should end with '.'.");
            Assert.That(sentences.Any(s => s.Contains('。')), Is.False, "No generated sentence should contain '。'.");
            Assert.That(sentences.Any(s => s.Contains("  ", StringComparison.Ordinal)), Is.False, "No generated sentence should contain double spaces.");
        });
    }

    [Test]
    public void GenerateSentence_NormalizesEmbeddedPeriodTerminator()
    {
        var chainData = MarkovCorpusTranslationPatch.BuildChainData("始まり あ.い", 1);

        var sentence = MarkovCorpusTranslationPatch.GenerateSentence(chainData, "始まり").TrimEnd();

        Assert.Multiple(() =>
        {
            Assert.That(sentence, Does.Contain("あ.い"), "Generated sentences should preserve embedded-period Japanese tokens.");
            Assert.That(MarkovCorpusTranslationPatch.ContainsJapaneseCharacters(sentence), Is.True, "Generated sentences should retain Japanese characters.");
            Assert.That(sentence, Does.EndWith("."), "Generated sentences should be normalized to end with '.'.");
        });
    }

    [Test]
    public void GenerateSentence_RetainsJapaneseContentDespiteInternalPeriodTokens()
    {
        var chainData = MarkovCorpusTranslationPatch.BuildChainData("出力 ファイル . Love . log .", 2);

        var sentence = MarkovCorpusTranslationPatch.GenerateSentence(chainData).TrimEnd();

        Assert.That(MarkovCorpusTranslationPatch.ContainsJapaneseCharacters(sentence), Is.True, "Generated sentences should retain Japanese content even when the corpus contains internal '.' tokens.");
    }

    [Test]
    public void FindJapaneseSeed_PrefersJapaneseOpeningWord()
    {
        var chainData = MarkovCorpusTranslationPatch.BuildChainData("alpha beta gamma", 1);
        SetOpeningWords(chainData, "alpha", "始まり", "beta");
        SetChainEntries(chainData, ("連鎖", GetReusableChainValue(chainData)));

        var seed = InvokeFindJapaneseSeed(chainData);

        Assert.That(seed, Is.EqualTo("始まり"));
    }

    [Test]
    public void FindJapaneseSeed_FallsBackToJapaneseChainKey()
    {
        var chainData = MarkovCorpusTranslationPatch.BuildChainData("alpha beta gamma", 1);
        SetOpeningWords(chainData, "alpha", "beta");
        SetChainEntries(chainData, ("連鎖", GetReusableChainValue(chainData)));

        var seed = InvokeFindJapaneseSeed(chainData);

        Assert.That(seed, Is.EqualTo("連鎖"));
    }

    [Test]
    public void GenerateSentence_LogsWarningWhenJapaneseSeedCannotBeFound()
    {
        // BuildChainData("alpha beta gamma", 1) plus the helper overrides below creates an English-only
        // Markov chain: SetOpeningWords keeps OpeningWords non-Japanese, SetChainEntries keeps the Chain keys
        // non-Japanese, and InvokeFindJapaneseSeed therefore returns null. In that state GenerateSentence is
        // expected to emit the "could not find a Japanese seed" warning and then fail inside the upstream
        // game Markov generator, so this test intentionally asserts both the warning log and the exception.
        var chainData = MarkovCorpusTranslationPatch.BuildChainData("alpha beta gamma", 1);
        SetOpeningWords(chainData, "alpha", "beta");
        SetChainEntries(chainData, ("alpha", GetReusableChainValue(chainData)));

        Assert.That(InvokeFindJapaneseSeed(chainData), Is.Null);

        var trace = TestTraceHelper.CaptureTrace(() =>
            Assert.That(() => MarkovCorpusTranslationPatch.GenerateSentence(chainData), Throws.Exception));

        Assert.That(trace, Does.Contain("could not find a Japanese seed"));
    }

    private static string? InvokeFindJapaneseSeed(object chainData)
    {
        var method = typeof(MarkovCorpusTranslationPatch).GetMethod("FindJapaneseSeed", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FindJapaneseSeed method not found.");

        return method.Invoke(null, new[] { chainData }) as string;
    }

    private static void SetOpeningWords(object chainData, params string[] values)
    {
        var openingWords = GetFieldValue<IList>(chainData, "OpeningWords");
        openingWords.Clear();
        foreach (var value in values)
        {
            openingWords.Add(value);
        }
    }

    private static object GetReusableChainValue(object chainData)
    {
        var chain = GetFieldValue<IDictionary>(chainData, "Chain");
        foreach (DictionaryEntry entry in chain)
        {
            if (entry.Value is not null)
            {
                return entry.Value;
            }
        }

        throw new InvalidOperationException("No existing Markov chain entry is available for test reuse.");
    }

    private static void SetChainEntries(object chainData, params (string Key, object Value)[] entries)
    {
        var chain = GetFieldValue<IDictionary>(chainData, "Chain");
        chain.Clear();
        foreach (var (key, value) in entries)
        {
            chain.Add(key, value);
        }
    }

    private static T GetFieldValue<T>(object target, string fieldName) where T : class
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on '{target.GetType().FullName}'.");

        return field.GetValue(target) as T
            ?? throw new InvalidOperationException($"Field '{fieldName}' on '{target.GetType().FullName}' is not a {typeof(T).Name}.");
    }
}
#endif
