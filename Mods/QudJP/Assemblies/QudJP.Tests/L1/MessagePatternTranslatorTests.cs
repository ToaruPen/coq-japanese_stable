using System.Runtime.Serialization;
using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class MessagePatternTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-message-pattern-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Translate_AppliesSingleCapturePattern()
    {
        WritePatternDictionary(("^You miss (.+?)[.!]?$", "{0}への攻撃をはずした"));

        var translated = MessagePatternTranslator.Translate("You miss snapjaw.");

        Assert.That(translated, Is.EqualTo("snapjawへの攻撃をはずした"));
    }

    [Test]
    public void Translate_AppliesWeaponMissPatternBeforeGenericMissPattern()
    {
        WritePatternDictionary(
            ("^You miss with your (.+?)[.!] \\[(.+?) vs (.+?)\\]$", "{0}での攻撃は外れた。[{1} vs {2}]"),
            ("^You miss (.+?)[.!]?$", "{0}への攻撃は外れた"));

        var translated = MessagePatternTranslator.Translate("You miss with your レンチ! [10 vs 10]");

        Assert.That(translated, Is.EqualTo("レンチでの攻撃は外れた。[10 vs 10]"));
    }

    [Test]
    public void Translate_AppliesMultipleCapturePattern()
    {
        WritePatternDictionary(("^You hit (.+) for (\\d+) damage[.!]?$", "{0}に{1}ダメージを与えた"));

        var translated = MessagePatternTranslator.Translate("You hit glowfish for 12 damage!");

        Assert.That(translated, Is.EqualTo("glowfishに12ダメージを与えた"));
    }

    [Test]
    public void Translate_SupportsPlaceholderReordering()
    {
        WritePatternDictionary(("^(.+?) gives you (.+?)[.!]?$", "{1}を{0}から受け取った"));

        var translated = MessagePatternTranslator.Translate("warden gives you brass key.");

        Assert.That(translated, Is.EqualTo("brass keyをwardenから受け取った"));
    }

    [Test]
    public void Translate_UsesFirstMatchingPattern_WhenMultiplePatternsMatch()
    {
        WritePatternDictionary(
            ("^You hit (.+) for (\\d+) damage[.!]?$", "FIRST:{0}:{1}"),
            ("^You hit (.+) for (\\d+) damage[.!]?$", "SECOND:{0}:{1}"));

        var translated = MessagePatternTranslator.Translate("You hit goatfolk for 3 damage.");

        Assert.That(translated, Is.EqualTo("FIRST:goatfolk:3"));
    }

    [Test]
    public void Translate_HandlesPatternWithEscapedRegexSymbols()
    {
        WritePatternDictionary(("^You use \\((.+)\\)\\.$", "{0}を使用した"));

        var translated = MessagePatternTranslator.Translate("You use (phase cannon).");

        Assert.That(translated, Is.EqualTo("phase cannonを使用した"));
    }

    [Test]
    public void Translate_HandlesOptionalPunctuation()
    {
        WritePatternDictionary(("^You are stunned[.!]?$", "あなたは朦朧としている"));

        var first = MessagePatternTranslator.Translate("You are stunned");
        var second = MessagePatternTranslator.Translate("You are stunned!");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("あなたは朦朧としている"));
            Assert.That(second, Is.EqualTo("あなたは朦朧としている"));
        });
    }

    [Test]
    public void Translate_PreservesBraceColorMarkup()
    {
        WritePatternDictionary(("^You hit (.+) for (\\d+) damage[.!]?$", "{0}に{1}ダメージを与えた"));

        var translated = MessagePatternTranslator.Translate("{{W|You hit snapjaw for 7 damage}}!");

        Assert.That(translated, Is.EqualTo("{{W|snapjawに7ダメージを与えた}}"));
    }

    [Test]
    public void Translate_PreservesAmpersandAndCaretColorCodes()
    {
        WritePatternDictionary(("^You stop moving[.!]?$", "あなたは移動を止めた"));

        var translated = MessagePatternTranslator.Translate("&GYou stop moving^k.");

        Assert.That(translated, Is.EqualTo("&Gあなたは移動を止めた^k"));
    }

    [Test]
    public void Translate_PreservesCaptureLocalMarkupWhenReorderingPlaceholders()
    {
        WritePatternDictionary(("^You hit (.+) for (\\d+) damage[.!]?$", "{1}ダメージを{0}に与えた"));

        var translated = MessagePatternTranslator.Translate("You hit {{R|snapjaw}} for {{G|7}} damage!");

        Assert.That(translated, Is.EqualTo("{{G|7}}ダメージを{{R|snapjaw}}に与えた"));
    }

    [Test]
    public void Translate_PreservesCaptureLocalMarkupForTranslatedCaptures()
    {
        WritePatternDictionary((
            "^You see (.+?) to the (north|south|east|west|northeast|northwest|southeast|southwest) and stop moving[.!]?$",
            "{t1}に{0}が見えたので移動をやめた。"));
        WriteExactDictionary(("north", "北"));

        var translated = MessagePatternTranslator.Translate("You see タム、ドロマド商人 to the {{G|north}} and stop moving.");

        Assert.That(translated, Is.EqualTo("{{G|北}}にタム、ドロマド商人が見えたので移動をやめた。"));
    }

    [Test]
    public void Translate_AppliesJournalNotesPattern()
    {
        WritePatternDictionary(("^Notes: (.+)$", "備考: {0}"));

        var translated = MessagePatternTranslator.Translate("Notes: Damur");

        Assert.That(translated, Is.EqualTo("備考: Damur"));
    }

    [Test]
    public void Translate_AppliesEmbarkPattern()
    {
        WritePatternDictionary(("^You embark for the caves of Qud\\.$", "あなたはQudの洞窟へ旅立った。"));

        var translated = MessagePatternTranslator.Translate("You embark for the caves of Qud.");

        Assert.That(translated, Is.EqualTo("あなたはQudの洞窟へ旅立った。"));
    }

    [Test]
    public void Translate_AppliesVillageArrivalPattern()
    {
        WritePatternDictionary(
            (
                "^On the (.+?) of (.+?), you arrive at the village of (.+?)\\.\\n\\nOn the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth\\. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky\\.$",
                "{1}の{0}日、あなたは{2}の村に到着した。\n\n地平線では、Qudのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。"));

        var source = "On the 5th of Ut yara Ux, you arrive at the village of Damur and fungus patch.\n\n" +
            "On the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky.";

        var translated = MessagePatternTranslator.Translate(source);

        var expected = "Ut yara Uxの5th日、あなたはDamur and fungus patchの村に到着した。\n\n" +
            "地平線では、Qudのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。";

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void Translate_AppliesLowHealthWarningPattern()
    {
        WritePatternDictionary(("^Your health has dropped below 40%![.!]?$", "体力が40%を下回った！"));

        var translated = MessagePatternTranslator.Translate("Your health has dropped below 40%!");

        Assert.That(translated, Is.EqualTo("体力が40%を下回った！"));
    }

    [Test]
    public void Translate_AppliesWeaponHitPatternWithPenetrations()
    {
        WritePatternDictionary(
            ("^You hit (?:the |a |an )?(.+?) \\(x(\\d+)\\) with (?:a |an |the )?(.+?) for (\\d+) damage!$", "{2}で{0}に{3}ダメージを与えた！ (x{1})"));

        var translated = MessagePatternTranslator.Translate("You hit the 熊 (x2) with 青銅の短剣 for 3 damage!");

        Assert.That(translated, Is.EqualTo("青銅の短剣で熊に3ダメージを与えた！ (x2)"));
    }

    [Test]
    public void Translate_AppliesArmorPenetrationPatternWithLeadingArticle()
    {
        WritePatternDictionary(
            ("^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor with your (.+?)[.!] \\[(.+?)\\]$", "{1}では{0}の装甲を貫けない。[{2}]"));

        var translated = MessagePatternTranslator.Translate("You don't penetrate the 花瓶の armor with your 青銅の短剣. [19]");

        Assert.That(translated, Is.EqualTo("青銅の短剣では花瓶の装甲を貫けない。[19]"));
    }

    [Test]
    public void Translate_AppliesArmorPenetrationPatternWithColorizedWeapon()
    {
        WritePatternDictionary(
            ("^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor with your (.+?)[.!] \\[(.+?)\\]$", "{1}では{0}の装甲を貫けない。[{2}]"));

        var translated = MessagePatternTranslator.Translate("You don't penetrate タムの armor with your {{w|青銅の短剣}}. [17]");

        Assert.That(translated, Is.EqualTo("{{w|青銅の短剣}}ではタムの装甲を貫けない。[17]"));
    }

    [Test]
    public void Translate_AppliesArmorPenetrationPatternWithEnglishPossessive()
    {
        WritePatternDictionary(
            ("^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor with your (.+?)[.!] \\[(.+?)\\]$", "{1}では{0}の装甲を貫けない。[{2}]"));

        var translated = MessagePatternTranslator.Translate("You don't penetrate the snapjaw's armor with your iron longsword. [21]");

        Assert.That(translated, Is.EqualTo("iron longswordではsnapjawの装甲を貫けない。[21]"));
    }

    [Test]
    public void Translate_AppliesPossessiveCrackedPattern()
    {
        WritePatternDictionary(("^Your (.+?) was cracked\\.$", "{0}にひびが入った。"));

        var translated = MessagePatternTranslator.Translate("Your 布のローブ was cracked.");

        Assert.That(translated, Is.EqualTo("布のローブにひびが入った。"));
    }

    [Test]
    public void Translate_AppliesArmorPenetrationPatternWithoutWeaponRoll()
    {
        WritePatternDictionary(
            ("^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor[.!] \\[(.+?)\\]$", "{0}の装甲を貫けない。[{1}]"));

        var translated = MessagePatternTranslator.Translate("You don't penetrate タムの armor. [17]");

        Assert.That(translated, Is.EqualTo("タムの装甲を貫けない。[17]"));
    }

    [Test]
    public void Translate_AppliesShieldBlockPattern()
    {
        WritePatternDictionary(
            ("^You block with (.+)! \\(\\+(\\d+) AV\\)$", "{0}で防御した！ (+{1} AV)"));

        var translated = MessagePatternTranslator.Translate("You block with 乳棒! (+2 AV)");

        Assert.That(translated, Is.EqualTo("乳棒で防御した！ (+2 AV)"));
    }

    [Test]
    public void Translate_AppliesShieldStaggerPattern()
    {
        WritePatternDictionary(
            ("^You stagger (.+) with your shield block!$", "盾で受け止めて{0}をよろめかせた！"));

        var translated = MessagePatternTranslator.Translate("You stagger タム with your shield block!");

        Assert.That(translated, Is.EqualTo("盾で受け止めてタムをよろめかせた！"));
    }

    [Test]
    public void Translate_AppliesShieldStaggeredByMixedPossessivePattern()
    {
        WritePatternDictionary(
            ("^You are staggered by (?:the )?(.+?)(?:'s|s'|の) block!$", "{0}の防御でよろめいた！"));

        var translated = MessagePatternTranslator.Translate("You are staggered by タムの block!");

        Assert.That(translated, Is.EqualTo("タムの防御でよろめいた！"));
    }

    [Test]
    public void Translate_AppliesTerseMissPattern()
    {
        WritePatternDictionary(("^You miss!$", "攻撃は外れた！"));

        var translated = MessagePatternTranslator.Translate("You miss!");

        Assert.That(translated, Is.EqualTo("攻撃は外れた！"));
    }

    [Test]
    public void Translate_AppliesMissWithRollPatternStrippingPartialColor()
    {
        WritePatternDictionary(("^You miss! \\[(.+?) vs (.+?)\\]$", "攻撃は外れた！ [{0} vs {1}]"));

        var translated = MessagePatternTranslator.Translate("{{r|You miss!}} [12 vs 14]");

        Assert.That(translated, Is.EqualTo("{{r|攻撃は外れた！}} [12 vs 14]"));
    }

    [Test]
    public void Translate_AppliesIncomingMissWithRollPattern()
    {
        WritePatternDictionary(
            ("^(?:The |the |[Aa]n? )?(.+?) misses you! \\[(.+?) vs (.+?)\\]$", "{0}の攻撃は外れた！ [{1} vs {2}]"));

        var translated = MessagePatternTranslator.Translate("The snapjaw misses you! [8 vs 14]");

        Assert.That(translated, Is.EqualTo("snapjawの攻撃は外れた！ [8 vs 14]"));
    }

    [Test]
    public void Translate_AppliesMentalAttackNoEffectPattern()
    {
        WritePatternDictionary(
            ("^Your mental attack does not affect (.+?)\\.$", "あなたの精神攻撃は{0}に効かない。"));

        var translated = MessagePatternTranslator.Translate("Your mental attack does not affect the turret.");

        Assert.That(translated, Is.EqualTo("あなたの精神攻撃はthe turretに効かない。"));
    }

    [Test]
    public void Translate_AppliesFailToDealDamagePattern()
    {
        WritePatternDictionary(
            ("^You fail to deal damage with your attack! \\[(.+?)\\]$", "あなたの攻撃はダメージを与えられなかった！ [{0}]"));

        var translated = MessagePatternTranslator.Translate("You fail to deal damage with your attack! [17]");

        Assert.That(translated, Is.EqualTo("あなたの攻撃はダメージを与えられなかった！ [17]"));
    }

    [Test]
    public void Translate_AppliesIncomingWeaponMissPatternWithRollComparison()
    {
        WritePatternDictionary(
            ("^(.+) misses you with (?:his|her|its) (.+?)[.!] \\[(.+?) vs (.+?)\\]$", "{0}の{1}は外れた。[{2} vs {3}]"));

        var translated = MessagePatternTranslator.Translate("Naruur misses you with her 乳棒! [5 vs 11]");

        Assert.That(translated, Is.EqualTo("Naruurの乳棒は外れた。[5 vs 11]"));
    }

    [Test]
    public void Translate_AppliesNpcHitsSomethingPatternWithExclamation()
    {
        WritePatternDictionary(("^(.+?) hits something (.+?)[.!]?$", "{0}の射撃が{1}の何かに命中した。"));

        var translated = MessagePatternTranslator.Translate("Turret hits something to the east!");

        Assert.That(translated, Is.EqualTo("Turretの射撃がto the eastの何かに命中した。"));
    }

    [Test]
    public void Translate_AppliesFreezingEffectDamagePattern()
    {
        WritePatternDictionary(("^You take (\\d+) damage from (.+?)の freezing effect![.!]?$", "{1}の凍結効果で{0}ダメージを受けた！"));

        var translated = MessagePatternTranslator.Translate("You take 14 damage from 監視官イラメの freezing effect!");

        Assert.That(translated, Is.EqualTo("監視官イラメの凍結効果で14ダメージを受けた！"));
    }

    [Test]
    public void Translate_AppliesFreezingRayPattern()
    {
        WritePatternDictionary(("^(.+) emits a freezing ray from (?:his|her|its|their) hands![.!]?$", "{0}は手から凍結光線を放った！"));

        var translated = MessagePatternTranslator.Translate("監視官イラメ emits a freezing ray from her hands!");

        Assert.That(translated, Is.EqualTo("監視官イラメは手から凍結光線を放った！"));
    }

    [Test]
    public void Translate_AppliesIncomingWeaponHitPatternWithLeadingArticleOutsideCapture()
    {
        WritePatternDictionary(
            ("^(?:The )?(.+) hits \\((x\\d+)\\) for (\\d+) damage with (?:his|her|its) (.+?)[.!] \\[(.+?)\\]$", "{0}の{3}で{2}ダメージを受けた。({1}) [{4}]"));

        var translated = MessagePatternTranslator.Translate("The ウォーターヴァイン農家 hits (x2) for 4 damage with his 鉄の蔓刈り斧. [17]");

        Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家の鉄の蔓刈り斧で4ダメージを受けた。(x2) [17]"));
    }

    [Test]
    public void Translate_AppliesIncomingWeaponMissPatternWithLeadingArticleOutsideCapture()
    {
        WritePatternDictionary(
            ("^(?:The )?(.+) misses you with (?:his|her|its) (.+?)[.!] \\[(.+?) vs (.+?)\\]$", "{0}の{1}は外れた。[{2} vs {3}]"));

        var translated = MessagePatternTranslator.Translate("The ウォーターヴァイン農家 misses you with his 鉄の蔓刈り斧! [3 vs 7]");

        Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家の鉄の蔓刈り斧は外れた。[3 vs 7]"));
    }

    [Test]
    public void Translate_RepositoryDictionary_UsesPlayerHitWithRollPatternBeforeGenericHitPattern()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You hit (x1) for 1 damage with your レンチ! [18]");

        Assert.That(translated, Is.EqualTo("レンチで1ダメージを与えた。(x1) [18]"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesPlayerAcidDamageMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You take 1 damage from the 腐食性ガスの acid!");

        Assert.That(translated, Is.EqualTo("腐食性ガスの酸で1ダメージを受けた！"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesThirdPersonAcidDamageMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("The ワニ takes 1 damage from the 腐食性ガスの acid!");

        Assert.That(translated, Is.EqualTo("ワニは腐食性ガスの酸で1ダメージを受けた！"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesPlayerBleedingDamageMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You take 1 damage from bleeding.");

        Assert.That(translated, Is.EqualTo("あなたは出血で1ダメージを受けた。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_UsesSpecificWoundStopBleedingPatternBeforeGenericBleedingStopPattern()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("One of タムの wounds stops bleeding.");

        Assert.That(translated, Is.EqualTo("タムの傷のひとつの出血が止まった。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_FallsBackToEnglishWhenNoPatternMatches()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("This message should remain in English.");

        Assert.That(translated, Is.EqualTo("This message should remain in English."));
    }

    [Test]
    public void Translate_RepositoryDictionary_HandlesEmptyInput()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(string.Empty);

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesColorCodesOnFallback()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("&GThis specific phrase has no matching pattern.^k");

        Assert.That(translated, Is.EqualTo("&GThis specific phrase has no matching pattern.^k"));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesMarkerAndColorCodesOnFallback()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("\u0001&GThis specific phrase has no matching pattern.^k");

        Assert.That(translated, Is.EqualTo("\u0001&GThis specific phrase has no matching pattern.^k"));
    }

    [Test]
    public void Translate_AppliesPassByPattern()
    {
        WritePatternDictionary(("^You pass by a (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var translated = MessagePatternTranslator.Translate("You pass by a 編みかご.");

        Assert.That(translated, Is.EqualTo("編みかごのそばを通り過ぎた。"));
    }

    [Test]
    public void Translate_AppliesPassByPatternWithoutArticle()
    {
        WritePatternDictionary(("^You pass by (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var translated = MessagePatternTranslator.Translate("You pass by ウォーターヴァインと薄めの塩の水たまり.");

        Assert.That(translated, Is.EqualTo("ウォーターヴァインと薄めの塩の水たまりのそばを通り過ぎた。"));
    }

    [TestCase("north", "北")]
    [TestCase("south", "南")]
    [TestCase("east", "東")]
    [TestCase("west", "西")]
    [TestCase("northeast", "北東")]
    [TestCase("northwest", "北西")]
    [TestCase("southeast", "南東")]
    [TestCase("southwest", "南西")]
    public void Translate_AppliesDirectionalSeeAndStopFamily(string direction, string expectedDirection)
    {
        WritePatternDictionary((
            "^You see (.+?) to the (north|south|east|west|northeast|northwest|southeast|southwest) and stop moving[.!]?$",
            "{t1}に{0}が見えたので移動をやめた。"));
        WriteExactDictionary(("north", "北"), ("south", "南"), ("east", "東"), ("west", "西"), ("northeast", "北東"), ("northwest", "北西"), ("southeast", "南東"), ("southwest", "南西"));

        var translated = MessagePatternTranslator.Translate($"You see タム、ドロマド商人 to the {direction} and stop moving.");

        Assert.That(translated, Is.EqualTo($"{expectedDirection}にタム、ドロマド商人が見えたので移動をやめた。"));
    }

    [Test]
    public void Translate_AppliesGenericSultanHistoriesJournalPattern()
    {
        WritePatternDictionary(("^You note this piece of information in the Sultan Histories > (.+?) section of your journal\\.[.!]?$", "この情報をジャーナルの「スルタン史 > {0}」欄に記録した。"));

        var translated = MessagePatternTranslator.Translate("You note this piece of information in the Sultan Histories > Nashid I section of your journal.");

        Assert.That(translated, Is.EqualTo("この情報をジャーナルの「スルタン史 > Nashid I」欄に記録した。"));
    }

    [Test]
    public void Translate_AppliesJournalLocationFamily_WithTranslatedSectionCapture()
    {
        WritePatternDictionary((
            "^You note the location of (.+?) in the Locations > (.+?) section of your journal\\.[.!]?$",
            "ジャーナルの「場所 > {t1}」欄に{0}の場所を記録した。"));
        WriteExactDictionary(("Historic Sites", "史跡"));

        var translated = MessagePatternTranslator.Translate(
            "You note the location of Shagganip in the Locations > Historic Sites section of your journal.");

        Assert.That(translated, Is.EqualTo("ジャーナルの「場所 > 史跡」欄にShagganipの場所を記録した。"));
    }

    [Test]
    public void Translate_AppliesJournalJourneyPattern_WithTranslatedCapture()
    {
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));
        WriteExactDictionary(("Kyakukya", "キャクキャ"));

        var translated = MessagePatternTranslator.Translate("You journeyed to Kyakukya.");

        Assert.That(translated, Is.EqualTo("キャクキャに旅した。"));
    }

    [Test]
    public void Translate_AppliesJournalHiddenVillagePattern_WithTranslatedCapture()
    {
        WritePatternDictionary(("^You discovered the hidden village of (.+?)\\.$", "隠れ里{t0}を発見した。"));
        WriteExactDictionary(("Bey Lah", "ベイ・ラー"));

        var translated = MessagePatternTranslator.Translate("You discovered the hidden village of Bey Lah.");

        Assert.That(translated, Is.EqualTo("隠れ里ベイ・ラーを発見した。"));
    }

    [Test]
    public void Translate_AppliesJournalMapNoteLastVisitedPattern()
    {
        WritePatternDictionary(("^Last visited on the (.+?) of (.+?)$", "{1}の{0}日に最後に訪れた。"));

        var translated = MessagePatternTranslator.Translate("Last visited on the 5th of Ut yara Ux");

        Assert.That(translated, Is.EqualTo("Ut yara Uxの5th日に最後に訪れた。"));
    }

    [Test]
    public void Translate_AppliesDisassembleAndBitsReceiptPattern()
    {
        WritePatternDictionary(("^You disassemble the (.+?)\\. You receive tinkering bits <(.+?)>\\.[.!]?$", "{0}を分解し、修理ビット<{1}>を受け取った。"));

        var translated = MessagePatternTranslator.Translate("You disassemble the 奇妙な遺物. You receive tinkering bits <CD>.");

        Assert.That(translated, Is.EqualTo("奇妙な遺物を分解し、修理ビット<CD>を受け取った。"));
    }

    [Test]
    public void Translate_AppliesThirdPersonStandUpPattern()
    {
        WritePatternDictionary(("^(.+?) stands up[.!]?$", "{0}は立ち上がった。"));

        var translated = MessagePatternTranslator.Translate("タム stands up.");

        Assert.That(translated, Is.EqualTo("タムは立ち上がった。"));
    }

    [Test]
    public void Translate_AppliesFreezingWeaponDamagePattern()
    {
        WritePatternDictionary(("^(.+?) takes (\\d+) damage from your freezing weapon![.!]?$", "{0}はあなたの凍てつく武器で{1}ダメージを受けた！"));

        var translated = MessagePatternTranslator.Translate("血まみれのタム takes 1 damage from your freezing weapon!");

        Assert.That(translated, Is.EqualTo("血まみれのタムはあなたの凍てつく武器で1ダメージを受けた！"));
    }

    [Test]
    public void Translate_AppliesYellPattern()
    {
        WritePatternDictionary(("^(.+?) yells, '(.+)'$", "{0}は「{1}」と叫んだ。"));

        var translated = MessagePatternTranslator.Translate("The ウォーターヴァイン農家のメカニマス教徒改宗者 yells, 'Is it a dybbuk that possesses the robot? It should be sacred and still.'");

        Assert.That(translated, Is.EqualTo("The ウォーターヴァイン農家のメカニマス教徒改宗者は「Is it a dybbuk that possesses the robot? It should be sacred and still.」と叫んだ。"));
    }

    [Test]
    public void Translate_PreservesColorOwnershipForSpecialErosYellPattern()
    {
        WritePatternDictionary(("^E-Ros yells, 'I'm coming, (.+?)!'$", "E-Rosは「今行くよ、{0}！」と叫んだ"));

        var translated = MessagePatternTranslator.Translate("E-Ros yells, {{W|'I'm coming, リーダー!'}}");

        Assert.That(translated, Is.EqualTo("E-Rosは{{W|「今行くよ、リーダー！」}}と叫んだ"));
    }

    [Test]
    public void Translate_AppliesJoppaArrivalPattern()
    {
        WritePatternDictionary((
            "^On the (.+?) of (.+?), you arrive at the oasis-hamlet of Joppa, along the far rim of Moghra'yi, the Great Salt Desert\\.\\n\\nAll around you, moisture farmers tend to groves of viridian watervine\\. There are huts wrought from rock salt and brinestalk\\.\\n\\nOn the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth\\. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky\\.$",
            "{1}の{0}日、あなたは大塩砂漠モグライィの遥かな縁にあるオアシス集落ジョッパに到着した。\n\nあたりではウォーターヴァインの茂みを水耕農家たちが世話している。岩塩とブラインストークで組まれた小屋が建っている。\n\n地平線では、Qudのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。"));

        var source = "On the 27th of Uru Ux, you arrive at the oasis-hamlet of Joppa, along the far rim of Moghra'yi, the Great Salt Desert.\n\n" +
            "All around you, moisture farmers tend to groves of viridian watervine. There are huts wrought from rock salt and brinestalk.\n\n" +
            "On the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky.";

        var translated = MessagePatternTranslator.Translate(source);

        var expected = "Uru Uxの27th日、あなたは大塩砂漠モグライィの遥かな縁にあるオアシス集落ジョッパに到着した。\n\n" +
            "あたりではウォーターヴァインの茂みを水耕農家たちが世話している。岩塩とブラインストークで組まれた小屋が建っている。\n\n" +
            "地平線では、Qudのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。";

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void Translate_AppliesBlockedMovementPattern()
    {
        WritePatternDictionary(("^You stop moving because the (.+?) is in the way[.!]?$", "{0}が邪魔で移動をやめた。"));

        var translated = MessagePatternTranslator.Translate("You stop moving because the 泥灰岩 is in the way.");

        Assert.That(translated, Is.EqualTo("泥灰岩が邪魔で移動をやめた。"));
    }

    [Test]
    public void Translate_AppliesBlockedPathPatternWithArticle()
    {
        WritePatternDictionary(("^The way is blocked by a (.+?)[.!]?$", "{0}が道を塞いでいる。"));

        var translated = MessagePatternTranslator.Translate("The way is blocked by a 帆布.");

        Assert.That(translated, Is.EqualTo("帆布が道を塞いでいる。"));
    }

    [Test]
    public void Translate_AppliesBleedingDamagePattern()
    {
        WritePatternDictionary(("^(.+) takes (\\d+) damage from bleeding[.!]?$", "{0}は出血で{1}ダメージを受けた。"));

        var translated = MessagePatternTranslator.Translate("bloody Naruur takes 1 damage from bleeding.");

        Assert.That(translated, Is.EqualTo("bloody Naruurは出血で1ダメージを受けた。"));
    }

    [Test]
    public void Translate_AppliesLostSightPatternWithLeadingArticle()
    {
        WritePatternDictionary(("^You have lost sight of (?:the )?(.+?)[.!]?$", "{0}を見失った。"));

        var translated = MessagePatternTranslator.Translate("You have lost sight of the レシェフの神殿.");

        Assert.That(translated, Is.EqualTo("レシェフの神殿を見失った。"));
    }

    [Test]
    public void Translate_AppliesLostSightPattern()
    {
        WritePatternDictionary(("^You have lost sight of (.+?)[.!]?$", "{0}を見失った。"));

        var translated = MessagePatternTranslator.Translate("You have lost sight of bloody Naruur.");

        Assert.That(translated, Is.EqualTo("bloody Naruurを見失った。"));
    }

    [Test]
    public void Translate_AppliesJournalHistoryNotePattern()
    {
        WritePatternDictionary(("^You note this piece of information in the Sultan Histories > Resheph section of your journal\\.[.!]?$", "この情報をジャーナルの「スルタン史 > レシェフ」欄に記録した。"));

        var translated = MessagePatternTranslator.Translate("You note this piece of information in the Sultan Histories > Resheph section of your journal.");

        Assert.That(translated, Is.EqualTo("この情報をジャーナルの「スルタン史 > レシェフ」欄に記録した。"));
    }

    [Test]
    public void Translate_AppliesHarvestPattern()
    {
        WritePatternDictionary(("^The (.+?) harvests a (.+?)[.!]?$", "{0}は{1}を収穫した。"));

        var translated = MessagePatternTranslator.Translate("The ウォーターヴァイン農家 harvests a ヴァインウェハー.");

        Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家はヴァインウェハーを収穫した。"));
    }

    [Test]
    public void ShippedPatternFile_DoesNotKeepShadowedHarvestFallbackRegexes()
    {
        var repositoryRoot = TestProjectPaths.GetRepositoryRoot();
        var patternFile = Path.Combine(
            repositoryRoot,
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries",
            "messages.ja.json");
        var text = File.ReadAllText(patternFile);
        UseRepositoryPatternDictionary();
        var translated = MessagePatternTranslator.Translate("You harvest a 果実 from the 茂み.");

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Not.Contain("^You harvest (.+?) from (.+)\\.$"));
            Assert.That(text, Does.Not.Contain("^You harvest (.+?)\\.$"));
            Assert.That(text, Does.Not.Contain("^(.+?) harvests? (.+?) from (.+)\\.$"));
            Assert.That(text, Does.Not.Contain("^(.+?) harvests? (.+?)\\.$"));
            Assert.That(translated, Is.EqualTo("茂みから果実を収穫した"));
        });
    }

    [Test]
    public void Translate_AppliesDeathPattern()
    {
        WritePatternDictionary(("^You died\\.\\n\\nYou were killed by (.+?)[.!]?$", "あなたは死んだ。\n\n{0}に殺された。"));

        var translated = MessagePatternTranslator.Translate("You died.\n\nYou were killed by メフメット.");

        Assert.That(translated, Is.EqualTo("あなたは死んだ。\n\nメフメットに殺された。"));
    }

    [Test]
    public void Translate_AppliesWrappedDeathWrapperViaSharedFamily()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "{0}を聞いた。"));
        WriteExactDictionary(
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.KilledBy.Bare", "{killer}に殺された。"));

        var translated = MessagePatternTranslator.Translate("You died.\n\nYou were killed by a ウォーターヴァイン農家.");

        Assert.That(translated, Is.EqualTo("あなたは死んだ。\n\nウォーターヴァイン農家に殺された。"));
    }

    [Test]
    public void Translate_AppliesWrappedDeathWrapperWithFromPrepositionViaSharedFamily()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "{0}を聞いた。"));
        WriteExactDictionary(
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.DiedOfPoisonFrom.Bare", "{killer}の毒で死亡した。"));

        var translated = MessagePatternTranslator.Translate("You died.\n\nYou died of poison from a ウォーターヴァイン農家.");

        Assert.That(translated, Is.EqualTo("あなたは死んだ。\n\nウォーターヴァイン農家の毒で死亡した。"));
    }

    [Test]
    public void Translate_AppliesExplosionDeathWrapperViaSharedFamily()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "{0}を聞いた。"));
        WriteExactDictionary(
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.DiedInExplosionOf.Bare", "{killer}の爆発で死んだ。"),
            ("grenade", "グレネード"));

        var translated = MessagePatternTranslator.Translate("You died.\n\nYou died in the explosion of a grenade.");

        Assert.That(translated, Is.EqualTo("あなたは死んだ。\n\nグレネードの爆発で死んだ。"));
    }

    [Test]
    public void Translate_AppliesBareAccidentalDeathWrapperViaSharedFamily()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "{0}を聞いた。"));
        WriteExactDictionary(("QudJP.DeathWrapper.AccidentallyKilledBy.Bare", "{killer}にうっかり殺された。"));

        var translated = MessagePatternTranslator.Translate("You were accidentally killed by the ウォーターヴァイン農家.");

        Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家にうっかり殺された。"));
    }

    [Test]
    public void Translate_LogsDynamicTransformProbe_WhenPatternMatches()
    {
        WritePatternDictionary(("^You pass by (?:a |an |the )?(.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                MessagePatternTranslator.Translate("You pass by a ウォーターヴァイン.", "MessageLogPatch"),
                Is.EqualTo("ウォーターヴァインのそばを通り過ぎた。")));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("DynamicTextProbe/v1"));
            Assert.That(output, Does.Contain("route='MessagePatternTranslator'"));
            Assert.That(output, Does.Contain("family='^You pass by (?:a |an |the )?(.+?)[.!]?$'"));
            Assert.That(output, Does.Contain("source='You pass by a ウォーターヴァイン.'"));
            Assert.That(output, Does.Contain("translated='ウォーターヴァインのそばを通り過ぎた。'"));
        });
    }

    [Test]
    public void Translate_ReturnsOriginal_WhenPatternDoesNotMatch()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));

        var translated = MessagePatternTranslator.Translate("You begin moving.");

        Assert.That(translated, Is.EqualTo("You begin moving."));
    }

    [Test]
    public void Translate_LogsContext_WhenPatternDoesNotMatch()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));
        using var writer = new StringWriter();
        using var listener = new System.Diagnostics.TextWriterTraceListener(writer);
        System.Diagnostics.Trace.Listeners.Add(listener);

        try
        {
            var translated = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");

            listener.Flush();
            var output = writer.ToString();

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo("You begin moving."));
                Assert.That(output, Does.Contain("no pattern for 'You begin moving.'"));
                Assert.That(output, Does.Contain("context: MessageLogPatch"));
            });
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    [Test]
    public void Translate_ReturnsEmptyString_WhenInputIsNull()
    {
        WritePatternDictionary(("^You die![.!]?$", "あなたは死んだ！"));

        var translated = MessagePatternTranslator.Translate(null);

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_ReturnsEmptyString_WhenInputIsEmpty()
    {
        WritePatternDictionary(("^You die![.!]?$", "あなたは死んだ！"));

        var translated = MessagePatternTranslator.Translate(string.Empty);

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_LoadsPatternFileOnlyOnce_WhenCalledRepeatedly()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "あなたは{0}を聞いた"));

        var first = MessagePatternTranslator.Translate("You hear thunder.");
        var second = MessagePatternTranslator.Translate("You hear thunder.");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("あなたはthunderを聞いた"));
            Assert.That(second, Is.EqualTo("あなたはthunderを聞いた"));
            Assert.That(MessagePatternTranslator.LoadInvocationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Translate_RepeatedMissingPatternsRemainMeasurable()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));

        _ = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");
        _ = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");
        _ = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");

        Assert.Multiple(() =>
        {
            Assert.That(
                MessagePatternTranslator.GetMissingPatternHitCountForTests("You begin moving."),
                Is.EqualTo(3));
            Assert.That(
                MessagePatternTranslator.GetMissingRouteHitCountForTests("MessageLogPatch"),
                Is.EqualTo(3));
        });
    }

    [Test]
    public void Translate_MissingPatternLogging_IsThrottledToPowerOfTwoHits()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            _ = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");
            _ = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");
            _ = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");
            _ = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("hit 1"));
            Assert.That(output, Does.Contain("hit 2"));
            Assert.That(output, Does.Not.Contain("hit 3"));
            Assert.That(output, Does.Contain("hit 4"));
        });
    }

    [Test]
    public void Translate_MissingPatternSummary_RanksRoutes()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));

        _ = MessagePatternTranslator.Translate("You stop moving.", "PopupTranslationPatch");
        _ = MessagePatternTranslator.Translate("You begin moving.", "MessageLogPatch");
        _ = MessagePatternTranslator.Translate("You begin resting.", "MessageLogPatch");

        var summary = MessagePatternTranslator.GetMissingPatternSummaryForTests();
        var messageLogIndex = summary.IndexOf("MessageLogPatch=2", StringComparison.Ordinal);
        var popupIndex = summary.IndexOf("PopupTranslationPatch=1", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(summary, Does.Contain("MessageLogPatch=2"));
            Assert.That(summary, Does.Contain("PopupTranslationPatch=1"));
            Assert.That(messageLogIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(popupIndex, Is.GreaterThan(messageLogIndex));
        });
    }

    [Test]
    public void Translate_LogsPatternLoadSummary_AndDuplicatePatternDiagnostics()
    {
        WritePatternDictionary(
            ("^You hit (.+) for (\\d+) damage[.!]?$", "FIRST:{0}:{1}"),
            ("^You hit (.+) for (\\d+) damage[.!]?$", "SECOND:{0}:{1}"),
            ("^You miss (.+?)[.!]?$", "MISS:{0}"));

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                MessagePatternTranslator.Translate("You hit snapjaw for 2 damage."),
                Is.EqualTo("FIRST:snapjaw:2")));
        var summary = MessagePatternTranslator.GetPatternLoadSummaryForTests();

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("duplicate patterns: ^You hit (.+) for (\\d+) damage[.!]?$=1"));
            Assert.That(output, Does.Contain("loaded 3 pattern(s)"));
            Assert.That(summary, Does.Contain("2 unique"));
            Assert.That(summary, Does.Contain("1 duplicate pattern(s)"));
            Assert.That(summary, Does.Contain("1 distinct pattern(s)"));
        });
    }

    [Test]
    public void Translate_ThrowsFileNotFoundException_WhenPatternFileMissing()
    {
        MessagePatternTranslator.SetPatternFileForTests(Path.Combine(tempDirectory, "missing-messages.ja.json"));

        Assert.Throws<FileNotFoundException>(() => MessagePatternTranslator.Translate("You miss snapjaw."));
    }

    [Test]
    public void Translate_ThrowsSerializationException_WhenPatternJsonIsCorrupt()
    {
        WriteRawPatternFile("{\"patterns\":[{\"pattern\":\"^You miss (.+)$\",\"template\":\"{0}\"}");

        Assert.Throws<SerializationException>(() => MessagePatternTranslator.Translate("You miss snapjaw."));
    }

    [Test]
    public void Translate_ThrowsInvalidDataException_WhenPatternsArrayIsMissing()
    {
        WriteRawPatternFile("{}");

        Assert.Throws<InvalidDataException>(() => MessagePatternTranslator.Translate("You miss snapjaw."));
    }

    [Test]
    public void Translate_AppliesCookingAtePattern()
    {
        WritePatternDictionary(("^You eat the meal\\.$", "食事をとった。"));

        var translated = MessagePatternTranslator.Translate("You eat the meal.");

        Assert.That(translated, Is.EqualTo("食事をとった。"));
    }

    [Test]
    public void Translate_IgnoresRouteFieldInPatternEntries()
    {
        WriteRawPatternFile(
            "{\"patterns\":[{\"pattern\":\"^You miss (.+?)[.!]?$\",\"template\":\"{0}への攻撃は外れた\",\"route\":\"emit-message\"}]}");

        var translated = MessagePatternTranslator.Translate("You miss snapjaw.");

        Assert.That(translated, Is.EqualTo("snapjawへの攻撃は外れた"));
    }

    [Test]
    public void Translate_ThrowsInvalidDataException_WhenPatternEntryIsMalformed()
    {
        WriteRawPatternFile("{\"patterns\":[{\"pattern\":\"^You miss (.+)$\"}]}");

        Assert.Throws<InvalidDataException>(() => MessagePatternTranslator.Translate("You miss snapjaw."));
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"patterns\":[");
        AppendPatternEntries(builder, patterns);
        builder.AppendLine("]}");
        WritePatternFile(builder.ToString());
    }

    private void WriteRawPatternFile(string json)
    {
        WritePatternFile(json + Environment.NewLine);
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

        builder.AppendLine("]}");
        File.WriteAllText(Path.Combine(dictionaryDirectory, "ui-test.ja.json"), builder.ToString(), Utf8WithoutBom);
    }

    private static void AppendPatternEntries(StringBuilder builder, IReadOnlyList<(string pattern, string template)> patterns)
    {
        for (var index = 0; index < patterns.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (pattern, template) = patterns[index];
            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(template));
            builder.Append("\"}");
        }
    }

    private void WritePatternFile(string content)
    {
        File.WriteAllText(patternFilePath, content, Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void UseRepositoryPatternDictionary()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        MessagePatternTranslator.SetPatternFileForTests(null);
    }
}
