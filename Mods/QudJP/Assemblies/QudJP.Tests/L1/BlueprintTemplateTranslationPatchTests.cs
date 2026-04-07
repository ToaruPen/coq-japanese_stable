using System.Collections;
using System.Reflection;
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
    [TestCase("=subject.The==subject.name= =verb:recognize= your =object.name=.",
        "=subject.name=があなたの=object.name=を認識した。")]
    [TestCase("You touch =subject.t= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.",
        "あなたは=subject.name=に触れ、パスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。")]
    [TestCase("A loud buzz is emitted. The unauthorized glyph flashes on the display.",
        "大きなブザー音が鳴った。認証されていないグリフがディスプレイに点滅した。")]
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
    public void LoadTranslations_PowerSwitchAndForceProjectorTemplatesPreserveAccessPlaceholders()
    {
        var translations = BlueprintTemplateTranslationPatch.LoadTranslations();

        Assert.That(translations, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(
                translations!["=subject.The==subject.name= =verb:recognize= your =object.name=."],
                Does.Contain("=object.name="),
                "KeyObjectAccessMessage translation should preserve the accessed object placeholder.");
            Assert.That(
                translations["You touch =subject.t= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly."],
                Does.Contain("=subject.name="),
                "PsychometryAccessMessage translation should preserve the subject placeholder.");
            Assert.That(
                translations["A loud buzz is emitted. The unauthorized glyph flashes on the display."],
                Does.Not.Contain("=object.name="),
                "AccessFailureMessage should remain a stable leaf without inventing placeholders.");
        });
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

    [Test]
    public void TranslatePartFields_NormalizesPowerSwitchAccessTemplates_BeforeEmitStage()
    {
        var translations = BlueprintTemplateTranslationPatch.LoadTranslations();
        Assert.That(translations, Is.Not.Null);

        SetPrivateStaticField("settersCacheField", typeof(DummyBlueprintPart).GetField("_SettersCache")!);
        SetPrivateStaticField("originalValueField", typeof(DummyPartSetter).GetField(nameof(DummyPartSetter.OriginalValue))!);
        SetPrivateStaticField("parsedValueField", typeof(DummyPartSetter).GetField(nameof(DummyPartSetter.ParsedValue))!);

        var part = new DummyBlueprintPart();
        part._SettersCache["KeyObjectAccessMessage"] = new DummyPartSetter
        {
            OriginalValue = "=subject.The==subject.name= =verb:recognize= your =object.name=.",
            ParsedValue = "=subject.The==subject.name= =verb:recognize= your =object.name=.",
        };
        part._SettersCache["PsychometryAccessMessage"] = new DummyPartSetter
        {
            OriginalValue = "You touch =subject.t= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.",
            ParsedValue = "You touch =subject.t= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.",
        };
        part._SettersCache["AccessFailureMessage"] = new DummyPartSetter
        {
            OriginalValue = "A loud buzz is emitted. The unauthorized glyph flashes on the display.",
            ParsedValue = "A loud buzz is emitted. The unauthorized glyph flashes on the display.",
        };

        var translatedCount = (int)typeof(BlueprintTemplateTranslationPatch)
            .GetMethod("TranslatePartFields", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { part, BlueprintTemplateTranslationPatch.GetTranslatablePartFields()["PowerSwitch"], translations! })!;

        Assert.Multiple(() =>
        {
            Assert.That(translatedCount, Is.EqualTo(3));
            Assert.That(
                GetDummyPartSetter(part, "KeyObjectAccessMessage").OriginalValue,
                Is.EqualTo("=subject.name=があなたの=object.name=を認識した。"));
            Assert.That(
                GetDummyPartSetter(part, "PsychometryAccessMessage").OriginalValue,
                Is.EqualTo("あなたは=subject.name=に触れ、パスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。"));
            Assert.That(
                GetDummyPartSetter(part, "AccessFailureMessage").OriginalValue,
                Is.EqualTo("大きなブザー音が鳴った。認証されていないグリフがディスプレイに点滅した。"));
        });
    }

    private static void SetPrivateStaticField(string fieldName, object value)
    {
        typeof(BlueprintTemplateTranslationPatch)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, value);
    }

    private static DummyPartSetter GetDummyPartSetter(DummyBlueprintPart part, string fieldName)
    {
        var setter = part._SettersCache[fieldName] as DummyPartSetter;
        Assert.That(setter, Is.Not.Null, $"Missing dummy setter for {fieldName}");
        return setter!;
    }

    private sealed class DummyBlueprintPart
    {
        // Match the reflected cache shape expected by BlueprintTemplateTranslationPatch.
        public IDictionary _SettersCache = new Hashtable();
    }

    private sealed class DummyPartSetter
    {
        public string OriginalValue = string.Empty;
        public string ParsedValue = string.Empty;
    }
}
