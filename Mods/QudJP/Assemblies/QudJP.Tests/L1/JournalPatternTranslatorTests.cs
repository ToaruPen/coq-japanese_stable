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
    public void Translate_AppliesArticlelessDeathEntryPattern()
    {
        WriteDictionaryFile("date-l1.ja.json", new[] { ("5th", "第5"), ("Tishru ii Ux", "ティシュル II・ウクス") });
        WritePatternDictionary(("^On the (.+?) of (.+?), you were killed by (.+?)\\.$", "{t1}の{t0}日、{t2}に殺された。"));

        var translated = JournalPatternTranslator.Translate("On the 5th of Tishru ii Ux, you were killed by イジル, 村の修理工.");

        Assert.That(translated, Is.EqualTo("ティシュル II・ウクスの第5日、イジル, 村の修理工に殺された。"));
    }

    [Test]
    public void Translate_AppliesArticlelessDeathEntryPattern_WithEnglishCaptureFallback()
    {
        WriteDictionaryFile("date-l1.ja.json", new[] { ("5th", "第5"), ("Tishru ii Ux", "ティシュル II・ウクス") });
        WritePatternDictionary(("^On the (.+?) of (.+?), you were killed by (.+?)\\.$", "{t1}の{t0}日、{t2}に殺された。"));

        var translated = JournalPatternTranslator.Translate("On the 5th of Tishru ii Ux, you were killed by a chrome pyramid.");

        Assert.That(translated, Is.EqualTo("ティシュル II・ウクスの第5日、chrome pyramidに殺された。"));
    }

    [Test]
    public void Translate_AppliesArticlelessDeathEntryPattern_TranslatesHistoricSpiceGeneratedCapture()
    {
        WriteDictionaryFile(
            "date-l1.ja.json",
            new[] { ("5th", "第5"), ("Tishru ii Ux", "ティシュル II・ウクス") });
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", new[] { ("desiccated", "乾ききった"), ("spectre", "亡霊") });
        WritePatternDictionary(("^On the (.+?) of (.+?), you were killed by (.+?)\\.$", "{t1}の{t0}日、{t2}に殺された。"));

        var translated = JournalPatternTranslator.Translate(
            "On the 5th of Tishru ii Ux, you were killed by the Desiccated Spectre.");

        Assert.That(translated, Is.EqualTo("ティシュル II・ウクスの第5日、乾ききった亡霊に殺された。"));
    }

    [Test]
    public void Translate_AppliesArticlelessDeathEntryPattern_PreservesColorTags()
    {
        WriteDictionaryFile("date-l1.ja.json", new[] { ("5th", "第5"), ("Tishru ii Ux", "ティシュル II・ウクス") });
        WritePatternDictionary(("^On the (.+?) of (.+?), you were killed by (.+?)\\.$", "{t1}の{t0}日、{t2}に殺された。"));

        var translated = JournalPatternTranslator.Translate(
            "On the 5th of Tishru ii Ux, you were killed by <color=#ff0>イジル, 村の修理工</color>.");

        Assert.That(translated, Is.EqualTo("ティシュル II・ウクスの第5日、<color=#ff0>イジル, 村の修理工</color>に殺された。"));
    }

    [Test]
    public void Translate_TranslatesArticlelessLocationCapture()
    {
        WriteDictionaryFile(
            "location-l1.ja.json",
            new[] { ("snapjaw fort", "スナップジョーの砦"), ("Settlements", "集落") });
        WritePatternDictionary(("^You note the location of (.+?) in the Locations > (.+?) section of your journal\\.[.!]?$", "ジャーナルの「場所 > {t1}」欄に{t0}の場所を記録した。"));

        var translated = JournalPatternTranslator.Translate(
            "You note the location of a snapjaw fort in the Locations > Settlements section of your journal.");

        Assert.That(translated, Is.EqualTo("ジャーナルの「場所 > 集落」欄にスナップジョーの砦の場所を記録した。"));
    }

    [Test]
    public void Translate_TranslatesArticlelessLocationCapture_WithEnglishCaptureFallback()
    {
        WriteDictionaryFile("location-l1.ja.json", new[] { ("Settlements", "集落") });
        WritePatternDictionary(("^You note the location of (.+?) in the Locations > (.+?) section of your journal\\.[.!]?$", "ジャーナルの「場所 > {t1}」欄に{t0}の場所を記録した。"));

        var translated = JournalPatternTranslator.Translate(
            "You note the location of a forgotten ruin in the Locations > Settlements section of your journal.");

        Assert.That(translated, Is.EqualTo("ジャーナルの「場所 > 集落」欄にforgotten ruinの場所を記録した。"));
    }

    [Test]
    public void Translate_TranslatesArticlelessLocationCapture_PreservesColorTags()
    {
        WriteDictionaryFile(
            "location-l1.ja.json",
            new[] { ("snapjaw fort", "スナップジョーの砦"), ("Settlements", "集落") });
        WritePatternDictionary(("^You note the location of (.+?) in the Locations > (.+?) section of your journal\\.[.!]?$", "ジャーナルの「場所 > {t1}」欄に{t0}の場所を記録した。"));

        var translated = JournalPatternTranslator.Translate(
            "You note the location of <color=#ff0>a snapjaw fort</color> in the Locations > Settlements section of your journal.");

        Assert.That(translated, Is.EqualTo("ジャーナルの「場所 > 集落」欄に<color=#ff0>スナップジョーの砦</color>の場所を記録した。"));
    }

    [Test]
    public void Translate_AppliesLateSultanateMarkOfDeathRecoveryPattern()
    {
        WritePatternDictionary(("^You recover the Mark of Death of the late sultanate\\.$", "亡きスルタンの死の刻印を回収した。"));

        var translated = JournalPatternTranslator.Translate("You recover the Mark of Death of the late sultanate.");

        Assert.That(translated, Is.EqualTo("亡きスルタンの死の刻印を回収した。"));
    }

    [Test]
    public void Translate_AppliesLateSultanateMarkOfDeathRecoveryPattern_FallsBackToEnglishWhenPatternMissing()
    {
        WritePatternDictionary();

        const string source = "You recover the Mark of Death of the late sultanate.";

        Assert.That(JournalPatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_AppliesLateSultanateMarkOfDeathRecoveryPattern_PreservesColorTags()
    {
        WritePatternDictionary(("^You recover the Mark of Death of the late sultanate\\.$", "亡きスルタンの死の刻印を回収した。"));

        var translated = JournalPatternTranslator.Translate(
            "<color=#ff0>You recover the Mark of Death of the late sultanate.</color>");

        Assert.That(translated, Is.EqualTo("<color=#ff0>亡きスルタンの死の刻印を回収した。</color>"));
    }

    [Test]
    public void Translate_TranslatesChallengeSultanCrownedDuelPattern()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[]
            {
                ("water barons", "水の男爵たち"),
                ("seized the crown", "王冠を奪い取った"),
            });
        WritePatternDictionary((
            "^((?:In|Early in|Late in|Sometime in))\\ (.+?)\\ (?:BR|AR),\\ (.+?)\\ challenged\\ the\\ sultan\\ of\\ Qud\\ to\\ a\\ duel\\ (?:over\\ the\\ rights\\ of|over\\ an\\ ordinance\\ prohibiting\\ the\\ practice\\ of|over\\ the\\ sanctioned\\ persecution\\ of)\\ (.+?)\\.\\ (.+?)\\ won\\ and\\ (.+?)\\.\\ (.+?)\\ was\\ (.+?)\\ years\\ old\\.$",
            "{t1}{t0}、{t2}は{t3}を巡ってクッドのスルタンに決闘を挑んだ。{t4}は勝利し、{t5}。{t6}は{t7}歳であった。"));

        var translated = JournalPatternTranslator.Translate(
            "In 1925 BR, ナレドゥクフト challenged the sultan of Qud to a duel over the rights of water barons. She won and seized the crown. She was 49 years old.");

        Assert.That(
            translated,
            Is.EqualTo("1925年、ナレドゥクフトは水の男爵たちを巡ってクッドのスルタンに決闘を挑んだ。その者は勝利し、王冠を奪い取った。その者は49歳であった。"));
    }

    [Test]
    public void Translate_TranslatesChallengeSultanPretenderWhileLeadingPattern()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[]
            {
                ("aspirant", "野心家"),
                ("mollusks", "軟体動物"),
                ("drawn and quartered", "八つ裂きにされた"),
            });
        WritePatternDictionary((
            "^While\\ leading\\ a\\ small\\ army\\ in\\ (.+?),\\ (.+?)\\ was\\ challenged\\ by\\ (?:a|an|the)\\ (.+?)\\ to\\ a\\ duel\\ (?:over\\ the\\ rights\\ of|over\\ an\\ ordinance\\ mandating\\ the\\ practice\\ of|over\\ the\\ sanctioned\\ persecution\\ of)\\ (.+?)\\.\\ (.+?)\\ lost\\ and\\ was\\ (.+?)\\.\\ (.+?)\\ was\\ (.+?)\\ years\\ old\\.$",
            "{t0}で小軍を率いていたとき、{t1}は{t3}を巡って{t2}に決闘を挑まれた。{t4}は敗れ、{t5}。{t6}は{t7}歳であった。"));

        var translated = JournalPatternTranslator.Translate(
            "While leading a small army in The Great Salt Desert, ナレドゥクフト was challenged by an aspirant to a duel over the rights of mollusks. She lost and was drawn and quartered. She was 49 years old.");

        Assert.That(
            translated,
            Is.EqualTo("Great Salt Desertで小軍を率いていたとき、ナレドゥクフトは軟体動物を巡って野心家に決闘を挑まれた。その者は敗れ、八つ裂きにされた。その者は49歳であった。"));
    }

    [Test]
    public void Translate_ChallengeSultanDuelPatterns_CoverFallbackAndEdges()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[]
            {
                ("water barons", "水の男爵たち"),
                ("seized the crown", "王冠を奪い取った"),
            });
        WritePatternDictionary((
            "^((?:In|Early in|Late in|Sometime in))\\ (.+?)\\ (?:BR|AR),\\ (.+?)\\ challenged\\ the\\ sultan\\ of\\ Qud\\ to\\ a\\ duel\\ (?:over\\ the\\ rights\\ of|over\\ an\\ ordinance\\ prohibiting\\ the\\ practice\\ of|over\\ the\\ sanctioned\\ persecution\\ of)\\ (.+?)\\.\\ (.+?)\\ won\\ and\\ (.+?)\\.\\ (.+?)\\ was\\ (.+?)\\ years\\ old\\.$",
            "{t1}{t0}、{t2}は{t3}を巡ってクッドのスルタンに決闘を挑んだ。{t4}は勝利し、{t5}。{t6}は{t7}歳であった。"));

        const string source =
            "In 1925 BR, ナレドゥクフト challenged the sultan of Qud to a duel over the rights of water barons. She won and seized the crown. She was 49 years old.";

        Assert.Multiple(() =>
        {
            Assert.That(
                JournalPatternTranslator.Translate(
                    "In 1925 BR, ナレドゥクフト challenged the sultan of Qud to a duel over the rights of <color=#ff0>water barons</color>. She won and seized the crown. She was 49 years old."),
                Is.EqualTo("1925年、ナレドゥクフトは<color=#ff0>水の男爵たち</color>を巡ってクッドのスルタンに決闘を挑んだ。その者は勝利し、王冠を奪い取った。その者は49歳であった。"));
            Assert.That(JournalPatternTranslator.Translate("\u0001" + source), Is.EqualTo("\u0001" + source));
            Assert.That(JournalPatternTranslator.Translate("A different chronicle entry."), Is.EqualTo("A different chronicle entry."));
            Assert.That(JournalPatternTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Translate_NewArticlelessAndDeathPatterns_ReturnEmptyString_WhenSourceIsEmpty()
    {
        WritePatternDictionary(
            ("^On the (.+?) of (.+?), you were killed by (.+?)\\.$", "{t1}の{t0}日、{t2}に殺された。"),
            ("^You note the location of (.+?) in the Locations > (.+?) section of your journal\\.[.!]?$", "ジャーナルの「場所 > {t1}」欄に{t0}の場所を記録した。"),
            ("^You recover the Mark of Death of the late sultanate\\.$", "亡きスルタンの死の刻印を回収した。"));

        Assert.That(JournalPatternTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
    }

    [TestCase("\u0001On the 5th of Tishru ii Ux, you were killed by イジル, 村の修理工.")]
    [TestCase("\u0001You note the location of a snapjaw fort in the Locations > Settlements section of your journal.")]
    [TestCase("\u0001You recover the Mark of Death of the late sultanate.")]
    public void Translate_NewArticlelessAndDeathPatterns_PreserveDirectMarkerInput(string source)
    {
        WritePatternDictionary(
            ("^On the (.+?) of (.+?), you were killed by (.+?)\\.$", "{t1}の{t0}日、{t2}に殺された。"),
            ("^You note the location of (.+?) in the Locations > (.+?) section of your journal\\.[.!]?$", "ジャーナルの「場所 > {t1}」欄に{t0}の場所を記録した。"),
            ("^You recover the Mark of Death of the late sultanate\\.$", "亡きスルタンの死の刻印を回収した。"));

        Assert.That(JournalPatternTranslator.Translate(source), Is.EqualTo(source));
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
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[] { ("some organization", "ある組織"), ("some party", "ある一団") });
        WritePatternDictionary(("^(.+?) repeatedly beat (.+?) at dice\\.$", "{t0}は{t1}を何度も賽子で打ち負かした。"));

        var translated = JournalPatternTranslator.Translate("some organization repeatedly beat some party at dice.");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("ある組織はある一団を何度も賽子で打ち負かした。"));
            Assert.That(Translator.Translate("some organization"), Is.EqualTo("some organization"));
        });
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
    public void Translate_TranslatesGeneratedRelationshipTitleCapture()
    {
        WritePatternDictionary(("^You defeated (.+?)\\.$", "{t0}を倒した。"));

        var translated = JournalPatternTranslator.Translate(
            "You defeated leader of the シャッガンナ Pest Flock.");

        Assert.That(translated, Is.EqualTo("シャッガンナ Pest Flockの指導者を倒した。"));
    }

    [Test]
    public void Translate_TranslatesGeneratedRelationshipTitleCapture_PreservesColorTags()
    {
        WritePatternDictionary(("^You defeated (.+?)\\.$", "{t0}を倒した。"));

        var translated = JournalPatternTranslator.Translate(
            "You defeated {{M|leader of the シャッガンナ Pest Flock}}.");

        Assert.That(translated, Is.EqualTo("{{M|シャッガンナ Pest Flockの指導者}}を倒した。"));
    }

    [Test]
    public void Translate_TranslatesCapitalizedRelationshipTitleCapture()
    {
        WriteDictionaryFile("journal-test.ja.json", new[] { ("Farmers' Guild", "農民のギルド") });
        WritePatternDictionary(("^You defeated (.+?)\\.$", "{t0}を倒した。"));

        var translated = JournalPatternTranslator.Translate("You defeated Leader of the Farmers' Guild.");

        Assert.That(translated, Is.EqualTo("農民のギルドの指導者を倒した。"));
    }

    [Test]
    public void Translate_TranslatesCompactHistoricItemNameInsideCommaSeparatedCapture()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[] { ("antelope", "アンテロープ"), ("gift", "賜物") });
        WritePatternDictionary(("^A wedding gift they called (.+?)\\.$", "婚礼の贈り物は{t0}と呼ばれた。"));

        var translated = JournalPatternTranslator.Translate("A wedding gift they called Betrothedecus, antelopegift.");

        Assert.That(translated, Is.EqualTo("婚礼の贈り物はBetrothedecus、アンテロープの賜物と呼ばれた。"));
    }

    [Test]
    public void Translate_TranslatesAbdicateSuccessorAnnalWithExpandedHistorySpiceCaptures()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[]
            {
                ("with malicious soldering", "悪意あるはんだ付け"),
                ("with poisonous gas", "毒ガスで"),
                ("disappeared", "姿を消した"),
                ("shining", "輝く"),
                ("visage", "顔立ち"),
            });
        WritePatternDictionary((
            "^((?:In|Early in|Late in|Sometime in))\\ (.+?)\\ (?:BR|AR),\\ (.+?),\\ the\\ sultan\\ of\\ Qud\\ (.+?)\\.\\ Because\\ of\\ (.+?),\\ (.+?)\\ was\\ chosen\\ as\\ the\\ successor\\.$",
            "{t1}{t0}、{t2}、クッドのスルタンは{t3}。{t4}のため、{t5}が後継者に選ばれた。"));

        var translated = JournalPatternTranslator.Translate(
            "Late in 4100 AR, after murdering a popular rival with malicious soldering, the sultan of Qud disappeared. Because of シビブの shining visage, they was chosen as the successor.");

        Assert.That(
            translated,
            Is.EqualTo("4100年末、人気のあるライバルを悪意あるはんだ付けで殺したあと、クッドのスルタンは姿を消した。シビブの輝く顔立ちのため、彼らが後継者に選ばれた。"));

        var translatedParticleBoundary = JournalPatternTranslator.Translate(
            "Late in 4100 AR, after murdering a popular rival with poisonous gas, the sultan of Qud disappeared. Because of シビブの shining visage, they was chosen as the successor.");

        Assert.That(
            translatedParticleBoundary,
            Is.EqualTo("4100年末、人気のあるライバルを毒ガスで殺したあと、クッドのスルタンは姿を消した。シビブの輝く顔立ちのため、彼らが後継者に選ばれた。"));

        var translatedCapitalIt = JournalPatternTranslator.Translate(
            "Late in 4100 AR, after murdering a popular rival with malicious soldering, the sultan of Qud disappeared. Because of シビブの shining visage, It was chosen as the successor.");

        Assert.That(
            translatedCapitalIt,
            Is.EqualTo("4100年末、人気のあるライバルを悪意あるはんだ付けで殺したあと、クッドのスルタンは姿を消した。シビブの輝く顔立ちのため、それが後継者に選ばれた。"));

        var translatedEnglishPossessive = JournalPatternTranslator.Translate(
            "Late in 4100 AR, after murdering a popular rival with malicious soldering, the sultan of Qud disappeared. Because of Sbib's shining visage, they was chosen as the successor.");

        Assert.That(
            translatedEnglishPossessive,
            Is.EqualTo("4100年末、人気のあるライバルを悪意あるはんだ付けで殺したあと、クッドのスルタンは姿を消した。Sbibの輝く顔立ちのため、彼らが後継者に選ばれた。"));
        AssertJournalPatternEdgeCases();
    }

    [Test]
    public void Translate_TranslatesCapitalizedExpandedHistorySpiceCapture()
    {
        WriteDictionaryFile("journal-test.ja.json", new[] { ("Farmers' Guild", "農民のギルド") });
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[]
            {
                ("with malicious soldering", "悪意あるはんだ付け"),
                ("shining", "輝く"),
                ("visage", "顔立ち"),
            });
        WritePatternDictionary((
            "^Because\\ of\\ (.+?),\\ (.+?)\\ was\\ chosen\\ as\\ the\\ successor\\.$",
            "{t0}のため、{t1}が後継者に選ばれた。"));

        var translated = JournalPatternTranslator.Translate(
            "Because of Leader of the Farmers' Guildの Shining Visage, they was chosen as the successor.");

        Assert.That(translated, Is.EqualTo("農民のギルドの指導者の輝く顔立ちのため、彼らが後継者に選ばれた。"));
        AssertJournalPatternEdgeCases();
    }

    [Test]
    public void Translate_LeavesAbdicateSuccessorAnnalCaptureEnglish_WhenExpandedComponentIsUnknown()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[] { ("disappeared", "姿を消した") });
        WritePatternDictionary((
            "^((?:In|Early in|Late in|Sometime in))\\ (.+?)\\ (?:BR|AR),\\ (.+?),\\ the\\ sultan\\ of\\ Qud\\ (.+?)\\.\\ Because\\ of\\ (.+?),\\ (.+?)\\ was\\ chosen\\ as\\ the\\ successor\\.$",
            "{t1}{t0}、{t2}、クッドのスルタンは{t3}。{t4}のため、{t5}が後継者に選ばれた。"));

        var translated = JournalPatternTranslator.Translate(
            "Sometime in 4100 AR, after inventing a clock, the sultan of Qud disappeared. Because of bright destiny, he was chosen as the successor.");

        Assert.That(
            translated,
            Is.EqualTo("4100年ごろ、after inventing a clock、クッドのスルタンは姿を消した。bright destinyのため、その者が後継者に選ばれた。"));
        AssertJournalPatternEdgeCases();
    }

    [Test]
    public void Translate_TranslatesExpandedFurnitureStuckTemplates()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[] { ("finger", "指"), ("throne", "玉座") });
        WritePatternDictionary(
            ("^(.+?) got (.+?) (.+?) stuck in (.+?)\\.$", "{t0}は{t1}{t2}を{t3}に挟まれた。"),
            ("^(.+?) got (.+?) (.+?) stuck under (.+?)\\.$", "{t0}は{t1}{t2}を{t3}の下に挟まれた。"),
            ("^(.+?) got (.+?) (.+?) stuck inside (.+?)\\.$", "{t0}は{t1}{t2}を{t3}の中に挟まれた。"),
            ("^(.+?) got (.+?) (.+?) stuck behind (.+?)\\.$", "{t0}は{t1}{t2}を{t3}の後ろに挟まれた。"));

        var translated = JournalPatternTranslator.Translate(
            "Oboroqoru got his finger stuck behind a throne.");

        Assert.That(translated, Is.EqualTo("Oboroqoruはその指を玉座の後ろに挟まれた。"));

        var translatedIn = JournalPatternTranslator.Translate(
            "Oboroqoru got his finger stuck in a throne.");

        Assert.That(translatedIn, Is.EqualTo("Oboroqoruはその指を玉座に挟まれた。"));
        AssertJournalPatternEdgeCases();
    }

    [Test]
    public void Translate_TranslatesDrownedInLakeOfLiquidCapture()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[] { ("acid", "酸") });
        WritePatternDictionary((
            "^(.+?) drowned in a lake of (.+?)\\.$",
            "{t0}は{t1}の湖で溺れた。"));

        var translated = JournalPatternTranslator.Translate("sib drowned in a lake of acid.");

        Assert.That(translated, Is.EqualTo("sibは酸の湖で溺れた。"));
        AssertJournalPatternEdgeCases();
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
                "{1}年{0}、=name=は{t2}の権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name=は勝利し、偽者の精神を刻んで吸収した。"));

        var translated = JournalPatternTranslator.Translate(
            "In the month of Ut yara Ux of 1012, =name= was challenged by <spice.commonPhrases.pretender.!random.article> to a duel over the rights of the Mechanimists. =name= won and had the pretender's psyche kibbled and absorbed into their own.");

        Assert.That(
            translated,
            Is.EqualTo("1012年Ut yara Ux、=name=はMechanimistsの権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name=は勝利し、偽者の精神を刻んで吸収した。"));
    }

    [Test]
    public void Translate_AppliesGivesRepMuralAndGospelPatterns()
    {
        WriteGivesRepPatternDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                JournalPatternTranslator.Translate("Blasphemously, the traitor a snapjaw scavenger attacked =name=, his water-sib, and =name= was forced to slay him. Deep in grief, =name= wept for one year."),
                Is.EqualTo("冒涜的にも、裏切り者のsnapjaw scavengerは水の同胞である=name=を襲い、=name=はsnapjaw scavengerを殺さざるを得なかった。深い悲しみの中、=name=は一年間泣き続けた。"));
            Assert.That(
                JournalPatternTranslator.Translate("In the month of Ut yara Ux of 1012, =name= was challenged by <spice.commonPhrases.pretender.!random.article> to a duel over the rights of the Mechanimists. =name= won and murdered the pretender before tragically realizing <spice.pronouns.subject.!random> was your water-sib."),
                Is.EqualTo("1012年Ut yara Ux、=name=はMechanimistsの権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name=は勝利し、偽者を殺した後、悲劇的にも<spice.pronouns.subject.!random>が水の同胞だったと気づいた。"));
            Assert.That(
                JournalPatternTranslator.Translate("In the month of Ut yara Ux of 1012, brave =name= slew loathsome a snapjaw scavenger in single combat."),
                Is.EqualTo("1012年Ut yara Ux、勇敢なる=name=は一騎打ちでloathsome a snapjaw scavengerを倒した。"));
            Assert.That(
                JournalPatternTranslator.Translate("In the month of Ut yara Ux of 1012, =name= was challenged by <spice.commonPhrases.pretender.!random.article> to a duel over the rights of the Mechanimists. =name= won and murdered the pretender <spice.elements.salt.murdermethods.!random>."),
                Is.EqualTo("1012年Ut yara Ux、=name=はMechanimistsの権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name=は勝利し、<spice.elements.salt.murdermethods.!random>で偽者を殺した。"));
        });
    }

    [Test]
    public void Translate_AppliesGivesRepMuralAndGospelPatterns_EdgeCases()
    {
        WriteGivesRepPatternDictionary();

        const string unmatched = "In the month of Ut yara Ux of 1012, =name= planted a garden.";
        const string source = "In the month of Ut yara Ux of 1012, brave =name= slew {{r|loathsome a snapjaw scavenger}} in single combat.";
        const string expected = "1012年Ut yara Ux、勇敢なる=name=は一騎打ちで{{r|loathsome a snapjaw scavenger}}を倒した。";

        Assert.Multiple(() =>
        {
            Assert.That(JournalPatternTranslator.Translate(unmatched), Is.EqualTo(unmatched));
            Assert.That(JournalPatternTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
            Assert.That(JournalPatternTranslator.Translate(null), Is.EqualTo(string.Empty));
            Assert.That(
                JournalPatternTranslator.Translate(MessageFrameTranslator.MarkDirectTranslation(source)),
                Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(source)));
            Assert.That(
                JournalPatternTranslator.Translate("{{W|" + source + "}}"),
                Is.EqualTo("{{W|" + expected + "}}"));
        });
    }

    [Test]
    public void Translate_AppliesDynamicQuestCompletionPatterns_FromAssets()
    {
        WriteDictionaryFile(
            "dynamic-quest-completion-l1.ja.json",
            new[]
            {
                ("Grit Gate", "グリット・ゲート"),
                ("your", "あなたの"),
                ("shining", "輝く"),
                ("the Barathrumites", "バラサルマイト"),
                ("the glass lens", "ガラスレンズ"),
                ("Joppa", "ジョッパ"),
                ("Stopsvalinn", "ストップスヴァリン"),
            });
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    JournalPatternTranslator.Translate("You located Grit Gate."),
                    Is.EqualTo("グリット・ゲートを発見した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Through the use of your divinely shining eyes, =name= discovered the lost location of Grit Gate."),
                    Is.EqualTo("あなたの神々しい輝く目を用いて、=name=は失われたグリット・ゲートの場所を発見した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Acting against the persecution of the Barathrumites, =name= led an army to the lost gates of Grit Gate. They liberated its citizens, who together in your honor <spice.history.gospels.Celebration.LateSultanate.!random>."),
                    Is.EqualTo("バラサルマイトへの迫害に抗し、=name=は軍勢を率いて失われたグリット・ゲートの門へ至った。彼らはその市民を解放し、あなたの栄誉のもと<spice.history.gospels.Celebration.LateSultanate.!random>した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("You recovered the glass lens."),
                    Is.EqualTo("ガラスレンズを回収した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("While exploring Joppa, =name= recovered the fabled artifact called the glass lens."),
                    Is.EqualTo("ジョッパを探索中、=name=はガラスレンズという伝説のアーティファクトを回収した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("While visiting an obscure <spice.professions.apothecary.guildhall>, =name= met with a group of <spice.professions.apothecary.plural> and commissed what came to be known as the the glass lens."),
                    Is.EqualTo("とある<spice.professions.apothecary.guildhall>を訪れた際、=name=は<spice.professions.apothecary.plural>の一団と会い、のちにガラスレンズとして知られるものを依頼した。"));
                foreach (var (source, expected) in DynamicQuestInteractCompletionCases())
                {
                    Assert.That(JournalPatternTranslator.Translate(source), Is.EqualTo(expected));
                }
                Assert.That(
                    JournalPatternTranslator.Translate("You recovered the historic relic, Stopsvalinn."),
                    Is.EqualTo("歴史的遺物ストップスヴァリンを回収した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("<spice.commonPhrases.intrepid.!random.capitalize> =name= recovered Stopsvalinn, a historic relic once thought lost to the sands of time."),
                    Is.EqualTo("<spice.commonPhrases.intrepid.!random.capitalize>=name=は、かつて時の砂に失われたと思われていた歴史的遺物ストップスヴァリンを回収した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("<spice.commonPhrases.intrepid.!random.capitalize> =name= discovered アラアッラワン, once thought lost to the sands of time."),
                    Is.EqualTo("<spice.commonPhrases.intrepid.!random.capitalize>=name=は、かつて時の砂に失われたと思われていたアラアッラワンを発見した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("In =year=, =name= won a decisive victory against the combined force of スナップジョー from the 密林 at the bloody Battle of アラアッラワン."),
                    Is.EqualTo("=year=、=name=はアラアッラワンの血塗られた戦いで、密林のスナップジョー連合軍に決定的勝利を収めた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("In an excavation at a site of deep history near Joppa, =name= recovered Stopsvalinn, the historic relic once thought lost to the sands of time."),
                    Is.EqualTo("ジョッパ近くの深い歴史を持つ場所での発掘において、=name=はかつて時の砂に失われたと思われていた歴史的遺物ストップスヴァリンを回収した。"));
                AssertJournalPatternEdgeCases();
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    private static (string Source, string Expected)[] DynamicQuestInteractCompletionCases() =>
        new[]
        {
            ("You opened the glass lens.", "ガラスレンズを開けた。"),
            ("You closed the glass lens.", "ガラスレンズを閉じた。"),
            ("You entered the glass lens.", "ガラスレンズに入った。"),
            ("You slept in the glass lens.", "ガラスレンズで眠った。"),
            ("You slept on the glass lens.", "ガラスレンズの上で眠った。"),
            ("You sat on the glass lens.", "ガラスレンズに座った。"),
            ("You put something in the glass lens.", "ガラスレンズに何かを入れた。"),
            ("You put something on the glass lens.", "ガラスレンズに何かを置いた。"),
            ("You drank from the glass lens.", "ガラスレンズから飲んだ。"),
            ("You cooked at the glass lens.", "ガラスレンズで料理した。"),
            ("You smoked from the glass lens.", "ガラスレンズで吸った。"),
            ("You prayed at the glass lens.", "ガラスレンズで祈った。"),
            ("You desecrated the glass lens.", "ガラスレンズを冒涜した。"),
            (
                "While exploring Joppa, =name= opened the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けを開けた。"
            ),
            (
                "While exploring Joppa, =name= closed the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けを閉じた。"
            ),
            (
                "While exploring Joppa, =name= entered the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けに入った。"
            ),
            (
                "While exploring Joppa, =name= slept in the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けで眠った。"
            ),
            (
                "While exploring Joppa, =name= slept on the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けの上で眠った。"
            ),
            (
                "While exploring Joppa, =name= sat on the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けに座った。"
            ),
            (
                "While exploring Joppa, =name= put something in the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けに何かを入れた。"
            ),
            (
                "While exploring Joppa, =name= put something on the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けに何かを置いた。"
            ),
            (
                "While exploring Joppa, =name= drank from the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けから飲んだ。"
            ),
            (
                "While exploring Joppa, =name= cooked at the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けで料理した。"
            ),
            (
                "While exploring Joppa, =name= smoked from the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けで吸った。"
            ),
            (
                "While exploring Joppa, =name= prayed at the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けで祈った。"
            ),
            (
                "While exploring Joppa, =name= desecrated the fabled contraption called the glass lens.",
                "ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けを冒涜した。"
            ),
        };

    [Test]
    public void Translate_AppliesVillageProverbPatterns_FromAssets()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[]
            {
                ("copper", "銅"),
                ("glass workshop", "ガラス工房"),
                ("garden", "庭園"),
                ("prayer", "祈り"),
                ("idleness", "怠惰"),
                ("bless", "祝福する"),
                ("curse", "呪う"),
                ("sage", "賢者"),
                ("temple", "神殿"),
                ("grace", "恩寵"),
                ("shame", "恥辱"),
                ("bright", "輝く"),
                ("sanctity", "神聖"),
                ("profanity", "冒涜"),
                ("hearth", "炉辺"),
                ("wander", "さまよう"),
                ("stone", "石"),
            });
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    JournalPatternTranslator.Translate("Copper hold no value over Glass Workshop."),
                    Is.EqualTo("ガラス工房に比べれば銅に価値はない。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Plant seeds in Garden and reap Prayer."),
                    Is.EqualTo("庭園に種を蒔けば祈りを刈り取る。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Idleness leads to Glass Workshop."),
                    Is.EqualTo("怠惰はガラス工房につながる。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Glass Workshop cannot compare to glass workshop."),
                    Is.EqualTo("ガラス工房はガラス工房に及ばない。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Bless Glass Workshop."),
                    Is.EqualTo("ガラス工房を祝福する。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Thank Glass Workshop."),
                    Is.EqualTo("ガラス工房に感謝する。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Give thanks for Glass Workshop."),
                    Is.EqualTo("ガラス工房に感謝を捧げる。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Praise Glass Workshop."),
                    Is.EqualTo("ガラス工房を称賛する。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Honor Glass Workshop."),
                    Is.EqualTo("ガラス工房を敬う。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Exalt Glass Workshop."),
                    Is.EqualTo("ガラス工房を讃える。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Curse Idleness."),
                    Is.EqualTo("怠惰を呪う。"));
                Assert.That(
                    JournalPatternTranslator.Translate("A blight upon Idleness."),
                    Is.EqualTo("怠惰に災いあれ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("A curse upon Idleness."),
                    Is.EqualTo("怠惰に呪いあれ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Only the sage can know Glass Workshop."),
                    Is.EqualTo("ガラス工房を知り得るのは賢者だけだ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("At the temple lies grace."),
                    Is.EqualTo("神殿には恩寵が宿る。"));
                Assert.That(
                    JournalPatternTranslator.Translate("The bright sage knows the sanctity of Glass Workshop."),
                    Is.EqualTo("輝く賢者はガラス工房の神聖を知る。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Prayer is no way to bless Glass Workshop."),
                    Is.EqualTo("祈りはガラス工房を祝福する方法ではない。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Prayer is no way to thank Glass Workshop."),
                    Is.EqualTo("祈りはガラス工房に感謝する方法ではない。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Prayer is no way to give thanks for Glass Workshop."),
                    Is.EqualTo("祈りはガラス工房に感謝を捧げる方法ではない。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Prayer is no way to praise Glass Workshop."),
                    Is.EqualTo("祈りはガラス工房を称賛する方法ではない。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Prayer is no way to honor Glass Workshop."),
                    Is.EqualTo("祈りはガラス工房を敬う方法ではない。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Prayer is no way to exalt Glass Workshop."),
                    Is.EqualTo("祈りはガラス工房を讃える方法ではない。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Welcome strangers in prayer, but teach them the sanctity of Glass Workshop."),
                    Is.EqualTo("祈りの中で異邦人を迎えよ、されど彼らにガラス工房の神聖を教えよ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Don't wander to the temple."),
                    Is.EqualTo("神殿へさまような。"));
                Assert.That(
                    JournalPatternTranslator.Translate("A stone today is worth a stone tomorrow."),
                    Is.EqualTo("今日の石は明日の石に勝る。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Knowing Glass Workshop means knowing the stone."),
                    Is.EqualTo("ガラス工房を知ることは石を知ることだ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Like the stone to the sage, so Glass Workshop to us."),
                    Is.EqualTo("賢者にとっての石のように、我らにとってのガラス工房もまたそうだ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Glass Workshop dwells in the hearth."),
                    Is.EqualTo("ガラス工房は炉辺に宿る。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Do not ask the sage to teach sanctity."),
                    Is.EqualTo("賢者に神聖を教えよと求めるな。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Keep an eye on your stone."),
                    Is.EqualTo("自らの石から目を離すな。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Better to be bright than to be shame."),
                    Is.EqualTo("恥辱であるより輝くほうがよい。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Find sanctity in every stone."),
                    Is.EqualTo("あらゆる石の中に神聖を見いだせ。"));
                AssertJournalPatternEdgeCases();
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void Translate_AppliesRemainingHistorySpiceRouteGrammarPatterns_FromAssets()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            new[]
            {
                ("ravaged", "荒らした"),
                ("scourge", "災厄"),
                ("salt", "塩"),
                ("sowing with salt the fields", "畑に塩を撒くこと"),
                ("Salt Dunes", "塩砂丘"),
                ("fish", "魚"),
                ("birds", "鳥"),
            });
        WriteDictionaryFile(
            "historyspice-route-grammar-l1.ja.json",
            new[]
            {
                ("Throughout", "年を通じて"),
                ("Around", "年頃"),
                ("flower fields", "花畑"),
                ("Bey Lah", "ベイ・ラー"),
                ("Hindren", "ヒンドレン"),
            });
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    JournalPatternTranslator.Translate(
                        "In 1001, Resheph ravaged all of Salt Dunes, sowing with salt the fields of fish and birds. He became known as the Salt Scourge."),
                    Is.EqualTo("1001年、Reshephは塩砂丘全域を荒らしたうえ、魚と鳥に対して畑に塩を撒くことを行った。"
                        + "その者は以後塩の災厄として知られるようになった。"));
                Assert.That(
                    JournalPatternTranslator.Translate(
                        "Throughout 1001, =name= ravaged the flower fields and brought turmoil to the troubled village of Bey Lah. He became known as the Hindren Scourge."),
                    Is.EqualTo("1001年を通じて、=name=は花畑を荒らしたうえ、悩めるベイ・ラーの村に混乱をもたらした。"
                        + "その者は以後ヒンドレンの災厄として知られるようになった。"));
                Assert.That(
                    JournalPatternTranslator.Translate(
                        "Around 1001, =name= ravaged the flower fields and brought turmoil to the troubled village of Bey Lah. He became known as the Hindren Scourge."),
                    Is.EqualTo("1001年頃、=name=は花畑を荒らしたうえ、悩めるベイ・ラーの村に混乱をもたらした。"
                        + "その者は以後ヒンドレンの災厄として知られるようになった。"));
                Assert.That(
                    JournalPatternTranslator.Translate(
                        "In 1001 AR, it was discovered that a clone of Resheph had been the one who died. Despite reports to the contrary, Resheph was alive and well. He was known thenceforth as the Glassborn."),
                    Is.EqualTo("1001年、死亡したのはReshephのクローンだったとの事実が明らかになった。"
                        + "相反する報告にもかかわらず、Reshephは健在だった。その者は以後Glassbornとして知られるようになった。"));
                Assert.That(
                    JournalPatternTranslator.Translate(
                        "In 1001 AR, it was discovered that Resheph's twin had been the one who died. Despite reports to the contrary, Resheph was alive and well. He was known thenceforth as the Glassborn."),
                    Is.EqualTo("1001年、死亡したのはReshephの双子だったとの事実が明らかになった。"
                        + "相反する報告にもかかわらず、Reshephは健在だった。その者は以後Glassbornとして知られるようになった。"));
                AssertJournalPatternEdgeCases();
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void Translate_AppliesOpeningStoryAndAnimatorSprayPatterns_FromAssets()
    {
        WriteDictionaryFile(
            "opening-animator-l1.ja.json",
            new[]
            {
                ("5th", "第5"),
                ("Ut yara Ux", "ウト・ヤラ・ウクス"),
                ("Joppa", "ジョッパ"),
                ("your", "あなたの"),
                ("cerulean", "空色"),
                ("ghost", "幽鬼"),
                ("chair", "椅子"),
                ("it", "それ"),
                ("with ivory limbs", "象牙色の四肢を持つ"),
            });
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    JournalPatternTranslator.Translate("On the auspicious 5th of Ut yara Ux, =name= arrived in Joppa and began your prodigious odyssey through Qud."),
                    Is.EqualTo("ウト・ヤラ・ウクスの第5日、=name=はジョッパに到着し、あなたのクッドを巡る驚異的な旅路を始めた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("At <spice.time.partsOfDay.!random> under <spice.commonPhrases.strange.!random.article> and cerulean sky, the people of Joppa saw an image on the horizon that looked like a ghost bathed in cerulean. It was =name=, and after he came and left, the people of Joppa built a monument to =name= and thenceforth called him Ghost-in-Cerulean."),
                    Is.EqualTo("<spice.time.partsOfDay.!random>、<spice.commonPhrases.strange.!random.article>と空色の空の下で、ジョッパの民は地平線に空色を浴びた幽鬼のような姿を見た。それは=name=だった。その者が来て去った後、ジョッパの民は=name=の記念碑を建て、以後その者を空色の幽鬼と呼んだ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("You imbued a chair with life. Why?"),
                    Is.EqualTo("椅子に命を吹き込んだ。なぜ？"));
                Assert.That(
                    JournalPatternTranslator.Translate("While traveling in Joppa, =name= performed a sacred ritual with a chair, imbuing it with life and arranging it with ivory limbs. Many of the local denizens declared it a miracle. Some weren't so sure."),
                    Is.EqualTo("ジョッパを旅する中で、=name=は椅子を用いて神聖な儀式を行い、それに命を吹き込み、それを象牙色の四肢を持つよう整えた。地元の多くの住民はそれを奇跡だと宣言した。疑う者もいた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("While traveling in Joppa, =name= performed a sacred ritual with a chair, imbuing it with life and arranging it with ivory limbs. Many of the local denizens declared it a miracle."),
                    Is.EqualTo("ジョッパを旅する中で、=name=は椅子を用いて神聖な儀式を行い、それに命を吹き込み、それを象牙色の四肢を持つよう整えた。地元の多くの住民はそれを奇跡だと宣言した。"));
                AssertJournalPatternEdgeCases();
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void Translate_AppliesZoneManagerJourneyPatterns_FromAssets()
    {
        WriteDictionaryFile(
            "zone-manager-journey-l1.ja.json",
            new[]
            {
                ("Kyakukya", "キャクキャ"),
                ("Oboroqoru", "オボロコル"),
                ("Nuntu", "ヌントゥ"),
                ("Omonporch", "オモンポーチ"),
                ("Red Rock", "レッドロック"),
                ("Grit Gate", "グリット・ゲート"),
                ("Golgotha", "ゴルゴタ"),
                ("Asphalt Mines", "アスファルト鉱山"),
                ("the Great Sea in the Asphalt Mines", "アスファルト鉱山の大海"),
                ("salt", "塩"),
                ("Salt", "塩"),
                ("Ubu Ut", "ウブ・ウト"),
                ("your", "あなたの"),
            });
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    JournalPatternTranslator.Translate("Done trekking through the root-strangled earth, =name= arrived in Kyakukya and was greeted by the village with warmth and reverence. Upon leaving, =name= was named Friend to Oboroqoru."),
                    Is.EqualTo("根に絡まれた大地を歩き終え、=name=はキャクキャに到着し、村から温かな敬意をもって迎えられた。去る時、=name=はオボロコルの友と呼ばれた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("On an expedition down the River Svy, =name= was captured by bandits. He languished in captivity for 8 years, eventually escaping to Kyakukya and befriending its mayor, the albino ape called Nuntu."),
                    Is.EqualTo("スヴィ川を下る遠征中、=name=は盗賊に捕らえられた。その者は8年にわたり囚われの身で苦しんだが、ついにはキャクキャへ逃れ、白い類人猿ヌントゥとして知られる村長と親交を結んだ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("In =year=, =name= appointed the corrupt administrator Asphodel as earl and minister of Omonporch. There xe mandated the practice of <spice.elements.salt.practices.!random> in your name."),
                    Is.EqualTo("=year=、=name=は腐敗した行政官Asphodelをオモンポーチの伯爵兼大臣に任じた。そこでxeはあなたの名のもと<spice.elements.salt.practices.!random>の実践を義務づけた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("=name= trekked through the salt pans, north and west, to the merchant bazaar and grand cathedral of the Six Day Stilt. There, the stiltfolk sang hymns in the sultan's honor."),
                    Is.EqualTo("=name=は塩原を北へ西へと進み、シックス・デイ・スティルトの商人バザールと大聖堂へ到達した。そこでスティルトの民はスルタンを讃える聖歌を歌った。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Near the location of Red Rock, =name= was captured by baboons. He murdered their leader <spice.elements.salt.murdermethods.!random> and from then on wore a neck ring stained with baboon blood."),
                    Is.EqualTo("レッドロックの近くで、=name=はヒヒに捕らえられた。その者はその指導者を<spice.elements.salt.murdermethods.!random>で殺し、それ以来ヒヒの血で染まった首輪を身につけた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("At <spice.time.partsOfDay.!random> under <spice.commonPhrases.strange.!random.article> and rusted sky, the people of the desert canyon saw an image on the horizon that looked like a salt under an archway. It was =name=, and after he came and left, the people built a monument to =name= and thenceforth called him the Underarch Salt."),
                    Is.EqualTo("<spice.time.partsOfDay.!random>、<spice.commonPhrases.strange.!random.article>と錆びた空の下で、砂漠峡谷の民は地平線にアーチの下の塩のような姿を見た。それは=name=だった。その者が来て去った後、人々は=name=の記念碑を建て、以後その者をアンダーアーチの塩と呼んだ。"));
                Assert.That(
                    JournalPatternTranslator.Translate("In the month of Ubu Ut of 1001 AR, =name= ascended the trash chutes of Golgotha, victorious and bathed in slime."),
                    Is.EqualTo("1001年ウブ・ウト、=name=は勝利を得て粘液を浴びながら、ゴルゴタの廃棄物シュートを登った。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Sometime in =year=, =name= wandered over the high mounts and voyaged to the Great Sea in the Asphalt Mines. There he befriended no one and instead bathed in the black blood of the earth."),
                    Is.EqualTo("=year=のある時、=name=は高き山々をさまよい、アスファルト鉱山の大海へ旅した。そこでその者は誰とも親交を結ばず、代わりに大地の黒き血を浴びた。"));
                AssertJournalPatternEdgeCases();
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void Translate_AppliesBodyAndMutationAccomplishmentPatterns_FromAssets()
    {
        WriteDictionaryFile(
            "body-mutation-l1.ja.json",
            new[]
            {
                ("left arm", "左腕"),
                ("shining visage", "輝く顔"),
                ("Light Manipulation", "光操作"),
                ("mutation", "変異"),
                ("him", "彼"),
                ("mutants", "変異者"),
                ("around Salt Dunes", "塩砂丘の辺り"),
                ("Player's", "プレイヤーの"),
            });
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    JournalPatternTranslator.Translate("Your left arm was dismembered."),
                    Is.EqualTo("左腕が切断された。"));
                Assert.That(
                    JournalPatternTranslator.Translate("While fighting a battle to protect the practice of shining visage, =name= valorously had his left arm dismembered."),
                    Is.EqualTo("輝く顔の実践を守る戦いの中で、=name=は勇敢にも左腕を切断された。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Your genome destabilized and you gained the Light Manipulation mutation."),
                    Is.EqualTo("あなたのゲノムが不安定になり、光操作の変異を得た。"));
                Assert.That(
                    JournalPatternTranslator.Translate("<spice.commonPhrases.oneStarryNight.!random.capitalize>, =name= manifested a latent power inside him and joined the divine ranks of mutants."),
                    Is.EqualTo("<spice.commonPhrases.oneStarryNight.!random.capitalize>、=name=は内なる潜在能力を顕現させ、変異者の神聖なる列に加わった。"));
                Assert.That(
                    JournalPatternTranslator.Translate("While wandering around Salt Dunes, =name= stumbled upon a clan of mutants. Because of Player's <spice.elements.salt.quality.!random>, they accepted him into their fold and taught him their secrets."),
                    Is.EqualTo("塩砂丘の辺りをさまよううち、=name=は変異者の一族に出くわした。プレイヤーの<spice.elements.salt.quality.!random>ゆえ、彼らはその者を仲間に迎え入れ、その者に彼らの秘密を授けた。"));
                AssertJournalPatternEdgeCases();
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void Translate_AppliesVillageSurfaceVisitPatterns_FromAssets()
    {
        WriteDictionaryFile(
            "village-surface-l1.ja.json",
            new[]
            {
                ("Ut yara Ux", "ウト・ヤラ・ウクス"),
                ("Kyakukya", "キャクキャ"),
                ("his", "その"),
            });
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    JournalPatternTranslator.Translate("In the month of Ut yara Ux of 1012 AR, =name= founded the village of Kyakukya to <spice.history.gospels.HumblePractice.LateSultanate.!random>."),
                    Is.EqualTo("1012年ウト・ヤラ・ウクス、=name=は<spice.history.gospels.HumblePractice.LateSultanate.!random>ためにキャクキャの村を建てた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Acting against the prohibition on the practice of <spice.elements.salt.practices.!random>, =name= led an army to the gates of Kyakukya. =name= <spice.commonPhrases.liberated.!random> its citizens, and in his honor they <spice.history.gospels.Celebration.LateSultanate.!random>."),
                    Is.EqualTo("<spice.elements.salt.practices.!random>の実践への禁令に抗し、=name=は軍勢を率いてキャクキャの門へ至った。=name=はその市民を<spice.commonPhrases.liberated.!random>し、その栄誉のもと彼らは<spice.history.gospels.Celebration.LateSultanate.!random>した。"));
                AssertJournalPatternEdgeCases();
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void Translate_AppliesTranche42SocialActiveEffectAccomplishmentPatterns_FromAssets()
    {
        WriteDictionaryFile(
            "social-active-effects-l1.ja.json",
            new[]
            {
                ("chrome idol", "クローム偶像"),
                ("snapjaw", "スナップジョー"),
                ("clockwork beetle", "クロックワークビートル"),
                ("5th", "第5"),
                ("Iyur Ut", "イユル・ウト"),
                ("Barathrumites", "バラサルマイト"),
            });
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    JournalPatternTranslator.Translate("Your heart sang at the sight of a chrome idol."),
                    Is.EqualTo("クローム偶像を見て心が歌った。"));
                Assert.That(
                    JournalPatternTranslator.Translate("A snapjaw ogled you lovingly after you employed your charm."),
                    Is.EqualTo("あなたの魅了術を受けてスナップジョーがうっとりとこちらを見つめた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("You convinced a snapjaw to join your cause."),
                    Is.EqualTo("スナップジョーを説得し仲間に加えた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("You rebuked a clockwork beetle into submission."),
                    Is.EqualTo("クロックワークビートルを叱責して従わせた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("The troubadour-hero =name= rode the tides of your passions and shipwrecked on the shores of a chrome idol."),
                    Is.EqualTo("吟遊詩人の英雄=name=は情熱の潮に乗り、クローム偶像の岸辺に漂着した。"));
                Assert.That(
                    JournalPatternTranslator.Translate("The storied eroticism of =name= became intimately known to a snapjaw."),
                    Is.EqualTo("=name=の名高い色香はスナップジョーに深く知られることとなった。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Few were possessed of such potent charm as =name=, who -- on the 5th of Iyur Ut -- bent the will of a snapjaw with mere words."),
                    Is.EqualTo("イユル・ウトの第5日、=name=ほど強力な魅力を備えた者は稀であり、ただ言葉だけでスナップジョーの意志を曲げた。"));
                Assert.That(
                    JournalPatternTranslator.Translate("Onlookers! Remember the admonishment =name= gave a clockwork beetle when it presumed to speak the sacred tongue!"),
                    Is.EqualTo("見る者よ！=name=がクロックワークビートルに与えた戒めを思い起こせ。聖なる言葉を口にしようとしたためだ！"));
                Assert.That(
                    JournalPatternTranslator.Translate("<spice.elements.salt.weddingConditions.!random.capitalize>, =name= cemented your friendship with Barathrumites by marrying a snapjaw."),
                    Is.EqualTo("<spice.elements.salt.weddingConditions.!random.capitalize>、=name=はバラサルマイトとの友情を固めるためスナップジョーと結婚した。"));
                AssertJournalPatternEdgeCases();
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
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

    private static void AssertJournalPatternEdgeCases()
    {
        const string fallback = "This issue 737 journal pattern should not match.";
        var marked = MessageFrameTranslator.MarkDirectTranslation(fallback);

        Assert.That(JournalPatternTranslator.Translate(fallback), Is.EqualTo(fallback));
        Assert.That(JournalPatternTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
        Assert.That(JournalPatternTranslator.Translate("{{R|" + fallback + "}}"), Is.EqualTo("{{R|" + fallback + "}}"));
        Assert.That(JournalPatternTranslator.Translate(marked), Is.EqualTo(marked));
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

    private void WriteGivesRepPatternDictionary()
    {
        WritePatternDictionary(
            (
                "^Blasphemously, the traitor (.+?) attacked =name=, (?:his|her|their|its) water-sib, and =name= was forced to slay (?:him|her|them|it)\\. Deep in grief, =name= wept for one year\\.$",
                "冒涜的にも、裏切り者の{t0}は水の同胞である=name=を襲い、=name=は{t0}を殺さざるを得なかった。深い悲しみの中、=name=は一年間泣き続けた。"),
            (
                "^In the month of (.+?) of (.+?), =name= was challenged by <spice\\.commonPhrases\\.pretender\\.!random\\.article> to a duel over the rights of (.+?)\\. =name= won and murdered the pretender before tragically realizing <spice\\.pronouns\\.subject\\.!random> was (?:your|his|her|their|its) water-sib\\.$",
                "{1}年{0}、=name=は{t2}の権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name=は勝利し、偽者を殺した後、悲劇的にも<spice.pronouns.subject.!random>が水の同胞だったと気づいた。"),
            (
                "^In the month of (.+?) of (.+?), brave =name= slew (.+?) in single combat\\.$",
                "{1}年{0}、勇敢なる=name=は一騎打ちで{t2}を倒した。"),
            (
                "^In the month of (.+?) of (.+?), =name= was challenged by <spice\\.commonPhrases\\.pretender\\.!random\\.article> to a duel over the rights of (.+?)\\. =name= won and murdered the pretender <spice\\.elements\\.(.+?)\\.murdermethods\\.!random>\\.$",
                "{1}年{0}、=name=は{t2}の権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name=は勝利し、<spice.elements.{3}.murdermethods.!random>で偽者を殺した。"));
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

        var path = Path.Combine(dictionaryDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, builder.ToString(), Utf8WithoutBom);
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
