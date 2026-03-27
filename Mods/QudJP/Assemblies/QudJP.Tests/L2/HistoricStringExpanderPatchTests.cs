using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HistoricStringExpanderPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_ObservationOnly_LeavesKnownExpandedTextUnchanged_WhenPatched()
    {
        WriteDictionary(("In the beginning, Resheph created Qud", "はじめに、レシェフがクッドを創造した"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHistoricStringExpander), nameof(DummyHistoricStringExpander.ExpandString)),
                postfix: new HarmonyMethod(RequireMethod(typeof(HistoricStringExpanderPatch), nameof(HistoricStringExpanderPatch.Postfix))));

            const string source = "In the beginning, Resheph created Qud";
            var result = DummyHistoricStringExpander.ExpandString(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(HistoricStringExpanderPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughUnknownText_WhenPatched()
    {
        WriteDictionary(("Known lore line", "既知の伝承文"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHistoricStringExpander), nameof(DummyHistoricStringExpander.ExpandString)),
                postfix: new HarmonyMethod(RequireMethod(typeof(HistoricStringExpanderPatch), nameof(HistoricStringExpanderPatch.Postfix))));

            var result = DummyHistoricStringExpander.ExpandString("Unknown procedurally generated line");

            Assert.That(result, Is.EqualTo("Unknown procedurally generated line"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_ObservationOnly_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Sultan was crowned", "スルタンが戴冠した"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHistoricStringExpander), nameof(DummyHistoricStringExpander.ExpandString)),
                postfix: new HarmonyMethod(RequireMethod(typeof(HistoricStringExpanderPatch), nameof(HistoricStringExpanderPatch.Postfix))));

            const string source = "{{C|Sultan was crowned}}";
            var result = DummyHistoricStringExpander.ExpandString(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(HistoricStringExpanderPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        "Sultan was crowned"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_StripsDirectTranslationMarker_WhenPatched()
    {
        WriteDictionary(("In the beginning, Resheph created Qud", "はじめに、レシェフがクッドを創造した"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHistoricStringExpander), nameof(DummyHistoricStringExpander.ExpandString)),
                postfix: new HarmonyMethod(RequireMethod(typeof(HistoricStringExpanderPatch), nameof(HistoricStringExpanderPatch.Postfix))));

            const string source = "\u0001In the beginning, Resheph created Qud";
            var result = DummyHistoricStringExpander.ExpandString(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("In the beginning, Resheph created Qud"));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(HistoricStringExpanderPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        "In the beginning, Resheph created Qud"),
                    Is.EqualTo(0));
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

        var path = Path.Combine(tempDirectory, "historic-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
