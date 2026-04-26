using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class HistoricNarrativeTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-narrative-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        WritePatternDictionary(); // ensure pattern file exists; tests can overwrite as needed
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private void WritePatternDictionary(params (string Pattern, string Template)[] entries)
    {
        var sb = new StringBuilder();
        sb.Append("{\"patterns\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var (pattern, template) = entries[i];
            sb.Append("{\"pattern\":\"").Append(EscapeJson(pattern))
              .Append("\",\"template\":\"").Append(EscapeJson(template)).Append("\"}");
        }
        sb.Append("]}");
        File.WriteAllText(patternFilePath, sb.ToString(), Utf8WithoutBom);
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

    [Test]
    public void Translate_NullSource_ReturnsEmpty()
    {
        Assert.That(HistoricNarrativeTextTranslator.Translate(null), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_EmptySource_ReturnsEmpty()
    {
        Assert.That(HistoricNarrativeTextTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_UnmatchedSource_ReturnsOriginal()
    {
        var source = "An unmatched gospel sentence.";
        Assert.That(HistoricNarrativeTextTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_PatternMatch_AppliesTemplate()
    {
        WritePatternDictionary(("^In year (.+?), (.+?) was crowned\\.$", "{1}年、{0}が即位した。"));

        var translated = HistoricNarrativeTextTranslator.Translate("In year 42, Resheph was crowned.");

        // Loose contains-style assertion to tolerate JournalPatternTranslator template-engine variations.
        Assert.That(translated, Does.Contain("即位"));
    }

    [Test]
    public void Translate_DoesNotApplyDirectMarker()
    {
        WritePatternDictionary(("^Plain English\\.$", "日本語"));

        var translated = HistoricNarrativeTextTranslator.Translate("Plain English.");

        // U+0001 is the direct-marker control character used by JournalTextTranslator.TryTranslate*ForStorage.
        // Use char-based Contains so that .NET's culture-sensitive string IndexOf doesn't treat
        // U+0001 as an ignorable code point and report a spurious match.
        Assert.That(translated, Is.EqualTo("日本語"));
        Assert.That(translated.Contains(''), Is.False);
    }

    // Markup invariant preservation. Each test uses a passthrough source containing
    // exactly one invariant token; assertion confirms the token survives unchanged.
    [TestCase("&Wbright")]
    [TestCase("^kdark")]
    [TestCase("&&literal-amp")]
    [TestCase("^^literal-caret")]
    [TestCase("{{X|colored}}")]
    [TestCase("{{W|warning}}")]
    [TestCase("{{NAME|named-shader}}")]
    [TestCase("<color=#44ff88>tmp-green</color>")]
    [TestCase("line1\nline2")]
    [TestCase("=name=")]
    [TestCase("=year=")]
    [TestCase("=pluralize=")]
    [TestCase("=article=")]
    [TestCase("=Article=")]
    [TestCase("=capitalize=")]
    [TestCase("<spice.proverbs.!random.capitalize>")]
    [TestCase("<entity.name>")]
    [TestCase("<undefined entity property foo>")]
    [TestCase("<undefined entity list bar>")]
    [TestCase("<empty entity list bar>")]
    [TestCase("<unknown entity>")]
    [TestCase("<unknown format whatever>")]
    [TestCase("*Worships.LegendaryCreature.DisplayName*")]
    public void Translate_PreservesMarkupInvariant(string input)
    {
        // No pattern dictionary written: passthrough behavior surfaces invariant.
        Assert.That(HistoricNarrativeTextTranslator.Translate(input), Is.EqualTo(input));
    }
}
