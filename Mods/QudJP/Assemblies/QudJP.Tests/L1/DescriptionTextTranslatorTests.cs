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

    [Test]
    public void TranslateLongDescription_TranslatesLinesInsideMultilineColorWrappers()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"));
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class: Axe (cleaves armor on critical hit)", "武器カテゴリ: 斧（クリティカル時に装甲破砕）"),
            ("Painted: This item is painted with a scene from the life of the ancient {0}:", "彩色: この品には古代の{0}の生涯の一場面が描かれている:"),
            ("sultan", "スルタン"));

        var source =
            "{{rules|Strength Bonus Cap: 1\nWeapon Class: Axe (cleaves armor on critical hit)}}\n" +
            "{{cyan|Painted: This item is painted with a scene from the life of the ancient sultan クホマスプ II:\n\nIn 4834 BR}}";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo(
                "{{rules|筋力ボーナス上限: 1\n武器カテゴリ: 斧（クリティカル時に装甲破砕）}}\n" +
                "{{cyan|彩色: この品には古代のスルタン クホマスプ IIの生涯の一場面が描かれている:\n\nIn 4834 BR}}"));
    }

    [Test]
    public void TranslateLongDescription_TranslatesContinuationLineWithNestedColorWrapper()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"));
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class: Axe (cleaves armor on critical hit)", "武器カテゴリ: 斧（クリティカル時に装甲破砕）"));

        var source =
            "{{rules|Strength Bonus Cap: 1\n" +
            "{{Y|Weapon Class: Axe (cleaves armor on critical hit)}}}}";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo(
                "{{rules|筋力ボーナス上限: 1\n" +
                "{{Y|武器カテゴリ: 斧（クリティカル時に装甲破砕）}}}}"));
    }

    [Test]
    public void TranslateLongDescription_TranslatesLinesInsideSplitTmpColorWrapper()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"));
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class: Axe (cleaves armor on critical hit)", "武器カテゴリ: 斧（クリティカル時に装甲破砕）"));

        var source =
            "<color=yellow>Strength Bonus Cap: 1\n" +
            "Weapon Class: Axe (cleaves armor on critical hit)</color>";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo(
                "<color=yellow>筋力ボーナス上限: 1\n" +
                "武器カテゴリ: 斧（クリティカル時に装甲破砕）</color>"));
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
        WriteDictionary("historyspice-common.ja.json", entries);
    }

    private void WriteDictionary(string fileName, params (string key, string text)[] entries)
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
            Path.Combine(dictionaryDirectory, fileName),
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
