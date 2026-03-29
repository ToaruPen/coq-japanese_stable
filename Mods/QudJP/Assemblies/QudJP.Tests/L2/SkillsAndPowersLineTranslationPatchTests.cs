using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SkillsAndPowersLineTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-skills-line-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
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
    public void Prefix_TranslatesSkillAndPowerLineTexts_WhenPatched()
    {
        WriteDictionary(
            ("Long Blades", "長刃"),
            ("Starting Cost [{val} sp]", "開始コスト [{val} sp]"),
            ("Pyrokinesis", "発火術"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySkillsAndPowersLineTarget), nameof(DummySkillsAndPowersLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(SkillsAndPowersLineTranslationPatch), nameof(SkillsAndPowersLineTranslationPatch.Prefix))));

            var skillTarget = new DummySkillsAndPowersLineTarget();
            skillTarget.setData(new DummySkillsAndPowersLineDataTarget
            {
                entry = new DummySPNodeTarget
                {
                    Name = "Long Blades",
                    Skill = new DummySkillDefinition
                    {
                        Cost = 100,
                    },
                    LearnedStatus = DummyLearnedStatus.None,
                },
            });

            var powerTarget = new DummySkillsAndPowersLineTarget();
            powerTarget.setData(new DummySkillsAndPowersLineDataTarget
            {
                entry = new DummySPNodeTarget
                {
                    Power = new DummyPowerDefinition(),
                    Name = "Pyrokinesis",
                    LearnedStatus = DummyLearnedStatus.Learned,
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(skillTarget.skillText.Text, Is.EqualTo("長刃"));
                Assert.That(skillTarget.skillRightText.Text, Is.EqualTo("開始コスト {{g|[100 sp]}}"));
                Assert.That(powerTarget.powerText.Text, Is.EqualTo("発火術"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(SkillsAndPowersLineTranslationPatch),
                        "SkillsAndPowers.ExactLeaf"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(SkillsAndPowersLineTranslationPatch),
                        "Starting Cost [{val} sp]"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(SkillsAndPowersLineTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Long Blades",
                        "Long Blades"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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
            Path.Combine(tempDirectory, "skills-line-l2.ja.json"),
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
