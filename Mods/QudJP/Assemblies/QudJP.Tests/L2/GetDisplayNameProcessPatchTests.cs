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
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("engraved carbide dagger");

            Assert.That(result, Is.EqualTo("刻印されたカーバイドダガー"));
        });
    }

    [Test]
    public void Postfix_TranslatesJapaneseEntry_WhenPatched()
    {
        WriteDictionary(("奇妙な遺物", "奇妙なアーティファクト"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("奇妙な遺物");

            Assert.That(result, Is.EqualTo("奇妙なアーティファクト"));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("engraved carbide dagger", "刻印されたカーバイドダガー"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("{{C|engraved carbide dagger}}");

            Assert.That(result, Is.EqualTo("{{C|刻印されたカーバイドダガー}}"));
        });
    }

    [Test]
    public void Postfix_PassesThroughUnknownDisplayName_WhenPatched()
    {
        WriteDictionary(("known relic", "既知の遺物"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("unknown relic");

            Assert.That(result, Is.EqualTo("unknown relic"));
        });
    }

    [Test]
    public void Postfix_SkipsMissingKeyLogging_ForFigurineFamily_WhenBuilderMatches()
    {
        WriteDictionary(("手袋屋", "手袋屋"));

        RunWithFigurineDisplayNameProcessPatch(() =>
        {
            var processor = new DummyFigurineDisplayNameProcessor();
            var result = processor.ProcessFor(displayName: "瑪瑙 手袋屋 のフィギュリン");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("瑪瑙 手袋屋 のフィギュリン"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("瑪瑙 手袋屋 のフィギュリン"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TransformsLegendaryFamily_WhenBuilderMatches()
    {
        WriteDictionary(("ヒヒ", "ヒヒ"));

        RunWithFigurineDisplayNameProcessPatch(() =>
        {
            var processor = new DummyFigurineDisplayNameProcessor { DB = new DummyDescriptionBuilder("ヒヒ", "legendary") };
            var result = processor.ProcessFor(displayName: "Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Oo-hoo-ho-HOO-OOO-ee-ho、伝説のヒヒ"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_SkipsMissingKeyLogging_ForWarlordFamily_WhenBuilderMatches()
    {
        WriteDictionary(("スナップジョー", "スナップジョー"));

        RunWithFigurineDisplayNameProcessPatch(() =>
        {
            var processor = new DummyFigurineDisplayNameProcessor { DB = new DummyDescriptionBuilder("スナップジョー", "軍主") };
            var result = processor.ProcessFor(displayName: "スナップジョーの軍主");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("スナップジョーの軍主"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("スナップジョーの軍主"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_LeavesComposedNameOnExactMatchPath_WhenBuilderLastAddedDoesNotMatch()
    {
        WriteDictionary(("ヒヒ", "ヒヒ"));

        RunWithFigurineDisplayNameProcessPatch(() =>
        {
            var processor = new DummyFigurineDisplayNameProcessor { DB = new DummyDescriptionBuilder("ヒヒ", "warlord") };
            var result = processor.ProcessFor(displayName: "Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ"), Is.EqualTo(1));
            });
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
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
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

    private static void RunWithFigurineDisplayNameProcessPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFigurineDisplayNameProcessor), nameof(DummyFigurineDisplayNameProcessor.ProcessFor)),
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

    private sealed class DummyDisplayNameProcessor
    {
        public object? DB = new object();

        public string ProcessFor(string displayName)
        {
            _ = DB;
            return displayName;
        }
    }

    private sealed class DummyFigurineDisplayNameProcessor
    {
        public DummyDescriptionBuilder DB = new DummyDescriptionBuilder("手袋屋", "のフィギュリン");

        public string ProcessFor(string displayName)
        {
            _ = string.Concat(DB.PrimaryBase, DB.LastAdded);
            return displayName;
        }
    }

    private sealed class DummyDescriptionBuilder
    {
        public DummyDescriptionBuilder(string primaryBase, string lastAdded)
        {
            PrimaryBase = primaryBase;
            LastAdded = lastAdded;
        }

        public string PrimaryBase;

        public string LastAdded;
    }
}
