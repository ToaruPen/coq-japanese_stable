using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LoadingStatusTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-loading-status-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DummyLoadingTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesDescription_WhenPatched()
    {
        WriteDictionary(("Loading world", "ワールドを読み込み中"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLoadingTarget), nameof(DummyLoadingTarget.SetLoadingStatus)),
                prefix: new HarmonyMethod(RequireMethod(typeof(LoadingStatusTranslationPatch), nameof(LoadingStatusTranslationPatch.Prefix))));

            DummyLoadingTarget.SetLoadingStatus("Loading world", waitForUiUpdate: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyLoadingTarget.LastDescription, Is.EqualTo("\u0001ワールドを読み込み中"));
                Assert.That(DummyLoadingTarget.LastWaitForUiUpdate, Is.True);
            });
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
                original: RequireMethod(typeof(DummyLoadingTarget), nameof(DummyLoadingTarget.SetLoadingStatus)),
                prefix: new HarmonyMethod(RequireMethod(typeof(LoadingStatusTranslationPatch), nameof(LoadingStatusTranslationPatch.Prefix))));

            DummyLoadingTarget.SetLoadingStatus("\u0001既に翻訳済み", true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyLoadingTarget.LastDescription, Is.EqualTo("既に翻訳済み"));
                Assert.That(DummyLoadingTarget.LastWaitForUiUpdate, Is.True);
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
            Path.Combine(tempDirectory, "loading-status.ja.json"),
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
