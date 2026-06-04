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

    [Test]
    public void TranslateExactOrLowerAsciiForContext_PrefersContextualEntry()
    {
        WriteDictionaryContents(
            "scoped.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"stone\",\"context\":\"Route.A\",\"text\":\"石\"}," +
            "{\"key\":\"stone\",\"text\":\"石ではない\"}" +
            "]}\n");

        Assert.Multiple(() =>
        {
            Assert.That(
                ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContext("stone", "Route.A", "scoped.ja.json"),
                Is.EqualTo("石"));
            Assert.That(
                ScopedDictionaryLookup.TranslateExactOrLowerAscii("stone", "scoped.ja.json"),
                Is.EqualTo("石ではない"));
        });
    }

    [Test]
    public void TranslateExactOrLowerAsciiForContextOnly_DoesNotFallbackToUnscopedEntry()
    {
        WriteDictionaryContents(
            "scoped.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"stone\",\"text\":\"石ではない\"}" +
            "]}\n");

        Assert.Multiple(() =>
        {
            Assert.That(
                ScopedDictionaryLookup.TranslateExactOrLowerAscii("stone", "scoped.ja.json"),
                Is.EqualTo("石ではない"));
            Assert.That(
                ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly("stone", "Route.A", "scoped.ja.json"),
                Is.Null);
        });
    }

    [Test]
    public void TranslateExactOrLowerAsciiForContextOnly_UsesLowerAsciiContextualEntry()
    {
        WriteDictionaryContents(
            "scoped.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"detonate\",\"context\":\"Route.A\",\"text\":\"起爆する\"}," +
            "{\"key\":\"detonate\",\"text\":\"起爆\"}" +
            "]}\n");

        Assert.Multiple(() =>
        {
            Assert.That(
                ScopedDictionaryLookup.TranslateExactOrLowerAscii("Detonate", "scoped.ja.json"),
                Is.EqualTo("起爆"));
            Assert.That(
                ScopedDictionaryLookup.TranslateExactOrLowerAsciiForContextOnly("Detonate", "Route.A", "scoped.ja.json"),
                Is.EqualTo("起爆する"));
        });
    }

    [Test]
    public void FindSourceByExactTranslationForContextOnly_ReturnsUniqueContextualSource()
    {
        WriteDictionaryContents(
            "scoped.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"disassemble all\",\"context\":\"Route.A\",\"text\":\"すべて分解\"}," +
            "{\"key\":\"disassemble all\",\"text\":\"別経路\"}," +
            "{\"key\":\"drop all\",\"context\":\"Route.B\",\"text\":\"すべて分解\"}" +
            "]}\n");

        Assert.That(
            ScopedDictionaryLookup.FindSourceByExactTranslationForContextOnly("すべて分解", "Route.A", "scoped.ja.json"),
            Is.EqualTo("disassemble all"));
    }

    [Test]
    public void FindSourceByExactTranslationForContextOnly_ReturnsNullForAmbiguousContextualSource()
    {
        WriteDictionaryContents(
            "scoped.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"drop all\",\"context\":\"Route.A\",\"text\":\"すべて\"}," +
            "{\"key\":\"take all\",\"context\":\"Route.A\",\"text\":\"すべて\"}" +
            "]}\n");

        Assert.That(
            ScopedDictionaryLookup.FindSourceByExactTranslationForContextOnly("すべて", "Route.A", "scoped.ja.json"),
            Is.Null);
    }

    [Test]
    public void FindSourceByExactTranslationForContextOnly_PropagatesAmbiguityBeforeLaterCandidate()
    {
        WriteDictionaryContents(
            "first.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"drop all\",\"context\":\"Route.A\",\"text\":\"すべて\"}," +
            "{\"key\":\"take all\",\"context\":\"Route.A\",\"text\":\"すべて\"}" +
            "]}\n");
        WriteDictionaryContents(
            "second.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"disassemble all\",\"context\":\"Route.A\",\"text\":\"すべて\"}" +
            "]}\n");

        Assert.That(
            ScopedDictionaryLookup.FindSourceByExactTranslationForContextOnly("すべて", "Route.A", "first.ja.json", "second.ja.json"),
            Is.Null);
    }

    private void WriteDictionary(string fileName, params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        File.WriteAllText(Path.Combine(tempDirectory, fileName), builder.ToString(), Utf8WithoutBom);
    }

    private void WriteDictionaryContents(string fileName, string contents)
    {
        File.WriteAllText(Path.Combine(tempDirectory, fileName), contents, Utf8WithoutBom);
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
