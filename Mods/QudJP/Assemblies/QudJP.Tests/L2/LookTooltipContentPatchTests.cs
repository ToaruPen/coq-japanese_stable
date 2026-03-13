using System;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using NUnit.Framework;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LookTooltipContentPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-look-tooltip-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
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
    public void Postfix_TranslatesKnownTooltip_WhenPatched()
    {
        WriteDictionary(("This relic hums softly.", "この遺物はかすかに唸っている。"));

        RunWithTooltipPatch(() =>
        {
            var result = DummyLookTooltipTarget.GenerateTooltipContent("This relic hums softly.");

            Assert.That(result, Is.EqualTo("この遺物はかすかに唸っている。"));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Ancient ruin", "古代の廃墟"));

        RunWithTooltipPatch(() =>
        {
            var result = DummyLookTooltipTarget.GenerateTooltipContent("{{Y|Ancient ruin}}");

            Assert.That(result, Is.EqualTo("{{Y|古代の廃墟}}"));
        });
    }

    [Test]
    public void Postfix_PassesThroughUnknownTooltip_WhenPatched()
    {
        WriteDictionary(("Known text", "既知の文"));

        RunWithTooltipPatch(() =>
        {
            var result = DummyLookTooltipTarget.GenerateTooltipContent("Unknown tooltip text");

            Assert.That(result, Is.EqualTo("Unknown tooltip text"));
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
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        WriteDictionaryFile(builder.ToString());
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void AppendEntries(StringBuilder builder, IReadOnlyList<(string key, string text)> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (key, text) = entries[index];
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }
    }

    private static HarmonyMethod TooltipPostfix =>
        new HarmonyMethod(RequireMethod(typeof(LookTooltipContentPatch), nameof(LookTooltipContentPatch.Postfix)));

    private static void RunWithTooltipPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLookTooltipTarget), nameof(DummyLookTooltipTarget.GenerateTooltipContent)),
                postfix: TooltipPostfix);
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string content)
    {
        var path = Path.Combine(tempDirectory, "look-tooltip-l2.ja.json");
        File.WriteAllText(path, content, Utf8WithoutBom);
    }

    private static class DummyLookTooltipTarget
    {
        public static string GenerateTooltipContent(string content)
        {
            return content;
        }
    }
}
