using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class ScopedDictionaryLookupTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-scoped-dictionary-tests", Guid.NewGuid().ToString("N"));
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
    public void TranslateExactOrLowerAscii_LogsDuplicateKeyOverrides_WithinScopedDictionaryFile()
    {
        WriteDictionary(
            "scoped.ja.json",
            ("Hello", "こんにちは"),
            ("Hello", "やあ"),
            ("Inventory", "インベントリ"));

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                ScopedDictionaryLookup.TranslateExactOrLowerAscii("Hello", "scoped.ja.json"),
                Is.EqualTo("やあ")));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("ScopedDictionaryLookup duplicate key 'Hello'"));
            Assert.That(output, Does.Contain("scoped.ja.json"));
            Assert.That(output, Does.Contain("ScopedDictionaryLookup duplicate key overrides in"));
            Assert.That(output, Does.Contain("Hello=1"));
        });
    }

    private void WriteDictionary(string fileName, params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        File.WriteAllText(Path.Combine(tempDirectory, fileName), builder.ToString(), Utf8WithoutBom);
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

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
