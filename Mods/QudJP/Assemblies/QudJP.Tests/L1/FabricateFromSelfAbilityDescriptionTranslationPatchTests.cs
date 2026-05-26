using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class FabricateFromSelfAbilityDescriptionTranslationPatchTests
{
    [Test]
    public void TranslateAbilityDescription_TranslatesFabricatePrefix()
    {
        var translated = FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests("Fabricate phase cannons");

        Assert.That(translated, Is.EqualTo("phase cannonsを生成する"));
    }

    [Test]
    public void TranslateAbilityDescription_PreservesUnknownAndMarkerPrefixedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests("Excavate phase cannons"),
                Is.EqualTo("Excavate phase cannons"));
            Assert.That(
                FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests("\u0001Fabricate phase cannons"),
                Is.EqualTo("\u0001Fabricate phase cannons"));
        });
    }

    [TestCase("")]
    [TestCase("Fabricate")]
    [TestCase("fabricate phase cannons")]
    public void TranslateAbilityDescription_EdgeInputsReturnUnchanged(string source)
    {
        var result = FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests(source);

        Assert.That(result, Is.EqualTo(source));
    }

    [Test]
    public void TranslateAbilityDescription_FabricatePrefixResultContainsTarget()
    {
        var result = FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests("Fabricate lead slugs");

        Assert.That(result, Does.Contain("lead slugs"));
    }

    [Test]
    public void TranslateAbilityDescription_TranslatedResultDoesNotStartWithFabricate()
    {
        var result = FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests("Fabricate phase cannons");

        Assert.That(result, Does.Not.StartWith("Fabricate"));
    }
}
