using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ModMenuLineTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-mod-menu-line-l2", Guid.NewGuid().ToString("N"));
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
    public void UpdateAndSetTag_TranslateAuthorAndStatusTags_WhenPatched()
    {
        WriteDictionary(
            ("{{green|ENABLED}}", "{{green|有効}}"),
            ("{{black|DISABLED}}", "{{black|無効}}"),
            ("{{red|FAILED}}", "{{red|エラー}}"),
            ("{{W|# UPDATE AVAILABLE}}", "{{W|# 更新あり}}"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyModMenuLineTarget), nameof(DummyModMenuLineTarget.Update)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ModMenuLineTranslationPatch), nameof(ModMenuLineTranslationPatch.Postfix))));

            var target = new DummyModMenuLineTarget();
            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(target.authorText.Text, Is.EqualTo("{{y|作者: Example Author}}"));
                Assert.That(
                    target.tags,
                    Is.EqualTo(new[]
                    {
                        "{{green|有効}}",
                        "{{black|無効}}",
                        "{{red|エラー}}",
                        "{{W|# 更新あり}}",
                    }));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(ModMenuLineTranslationPatch), "ModMenuLine.AuthorText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(ModMenuLineTranslationPatch), "ModMenuLine.TagText"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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
            Path.Combine(tempDirectory, "ui-modpage-l2.ja.json"),
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
