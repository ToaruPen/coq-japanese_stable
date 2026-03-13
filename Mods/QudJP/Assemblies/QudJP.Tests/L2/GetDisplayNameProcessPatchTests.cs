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

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDisplayNameProcessor), nameof(DummyDisplayNameProcessor.ProcessFor)),
                postfix: new HarmonyMethod(RequireMethod(typeof(GetDisplayNameProcessPatch), nameof(GetDisplayNameProcessPatch.Postfix))));

            var result = DummyDisplayNameProcessor.ProcessFor("engraved carbide dagger");

            Assert.That(result, Is.EqualTo("刻印されたカーバイドダガー"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("engraved carbide dagger", "刻印されたカーバイドダガー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDisplayNameProcessor), nameof(DummyDisplayNameProcessor.ProcessFor)),
                postfix: new HarmonyMethod(RequireMethod(typeof(GetDisplayNameProcessPatch), nameof(GetDisplayNameProcessPatch.Postfix))));

            var result = DummyDisplayNameProcessor.ProcessFor("{{C|engraved carbide dagger}}");

            Assert.That(result, Is.EqualTo("{{C|刻印されたカーバイドダガー}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughUnknownDisplayName_WhenPatched()
    {
        WriteDictionary(("known relic", "既知の遺物"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDisplayNameProcessor), nameof(DummyDisplayNameProcessor.ProcessFor)),
                postfix: new HarmonyMethod(RequireMethod(typeof(GetDisplayNameProcessPatch), nameof(GetDisplayNameProcessPatch.Postfix))));

            var result = DummyDisplayNameProcessor.ProcessFor("unknown relic");

            Assert.That(result, Is.EqualTo("unknown relic"));
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

        var path = Path.Combine(tempDirectory, "displayname-process-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static class DummyDisplayNameProcessor
    {
        public static string ProcessFor(string displayName)
        {
            return displayName;
        }
    }
}
