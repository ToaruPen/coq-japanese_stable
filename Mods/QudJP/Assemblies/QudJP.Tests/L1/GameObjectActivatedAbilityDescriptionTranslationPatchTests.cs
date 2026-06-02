using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class GameObjectActivatedAbilityDescriptionTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-gameobject-ability-description-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "empty.ja.json"), "{\"entries\":[]}");
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateActivatedAbilityDescription_TranslatesGeneratedDetailLabels()
    {
        var ability = new DummyActivatedAbility
        {
            Description = "Cooldown: 40\nRange: 8\nCooldown reduced by 3 due to high Willpower.\nDuration increased by 30% due to Two-hearted mutation.",
        };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability);

        Assert.That(
            ability.Description,
            Is.EqualTo("クールダウン: 40\n射程: 8\nクールダウンが3短縮（高い意志力による）。\nTwo-hearted変異により持続時間が30%増加。"));
    }

    [Test]
    public void TranslateActivatedAbilityDescription_TranslatesAbilityManagerRuntimeDetailFragments()
    {
        var ability = new DummyActivatedAbility
        {
            Description =
                "近くのクリーチャーを辱め、DV、命中、自我、意志力に-4、クイックネスに-10%のペナルティを与える。\n\nDuration: 6d6 round\nRange: 8\nCooldown: {{G|43}} round\n\nCooldown reduced by 7 due to high Willpower.",
        };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability);

        Assert.That(
            ability.Description,
            Is.EqualTo("近くのクリーチャーを辱め、DV、命中、自我、意志力に-4、クイックネスに-10%のペナルティを与える。\n\n持続時間: 6d6 ラウンド\n射程: 8\nクールダウン: {{G|43}} ラウンド\n\nクールダウンが7短縮（高い意志力による）。"));
    }

    [Test]
    public void TranslateActivatedAbilityDescription_TranslatesRuntimeElementalRayIntro()
    {
        var ability = new DummyActivatedAbility
        {
            Description =
                "You emit a ray of frost from your forefeet.\n\n指定方向へ9マスの冷気の光線を放つ。\nDamage: 10d3+2\nCooldown: {{G|17}} round\n\nCooldown reduced by 3 due to high Willpower.",
        };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability);

        Assert.Multiple(() =>
        {
            Assert.That(ability.Description, Does.Contain("足先から冷気の光線を放つ。"));
            Assert.That(ability.Description, Does.Contain("ダメージ: 10d3+2"));
            Assert.That(ability.Description, Does.Not.Contain("You emit a ray"));
        });
    }

    [Test]
    public void TranslateActivatedAbilityDescription_RecordsSingleObservabilityHit()
    {
        var ability = new DummyActivatedAbility
        {
            Description = "Duration: 6d6 round\nRange: 8\nCooldown: {{G|43}} round",
        };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(ability);

        Assert.That(
            DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(GameObjectActivatedAbilityDescriptionTranslationPatch),
                nameof(GameObjectActivatedAbilityDescriptionTranslationPatch) + ".Description"),
            Is.EqualTo(1));
    }

    [Test]
    public void TranslateActivatedAbilityDescription_PreservesUnknownAndStripsMarkerPrefixedValues()
    {
        var unknown = new DummyActivatedAbility { Description = "Special generated detail." };
        var marker = new DummyActivatedAbility { Description = "\u0001Cooldown: 40" };

        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(unknown);
        GameObjectActivatedAbilityDescriptionTranslationPatch.TranslateActivatedAbilityDescriptionForTests(marker);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Description, Is.EqualTo("Special generated detail."));
            Assert.That(marker.Description, Is.EqualTo("Cooldown: 40"));
        });
    }

    private sealed class DummyActivatedAbility
    {
        public string? Description { get; set; }
    }
}
