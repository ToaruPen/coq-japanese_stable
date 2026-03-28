using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WorldCreationProgressStepProgressTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-world-creation-step-l2", Guid.NewGuid().ToString("N"));
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

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesStepText_WhenPatched()
    {
        WriteDictionary(("Generating villages", "村を生成中"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldCreationProgressTarget), nameof(DummyWorldCreationProgressTarget.StepProgress)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldCreationProgressStepProgressTranslationPatch), nameof(WorldCreationProgressStepProgressTranslationPatch.Prefix))));

            DummyWorldCreationProgressTarget.StepProgress("Generating villages", Last: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyWorldCreationProgressTarget.LastStepText, Is.EqualTo("村を生成中"));
                Assert.That(DummyWorldCreationProgressTarget.LastLastFlag, Is.True);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesAlreadyLocalizedTextUnchanged()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldCreationProgressTarget), nameof(DummyWorldCreationProgressTarget.StepProgress)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldCreationProgressStepProgressTranslationPatch), nameof(WorldCreationProgressStepProgressTranslationPatch.Prefix))));

            DummyWorldCreationProgressTarget.StepProgress("既に翻訳済み", Last: false);

            Assert.That(DummyWorldCreationProgressTarget.LastStepText, Is.EqualTo("既に翻訳済み"));
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
            Path.Combine(tempDirectory, "world-creation-progress.ja.json"),
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
