using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class ActionEffectDescriptionReturnTranslatorTests
{
    private string localizationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        localizationRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [TestCase("Player", "プレイヤー")]
    [TestCase("disassembling", "分解中")]
    [TestCase("acting", "行動中")]
    [TestCase("Assuming another creature's form.", "別の生物の姿をとっている。")]
    [TestCase(
        "You bear a tail with a stinger that delivers poisonous venom to your enemies.",
        "臀部の毒針を持つ。")]
    [TestCase(
        "You bear a tail with a stinger that delivers paralyzing venom to your enemies.",
        "臀部の麻痺毒針を持つ。")]
    [TestCase(
        "You bear a tail with a stinger that delivers confusing venom to your enemies.",
        "臀部の混乱毒針を持つ。")]
    public void TryTranslate_TranslatesCoveredActionAndEffectDescriptions(string source, string expected)
    {
        var ok = ActionEffectDescriptionReturnTranslator.TryTranslate(source, out var translated, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(detail, Is.Not.Empty);
        });
    }

    [TestCase("{{W|acting}}", "{{W|行動中}}")]
    [TestCase("<color=yellow>disassembling</color>", "<color=yellow>分解中</color>")]
    public void TryTranslate_PreservesWholeSourceColorWrappers(string source, string expected)
    {
        var ok = ActionEffectDescriptionReturnTranslator.TryTranslate(source, out var translated, out _);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("Debug Target")]
    [TestCase("\u0001acting")]
    public void TryTranslate_LeavesUnsupportedTextUnchanged(string source)
    {
        var ok = ActionEffectDescriptionReturnTranslator.TryTranslate(source, out var translated, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(detail, Is.Empty);
        });
    }

    [Test]
    public void TryTranslate_ColorWrappedUnsupportedTextReturnsFalse()
    {
        var ok = ActionEffectDescriptionReturnTranslator.TryTranslate("{{W|Debug Target}}", out var translated, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo("{{W|Debug Target}}"));
            Assert.That(detail, Is.Empty);
        });
    }

    [Test]
    public void TryTranslate_ColorWrappedSuccessReturnsNonEmptyDetail()
    {
        var okBare = ActionEffectDescriptionReturnTranslator.TryTranslate("acting", out _, out var detailBare);
        var okWrapped = ActionEffectDescriptionReturnTranslator.TryTranslate("{{W|acting}}", out _, out var detailWrapped);

        Assert.Multiple(() =>
        {
            Assert.That(okBare, Is.True);
            Assert.That(okWrapped, Is.True);
            Assert.That(detailBare, Is.Not.Empty);
            Assert.That(detailWrapped, Is.Not.Empty);
        });
    }

    [TestCase("   ")]
    [TestCase("\t")]
    public void TryTranslate_WhitespaceOnlyInputIsUnsupported(string source)
    {
        var ok = ActionEffectDescriptionReturnTranslator.TryTranslate(source, out var translated, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(detail, Is.Empty);
        });
    }
}
