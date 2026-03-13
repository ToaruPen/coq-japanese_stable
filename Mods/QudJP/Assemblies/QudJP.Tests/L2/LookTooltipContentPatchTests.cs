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

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLookTooltipTarget), nameof(DummyLookTooltipTarget.GenerateTooltipContent)),
                postfix: new HarmonyMethod(RequireMethod(typeof(LookTooltipContentPatch), nameof(LookTooltipContentPatch.Postfix))));

            var result = DummyLookTooltipTarget.GenerateTooltipContent("This relic hums softly.");

            Assert.That(result, Is.EqualTo("この遺物はかすかに唸っている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Ancient ruin", "古代の廃墟"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLookTooltipTarget), nameof(DummyLookTooltipTarget.GenerateTooltipContent)),
                postfix: new HarmonyMethod(RequireMethod(typeof(LookTooltipContentPatch), nameof(LookTooltipContentPatch.Postfix))));

            var result = DummyLookTooltipTarget.GenerateTooltipContent("{{Y|Ancient ruin}}");

            Assert.That(result, Is.EqualTo("{{Y|古代の廃墟}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughUnknownTooltip_WhenPatched()
    {
        WriteDictionary(("Known text", "既知の文"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLookTooltipTarget), nameof(DummyLookTooltipTarget.GenerateTooltipContent)),
                postfix: new HarmonyMethod(RequireMethod(typeof(LookTooltipContentPatch), nameof(LookTooltipContentPatch.Postfix))));

            var result = DummyLookTooltipTarget.GenerateTooltipContent("Unknown tooltip text");

            Assert.That(result, Is.EqualTo("Unknown tooltip text"));
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

        var path = Path.Combine(tempDirectory, "look-tooltip-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static class DummyLookTooltipTarget
    {
        public static string GenerateTooltipContent(string content)
        {
            return content;
        }
    }
}
