using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class CookbookDisplayNameTranslatorTests
{
    [TestCase("&gThe Garden Of Cooking", "&g料理の庭園")]
    [TestCase("&gThe Cooking Of The Garden", "&g庭園の料理")]
    [TestCase("&gCooking: The Salt Roads", "&g料理：塩の道")]
    [TestCase("&gAstral Recipes", "&gアストラルのレシピ")]
    [TestCase("&gCooking Astral Recipes", "&gアストラルの料理レシピ")]
    [TestCase("&gGlowfish: The Garden Of Cooking", "&gグロウフィッシュ：料理の庭園")]
    [TestCase("&gThe Garden Of Glowfish", "&gグロウフィッシュの庭園")]
    [TestCase("&gCooking With Glowfish", "&gグロウフィッシュを使った料理")]
    [TestCase("&gAstral Recipes With Glowfish", "&gグロウフィッシュを使ったアストラルのレシピ")]
    [TestCase("&gCooking Astral Recipes With Glowfish", "&gグロウフィッシュを使ったアストラルの料理レシピ")]
    [TestCase("&gBoiling: Pretender", "&g茹で料理：僭称者")]
    [TestCase("&gPickling: Boiling's Pretender", "&g漬物：茹で料理の僭称者")]
    [TestCase("&gFermenting: Spouse", "&g発酵：伴侶")]
    public void TryTranslate_TranslatesFiniteCookbookFrames(string source, string expected)
    {
        var translated = CookbookDisplayNameTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_ReturnsFalse_ForUnknownFrame()
    {
        var translated = CookbookDisplayNameTranslator.TryTranslate("&gThe Salt Roads", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("&gThe Salt Roads"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    public void TryTranslate_ReturnsFalse_ForEmptyInput(string? source)
    {
        var translated = CookbookDisplayNameTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerAfterColorPrefix()
    {
        var translated = CookbookDisplayNameTranslator.TryTranslate(
            "&g" + MessageFrameTranslator.DirectTranslationMarker + "The Garden Of Cooking",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("&gThe Garden Of Cooking"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerAfterBackgroundColorPrefix()
    {
        var translated = CookbookDisplayNameTranslator.TryTranslate(
            "^g" + MessageFrameTranslator.DirectTranslationMarker + "The Garden Of Cooking",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("^gThe Garden Of Cooking"));
        });
    }
}
