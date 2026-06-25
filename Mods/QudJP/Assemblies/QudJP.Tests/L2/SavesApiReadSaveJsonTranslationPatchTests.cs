using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SavesApiReadSaveJsonTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-savesapi-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesTotalSize_WhenPatched()
    {
        WriteDictionary(
            ("Total size: {0}", "合計サイズ：{0}"),
            ("Level {0} {1} [{2}]", "レベル {0} {1}［{2}］"),
            ("Apostle", "使徒"),
            ("Roleplay", "ロールプレイ"),
            ("{0}, {1} turn {2}", "{0}、{1} ターン {2}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySavesApiTarget), nameof(DummySavesApiTarget.ReadSaveJson)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SavesApiReadSaveJsonTranslationPatch), nameof(SavesApiReadSaveJsonTranslationPatch.Postfix))));

            var result = DummySavesApiTarget.ReadSaveJson("dir", "Primary.json");

            Assert.Multiple(() =>
            {
                Assert.That(result.Size, Is.EqualTo("合計サイズ：12mb"));
                Assert.That(result.Description, Is.EqualTo("レベル 29 使徒［ロールプレイ］"));
                Assert.That(result.Info, Is.EqualTo("Bethesda Susa、7 ターン 12345"));
                Assert.That(result.SaveTime, Is.EqualTo("Wednesday, June 10, 2026 at 5:58:42 PM"));
                Assert.That(result.SaveTime, Does.Contain(" at "));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(SavesApiReadSaveJsonTranslationPatch),
                        "Total size: {0}"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesSizeUnchanged_WhenTemplateIsMissing()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySavesApiTarget), nameof(DummySavesApiTarget.ReadSaveJson)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SavesApiReadSaveJsonTranslationPatch), nameof(SavesApiReadSaveJsonTranslationPatch.Postfix))));

            var result = DummySavesApiTarget.ReadSaveJson("dir", "Primary.json");

            Assert.That(result.Size, Is.EqualTo("Total size: 12mb"));
            Assert.That(result.Description, Is.EqualTo("Level 29 Apostle [Roleplay]"));
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
            Path.Combine(tempDirectory, "saves-api-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
