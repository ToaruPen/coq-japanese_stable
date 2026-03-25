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
    public void Postfix_TranslatesKnownExpandedText()
    {
        WriteDictionary(("In the beginning, Resheph created Qud", "はじめに、レシェフがクッドを創造した"));

        var result = "In the beginning, Resheph created Qud";

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo("はじめに、レシェフがクッドを創造した"));
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
    public void Postfix_TranslatesColorWrappedText()
    {
        WriteDictionary(("Warning!", "警告！"));

        var result = "{{R|Warning!}}";

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo("{{R|警告！}}"));
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
    public void Postfix_TranslatesMultipleCallsIndependently()
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
            Assert.That(first, Is.EqualTo("スルタンが王になった"));
            Assert.That(second, Is.EqualTo("スルタンは追放された"));
        });
    }

    [Test]
    public void Postfix_TranslatesAmpersandColorCodedText()
    {
        WriteDictionary(("Sultan was crowned", "スルタンが戴冠した"));

        var result = "&GSultan was crowned^k";

        HistoricStringExpanderPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo("&Gスルタンが戴冠した^k"));
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
