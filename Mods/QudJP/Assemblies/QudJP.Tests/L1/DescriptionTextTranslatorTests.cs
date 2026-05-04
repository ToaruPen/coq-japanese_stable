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
    public void TranslateLongDescription_AppliesVillageHistoryMonumentPatterns()
    {
        WriteExactDictionary(
            ("brisket", "ブリスケット"),
            ("spreading", "スプレッディング"),
            ("carnival", "カーニバル"),
            ("traveling", "旅する"),
            ("prayer", "祈り"),
            ("gleefully", "喜んで"));
        WritePatternDictionary(
            (
                "^(?:\\{\\{C\\|)?This object is a monument to a scene from the history of the village (.+?):(?:\\}\\})?$",
                "これは{0}村の歴史の一場面を記念する碑である:"),
            (
                "^The sanctity of (?:the )?(.+?) was revealed to the people of (.+?) through the dish known as (.+?)\\.(?:\\}\\})?$",
                "{t2}として知られる料理を通じて、{0}の聖性が{1}の人々に示された。"),
            (
                "^Since the first (.+?), the villagers of (.+?) have (.+?) feasted on (.+?)\\.(?:\\}\\})?$",
                "最初の{t0}以来、{1}の村人たちは{t3}を{t2}食してきた。"));

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "{{C|This object is a monument to a scene from the history of the village テッガトゥム:\n" +
            "The sanctity of the 商人ギルド was revealed to the people of テッガトゥム through the dish known as Brisket Spreading.\n" +
            "Since the first Carnival of the Traveling Prayer, the villagers of テッガトゥム have gleefully feasted on Brisket Spreading.}}",
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo(
                "{{C|これはテッガトゥム村の歴史の一場面を記念する碑である:\n" +
                "ブリスケット・スプレッディングとして知られる料理を通じて、商人ギルドの聖性がテッガトゥムの人々に示された。\n" +
                "最初の旅する祈りのカーニバル以来、テッガトゥムの村人たちはブリスケット・スプレッディングを喜んで食してきた。}}"));
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
    public void TranslateLongDescription_TranslatesVillageDispositionReasonLeaf()
    {
        WriteExactDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("defending their village", "彼らの村を守っているため"));

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "Admired by {{C|the villagers of テルヴァマス}} for defending their village.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("{{C|テルヴァマスの村人たち}}に敬愛されている。理由: 彼らの村を守っているため。"));
    }

    [Test]
    public void TranslateLongDescription_TranslatesBrainDispositionLinesPreservingValueColor()
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "Base demeanor: {{g|docile}}\nEngagement style: {{r|aggressive}}",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("基本態度: {{g|温和}}\n交戦スタイル: {{r|攻撃的}}"));
    }

    [Test]
    public void TranslateLongDescription_BrainDispositionFallbackKeepsEnglishValue()
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "Base demeanor: {{g|unknown}}",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("基本態度: {{g|unknown}}"));
    }

    [Test]
    public void TranslateLongDescription_DoesNotReportNoPattern_ForAlreadyLocalizedDispositionReason()
    {
        const string reason = "巡礼者に施しをしたため";
        var source = "Admired by {{C|the Mechanimists}} for " + reason + ".";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("{{C|the Mechanimists}}に敬愛されている。理由: " + reason + "。"));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(reason), Is.EqualTo(0));
        });
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

    [Test]
    public void TranslateLongDescription_TranslatesInspectFixedLeavesFromOwnerDictionaries()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("defensive stance", "防御姿勢"));
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class: Bows && Rifles", "武器カテゴリ: 弓 && ライフル"),
            ("Accuracy: Medium", "命中率: 普通"),
            (
                "Projectiles fired with this weapon receive bonus penetration based on the wielder's Strength.",
                "この武器から発射された投射物は、使用者の筋力に基づいて追加の貫通力を得る。"));

        var source =
            "Weapon Class: Bows && Rifles\n" +
            "Accuracy: Medium\n" +
            "Projectiles fired with this weapon receive bonus penetration based on the wielder's Strength.\n" +
            "defensive stance";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo(
                "武器カテゴリ: 弓 && ライフル\n" +
                "命中率: 普通\n" +
                "この武器から発射された投射物は、使用者の筋力に基づいて追加の貫通力を得る。\n" +
                "防御姿勢"));
    }

    [Test]
    public void TranslateLongDescription_DoesNotReportNoPattern_ForAlreadyLocalizedDescriptionFragments()
    {
        const string localizedLine =
            "小さなコルクの芽が湿気でふくらむ。灼け付く週のあいだ、百里の風から一粒の雫をすすって育てた。";
        const string localizedWeight = "重量： 1 lbs.";
        var source = localizedLine + "\n\n" + localizedWeight;

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(source), Is.EqualTo(0));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(localizedLine), Is.EqualTo(0));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(localizedWeight), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateLongDescription_DoesNotReportNoPattern_ForAlreadyLocalizedDotLbsDescriptionFragment()
    {
        const string localizedWeight = "重量： 1 .lbs";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            localizedWeight,
            "DescriptionTextTranslatorTests");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(localizedWeight));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(localizedWeight), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateLongDescription_DoesNotReportNoPattern_ForMultilineDescriptionStatusBlock()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Weight:", "重量："));
        WriteDictionary(
            "world-mods.ja.json",
            ("Offhand Attack Chance: {0}%", "オフハンド命中率: {0}%"),
            ("Cudgel (dazes on critical hit)", "棍棒（クリティカル時に朦朧付与）"));
        const string source =
            "拳大の巻貝が柔らかな螺旋にとぐろを巻き、煤で黒く燻され、硫黄の臭気を放つ。\n\n" +
            "Strength Bonus Cap: 3\n" +
            "Weapon Class: Cudgel (dazes on critical hit)\n" +
            "Offhand Attack Chance: 15%\n\n" +
            "Weight: 2 lbs.";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.Multiple(() =>
        {
            Assert.That(
                translated,
                Is.EqualTo(
                    "拳大の巻貝が柔らかな螺旋にとぐろを巻き、煤で黒く燻され、硫黄の臭気を放つ。\n\n" +
                    "筋力ボーナス上限: 3\n" +
                    "武器カテゴリ: 棍棒（クリティカル時に朦朧付与）\n" +
                    "オフハンド命中率: 15%\n\n" +
                    "重量： 2 lbs."));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(source), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Strength"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Bonus Cap:"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Weapon Class:"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Offhand Attack Chance: {0}%"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Weight:"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateLongDescription_DoesNotReportNoPattern_ForLocalizedTonicRulesWithAllowedStatTokens()
    {
        const string source =
            "持続：41-50ラウンド　筋力 +9／レベルごとに一時HP +3／移動速度 -25。痛みを感じない。恐怖に免疫。毎ラウンド最大HPの1%のダメージを受ける（このダメージでHPは1未満にならない）。";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(source), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateShortDescription_TranslatesTonicLeafWithoutMissingSequenceTokens()
    {
        const string source = "This item is a tonic. Applying one tonic while under the effects of another may produce undesired results.";
        WriteDictionary(
            "world-effects-tonics.ja.json",
            (source, "このアイテムはトニックです。別のトニックの効果中にトニックを使用すると、望ましくない結果を招くことがあります。"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("このアイテムはトニックです。別のトニックの効果中にトニックを使用すると、望ましくない結果を招くことがあります。"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("This"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateShortDescription_TranslatesPreparedCookingIngredientEffectTemplate()
    {
        WriteDictionary(
            "world-effects-cooking.ja.json",
            ("simple plant-based", "シンプルな植物由来"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "Adds simple plant-based effects to cooked meals.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("シンプルな植物由来の効果を調理した食事に加える。"));
    }

    [Test]
    public void TranslateShortDescription_LeavesPreparedCookingIngredientEffectTemplateUnchanged_WhenEffectIsUnknownEnglish()
    {
        const string source = "Adds mysterious effects to cooked meals.";

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo(source));
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
