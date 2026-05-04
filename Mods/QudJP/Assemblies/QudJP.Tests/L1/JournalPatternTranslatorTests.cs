using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class JournalPatternTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-journal-pattern-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
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

    [Test]
    public void Translate_AppliesSingleCapturePattern()
    {
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{0}に旅した。"));

        var translated = JournalPatternTranslator.Translate("You journeyed to Kyakukya.");

        Assert.That(translated, Is.EqualTo("Kyakukyaに旅した。"));
    }

    [Test]
    public void Translate_AppliesMultipleCapturePattern()
    {
        WriteDictionaryFile("date-l1.ja.json", new[] { ("5th", "第5"), ("Ut yara Ux", "ウト・ヤラ・ウクス") });
        WritePatternDictionary(("^On the (.+?) of (.+?), you abandoned all hope\\.$", "{t1}の{t0}日、あなたはすべての希望を捨てた。"));

        var translated = JournalPatternTranslator.Translate("On the 5th of Ut yara Ux, you abandoned all hope.");

        Assert.That(translated, Is.EqualTo("ウト・ヤラ・ウクスの第5日、あなたはすべての希望を捨てた。"));
    }

    [Test]
    public void Translate_AppliesDeathEntryPattern()
    {
        WriteDictionaryFile("date-l1.ja.json", new[] { ("10th", "第10"), ("Iyur Ut", "イユル・ウト") });
        WritePatternDictionary(("^On the (.+?) of (.+?), you were killed by a (.+?)\\.$", "{t1}の{t0}日、{2}に殺された。"));

        var translated = JournalPatternTranslator.Translate("On the 10th of Iyur Ut, you were killed by a 血まみれのウォーターヴァイン農家.");

        Assert.That(translated, Is.EqualTo("イユル・ウトの第10日、血まみれのウォーターヴァイン農家に殺された。"));
    }

    [Test]
    public void Translate_AppliesVillageHistoriesNotificationPattern()
    {
        WritePatternDictionary(("^You note this piece of information in the Village Histories > (.+?) section of your journal\\.[.!]?$", "この情報をジャーナルの「村の歴史 > {0}」欄に記録した。"));

        var translated = JournalPatternTranslator.Translate(
            "You note this piece of information in the Village Histories > テッガトゥム section of your journal.");

        Assert.That(translated, Is.EqualTo("この情報をジャーナルの「村の歴史 > テッガトゥム」欄に記録した。"));
    }

    [Test]
    public void Translate_SupportsTranslatedCaptures()
    {
        WriteDictionaryFile("dict-l1.ja.json", new[] { ("kyakukya", "キャクキャ") });
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var translated = JournalPatternTranslator.Translate("You journeyed to Kyakukya.");

        Assert.That(translated, Is.EqualTo("キャクキャに旅した。"));
    }

    [Test]
    public void Translate_ReturnsSourceUnchanged_WhenNoPatternMatches()
    {
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{0}に旅した。"));

        var source = "Something completely unrelated.";
        var translated = JournalPatternTranslator.Translate(source);

        Assert.That(translated, Is.EqualTo(source));
    }

    [Test]
    public void Translate_ReturnsEmptyString_WhenSourceIsNull()
    {
        WritePatternDictionary();

        Assert.That(JournalPatternTranslator.Translate(null), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_ReturnsEmptyString_WhenSourceIsEmpty()
    {
        WritePatternDictionary();

        Assert.That(JournalPatternTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_LoadsPatterns_FromJournalPatternFile()
    {
        WritePatternDictionary(
            ("^Notes: (.+)$", "備考: {0}"),
            ("^You journeyed to (.+?)\\.$", "{0}に旅した。"));

        var translated1 = JournalPatternTranslator.Translate("Notes: some lore about the world.");
        var translated2 = JournalPatternTranslator.Translate("You journeyed to Joppa.");

        Assert.That(translated1, Is.EqualTo("備考: some lore about the world."));
        Assert.That(translated2, Is.EqualTo("Joppaに旅した。"));
        Assert.That(JournalPatternTranslator.LoadInvocationCount, Is.EqualTo(1),
            "Patterns should be loaded exactly once (lazy + cached).");
    }

    [Test]
    public void Translate_ThrowsFileNotFoundException_WhenPatternFileMissing()
    {
        // Do not write the pattern file.
        Assert.Throws<FileNotFoundException>(() => JournalPatternTranslator.Translate("anything"));
    }

    [Test]
    public void Translate_ThrowsFileNotFoundException_WhenDefaultPrimaryFileMissing_InProductionMode()
    {
        // Arrange: set a localization root with no journal-patterns.ja.json → production mode
        // will resolve the primary default path to a non-existent file.
        var emptyLocalizationRoot = Path.Combine(Path.GetTempPath(), "qudjp-prod-mode-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyLocalizationRoot);
        try
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(emptyLocalizationRoot);
            // ResetForTests() internally clears patternFileOverrides → production mode.
            JournalPatternTranslator.ResetForTests();

            // Act & Assert: primary file missing in production must throw, not silently skip.
            Assert.Throws<FileNotFoundException>(() => JournalPatternTranslator.Translate("anything"));
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            if (Directory.Exists(emptyLocalizationRoot))
            {
                Directory.Delete(emptyLocalizationRoot, recursive: true);
            }

            // Restore test-mode override so TearDown does not fail.
            JournalPatternTranslator.ResetForTests();
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void Translate_AppliesZeroCapturePattern()
    {
        WritePatternDictionary(("^A \"SATED\" baetyl$", "「満足した」ベテル"));

        var translated = JournalPatternTranslator.Translate("A \"SATED\" baetyl");

        Assert.That(translated, Is.EqualTo("「満足した」ベテル"));
    }

    [Test]
    public void Translate_AppliesHistoricGossipPatternWithTranslatedCaptures()
    {
        WriteDictionaryFile(
            "historyspice-common.ja.json",
            new[] { ("some organization", "ある組織"), ("some party", "ある一団") });
        WritePatternDictionary(("^(.+?) repeatedly beat (.+?) at dice\\.$", "{t0}は{t1}を何度も賽子で打ち負かした。"));

        var translated = JournalPatternTranslator.Translate("some organization repeatedly beat some party at dice.");

        Assert.That(translated, Is.EqualTo("ある組織はある一団を何度も賽子で打ち負かした。"));
    }

    [Test]
    public void Translate_PreservesColorOwnershipForSpecialErosYellPattern()
    {
        WritePatternDictionary(("^E-Ros yells, 'I'm coming, (.+?)!'$", "E-Rosは「今行くよ、{0}！」と叫んだ"));

        var translated = JournalPatternTranslator.Translate("E-Ros yells, {{W|'I'm coming, リーダー!'}}");

        Assert.That(translated, Is.EqualTo("E-Rosは{{W|「今行くよ、リーダー！」}}と叫んだ"));
    }

    [Test]
    public void Translate_DoesNotReapplySourceCaptureMarkup_WhenTranslatedCaptureOwnsMarkup()
    {
        WriteDictionaryFile(
            "dict-l1.ja.json",
            new[] { ("bloody Tam, dromad merchant [sitting]", "{{r|血まみれの}}Tam、ドロマド商人 [座っている]") });
        WritePatternDictionary(("^You were killed by (.+?)\\.$", "{t0}に殺された。"));

        var translated = JournalPatternTranslator.Translate(
            "You were killed by {{r|bloody}} Tam, dromad merchant [sitting].");

        Assert.Multiple(() =>
        {
            Assert.That(
                translated,
                Is.EqualTo("{{r|血まみれの}}Tam、ドロマド商人 [座っている]に殺された。"));
            Assert.That(translated, Does.Not.Contain("{{r|{{r|"));
            Assert.That(translated, Does.Not.Match("血ま.*}}.*みれ"));
            Assert.That(translated, Does.Not.Match("\\[座ってい}}る\\]"));
        });
    }

    [Test]
    public void Translate_PreservesSourceWholeCaptureWrapper_WhenTranslatedCaptureOwnsMarkup()
    {
        WriteDictionaryFile("dict-l1.ja.json", new[] { ("bloody Tam", "{{r|血まみれの}}Tam") });
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var translated = JournalPatternTranslator.Translate("You journeyed to {{C|bloody Tam}}.");

        Assert.That(translated, Is.EqualTo("{{C|{{r|血まみれの}}Tam}}に旅した。"));
    }

    [Test]
    public void Translate_DoesNotReapplyPartialSourceMarkupInSegmentedTranslatedCapture()
    {
        WriteDictionaryFile("dict-l1.ja.json", new[] { ("bloody Tam", "{{r|血まみれの}}Tam") });
        WritePatternDictionary(("^Notes: (.+)$", "備考: {t0}"));

        var translated = JournalPatternTranslator.Translate("Notes: {{r|bloody}} Tam");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("備考: {{r|血まみれの}}Tam"));
            Assert.That(translated, Does.Not.Contain("{{r|{{r|"));
            Assert.That(translated, Does.Not.Match("血ま.*}}.*みれ"));
        });
    }

    [Test]
    public void Translate_AppliesWakingDreamGospelPattern()
    {
        WritePatternDictionary(
            (
                "^<spice\\.commonPhrases\\.blessed\\.!random\\.capitalize> =name= dreamed of a thousand years of peace, and the people of Qud <spice\\.history\\.gospels\\.Celebration\\.LateSultanate\\.!random> in <spice\\.commonPhrases\\.celebration\\.!random>\\.$",
                "<spice.commonPhrases.blessed.!random.capitalize>=name=は千年の平和を夢見、クッドの民は<spice.commonPhrases.celebration.!random>で<spice.history.gospels.Celebration.LateSultanate.!random>した。"));

        var translated = JournalPatternTranslator.Translate(
            "<spice.commonPhrases.blessed.!random.capitalize> =name= dreamed of a thousand years of peace, and the people of Qud <spice.history.gospels.Celebration.LateSultanate.!random> in <spice.commonPhrases.celebration.!random>.");

        Assert.That(
            translated,
            Is.EqualTo("<spice.commonPhrases.blessed.!random.capitalize>=name=は千年の平和を夢見、クッドの民は<spice.commonPhrases.celebration.!random>で<spice.history.gospels.Celebration.LateSultanate.!random>した。"));
    }

    [Test]
    public void Translate_AppliesAbsorbablePsycheGospelPattern()
    {
        WritePatternDictionary(
            (
                "^In the month of (.+?) of (.+?), =name= was challenged by <spice\\.commonPhrases\\.pretender\\.!random\\.article> to a duel over the rights of (.+?)\\. =name= won and had the pretender's psyche kibbled and absorbed into (.+?) own\\.$",
                "{1}年{0}、=name= は {2}の権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name= は勝利し、偽者の精神を刻んで吸収した。"));

        var translated = JournalPatternTranslator.Translate(
            "In the month of Ut yara Ux of 1012, =name= was challenged by <spice.commonPhrases.pretender.!random.article> to a duel over the rights of the Mechanimists. =name= won and had the pretender's psyche kibbled and absorbed into their own.");

        Assert.That(
            translated,
            Is.EqualTo("1012年Ut yara Ux、=name= は the Mechanimistsの権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name= は勝利し、偽者の精神を刻んで吸収した。"));
    }

    [Test]
    public void GetPatternLoadSummaryForTests_ContainsJournalPatternTranslator()
    {
        WritePatternDictionary(("^Notes: (.+)$", "備考: {0}"));

        _ = JournalPatternTranslator.Translate("Notes: test");

        var summary = JournalPatternTranslator.GetPatternLoadSummaryForTests();
        Assert.That(summary, Does.Contain("JournalPatternTranslator"));
        Assert.That(summary, Does.Contain("1 pattern(s)"));
    }

    [Test]
    public void ResolvePatternFilePath_DefaultsToJournalPatternsFile()
    {
        // When no override is set, the file should resolve to journal-patterns.ja.json.
        // We verify this indirectly: the summary should include "journal-patterns.ja.json".
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        WritePatternDictionary(("^test$", "テスト"));

        _ = JournalPatternTranslator.Translate("test");

        var summary = JournalPatternTranslator.GetPatternLoadSummaryForTests();
        Assert.That(summary, Does.Contain("journal-patterns"));
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

    private void WriteDictionaryFile(string fileName, (string key, string text)[] entries)
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
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
