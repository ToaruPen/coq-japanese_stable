using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
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
}
