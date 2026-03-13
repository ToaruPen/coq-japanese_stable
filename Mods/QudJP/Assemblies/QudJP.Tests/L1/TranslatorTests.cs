using System.Runtime.Serialization;
using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class TranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-translator-tests", Guid.NewGuid().ToString("N"));
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
    public void Translate_ReturnsJapanese_WhenKeyExists()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        var translated = Translator.Translate("Hello");

        Assert.That(translated, Is.EqualTo("こんにちは"));
    }

    [Test]
    public void Translate_ReturnsOriginal_WhenKeyIsMissing()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        var translated = Translator.Translate("Goodbye");

        Assert.That(translated, Is.EqualTo("Goodbye"));
    }

    [Test]
    public void Translate_LogsContext_WhenKeyIsMissing()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");
        using var writer = new StringWriter();
        using var listener = new System.Diagnostics.TextWriterTraceListener(writer);
        System.Diagnostics.Trace.Listeners.Add(listener);

        try
        {
            using var _ = Translator.PushLogContext("TestRoute");

            var translated = Translator.Translate("Goodbye");

            listener.Flush();
            var output = writer.ToString();

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo("Goodbye"));
                Assert.That(output, Does.Contain("missing key 'Goodbye'"));
                Assert.That(output, Does.Contain("context: TestRoute"));
            });
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    [Test]
    public void Translate_LoadsDictionaryOnlyOnce_WhenCalledRepeatedly()
    {
        WriteDictionary("ui-test.ja.json", "Inventory", "インベントリ");

        var first = Translator.Translate("Inventory");
        var second = Translator.Translate("Inventory");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("インベントリ"));
            Assert.That(second, Is.EqualTo("インベントリ"));
            Assert.That(Translator.LoadInvocationCount, Is.EqualTo(1));
        });
    }

    [Test]
    [Category("L1")]
    public void Translate_ThrowsDirectoryNotFoundException_WhenDictionaryDirectoryMissing()
    {
        Translator.SetDictionaryDirectoryForTests("/nonexistent/qudjp/path");

        Assert.Throws<DirectoryNotFoundException>(() => Translator.Translate("test"));
    }

    [Test]
    [Category("L1")]
    public void Translate_ThrowsSerializationException_WhenDictionaryJsonIsCorrupt()
    {
        WriteRawDictionary("corrupt.ja.json", "{\"entries\":[{\"key\":\"Hello\",\"text\":\"こんにちは\"}");

        Assert.Throws<SerializationException>(() => Translator.Translate("Hello"));
    }

    [Test]
    [Category("L1")]
    public void Translate_ThrowsInvalidDataException_WhenEntriesArrayIsMissing()
    {
        WriteRawDictionary("missing-entries.ja.json", "{}");

        Assert.Throws<InvalidDataException>(() => Translator.Translate("Hello"));
    }

    [Test]
    [Category("L1")]
    public void Translate_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Translator.Translate(null!));
    }

    private void WriteDictionary(string fileName, string key, string text)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendEntries(builder, new[] { (key, text) });
        builder.AppendLine("]}");
        WriteDictionaryFile(fileName, builder.ToString());
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private void WriteRawDictionary(string fileName, string content)
    {
        WriteDictionaryFile(fileName, content + Environment.NewLine);
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

    private void WriteDictionaryFile(string fileName, string content)
    {
        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, content, Utf8WithoutBom);
    }
}
