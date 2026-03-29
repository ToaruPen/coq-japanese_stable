using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WorldGenerationScreenTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-worldgenscreen-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        SinkObservation.ResetForTests();
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
    public void Prefix_TranslatesMessage_WhenPatched()
    {
        WriteDictionary(
            ("Generating rivers...", "河川を生成しています..."));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            var target = new DummyWorldGenerationScreenTarget();

            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldGenerationScreenTarget), nameof(DummyWorldGenerationScreenTarget._AddMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldGenerationScreenTranslationPatch), nameof(WorldGenerationScreenTranslationPatch.Prefix))));

            target._AddMessage("Generating rivers...");

            Assert.That(target.LastMessage, Is.EqualTo("河川を生成しています..."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesUnknownMessageUnchanged_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            var target = new DummyWorldGenerationScreenTarget();

            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldGenerationScreenTarget), nameof(DummyWorldGenerationScreenTarget._AddMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldGenerationScreenTranslationPatch), nameof(WorldGenerationScreenTranslationPatch.Prefix))));

            const string source = "Unknown generation message";
            target._AddMessage(source);

            Assert.That(target.LastMessage, Is.EqualTo(source));
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
            var target = new DummyWorldGenerationScreenTarget();

            harmony.Patch(
                original: RequireMethod(typeof(DummyWorldGenerationScreenTarget), nameof(DummyWorldGenerationScreenTarget._AddMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(WorldGenerationScreenTranslationPatch), nameof(WorldGenerationScreenTranslationPatch.Prefix))));

            target._AddMessage("\u0001既に翻訳済み");

            Assert.That(target.LastMessage, Is.EqualTo("既に翻訳済み"));
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
            Path.Combine(tempDirectory, "worldgenscreen-test.ja.json"),
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
