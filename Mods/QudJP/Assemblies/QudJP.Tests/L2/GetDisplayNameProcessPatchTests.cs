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
public sealed class GetDisplayNameProcessPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-displayname-process-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesKnownDisplayName_WhenPatched()
    {
        WriteDictionary(("engraved carbide dagger", "刻印されたカーバイドダガー"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var result = DummyDisplayNameProcessor.ProcessFor("engraved carbide dagger");

            Assert.That(result, Is.EqualTo("刻印されたカーバイドダガー"));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("engraved carbide dagger", "刻印されたカーバイドダガー"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var result = DummyDisplayNameProcessor.ProcessFor("{{C|engraved carbide dagger}}");

            Assert.That(result, Is.EqualTo("{{C|刻印されたカーバイドダガー}}"));
        });
    }

    [Test]
    public void Postfix_PassesThroughUnknownDisplayName_WhenPatched()
    {
        WriteDictionary(("known relic", "既知の遺物"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var result = DummyDisplayNameProcessor.ProcessFor("unknown relic");

            Assert.That(result, Is.EqualTo("unknown relic"));
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

    private static HarmonyMethod DisplayNameProcessPostfix =>
        new HarmonyMethod(RequireMethod(typeof(GetDisplayNameProcessPatch), nameof(GetDisplayNameProcessPatch.Postfix)));

    private static void RunWithDisplayNameProcessPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDisplayNameProcessor), nameof(DummyDisplayNameProcessor.ProcessFor)),
                postfix: DisplayNameProcessPostfix);
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string content)
    {
        var path = Path.Combine(tempDirectory, "displayname-process-l2.ja.json");
        File.WriteAllText(path, content, Utf8WithoutBom);
    }

    private static class DummyDisplayNameProcessor
    {
        public static string ProcessFor(string displayName)
        {
            return displayName;
        }
    }
}
