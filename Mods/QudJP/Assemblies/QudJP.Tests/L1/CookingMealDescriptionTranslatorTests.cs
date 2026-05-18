using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class CookingMealDescriptionTranslatorTests
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

    [TestCase(
        "You toss snapjaw haunch, {{Y|starapple jam}}, and a dram of slime into a pot and stir.",
        "スナップジョーの腰肉、{{Y|スターアップルジャム}}と粘液1ドラムを鍋に放り込み、かき混ぜた。")]
    [TestCase(
        "You gather whatever you can find for your meal: a pinch of dust, some yuckwheat, and {{G|witchwood bark}}.\n\nYou toss them in a pot and stir.",
        "食事に使えそうなものをかき集めた: 砂塵ひとつまみ、ヤックウィート少々と{{G|ウィッチウッドの樹皮}}\n\nそれらを鍋に放り込み、かき混ぜた。")]
    [TestCase(
        "Rummaging over your surroundings, you find these ingredients: some salt, a dash of algae, and a dram of oil.\n\nYou toss them in a pot and stir.",
        "周囲を探り、次の材料を見つけた: 塩少々、藻少量と油1ドラム\n\nそれらを鍋に放り込み、かき混ぜた。")]
    [TestCase(
        "You gather some fixings: {{R|lava}}, carbide dust, and fermented starch.\n\nYou toss them in a pot and stir.",
        "いくつかの具材を集めた: {{R|溶岩}}、炭化物の粉塵と発酵デンプン\n\nそれらを鍋に放り込み、かき混ぜた。")]
    public void TryTranslate_TranslatesCookTemplateFrames(string source, string expected)
    {
        var ok = CookingMealDescriptionTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_RestoresWholeSourceColorBoundary()
    {
        var ok = CookingMealDescriptionTranslator.TryTranslate(
            "{{W|You toss snapjaw haunch into a pot and stir.}}",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{W|スナップジョーの腰肉を鍋に放り込み、かき混ぜた。}}"));
        });
    }

    [TestCase("")]
    [TestCase("A savory meal made from {{Y|snapjaw haunch}}.")]
    [TestCase("You eat the meal. It's tastier than usual.")]
    public void TryTranslate_LeavesNonCookTemplateTextUnchanged(string source)
    {
        var ok = CookingMealDescriptionTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");
}
