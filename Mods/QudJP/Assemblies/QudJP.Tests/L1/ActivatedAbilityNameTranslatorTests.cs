using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class ActivatedAbilityNameTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
    }

    [Test]
    public void TryTranslateVisibleName_TranslatesDeactivateTargetWithColorWrapper()
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(
            "Deactivate bronze long sword",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{w|青銅の長剣}}を停止"));
        });
    }
}
