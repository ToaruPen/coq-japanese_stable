using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DescriptionTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-description-text-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", Utf8WithoutBom);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateShortDescription_AppliesVillageDescriptionPattern()
    {
        WriteExactDictionary(
            ("some organization", "ある組織"),
            ("kin", "血縁"),
            ("conclave", "会合"));
        WritePatternDictionary((
            "^(.+?), there's a ((?i:gathering|conclave|congregation|settlement|band|flock|society)) of (.+?) and their ((?i:folk|communities|kindred|families|kin|kind|kinsfolk|tribe|clan))\\.$",
            "{0}、{t2}とその{t3}の{t1}がある。"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "sun-baked ruins, there's a conclave of some organization and their kin.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("sun-baked ruins、ある組織とその血縁の会合がある。"));
    }

    [Test]
    public void TranslateLongDescription_PreservesColoredFactionTarget_InDispositionLine()
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "Loved by {{C|the Barathrumites}}.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("{{C|the Barathrumites}}に愛されている。"));
    }

    [Test]
    public void TranslateLongDescription_PreservesWholeLineWrapper_InDispositionLine()
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "{{W|Loved by {{C|the Barathrumites}}.}}",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("{{W|{{C|the Barathrumites}}に愛されている。}}"));
    }

    [Test]
    public void TranslateLongDescription_PreservesRelationWrapper_InDispositionLine()
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "{{C|Loved by}} the Barathrumites.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("the Barathrumitesに{{C|愛されている}}。"));
    }

    [Test]
    public void TranslateLongDescription_TranslatesReasonBearingDispositionLine()
    {
        WriteExactDictionary(("giving alms to pilgrims", "巡礼者に施しをしたため"));

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "Admired by {{C|the Mechanimists}} for giving alms to pilgrims.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("{{C|the Mechanimists}}に敬愛されている。理由: 巡礼者に施しをしたため。"));
    }

    [Test]
    public void TranslateLongDescription_FallbackToEnglish_WhenNoTranslationMatches()
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "Admired by {{C|the Mechanimists}} for an unknown deed.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("{{C|the Mechanimists}}に敬愛されている。理由: an unknown deed。"));
    }

    [Test]
    public void TranslateLongDescription_EmptyInputReturnsEmpty()
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            string.Empty,
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void TranslateLongDescription_PreservesDirectTranslationMarker()
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "\u0001Already translated",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("\u0001Already translated"));
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"patterns\":[");
        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(patternFilePath, builder.ToString(), Utf8WithoutBom);
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
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
            Path.Combine(dictionaryDirectory, "historyspice-common.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
