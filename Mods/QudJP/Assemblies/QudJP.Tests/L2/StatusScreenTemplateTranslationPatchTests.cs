using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class StatusScreenTemplateTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-statusscreen-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesSkillPointsText_WhenPatched()
    {
        WriteDictionary(("Skill Points (SP): {val}", "スキルポイント (SP): {val}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySkillsAndPowersStatusScreen), nameof(DummySkillsAndPowersStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SkillsAndPowersStatusScreenTranslationPatch), nameof(SkillsAndPowersStatusScreenTranslationPatch.Postfix))));

            var screen = new DummySkillsAndPowersStatusScreen();
            screen.UpdateViewFromData();

            Assert.That(screen.spText.Text, Is.EqualTo("スキルポイント (SP): 0"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsSkillPointsOwnerRouteTransform_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(("Skill Points (SP): {val}", "スキルポイント (SP): {val}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySkillsAndPowersStatusScreen), nameof(DummySkillsAndPowersStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SkillsAndPowersStatusScreenTranslationPatch), nameof(SkillsAndPowersStatusScreenTranslationPatch.Postfix))));

            var screen = new DummySkillsAndPowersStatusScreen();
            screen.UpdateViewFromData();

            const string source = "Skill Points (SP): 0";
            Assert.Multiple(() =>
            {
                Assert.That(screen.spText.Text, Is.EqualTo("スキルポイント (SP): 0"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(SkillsAndPowersStatusScreenTranslationPatch),
                        "Skill Points (SP): {val}"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(SkillsAndPowersStatusScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesCharacterPointTexts_WhenPatched()
    {
        WriteDictionary(
            ("Attribute Points: {0}", "能力値ポイント: {0}"),
            ("Mutation Points: {0}", "突然変異ポイント: {0}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterStatusScreen), nameof(DummyCharacterStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenTranslationPatch), nameof(CharacterStatusScreenTranslationPatch.Postfix))));

            var screen = new DummyCharacterStatusScreen();
            screen.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(screen.attributePointsText.Text, Is.EqualTo("能力値ポイント: 0"));
                Assert.That(screen.mutationPointsText.Text, Is.EqualTo("突然変異ポイント: 0"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsCharacterPointOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(
            ("Attribute Points: {0}", "能力値ポイント: {0}"),
            ("Mutation Points: {0}", "突然変異ポイント: {0}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterStatusScreen), nameof(DummyCharacterStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenTranslationPatch), nameof(CharacterStatusScreenTranslationPatch.Postfix))));

            var screen = new DummyCharacterStatusScreen();
            screen.UpdateViewFromData();

            const string attributeSource = "Attribute Points: 0";
            const string mutationSource = "Mutation Points: 0";
            Assert.Multiple(() =>
            {
                Assert.That(screen.attributePointsText.Text, Is.EqualTo("能力値ポイント: 0"));
                Assert.That(screen.mutationPointsText.Text, Is.EqualTo("突然変異ポイント: 0"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterStatusScreenTranslationPatch),
                        "Attribute Points: {0}"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterStatusScreenTranslationPatch),
                        "Mutation Points: {0}"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(CharacterStatusScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        attributeSource,
                        attributeSource),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(CharacterStatusScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        mutationSource,
                        mutationSource),
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

        var path = Path.Combine(tempDirectory, "status-screen-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
