using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class CookingIngredientFragmentTranslatorTests
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

    [TestCase("a pinch of salt", "塩ひとつまみ")]
    [TestCase("a dash of algae", "藻少量")]
    [TestCase("a dram of {{C|water}}", "{{C|水}}1ドラム")]
    [TestCase("some bread", "パン少々")]
    [TestCase("a bread", "パン")]
    public void TryTranslate_TranslatesMeasuredAndArticleIngredientFragments(string source, string expected)
    {
        var translated = CookingIngredientFragmentTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = CookingIngredientFragmentTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "a pinch of salt",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("a pinch of salt"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("a pinch of qwern")]
    [TestCase("qwern")]
    public void TryTranslate_LeavesUnsupportedFragmentsUnchanged(string? source)
    {
        var translated = CookingIngredientFragmentTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
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
