using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class StringHelpersTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-stringhelpers-l1", Guid.NewGuid().ToString("N"));
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

    [TestCase("a snapjaw", "snapjaw")]
    [TestCase("an ape", "ape")]
    [TestCase("the watervine", "watervine")]
    [TestCase("The watervine", "The watervine")]
    public void StripLeadingEnglishArticle_UsesDisplayNameStyleRules(string source, string expected)
    {
        Assert.That(StringHelpers.StripLeadingEnglishArticle(source), Is.EqualTo(expected));
    }

    [TestCase("The watervine", "watervine")]
    [TestCase("the watervine", "watervine")]
    [TestCase("a snapjaw", "a snapjaw")]
    public void StripLeadingDefiniteArticle_UsesFactionsAndChargenStyleRules(string source, string expected)
    {
        var stripped = StringHelpers.StripLeadingDefiniteArticle(source, StringComparison.OrdinalIgnoreCase);

        Assert.That(stripped, Is.EqualTo(expected));
    }

    [Test]
    public void TranslateExactOrLowerAscii_FallsBackToLowerAsciiKey()
    {
        WriteDictionary(("desecrate", "冒涜する"));

        var translated = StringHelpers.TranslateExactOrLowerAscii("Desecrate");

        Assert.That(translated, Is.EqualTo("冒涜する"));
    }

    [Test]
    public void TranslateExactOrLowerAscii_FallsBackToLowerAsciiKeyWithoutMissingKeyNoise()
    {
        WriteDictionary(("desecrate", "冒涜する"));

        var translated = StringHelpers.TranslateExactOrLowerAscii("Desecrate");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("冒涜する"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Desecrate"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryGetTranslationExactOrLowerAscii_FallsBackToLowerAsciiKeyWithoutMissingKeyNoise()
    {
        WriteDictionary(("flower fields", "花畑"));

        var translated = StringHelpers.TryGetTranslationExactOrLowerAscii("Flower Fields", out var value);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(value, Is.EqualTo("花畑"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Flower Fields"), Is.EqualTo(0));
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

        File.WriteAllText(
            Path.Combine(tempDirectory, "string-helpers.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
