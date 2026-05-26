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

    [Test]
    public void TranslateAbilityDescription_ReturnsEmptyStringUnchanged()
    {
        var result = FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests(string.Empty);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void TranslateAbilityDescription_TranslatesMultiWordObjectName()
    {
        var result = FabricateFromSelfAbilityDescriptionTranslationPatch.TranslateAbilityDescriptionForTests(
            "Fabricate lead slugs and ball bearings");

        Assert.That(result, Is.EqualTo("lead slugs and ball bearingsを生成する"));
    }
}
