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
public sealed class DescriptionLongDescriptionPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-description-long-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesAppendedDescription_WhenPatched()
    {
        WriteDictionary(("It crackles with static.", "それは静電気を散らしている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionTarget), nameof(DummyDescriptionTarget.GetLongDescription)),
                prefix: new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Postfix))));

            var target = new DummyDescriptionTarget("It crackles with static.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("それは静電気を散らしている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PreservesExistingPrefix_WhenPatched()
    {
        WriteDictionary(("It crackles with static.", "それは静電気を散らしている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionTarget), nameof(DummyDescriptionTarget.GetLongDescription)),
                prefix: new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Postfix))));

            var target = new DummyDescriptionTarget("It crackles with static.");
            var builder = new StringBuilder("prefix: ");
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("prefix: それは静電気を散らしている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Charged item", "帯電したアイテム"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionTarget), nameof(DummyDescriptionTarget.GetLongDescription)),
                prefix: new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Postfix))));

            var target = new DummyDescriptionTarget("{{C|Charged item}}");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("{{C|帯電したアイテム}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughUnknownDescription_WhenPatched()
    {
        WriteDictionary(("Known text", "既知の文"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionTarget), nameof(DummyDescriptionTarget.GetLongDescription)),
                prefix: new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Postfix))));

            var target = new DummyDescriptionTarget("Unknown long description");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("Unknown long description"));
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

        var path = Path.Combine(tempDirectory, "description-long-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private sealed class DummyDescriptionTarget
    {
        private readonly string appendedText;

        public DummyDescriptionTarget(string appendedText)
        {
            this.appendedText = appendedText;
        }

        public void GetLongDescription(StringBuilder SB)
        {
            SB.Append(appendedText);
        }
    }
}
