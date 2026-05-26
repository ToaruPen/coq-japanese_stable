using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class GameObjectActivatedAbilityDescriptionTranslationPatchTests
{
    [Test]
    public void TranslateActivatedAbilityDescription_TranslatesGeneratedDetailLabels()
    {
        var ability = new DummyActivatedAbility
        {
            Description = "Cooldown: 40\nRange: 8",
        };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability);

        Assert.That(ability.Description, Is.EqualTo("クールダウン: 40\n射程: 8"));
    }

    [Test]
    public void TranslateActivatedAbilityDescription_PreservesUnknownAndMarkerPrefixedValues()
    {
        var unknown = new DummyActivatedAbility { Description = "Special generated detail." };
        var marker = new DummyActivatedAbility { Description = "\u0001Cooldown: 40" };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(unknown);
        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(marker);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Description, Is.EqualTo("Special generated detail."));
            Assert.That(marker.Description, Is.EqualTo("\u0001Cooldown: 40"));
        });
    }

    [Test]
    public void TranslateActivatedAbilityDescription_PreservesNullDescription()
    {
        var ability = new DummyActivatedAbility { Description = null };

        Assert.DoesNotThrow(() =>
            GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability));

        Assert.That(ability.Description, Is.Null);
    }

    [Test]
    public void TranslateActivatedAbilityDescription_TranslatesOnlyCooldownLabel()
    {
        var ability = new DummyActivatedAbility { Description = "Cooldown: 20" };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability);

        Assert.That(ability.Description, Is.EqualTo("クールダウン: 20"));
    }

    [Test]
    public void TranslateActivatedAbilityDescription_TranslatesOnlyRangeLabel()
    {
        var ability = new DummyActivatedAbility { Description = "Range: 5" };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability);

        Assert.That(ability.Description, Is.EqualTo("射程: 5"));
    }

    [Test]
    public void TranslateActivatedAbilityDescription_PreservesEmptyDescription()
    {
        var ability = new DummyActivatedAbility { Description = string.Empty };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability);

        Assert.That(ability.Description, Is.EqualTo(string.Empty));
    }

    private sealed class DummyActivatedAbility
    {
        public string? Description { get; set; }
    }
}
