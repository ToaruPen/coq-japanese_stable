using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class CookingRecipeDisplayNameTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase("{{W|Fried Wafers}}", "{{W|揚げ・ウェハー}}")]
    [TestCase("{{W|Honeyed Salt Stew}}", "{{W|ハチミツ風味の・塩・シチュー}}")]
    [TestCase("{{W|Salt Bread}}", "{{W|塩・パン}}")]
    public void TryProcessDisplayName_TranslatesGeneratedDishName(string source, string expected)
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            source,
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(actualTranslation, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryProcessDisplayName_TranslatesChefPossessiveDishPart()
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            "{{W|Argyve's Fried Wafers}}",
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(actualTranslation, Is.True);
            Assert.That(translated, Is.EqualTo("{{W|Argyveの揚げ・ウェハー}}"));
        });
    }

    [Test]
    public void TryProcessDisplayName_StripsDirectMarkerWithoutRetranslating()
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            "\x01{{W|Fried Wafers}}",
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(actualTranslation, Is.False);
            Assert.That(translated, Is.EqualTo("{{W|Fried Wafers}}"));
        });
    }

    [TestCase("")]
    [TestCase("Fried Wafers")]
    [TestCase("{{W|Qwern Wafers}}")]
    [TestCase("{{R|Fried Wafers}}")]
    public void TryProcessDisplayName_LeavesUnsupportedDisplayNamesUnchanged(string source)
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            source,
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(actualTranslation, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }
}
