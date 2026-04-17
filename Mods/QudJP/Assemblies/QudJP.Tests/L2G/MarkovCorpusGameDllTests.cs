#if HAS_GAME_DLL
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
    public void GenerateSentence_AvoidsFalseSentenceStartsWithoutJapanese()
    {
        var chainData = MarkovCorpusTranslationPatch.BuildChainData("出力 ファイル . Love . log .", 2);

        var sentence = MarkovCorpusTranslationPatch.GenerateSentence(chainData).TrimEnd();

        Assert.That(MarkovCorpusTranslationPatch.ContainsJapaneseCharacters(sentence), Is.True, "Generated sentences should retain Japanese content even when the corpus contains internal '.' tokens.");
    }
}
#endif
