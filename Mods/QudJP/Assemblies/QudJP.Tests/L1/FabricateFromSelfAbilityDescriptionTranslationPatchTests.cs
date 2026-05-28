using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class FabricateFromSelfAbilityDescriptionTranslationPatchTests
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
    public void TranslateAbilityDescription_TranslatesFabricatePrefix()
    {
        Translator.RegisterRuntimeTranslationForOwnerRoute("phase cannons", "位相砲");

        var translated = FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests("Fabricate phase cannons");

        Assert.That(translated, Is.EqualTo("位相砲を生成する"));
    }

    [Test]
    public void TranslateAbilityDescription_PreservesUnknownAndStripsMarkerPrefixedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests("Excavate phase cannons"),
                Is.EqualTo("Excavate phase cannons"));
            Assert.That(
                FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests("\u0001Fabricate phase cannons"),
                Is.EqualTo("Fabricate phase cannons"));
        });
    }
}
