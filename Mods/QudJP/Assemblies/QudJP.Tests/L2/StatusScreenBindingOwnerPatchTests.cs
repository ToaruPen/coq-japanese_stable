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

    [Test]
    public void CharacterAttributeLineTranslationPatch_KeepsAbbreviationInEnglish_WhenPatched()
    {
        // Stat abbreviations (STR, AGI, etc.) are intentionally kept in English
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
                stat = "Strength",
                go = new DummyStatusGameObject(),
                data = new DummyStatusStatistic
                {
                    Name = "Strength",
                    ShortDisplayName = "STR",
                    Value = 18,
                    BaseValue = 18,
                    Modifier = 2,
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.attributeText.Text, Is.EqualTo("STR"),
                    "Abbreviation must stay in English");
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(CharacterAttributeLineTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "STR",
                        "STR"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CharacterMutationLineTranslationPatch_TranslatesMutationLine_WhenPatched()
    {
        WriteDictionary(("Force Wall", "力の壁"));

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
                    DisplayName = "Force Wall",
                    Level = 1,
                    BaseLevel = 1,
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("{{y|力の壁 ({{C|1}})}}"));
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
                        "Force Wall (1)",
                        "Force Wall (1)"),
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
        WriteDictionary(("Beguiled", "魅了"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterEffectLineTarget), nameof(DummyCharacterEffectLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(CharacterEffectLineTranslationPatch), nameof(CharacterEffectLineTranslationPatch.Prefix))));

            var target = new DummyCharacterEffectLineTarget();
            target.setData(new DummyCharacterEffectLineDataTarget
            {
                effect = new DummyStatusEffect
                {
                    DisplayName = "Beguiled",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("魅了"));
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
        _ = CharacterStatusScreenBindingPatch.Prefix(target);

        Assert.Multiple(() =>
        {
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
