using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WorldCreationProgressTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-worldcreation-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        SinkObservation.ResetForTests();
        DummyWorldCreationProgressTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();
        DummyWorldCreationProgressTarget.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesNextStepText_WhenPatched()
    {
        WriteDictionary(
            ("Generating topography...", "地形を生成しています..."));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldCreationProgressTarget), nameof(DummyWorldCreationProgressTarget.NextStep)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldCreationProgressTranslationPatch), nameof(WorldCreationProgressTranslationPatch.Prefix))));

            DummyWorldCreationProgressTarget.NextStep("Generating topography...", 5);

            Assert.Multiple(() =>
            {
                Assert.That(DummyWorldCreationProgressTarget.LastNextStepText, Is.EqualTo("地形を生成しています..."));
                Assert.That(DummyWorldCreationProgressTarget.LastNextStepTotalSteps, Is.EqualTo(5));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesStepProgressText_WhenPatched()
    {
        WriteDictionary(
            ("Hardening math...", "数式を安定化しています..."));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldCreationProgressTarget), nameof(DummyWorldCreationProgressTarget.StepProgress)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldCreationProgressTranslationPatch), nameof(WorldCreationProgressTranslationPatch.Prefix))));

            DummyWorldCreationProgressTarget.StepProgress("Hardening math...", false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyWorldCreationProgressTarget.LastStepProgressText, Is.EqualTo("数式を安定化しています..."));
                Assert.That(DummyWorldCreationProgressTarget.LastStepProgressLast, Is.False);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesUnknownTextUnchanged_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldCreationProgressTarget), nameof(DummyWorldCreationProgressTarget.NextStep)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldCreationProgressTranslationPatch), nameof(WorldCreationProgressTranslationPatch.Prefix))));

            const string source = "Unknown step text";
            DummyWorldCreationProgressTarget.NextStep(source, 3);

            Assert.That(DummyWorldCreationProgressTarget.LastNextStepText, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesEmptyTextUnchanged_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldCreationProgressTarget), nameof(DummyWorldCreationProgressTarget.NextStep)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldCreationProgressTranslationPatch), nameof(WorldCreationProgressTranslationPatch.Prefix))));

            DummyWorldCreationProgressTarget.NextStep(string.Empty, 2);

            Assert.That(DummyWorldCreationProgressTarget.LastNextStepText, Is.EqualTo(string.Empty));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesVisibleTextAndPreservesColorTags_WhenPatched()
    {
        WriteDictionary(("Generating topography...", "地形を生成しています..."));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldCreationProgressTarget), nameof(DummyWorldCreationProgressTarget.NextStep)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldCreationProgressTranslationPatch), nameof(WorldCreationProgressTranslationPatch.Prefix))));

            DummyWorldCreationProgressTarget.NextStep("{{C|Generating topography...}}", 5);

            Assert.That(DummyWorldCreationProgressTarget.LastNextStepText, Is.EqualTo("{{C|地形を生成しています...}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarker_WhenAlreadyTranslated()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldCreationProgressTarget), nameof(DummyWorldCreationProgressTarget.NextStep)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldCreationProgressTranslationPatch), nameof(WorldCreationProgressTranslationPatch.Prefix))));

            DummyWorldCreationProgressTarget.NextStep("\u0001既に翻訳済み", 1);

            Assert.That(DummyWorldCreationProgressTarget.LastNextStepText, Is.EqualTo("既に翻訳済み"));
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
            Path.Combine(tempDirectory, "worldcreation-test.ja.json"),
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
