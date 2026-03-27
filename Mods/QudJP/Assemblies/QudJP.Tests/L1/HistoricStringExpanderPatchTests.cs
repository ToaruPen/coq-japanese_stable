using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class HistoricStringExpanderPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-l1", Guid.NewGuid().ToString("N"));
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
    public void Postfix_ObservationOnly_LeavesKnownExpandedTextUnchanged()
    {
        WriteDictionary(("In the beginning, Resheph created Qud", "はじめに、レシェフがクッドを創造した"));

        const string source = "In the beginning, Resheph created Qud";
        var result = source;

        HistoricStringExpanderPatch.Postfix(ref result);

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

    [Test]
    public void Postfix_PassesThroughUnknownExpandedText()
    {
        WriteDictionary(("Known lore line", "既知の伝承文"));

        var result = "Unknown procedurally generated line";

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo("Unknown procedurally generated line"));
    }

    [Test]
    public void Postfix_ObservationOnly_LeavesColorWrappedTextUnchanged()
    {
        WriteDictionary(("Warning!", "警告！"));

        const string source = "{{R|Warning!}}";
        var result = source;

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(source));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(HistoricStringExpanderPatch),
                    SinkObservation.ObservationOnlyDetail,
                    source,
                    "Warning!"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Postfix_ReturnsEmptyString_WhenResultIsEmpty()
    {
        WriteDictionary(("In the beginning, Resheph created Qud", "はじめに、レシェフがクッドを創造した"));

        var result = string.Empty;

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Postfix_ConvertsNullResultToEmptyString()
    {
        WriteDictionary(("In the beginning, Resheph created Qud", "はじめに、レシェフがクッドを創造した"));

        string result = null!;

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Postfix_PassesThroughMixedText_WhenExactKeyDoesNotMatch()
    {
        WriteDictionary(
            ("Resheph", "レシェフ"),
            ("Qud", "クッド"));

        var result = "Resheph founded Qud in this era.";

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo("Resheph founded Qud in this era."));
    }

    [Test]
    public void Postfix_ObservationOnly_PassesThroughMultipleCallsIndependently()
    {
        WriteDictionary(
            ("Sultan became king", "スルタンが王になった"),
            ("Sultan was exiled", "スルタンは追放された"));

        var first = "Sultan became king";
        HistoricStringExpanderPatch.Postfix(ref first);

        var second = "Sultan was exiled";
        HistoricStringExpanderPatch.Postfix(ref second);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("Sultan became king"));
            Assert.That(second, Is.EqualTo("Sultan was exiled"));
        });
    }

    [Test]
    public void Postfix_ObservationOnly_PassesThroughAmpersandColorCodedText()
    {
        WriteDictionary(("Sultan was crowned", "スルタンが戴冠した"));

        var result = "&GSultan was crowned^k";

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo("&GSultan was crowned^k"));
    }

    [Test]
    public void Postfix_StripsDirectTranslationMarkerBeforeObservation()
    {
        WriteDictionary(("In the beginning, Resheph created Qud", "はじめに、レシェフがクッドを創造した"));

        const string source = "\u0001In the beginning, Resheph created Qud";
        var result = source;

        HistoricStringExpanderPatch.Postfix(ref result);

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
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(HistoricStringExpanderPatch),
                    SinkObservation.ObservationOnlyDetail,
                    "In the beginning, Resheph created Qud",
                    "In the beginning, Resheph created Qud"),
                Is.EqualTo(1));
        });
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

        var path = Path.Combine(tempDirectory, "historic-l1.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
