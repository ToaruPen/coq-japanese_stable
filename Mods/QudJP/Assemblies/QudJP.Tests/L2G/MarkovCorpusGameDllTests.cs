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
    public void BuildChainData_ProducesJapaneseSentenceFromProductionCorpus()
    {
        var (order, corpusText) = MarkovCorpusTranslationPatch.LoadJapaneseCorpusSource();
        var chainData = MarkovCorpusTranslationPatch.BuildChainData(corpusText, order);
        var sentence = MarkovCorpusTranslationPatch.GenerateSentence(chainData);

        Assert.Multiple(() =>
        {
            Assert.That(order, Is.EqualTo(2));
            Assert.That(MarkovCorpusTranslationPatch.GetOpeningWordCount(chainData), Is.GreaterThan(50));
            Assert.That(MarkovCorpusTranslationPatch.GetTransitionCount(chainData), Is.GreaterThan(500));
            Assert.That(sentence, Is.Not.Empty);
            Assert.That(MarkovCorpusTranslationPatch.ContainsJapaneseCharacters(sentence), Is.True);
        });
    }
}
#endif
