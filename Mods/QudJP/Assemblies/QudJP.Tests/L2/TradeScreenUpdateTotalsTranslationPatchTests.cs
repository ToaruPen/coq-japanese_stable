using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TradeScreenUpdateTotalsTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-tradescreen-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesDramsLabel_WhenPatched()
    {
        WriteDictionary(
            (" drams", " ドラム"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            var target = new DummyTradeScreenTarget();

            harmony.Patch(
                original: RequireMethod(typeof(DummyTradeScreenTarget), nameof(DummyTradeScreenTarget.UpdateTotals)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUpdateTotalsTranslationPatch), nameof(TradeScreenUpdateTotalsTranslationPatch.Postfix))));

            target.UpdateTotals();

            Assert.Multiple(() =>
            {
                Assert.That(target.totalLabels[0].Text, Is.EqualTo("{{B|42 ドラム →}}"));
                Assert.That(target.totalLabels[1].Text, Is.EqualTo("{{B|← 10 ドラム}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesWeightLabel_WhenPatched()
    {
        WriteDictionary(
            ("lbs.", "ポンド"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            var target = new DummyTradeScreenTarget();

            harmony.Patch(
                original: RequireMethod(typeof(DummyTradeScreenTarget), nameof(DummyTradeScreenTarget.UpdateTotals)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUpdateTotalsTranslationPatch), nameof(TradeScreenUpdateTotalsTranslationPatch.Postfix))));

            target.UpdateTotals();

            Assert.That(target.freeDramsLabels[1].Text, Is.EqualTo("{{W|$50}} | {{K|123/200 ポンド}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesLabelsUnchanged_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            var target = new DummyTradeScreenTarget();

            harmony.Patch(
                original: RequireMethod(typeof(DummyTradeScreenTarget), nameof(DummyTradeScreenTarget.UpdateTotals)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TradeScreenUpdateTotalsTranslationPatch), nameof(TradeScreenUpdateTotalsTranslationPatch.Postfix))));

            target.UpdateTotals();

            Assert.Multiple(() =>
            {
                Assert.That(target.totalLabels[0].Text, Is.EqualTo("{{B|42 drams →}}"));
                Assert.That(target.totalLabels[1].Text, Is.EqualTo("{{B|← 10 drams}}"));
                Assert.That(target.freeDramsLabels[1].Text, Is.EqualTo("{{W|$50}} | {{K|123/200 lbs.}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_HandlesNullInstance_Gracefully()
    {
        Assert.DoesNotThrow(() => TradeScreenUpdateTotalsTranslationPatch.Postfix(null));
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
            Path.Combine(tempDirectory, "tradescreen-test.ja.json"),
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
