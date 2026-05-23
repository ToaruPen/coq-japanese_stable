using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class StatusScreenBindingOwnerPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-status-binding-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyCharacterStatusScreenBindingTarget.CP = 0;
        DummyCharacterStatusScreenBindingTarget.stats = new List<DummyStatusStatistic>
        {
            new DummyStatusStatistic
            {
                Name = "Strength",
                ShortDisplayName = "STR",
                Value = 18,
                BaseValue = 18,
                Modifier = 2,
            },
        };
        DummyCharacterStatusScreenBindingTarget.mutations = new List<DummyCharacterMutationRecord>();
        DummyCharacterStatusScreenBindingTarget.effects = new List<DummyStatusEffect>();
        DummyCharacterStatusScreenBindingTarget.PrimaryAttributes = new[] { "Strength" };
        DummyCharacterStatusScreenBindingTarget.SecondaryAttributes = Array.Empty<string>();
        DummyCharacterStatusScreenBindingTarget.SecondaryAttributesWithCP = new[] { "CP" };
        DummyCharacterStatusScreenBindingTarget.ResistanceAttributes = Array.Empty<string>();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase("Strength", "STR")]
    [TestCase("Agility", "AGI")]
    [TestCase("Toughness", "TOU")]
    [TestCase("Intelligence", "INT")]
    [TestCase("Willpower", "WIL")]
    [TestCase("Ego", "EGO")]
    [TestCase("MoveSpeed", "MS")]
    [TestCase("Armor", "AV")]
    [TestCase("Dodge", "DV")]
    [TestCase("MentalArmor", "MA")]
    public void CharacterAttributeLineTranslationPatch_KeepsAbbreviationInEnglish_WhenPatched(
        string statName,
        string shortDisplayName)
    {
        // Stat abbreviations (STR, AGI, MS, AV, DV, MA, etc.) are kept in English
        // to avoid layout shifts (see commit 63cc3ad).
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterAttributeLineTarget), nameof(DummyCharacterAttributeLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CharacterAttributeLineTranslationPatch), nameof(CharacterAttributeLineTranslationPatch.Prefix))));

            var target = new DummyCharacterAttributeLineTarget();
            target.setData(new DummyCharacterAttributeLineDataTarget
            {
                stat = statName,
                go = new DummyStatusGameObject(),
                data = new DummyStatusStatistic
                {
                    Name = statName,
                    ShortDisplayName = shortDisplayName,
                    Value = 18,
                    BaseValue = 18,
                    Modifier = 2,
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.attributeText.Text, Is.EqualTo(shortDisplayName),
                    "Stat abbreviations must remain in English to avoid layout shifts");
                Assert.That(target.valueText.Text, Is.Not.Null.And.Not.Empty,
                    "Value text should be populated by the prefix");
                Assert.That(target.modifierText.Text, Is.Not.Null.And.Not.Empty,
                    "Modifier text should be populated by the prefix");
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("Force Wall", "力の壁")]
    [TestCase("Triple Horn", "三本角")]
    [TestCase("Horns", "角")]
    [TestCase("Horn", "単角")]
    [TestCase("Antlers", "枝角")]
    public void CharacterMutationLineTranslationPatch_TranslatesMutationLine_WhenPatched(
        string sourceName,
        string translatedName)
    {
        WriteDictionary((sourceName, translatedName));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterMutationLineTarget), nameof(DummyCharacterMutationLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CharacterMutationLineTranslationPatch), nameof(CharacterMutationLineTranslationPatch.Prefix))));

            var target = new DummyCharacterMutationLineTarget();
            target.setData(new DummyCharacterMutationLineDataTarget
            {
                    mutation = new DummyCharacterMutationRecord
                    {
                    DisplayName = sourceName,
                    Level = 1,
                    BaseLevel = 1,
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("{{y|" + translatedName + " ({{C|1}})}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterMutationLineTranslationPatch),
                        "CharacterStatus.MutationLine"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(CharacterMutationLineTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        sourceName + " (1)",
                        sourceName + " (1)"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CharacterEffectLineTranslationPatch_TranslatesEffectName_WhenPatched()
    {
        WriteDictionary(
            ("Beguiled", "魅了"),
            ("astrally burdened", "星界的に重く縛られた"),
            ("bloody wet", "血に濡れている"));

        RunWithCharacterEffectLinePatch(() =>
        {
            var target = new DummyCharacterEffectLineTarget();
            target.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "Beguiled",
                },
            });
            var uncoloredTarget = new DummyCharacterEffectLineTarget();
            uncoloredTarget.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "astrally burdened",
                },
            });
            var compoundTarget = new DummyCharacterEffectLineTarget();
            compoundTarget.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "bloody wet",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("魅了"));
                Assert.That(uncoloredTarget.text.Text, Is.EqualTo("星界的に重く縛られた"));
                Assert.That(compoundTarget.text.Text, Is.EqualTo("血に濡れている"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterEffectLineTranslationPatch),
                        "CharacterStatus.EffectName"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(CharacterEffectLineTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Beguiled",
                        "Beguiled"),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void CharacterEffectLineTranslationPatch_TranslatesObservedMetabolizingNames_WhenPatched()
    {
        WriteDictionary(("metabolizing", "代謝中"));

        RunWithCharacterEffectLinePatch(() =>
        {
            var plainTarget = new DummyCharacterEffectLineTarget();
            plainTarget.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "metabolizing",
                },
            });

            var coloredTarget = new DummyCharacterEffectLineTarget();
            coloredTarget.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "{{W|metabolizing}}",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(plainTarget.text.Text, Is.EqualTo("代謝中"));
                Assert.That(coloredTarget.text.Text, Is.EqualTo("{{W|代謝中}}"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("metabolizing"), Is.EqualTo(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterEffectLineTranslationPatch),
                        "CharacterStatus.EffectName"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void CharacterEffectLineTranslationPatch_TranslatesObservedLongbladeStanceNames_WhenPatched()
    {
        WriteDictionary(
            ("defensive stance", "防御姿勢"),
            ("aggressive stance", "攻撃姿勢"),
            ("dueling stance", "決闘姿勢"));

        RunWithCharacterEffectLinePatch(() =>
        {
            var defensiveTarget = new DummyCharacterEffectLineTarget();
            defensiveTarget.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "{{G|defensive stance}}",
                },
            });

            var aggressiveTarget = new DummyCharacterEffectLineTarget();
            aggressiveTarget.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "{{R|aggressive stance}}",
                },
            });

            var duelingTarget = new DummyCharacterEffectLineTarget();
            duelingTarget.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "{{W|dueling stance}}",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(defensiveTarget.text.Text, Is.EqualTo("{{G|防御姿勢}}"));
                Assert.That(aggressiveTarget.text.Text, Is.EqualTo("{{R|攻撃姿勢}}"));
                Assert.That(duelingTarget.text.Text, Is.EqualTo("{{W|決闘姿勢}}"));
            });
        });
    }

    private static void RunWithCharacterEffectLinePatch(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterEffectLineTarget), nameof(DummyCharacterEffectLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CharacterEffectLineTranslationPatch), nameof(CharacterEffectLineTranslationPatch.Prefix))));

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CharacterStatusScreenBindingPatch_TranslatesDirectScreenFields_WhenPatched()
    {
        WriteDictionary(
            ("Salt Hopper", "ソルトホッパー"),
            ("Mutated Human Pilgrim", "変異人 巡礼者"),
            ("LVL", "レベル"),
            ("Weight", "重量"),
            ("Mutations", "突然変異"));

        var target = new DummyCharacterStatusScreenBindingTarget();
        target.primaryAttributesController = null!;
        target.secondaryAttributesController = null!;
        target.resistanceAttributesController = null!;
        target.mutationsController = null!;
        target.effectsController = null!;

        var shouldRunOriginal = true;
        var trace = TestTraceHelper.CaptureTrace(() => shouldRunOriginal = CharacterStatusScreenBindingPatch.Prefix(target));

        Assert.Multiple(() =>
        {
            Assert.That(shouldRunOriginal, Is.False, trace);
            Assert.That(target.mutationTermText.Text, Is.EqualTo("突然変異"));
            Assert.That(target.nameText.Text, Is.EqualTo("ソルトホッパー"));
            Assert.That(target.classText.Text, Is.EqualTo("変異人 巡礼者"));
            Assert.That(target.levelText.Text, Is.EqualTo("レベル: 1 ¯ HP: 10/10 ¯ XP: 100/200 ¯ 重量: 123#"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(CharacterStatusScreenBindingPatch),
                    "CharacterStatus.StatusSummary"),
                Is.GreaterThan(0));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(CharacterStatusScreenBindingPatch),
                    "CharacterStatus.ExactLookup"),
                Is.GreaterThan(0));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(CharacterStatusScreenBindingPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "Level: 1 ¯ HP: 10/10 ¯ XP: 100/200 ¯ Weight: 123#",
                    "Level: 1 ¯ HP: 10/10 ¯ XP: 100/200 ¯ Weight: 123#"),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void CharacterStatusScreenBindingPatch_AllowsOriginal_WhenControllerPopulationFails()
    {
        var target = new DummyCharacterStatusScreenBindingTarget();
        target.primaryAttributesController.ThrowOnBeforeShow = true;

        var shouldRunOriginal = CharacterStatusScreenBindingPatch.Prefix(target);

        Assert.That(shouldRunOriginal, Is.True);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(
            Path.Combine(tempDirectory, "status-binding-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
