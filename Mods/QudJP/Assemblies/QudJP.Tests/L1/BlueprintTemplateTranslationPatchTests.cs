using System.Text.Json;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class BlueprintTemplateTranslationPatchTests
{
    private string localizationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        localizationRoot = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        BlueprintTemplateTranslationPatch.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        BlueprintTemplateTranslationPatch.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [Test]
    public void LoadTranslations_LoadsDictionaryWithExpectedEntryCount()
    {
        var translations = BlueprintTemplateTranslationPatch.LoadTranslations();

        Assert.That(translations, Is.Not.Null);
        Assert.That(translations!.Count, Is.GreaterThanOrEqualTo(30),
            "Dictionary should contain at least 30 template entries (visible defaults + XML blueprint values).");
    }

    [Test]
    public void LoadTranslations_AllKeysAreNonEmptyAndDistinct()
    {
        var translations = BlueprintTemplateTranslationPatch.LoadTranslations();

        Assert.That(translations, Is.Not.Null);
        Assert.Multiple(() =>
        {
            foreach (var kvp in translations!)
            {
                Assert.That(kvp.Key, Is.Not.Empty, "Dictionary key must not be empty.");
                Assert.That(kvp.Value, Is.Not.Empty, $"Translation for key '{kvp.Key}' must not be empty.");
                Assert.That(kvp.Key, Is.Not.EqualTo(kvp.Value),
                    $"Translation must differ from key: '{kvp.Key}'");
            }
        });
    }

    [Test]
    public void LoadTranslations_TranslatedTemplatesPreserveVariableReplaceSlots()
    {
        var translations = BlueprintTemplateTranslationPatch.LoadTranslations();
        Assert.That(translations, Is.Not.Null);

        var slotsToCheck = new[] { "=subject.name=", "=object.name=" };

        Assert.Multiple(() =>
        {
            foreach (var kvp in translations!)
            {
                foreach (var slot in slotsToCheck)
                {
                    if (kvp.Key.Contains(slot, StringComparison.Ordinal))
                    {
                        Assert.That(kvp.Value, Does.Contain(slot),
                            $"Japanese template must preserve slot '{slot}'. Key: '{kvp.Key}'");
                    }
                }
            }
        });
    }

    [TestCase("Nothing happens.", "何も起こらなかった。")]
    [TestCase("You hear inaudible mumbling.", "聞き取れないつぶやきが聞こえた。")]
    [TestCase("=subject.The==subject.name= =verb:start= up with a hum.",
        "=subject.name=がうなり声を上げて起動した。")]
    public void LoadTranslations_ContainsExpectedMapping(string englishKey, string expectedJapanese)
    {
        var translations = BlueprintTemplateTranslationPatch.LoadTranslations();

        Assert.That(translations, Is.Not.Null);
        Assert.That(translations!.ContainsKey(englishKey), Is.True,
            $"Dictionary should contain key: '{englishKey}'");
        Assert.That(translations[englishKey], Is.EqualTo(expectedJapanese));
    }

    [Test]
    public void TranslatablePartFields_CoversAllKnownParts()
    {
        var fields = BlueprintTemplateTranslationPatch.GetTranslatablePartFields();

        var expectedParts = new[]
        {
            "PowerSwitch", "ForceProjector", "Consumer", "DesalinationPellet",
            "Explores", "LootOnStep", "Preacher",
            "BlowAwayGas", "CancelRangedAttacks", "Interactable", "LifeSaver",
            "NephalProperties", "RandomLongRangeTeleportOnDamage",
            "Reconstitution", "SpawnVessel", "Spawner", "SplitOnDeath",
            "SwapOnUse", "TimeCubeProtection",
        };

        Assert.Multiple(() =>
        {
            foreach (var part in expectedParts)
            {
                Assert.That(fields.ContainsKey(part), Is.True,
                    $"TranslatablePartFields should contain part '{part}'.");
            }
        });
    }

    [Test]
    public void TranslatablePartFields_PowerSwitchHasSevenFields()
    {
        var fields = BlueprintTemplateTranslationPatch.GetTranslatablePartFields();

        Assert.That(fields.ContainsKey("PowerSwitch"), Is.True);
        Assert.That(fields["PowerSwitch"].Length, Is.EqualTo(7),
            "PowerSwitch should have 7 translatable fields.");
    }

    [Test]
    public void DictionaryJson_IsValidJsonAndMatchesSchema()
    {
        var path = Path.Combine(localizationRoot, "BlueprintTemplates", "templates.ja.json");
        Assert.That(File.Exists(path), Is.True, $"Dictionary file should exist at {path}");

        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.That(root.TryGetProperty("entries", out var entries), Is.True,
            "Root should have 'entries' property.");
        Assert.That(entries.ValueKind, Is.EqualTo(JsonValueKind.Array));

        Assert.Multiple(() =>
        {
            foreach (var entry in entries.EnumerateArray())
            {
                Assert.That(entry.TryGetProperty("key", out _), Is.True,
                    "Each entry must have a 'key' property.");
                Assert.That(entry.TryGetProperty("text", out _), Is.True,
                    "Each entry must have a 'text' property.");
            }
        });
    }

    [Test]
    public void LoadTranslations_ReturnsNullWhenDictionaryFileMissing()
    {
        BlueprintTemplateTranslationPatch.SetDictionaryPathForTests(
            Path.Combine(localizationRoot, "BlueprintTemplates", "nonexistent.json"));

        var translations = BlueprintTemplateTranslationPatch.LoadTranslations();

        Assert.That(translations, Is.Null);
    }

    [Test]
    public void LoadTranslations_ReturnsEmptyDictionaryForEmptyEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "qudJpTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var tempFile = Path.Combine(tempDir, "empty.json");
            File.WriteAllText(tempFile, """{"entries":[]}""");
            BlueprintTemplateTranslationPatch.SetDictionaryPathForTests(tempFile);

            var translations = BlueprintTemplateTranslationPatch.LoadTranslations();

            Assert.That(translations, Is.Not.Null);
            Assert.That(translations!.Count, Is.EqualTo(0));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void LoadTranslations_ReturnsNullForMalformedJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "qudJpTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var tempFile = Path.Combine(tempDir, "bad.json");
            File.WriteAllText(tempFile, "{ not valid json }}}");
            BlueprintTemplateTranslationPatch.SetDictionaryPathForTests(tempFile);

            var translations = BlueprintTemplateTranslationPatch.LoadTranslations();

            Assert.That(translations, Is.Null);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
