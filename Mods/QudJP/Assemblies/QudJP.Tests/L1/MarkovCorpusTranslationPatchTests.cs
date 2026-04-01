using System.Text.Json;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class MarkovCorpusTranslationPatchTests
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
    public void JapaneseCorpusFile_HasExpandedSegmentedSourceMaterial()
    {
        var path = MarkovCorpusTranslationPatch.ResolveJapaneseCorpusPath();

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var sentences = document.RootElement
            .GetProperty("sentences")
            .EnumerateArray()
            .Select(static element => element.GetString() ?? string.Empty)
            .ToArray();
        var wordCount = sentences
            .SelectMany(static sentence => sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Count();

        Assert.Multiple(() =>
        {
            Assert.That(sentences.Length, Is.InRange(700, 900), "The expanded corpus should provide broad sentence variety for Markov chaining.");
            Assert.That(wordCount, Is.InRange(20000, 26000), "The expanded corpus should carry substantially more translated source material.");
            Assert.That(sentences.All(static sentence => sentence.IndexOf(' ') >= 0), Is.True, "Each sentence should be morpheme-segmented with spaces.");
            Assert.That(sentences.All(static sentence => sentence.EndsWith(".", StringComparison.Ordinal)), Is.True, "Each sentence should end in '.' so the vanilla Markov chain can detect sentence boundaries.");
            Assert.That(MarkovCorpusTranslationPatch.ContainsJapaneseCharacters(string.Join(" ", sentences)), Is.True);
        });
    }

}
