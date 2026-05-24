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

    [Test]
    public void TryTranslateVisibleName_DeactivateFallsBackWhenTargetRemainsAscii()
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(
            "Deactivate odd gizmo",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Deactivate odd gizmo"));
        });
    }

    [TestCase("")]
    [TestCase("Deactivate ")]
    [TestCase("Deactivate {{Y|odd gizmo}}")]
    [TestCase("\u0001Deactivate bronze long sword")]
    public void TryTranslateVisibleName_DeactivateEdgeInputsPassThrough(string input)
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(input, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(input));
        });
    }

    [Test]
    public void TranslatePreservingColors_DeactivatePreservesOuterBoundaryMarkers()
    {
        var result = ActivatedAbilityNameTranslator.TranslatePreservingColors(
            "\u0001Deactivate bronze long sword",
            "ActivatedAbilityNameTranslatorTests",
            "Ability.Name");

        Assert.That(result, Is.EqualTo("\u0001Deactivate bronze long sword"));
    }

    [Test]
    public void TranslatePreservingColors_DeactivatePreservesColorWrappers()
    {
        var result = ActivatedAbilityNameTranslator.TranslatePreservingColors(
            "{{Y|Deactivate bronze long sword}}",
            "ActivatedAbilityNameTranslatorTests",
            "Ability.Name");

        Assert.That(result, Is.EqualTo("{{Y|{{w|青銅の長剣}}を停止}}"));
    }
}
