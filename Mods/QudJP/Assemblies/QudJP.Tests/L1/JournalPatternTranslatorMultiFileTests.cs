using System;
using System.IO;
using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class JournalPatternTranslatorMultiFileTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-multifile-l1",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        // Create a Dictionaries subdirectory with an empty journal-patterns.ja.json so that
        // production-mode path resolution (fileIndex==0) finds the primary file and does not throw.
        var dictDirectory = Path.Combine(tempDirectory, "Dictionaries");
        Directory.CreateDirectory(dictDirectory);
        File.WriteAllText(
            Path.Combine(dictDirectory, "journal-patterns.ja.json"),
            "{\"entries\":[],\"patterns\":[]}\n",
            Utf8WithoutBom);

        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);

        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string WritePatternFile(string fileName, string contents)
    {
        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, contents, Utf8WithoutBom);
        return path;
    }

    private static string PatternFileBody(params (string Pattern, string Template)[] entries)
    {
        var sb = new StringBuilder();
        sb.Append("{\"entries\":[],\"patterns\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var (pattern, template) = entries[i];
            sb.Append("{\"pattern\":\"").Append(pattern.Replace("\\", "\\\\").Replace("\"", "\\\""))
              .Append("\",\"template\":\"").Append(template.Replace("\\", "\\\\").Replace("\"", "\\\""))
              .Append("\"}");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    [Test]
    public void Translate_FirstFilePatternWins_WhenSameInputMatchesBoth()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello world$", "First template")));
        var second = WritePatternFile("second.json",
            PatternFileBody(("^Hello world$", "Second template")));

        JournalPatternTranslator.SetPatternFilesForTests(first, second);

        Assert.That(JournalPatternTranslator.Translate("Hello world"), Is.EqualTo("First template"));
    }

    [Test]
    public void Translate_SecondFilePatternMatches_WhenFirstFileDoesNotMatch()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Apple$", "りんご")));
        var second = WritePatternFile("second.json",
            PatternFileBody(("^Banana$", "バナナ")));

        JournalPatternTranslator.SetPatternFilesForTests(first, second);

        Assert.That(JournalPatternTranslator.Translate("Banana"), Is.EqualTo("バナナ"));
    }

    [Test]
    public void SetPatternFilesForTests_NullArray_ResetsToDefaults()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Override$", "OverrideOK")));
        JournalPatternTranslator.SetPatternFilesForTests(first);
        Assert.That(JournalPatternTranslator.Translate("Override"), Is.EqualTo("OverrideOK"));

        JournalPatternTranslator.SetPatternFilesForTests((string[]?)null);

        Assert.That(JournalPatternTranslator.Translate("Override"), Is.EqualTo("Override"));
    }

    [Test]
    public void SetPatternFilesForTests_EmptyArray_ResetsToDefaults()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Override$", "OverrideOK")));
        JournalPatternTranslator.SetPatternFilesForTests(first);
        Assert.That(JournalPatternTranslator.Translate("Override"), Is.EqualTo("OverrideOK"));

        JournalPatternTranslator.SetPatternFilesForTests(Array.Empty<string>());

        Assert.That(JournalPatternTranslator.Translate("Override"), Is.EqualTo("Override"));
    }

    [Test]
    public void SetPatternFileForTests_LegacyApi_NullStillResetsToDefaults()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Legacy$", "LegacyOK")));
        JournalPatternTranslator.SetPatternFileForTests(first);
        Assert.That(JournalPatternTranslator.Translate("Legacy"), Is.EqualTo("LegacyOK"));

        JournalPatternTranslator.SetPatternFileForTests(null);
        Assert.That(JournalPatternTranslator.Translate("Legacy"), Is.EqualTo("Legacy"));
    }

    [Test]
    public void Translate_ThrowsFileNotFoundException_WhenFirstFileMissing()
    {
        var second = WritePatternFile("second.json",
            PatternFileBody(("^Hello$", "こんにちは")));
        var ghost = Path.Combine(tempDirectory, "does-not-exist.json");

        JournalPatternTranslator.SetPatternFilesForTests(ghost, second);

        Assert.Throws<FileNotFoundException>(() => JournalPatternTranslator.Translate("Hello"));
    }

    [Test]
    public void Translate_ThrowsFileNotFoundException_WhenSecondFileMissing()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello$", "こんにちは")));
        var ghost = Path.Combine(tempDirectory, "does-not-exist.json");

        JournalPatternTranslator.SetPatternFilesForTests(first, ghost);

        Assert.Throws<FileNotFoundException>(() => JournalPatternTranslator.Translate("Hello"));
    }

    [Test]
    public void Translate_ThrowsInvalidDataException_WhenSecondFileMalformedJson()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello$", "こんにちは")));
        var malformed = Path.Combine(tempDirectory, "malformed.json");
        File.WriteAllText(malformed, "{ this is not valid json", Utf8WithoutBom);

        JournalPatternTranslator.SetPatternFilesForTests(first, malformed);

        Assert.Throws<InvalidDataException>(() => JournalPatternTranslator.Translate("Hello"));
    }

    [Test]
    public void Translate_ThrowsInvalidDataException_WhenSecondFileHasNoPatternsArray()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello$", "こんにちは")));
        var noPatterns = Path.Combine(tempDirectory, "no-patterns.json");
        File.WriteAllText(noPatterns, "{\"entries\":[]}", Utf8WithoutBom);

        JournalPatternTranslator.SetPatternFilesForTests(first, noPatterns);

        Assert.Throws<InvalidDataException>(() => JournalPatternTranslator.Translate("Hello"));
    }

    [Test]
    public void LoadPatterns_DuplicateAcrossFiles_LogsDuplicateInLoadSummary()
    {
        var first = WritePatternFile("first.json",
            PatternFileBody(("^Hello world$", "First template")));
        var second = WritePatternFile("second.json",
            PatternFileBody(("^Hello world$", "Second template")));

        JournalPatternTranslator.SetPatternFilesForTests(first, second);
        _ = JournalPatternTranslator.Translate("Hello world");  // force load

        var summary = JournalPatternTranslator.GetPatternLoadSummaryForTests();
        Assert.That(summary, Does.Contain("duplicate"),
            "load summary must report cross-file duplicate count for observability");
    }
}
