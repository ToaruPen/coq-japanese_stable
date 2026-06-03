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
    private string journalPatternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-description-text-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");
        journalPatternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(journalPatternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", Utf8WithoutBom);
        File.WriteAllText(journalPatternFilePath, "{\"patterns\":[]}\n", Utf8WithoutBom);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        JournalPatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateShortDescription_AppliesVillageDescriptionPattern()
    {
        WriteHistorySpiceDictionary(
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
        WriteHistorySpiceDictionary(
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
                "ブリスケットスプレッディングとして知られる料理を通じて、商人ギルドの聖性がテッガトゥムの人々に示された。\n" +
                "最初の旅する祈りのカーニバル以来、テッガトゥムの村人たちはブリスケットスプレッディングを喜んで食してきた。}}"));
    }

    [Test]
    public void TranslateLongDescription_TranslatesHistoricSceneWrappersAndNarrativeBodies()
    {
        WriteHistorySpiceDictionary(
            ("brisket", "ブリスケット"),
            ("spreading", "スプレッディング"),
            ("carnival", "カーニバル"),
            ("traveling", "旅する"),
            ("prayer", "祈り"),
            ("gleefully", "喜んで"));
        WriteDictionary(
            "world-mods.ja.json",
            ("Painted: This item is painted with a scene from the life of the ancient {0}:", "彩色: この品には古代の{0}の生涯の一場面が描かれている:"),
            ("Engraved: This item is engraved with a scene from the life of the ancient {0}:", "彫刻: この品には古代の{0}の生涯の一場面が彫り刻まれている:"),
            ("sultan", "スルタン"));
        WritePatternDictionary(
            (
                "^(?:\\{\\{C\\|)?Painted: This object is painted with a scene from the history of the village (.+?):(?:\\}\\})?$",
                "この物体には{0}村の歴史の一場面が描かれている:"),
            (
                "^(?:\\{\\{C\\|)?Engraved: This object is engraved with a scene from the history of the village (.+?):(?:\\}\\})?$",
                "この物体には{0}村の歴史の一場面が彫り刻まれている:"),
            (
                "^(?:\\{\\{C\\|)?Holographic: This hologram depicts a scene from the history of the village (.+?):(?:\\}\\})?$",
                "このホログラムには{0}村の歴史の一場面が描かれている:"),
            (
                "^The sanctity of (?:the )?(.+?) was revealed to the people of (.+?) through the dish known as (.+?)\\.(?:\\}\\})?$",
                "{t2}として知られる料理を通じて、{0}の聖性が{1}の人々に示された。"),
            (
                "^Since the first (.+?), the villagers of (.+?) have (.+?) feasted on (.+?)\\.(?:\\}\\})?$",
                "最初の{t0}以来、{1}の村人たちは{t3}を{t2}食してきた。"));
        WriteJournalPatternDictionary((
            "^In\\ (.+?)\\ (?:BR|AR),\\ (.+?)\\ won\\ a\\ decisive\\ victory\\ against\\ the\\ combined\\ forces\\ of\\ (.+?)\\ at\\ the\\ bloody\\ Battle\\ of\\ (.+?)\\.\\ As\\ a\\ result\\ of\\ the\\ battle,\\ (.+?)\\ was\\ so\\ (.+?)\\ that\\ it\\ was\\ renamed\\ (.+)\\.$",
            "{t0}年、{t1}は血塗られし{t3}の戦いにて、{t2}の連合軍に決定的勝利を収めた。この戦いの結果、{t4}はあまりに{t5}となり、{t6}と改め名づけられた。"));

        const string annalsLine = "In 1886 BR, イシル III won a decisive victory against the combined forces of アムル Manor at the bloody Battle of シェケスフ Hollow. As a result of the battle, シェケスフ Hollow was so rife with stray portals to other places and times that it was renamed Perpetualwreck.";
        const string translatedAnnalsLine = "1886年、イシル IIIは血塗られしシェケスフ Hollowの戦いにて、アムル Manorの連合軍に決定的勝利を収めた。この戦いの結果、シェケスフ Hollowはあまりにrife with stray portals to other places and timesとなり、Perpetualwreckと改め名づけられた。";
        const string villageBody =
            "The sanctity of the 商人ギルド was revealed to the people of テッガトゥム through the dish known as Brisket Spreading.\n" +
            "Since the first Carnival of the Traveling Prayer, the villagers of テッガトゥム have gleefully feasted on Brisket Spreading.";
        const string translatedVillageBody =
            "ブリスケットスプレッディングとして知られる料理を通じて、商人ギルドの聖性がテッガトゥムの人々に示された。\n" +
            "最初の旅する祈りのカーニバル以来、テッガトゥムの村人たちはブリスケットスプレッディングを喜んで食してきた。";

        var source =
            "{{cyan|Painted: This item is painted with a scene from the life of the ancient sultan {{magenta|イシル III}}:\n\n" + annalsLine + "}}\n" +
            "{{cyan|Engraved: This item is engraved with a scene from the life of the ancient sultan {{magenta|イシル III}}:\n\n" + annalsLine + "}}\n" +
            "{{cyan|Painted: This item is painted with a scene from the life of the ancient sultan {{magenta|\nイシル III}}:\n\n" + annalsLine + "}}\n" +
            "{{cyan|Engraved: This item is engraved with a scene from the life of the ancient sultan {{magenta|\nイシル III}}:\n\n" + annalsLine + "}}\n" +
            "<color=#44ff88>Engraved: This item is engraved with a scene from the life of the ancient sultan {{magenta|\nイシル III}}:\n\n" + annalsLine + "</color>\n" +
            "{{cyan|The tomb mural depicts a significant event from the life of the ancient sultan {{magenta|\nイシル III}}:\n\n" + annalsLine + "}}\n" +
            "{{cyan|The tomb mural depicts a significant event from the life of the ancient sultan {{magenta|イシル III}}:\n\n" + annalsLine + "}}\n" +
            "{{cyan|The tomb mural depicts a significant event from the life of the sultan {{magenta|イシル III}}:\n\n" + annalsLine + "}}\n" +
            "{{C|Painted: This object is painted with a scene from the history of the village {{M|テッガトゥム}}:\n\n" + villageBody + "}}\n" +
            "{{C|Engraved: This object is engraved with a scene from the history of the village {{M|テッガトゥム}}:\n\n" + villageBody + "}}\n" +
            "{{C|Holographic: This hologram depicts a scene from the history of the village {{M|テッガトゥム}}:\n\n" + villageBody + "}}\n" +
            "{{C|Painted: This object is painted with a scene from the history of the village {{M|\nテッガトゥム}}:\n\n" + villageBody + "}}\n" +
            "{{C|Engraved: This object is engraved with a scene from the history of the village {{M|\nテッガトゥム}}:\n\n" + villageBody + "}}\n" +
            "{{C|Holographic: This hologram depicts a scene from the history of the village {{M|\nテッガトゥム}}:\n\n" + villageBody + "}}\n" +
            "Its face bears a tattoo of a scene from the history of the village {{M|テッガトゥム}}{{C|: The sanctity of the 商人ギルド was revealed to the people of テッガトゥム through the dish known as Brisket Spreading.}}\n" +
            "{{C|Its face bears a tattoo of a scene from the history of the village {{M|テッガトゥム}}: The sanctity of the 商人ギルド was revealed to the people of テッガトゥム through the dish known as Brisket Spreading.}}";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo(
                "{{cyan|彩色: この品には古代のスルタン {{magenta|イシル III}}の生涯の一場面が描かれている:\n\n" + translatedAnnalsLine + "}}\n" +
                "{{cyan|彫刻: この品には古代のスルタン {{magenta|イシル III}}の生涯の一場面が彫り刻まれている:\n\n" + translatedAnnalsLine + "}}\n" +
                "{{cyan|彩色: この品には古代のスルタン {{magenta|\nイシル III}}の生涯の一場面が描かれている:\n\n" + translatedAnnalsLine + "}}\n" +
                "{{cyan|彫刻: この品には古代のスルタン {{magenta|\nイシル III}}の生涯の一場面が彫り刻まれている:\n\n" + translatedAnnalsLine + "}}\n" +
                "<color=#44ff88>彫刻: この品には古代のスルタン {{magenta|\nイシル III}}の生涯の一場面が彫り刻まれている:\n\n" + translatedAnnalsLine + "</color>\n" +
                "{{cyan|墓所の壁画には、古代のスルタン {{magenta|\nイシル III}}の生涯における重要な出来事が描かれている:\n\n" + translatedAnnalsLine + "}}\n" +
                "{{cyan|墓所の壁画には、古代のスルタン {{magenta|イシル III}}の生涯における重要な出来事が描かれている:\n\n" + translatedAnnalsLine + "}}\n" +
                "{{cyan|墓所の壁画には、スルタン {{magenta|イシル III}}の生涯における重要な出来事が描かれている:\n\n" + translatedAnnalsLine + "}}\n" +
                "{{C|この物体には{{M|テッガトゥム}}村の歴史の一場面が描かれている:\n\n" + translatedVillageBody + "}}\n" +
                "{{C|この物体には{{M|テッガトゥム}}村の歴史の一場面が彫り刻まれている:\n\n" + translatedVillageBody + "}}\n" +
                "{{C|このホログラムには{{M|テッガトゥム}}村の歴史の一場面が描かれている:\n\n" + translatedVillageBody + "}}\n" +
                "{{C|この物体には{{M|\nテッガトゥム}}村の歴史の一場面が描かれている:\n\n" + translatedVillageBody + "}}\n" +
                "{{C|この物体には{{M|\nテッガトゥム}}村の歴史の一場面が彫り刻まれている:\n\n" + translatedVillageBody + "}}\n" +
                "{{C|このホログラムには{{M|\nテッガトゥム}}村の歴史の一場面が描かれている:\n\n" + translatedVillageBody + "}}\n" +
                "その顔には{{M|テッガトゥム}}村の歴史の一場面を描いた刺青がある{{C|: ブリスケットスプレッディングとして知られる料理を通じて、商人ギルドの聖性がテッガトゥムの人々に示された。}}\n" +
                "{{C|その顔には{{M|テッガトゥム}}村の歴史の一場面を描いた刺青がある: ブリスケットスプレッディングとして知られる料理を通じて、商人ギルドの聖性がテッガトゥムの人々に示された。}}"));
    }

    [Test]
    public void TranslateShortDescription_AppliesStoneStatueGeneratedDescriptionPattern()
    {
        WritePatternDictionary((
            "^This statue worked from stone intricately depicts (?:the |a |an )?(.+?):$",
            "石から彫り出されたこの像は{0}を精緻に描いている:"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "This statue worked from stone intricately depicts a 山羊人の種播き:",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("石から彫り出されたこの像は山羊人の種播きを精緻に描いている:"));
    }

    [Test]
    public void TranslateLongDescription_TranslatesEarlyHistoricSceneChallengeSultanBody()
    {
        WriteHistorySpiceDictionary(
            ("aspirant", "野心家"),
            ("mollusks", "軟体動物"),
            ("drawn and quartered", "八つ裂きにされた"));
        WriteJournalPatternDictionary((
            "^((?:Early in))\\ (.+?)\\ (?:BR|AR),\\ (.+?)\\ was\\ challenged\\ by\\ (?:a|an|the)\\ (.+?)\\ to\\ a\\ duel\\ (?:over\\ the\\ rights\\ of|over\\ an\\ ordinance\\ mandating\\ the\\ practice\\ of|over\\ the\\ sanctioned\\ persecution\\ of)\\ (.+?)\\.\\ (.+?)\\ lost\\ and\\ was\\ (.+?)\\.\\ (.+?)\\ was\\ (.+?)\\ years\\ old\\.$",
            "{t1}{t0}、{t2}は{t4}を巡って{t3}に決闘を挑まれた。{t5}は敗れ、{t6}。{t7}は{t8}歳であった。"));

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            "{{cyan|Early in 3476 BR, ナレドゥクフト was challenged by an aspirant to a duel over the rights of mollusks. She lost and was drawn and quartered. She was 49 years old.}}",
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo("{{cyan|3476年初頭、ナレドゥクフトは軟体動物を巡って野心家に決闘を挑まれた。その者は敗れ、八つ裂きにされた。その者は49歳であった。}}"));
    }

    [TestCase(
        "Engraved: This item is engraved with a scene from the life of the ancient sultan ウーヒム I:",
        "彫刻: この品には古代のスルタン ウーヒム Iの生涯の一場面が彫り刻まれている:")]
    [TestCase("+5 Heat Resistance", "熱耐性+5")]
    [TestCase("+9 Cold Resistance", "冷気耐性+9")]
    [TestCase("+5 Electrical Resistance", "電撃耐性+5")]
    [TestCase("+12 Acid Resistance", "酸耐性+12")]
    [TestCase("+2 to hit", "命中+2")]
    [TestCase("+1 Ego", "自我+1")]
    [TestCase("+1 Agility", "敏捷+1")]
    [TestCase("You are water-bonded with him.", "あなたは彼と水の絆で結ばれている。")]
    [TestCase("You are water-bonded with her.", "あなたは彼女と水の絆で結ばれている。")]
    [TestCase("You are water-bonded with it.", "あなたはそれと水の絆で結ばれている。")]
    [TestCase("You are water-bonded with them.", "あなたは彼らと水の絆で結ばれている。")]
    [TestCase("You are water-bonded with a dromad caravan.", "あなたはドロマドのキャラバンと水の絆で結ばれている。")]
    [TestCase("You are water-bonded with {{Y|a dromad caravan}}.", "あなたは{{Y|ドロマドのキャラバン}}と水の絆で結ばれている。")]
    [TestCase("身体的特徴: flaming pseudopod、flaming pseudopod、flaming pseudopod、flaming pseudopod", "身体的特徴: {{fiery|燃え盛る}}仮足、{{fiery|燃え盛る}}仮足、{{fiery|燃え盛る}}仮足、{{fiery|燃え盛る}}仮足")]
    [TestCase("身体的特徴: 枝角、thick fur", "身体的特徴: 枝角、厚い毛皮")]
    [TestCase("Replaces Sprint with Power Skate (unlimited duration).", "スプリントをパワースケート（持続時間無制限）に置き換える。")]
    [TestCase("Emits plumes of fire when the wearer moves while power skating.", "パワースケート中に装備者が移動すると炎の噴煙を放つ。")]
    [TestCase("Replaces Jump with Rocket Jump.", "ジャンプをロケットジャンプに置き換える。")]
    [TestCase("This item's AV and DV bonuses are being averaged across all body parts of the same type.", "このアイテムのAVとDVボーナスは同じ種類の全身体部位で平均化されている。")]
    [TestCase("This item's AV and DV penalties are being averaged across all body parts of the same type.", "このアイテムのAVとDVペナルティは同じ種類の全身体部位で平均化されている。")]
    [TestCase("This item's AV and DV modifiers are being averaged across all body parts of the same type.", "このアイテムのAVとDV修正は同じ種類の全身体部位で平均化されている。")]
    [TestCase("This item's AV bonus is being averaged across all body parts of the same type.", "このアイテムのAVボーナスは同じ種類の全身体部位で平均化されている。")]
    [TestCase("This item's AV penalty is being averaged across all body parts of the same type.", "このアイテムのAVペナルティは同じ種類の全身体部位で平均化されている。")]
    [TestCase("This item's DV bonus is being averaged across all body parts of the same type.", "このアイテムのDVボーナスは同じ種類の全身体部位で平均化されている。")]
    [TestCase("This item's DV penalty is being averaged across all body parts of the same type.", "このアイテムのDVペナルティは同じ種類の全身体部位で平均化されている。")]
    [TestCase("-4 to saves vs. forced movement, knockdown, Being restrained", "強制移動・転倒・拘束に対するセーヴ-4")]
    [TestCase("-4 to saves vs. forced movement, knockdown, and bleeding", "強制移動・転倒・出血に対するセーヴ-4")]
    [TestCase("+5 to saves vs. being restrained", "拘束に対するセーヴ+5")]
    [TestCase("Being restrained", "拘束")]
    [TestCase("+2 DV while occupying the same tile as foliage", "植物と同じタイルにいる間DV+2")]
    [TestCase("At the center of a particularly thick copse, the vegetation clears. Flower-bedecked huts huddle in the clearing within, surrounded by phalanxes of tidy watervine rows and carefully-tended lah.", "ひときわ密な雑木林の中心で植生が開けている。花で飾られた小屋がその空き地に寄り集まり、整然としたウォーターヴァインの畝と丹念に世話されたラーの列に囲まれている。")]
    [TestCase("a dromad caravan", "ドロマドのキャラバン")]
    [TestCase("Notes:", "注記:")]
    [TestCase("It reads, '爆発物'.", "「爆発物」と書かれている。")]
    [TestCase("無秩序が時間の仮想反転で静止へと巻き取られ、金属の円盤に封じられている。動きの命が下るときだけ、凝集を解き放つ。 It's been disarmed.", "無秩序が時間の仮想反転で静止へと巻き取られ、金属の円盤に封じられている。動きの命が下るときだけ、凝集を解き放つ。 解除済み。")]
    [TestCase("On penetration, this weapon causes bleeding: 1 damage per round; save difficulty 26.", "貫通時、この武器は出血を引き起こす: 1ラウンドあたり1ダメージ; セーブ難度26。")]
    [TestCase("Swarm Alpha: As long as this creature is adjacent to his target, he grants 2 to the swarm bonuses of each other swarmer who is adjacent to his target.", "群れのアルファ: このクリーチャーが対象に隣接している限り、対象に隣接している他の各スウォーマーの群れボーナスに2を付与する。")]
    [TestCase("Swarm Alpha: As long as this creature is adjacent to her target, she grants 2 to the swarm bonuses of each other swarmer who is adjacent to her target.", "群れのアルファ: このクリーチャーが対象に隣接している限り、対象に隣接している他の各スウォーマーの群れボーナスに2を付与する。")]
    [TestCase("Swarm Alpha: As long as this creature is adjacent to its target, it grants 2 to the swarm bonuses of each other swarmer who is adjacent to its target.", "群れのアルファ: このクリーチャーが対象に隣接している限り、対象に隣接している他の各スウォーマーの群れボーナスに2を付与する。")]
    [TestCase("Swarm Alpha: As long as this creature is adjacent to their target, they grant 3 to the swarm bonuses of each other swarmer who is adjacent to their target.", "群れのアルファ: このクリーチャーが対象に隣接している限り、対象に隣接している他の各スウォーマーの群れボーナスに3を付与する。")]
    [TestCase("Swarm Alpha: As long as this creature is adjacent to qyr target, qe grants +2 to the swarm bonuses of each other swarmer who is adjacent to qyr target.", "群れのアルファ: このクリーチャーが対象に隣接している限り、対象に隣接している他の各スウォーマーの群れボーナスに2を付与する。")]
    [TestCase("Swarmer: This creature receives +1 to hit in melee and +1 to penetration rolls for each other hostile swarmer beyond the first who is in another square adjacent to its target. (currently +1)", "スウォーマー: 対象に隣接する別のマスにいる、最初の1体を超える敵対的なスウォーマー1体ごとに、このクリーチャーは近接命中+1と貫通ロール+1を得る。(現在+1)")]
    [TestCase("Swarmer: This creature receives +1 to hit in melee and +1 to penetration rolls for each other hostile swarmer beyond the first who is in another square adjacent to her target. (currently +0)", "スウォーマー: 対象に隣接する別のマスにいる、最初の1体を超える敵対的なスウォーマー1体ごとに、このクリーチャーは近接命中+1と貫通ロール+1を得る。(現在+0)")]
    [TestCase("Swarmer: This creature receives +1 to hit in melee and +1 to penetration rolls for each other hostile swarmer beyond the first who is in another square adjacent to their target. (currently +12)", "スウォーマー: 対象に隣接する別のマスにいる、最初の1体を超える敵対的なスウォーマー1体ごとに、このクリーチャーは近接命中+1と貫通ロール+1を得る。(現在+12)")]
    [TestCase("Swarmer: This creature receives +1 to hit in melee and +1 to penetration rolls for each other hostile swarmer beyond the first who is in another square adjacent to qyr target. (currently +2)", "スウォーマー: 対象に隣接する別のマスにいる、最初の1体を超える敵対的なスウォーマー1体ごとに、このクリーチャーは近接命中+1と貫通ロール+1を得る。(現在+2)")]
    [TestCase("Contains wiring enabling it to function as part of power grid, producing electrical charge.", "電力網の一部として機能する配線を備え、電荷を生成する。")]
    [TestCase("Contains plumbing enabling it to function as part of hydraulic transmission system, consuming hydraulic power.", "油圧伝達システムの一部として機能する配管を備え、油圧を消費する。")]
    [TestCase("Contains plumbing enabling it to function as part of hydraulic transmission system, producing hydraulic power.", "油圧伝達システムの一部として機能する配管を備え、油圧を生成する。")]
    [TestCase("This item is a named 猿毛のクローク.", "このアイテムは名前付きの猿毛のクロークである。")]
    [TestCase("Spray fire: This item can be fired while adjacent to multiple enemies without risk of the shot going wild.", "スプレーファイア: 複数の敵に隣接していても、このアイテムは射撃が逸れる危険なしに発射できる。")]
    [TestCase("装備して電源を入れると、Intelligence スコアが 2 上昇したかのように遺物鑑定へボーナスを得る。", "装備して電源を入れると、Intelligence スコアが 2 上昇したかのように遺物鑑定へボーナスを得る。")]
    [TestCase("It is powered off.", "電源が切れている。")]
    [TestCase("They are powered off.", "電源が切れている。")]
    [TestCase("When activated, +1 Strength", "起動時、筋力+1")]
    [TestCase("When activated, +1 Agility", "起動時、敏捷+1")]
    [TestCase("When activated, +1 Toughness", "起動時、頑健+1")]
    [TestCase("When activated, +1 Intelligence", "起動時、知力+1")]
    [TestCase("When activated, +1 Willpower", "起動時、意志力+1")]
    [TestCase("When activated, +1 Ego", "起動時、自我+1")]
    [TestCase("When activated, +1 Strength（無電力）", "起動時、筋力+1（無電力）")]
    [TestCase("When activated, +1 Agility（無電力）", "起動時、敏捷+1（無電力）")]
    [TestCase("{{rules|When activated, +1 Strength ({{K|unpowered}})}}", "{{rules|起動時、筋力+1（無電力）}}")]
    [TestCase("{{rules|When activated, +1 Agility ({{K|unpowered}})}}", "{{rules|起動時、敏捷+1（無電力）}}")]
    [TestCase("+2 Strength", "筋力+2")]
    [TestCase("-1 Toughness", "頑健-1")]
    [TestCase("When activated, +25% quickness", "起動時、クイックネス+25%")]
    [TestCase("Chance of becoming lost reduced by 10%.", "道に迷う確率が10%低下する。")]
    [TestCase("Its readout indicates that its startup sequence will take an estimated 7 more rounds.", "表示には、起動シーケンス完了まであとおよそ7ラウンドかかると示されている。")]
    [TestCase("Graffiti is scrawled across the surface. It reads: ", "表面に落書きが走り書きされている。そこにはこう書かれている: ")]
    [TestCase("表情豊かな顔と筋肉質な胴体は明らかに人間の血脈を示すが、厚い毛がまだらの脇腹を覆い、ぴくぴく動く尖った耳と誇らしい角は別の遺伝子の strand を語る。", "表情豊かな顔と筋肉質な胴体は明らかに人間の血脈を示すが、厚い毛がまだらの脇腹を覆い、ぴくぴく動く尖った耳と誇らしい角は別の遺伝子の系統を物語る。")]
    [TestCase("stuck in a 凍結した 黒い滲出液の水たまり", "凍結した 黒い滲出液の水たまりにはまっている")]
    [TestCase("+20% carry capacity", "運搬容量+20%")]
    [TestCase("Provides 100% reduction in 注射器を適用する際のエネルギー消費.", "注射器を適用する際のエネルギー消費が100%軽減される。")]
    [TestCase("This object has a broadcast power receiver that can pick up electrical charge either from satellites if not too far underground or from a nearby broadcast power transmitter.", "この物体にはブロードキャスト電力受信機があり、地下深すぎない場所では衛星から、または近くのブロードキャスト電力送信機から電荷を受け取れる。")]
    [TestCase("Reflects 5% damage back at your attackers, rounded up.", "攻撃者に受けたダメージの5%（端数切り上げ）を反射する。")]
    [TestCase("Gigantic: This item has twice the energy capacity and is much heavier than usual.", "巨大: このアイテムはエネルギー容量が2倍で、通常より大幅に重い。")]
    [TestCase("Fighting a 血まみれの 電気カタツムリ", "血まみれの 電気カタツムリと交戦中")]
    [TestCase("gold で作られた細やかな彫像で、a 徘徊迫撃砲 を表現している。", "金で作られた細やかな彫像で、徘徊迫撃砲を表現している。")]
    [TestCase("jasper で作られた細やかな彫像で、a イノシシ を表現している。", "碧玉で作られた細やかな彫像で、イノシシを表現している。")]
    [TestCase("5555年、ウーヒム、Sorrow of ダビッパ、Cyan Bad Omenは自然の理により身罷った。その者は享年90歳であった。", "5555年、ウーヒム、ダビッパの悲哀、青き凶兆は自然の理により身罷った。その者は享年90歳であった。")]
    [TestCase("3423年、ウーヒム IV、Shining Heir of 犬、Wife to テッム、Bane of ナシャンは自然の理により身罷った。その者は享年88歳であった。", "3423年、ウーヒム IV、犬の輝く後継者、テッムの妻、ナシャンの災いは自然の理により身罷った。その者は享年88歳であった。")]
    [TestCase("At daybreak on the first day of autumn、ひとりの嬰児（with colossal mace in each hand）がin the mouth of a she-wolfにて産着に包まれて見いだされた。その嬰児はのちにウーヒム IIとして知られるようになった。", "秋の第一日、夜明けに、両手に巨大なメイスを握ったひとりの嬰児が雌狼の口の中で産着に包まれて見いだされた。その嬰児はのちにウーヒム IIとして知られるようになった。")]
    [TestCase("While、visiting an obscure observatory in the Jewelersの Province of ドゥシュル, ウーヒム IV fabricated horoscope reading that evoked the presence of lucent ruby. SheはそれをRubycusと名づけた。", "宝石商の州ドゥシュルの無名の天文台を訪れていたとき、ウーヒム IVは透明なルビーの存在を呼び起こす星占いを作り上げた。彼女はそれをRubycusと名づけた。")]
    [TestCase("After treating with 昆虫, ウーヒム IV convinced them to help her found observatory in the Stargazersの Province of カルクヘタラ for the purpose of mapping stars to the shapes of jewels. They named it the Jeweled O...", "昆虫と交渉した後、ウーヒム IVは宝石の形に星を対応づける目的で、カルクヘタラの星見の州に天文台を創設する手助けをするよう彼らを説得した。彼らはそれをJeweled O...と名づけた。")]
    [TestCase("Credits remaining: 2\u009B", "残りクレジット: 2\u009B")]
    [TestCase("Creates: バイオダイナミック セル", "作成物：バイオダイナミック セル")]
    [TestCase("Deactivated: Currently without power.", "停止中: 現在電力がない。")]
    [TestCase("Integrated power systems: When equipped, you can power this device via Electrical Generation.", "統合電力システム: 装備中、発電でこの装置に電力を供給できる。")]
    [TestCase("Fitted with cleats: +2 to saves vs. forced movement、knockdown、とbeing restrained", "クリート付き: 強制移動・転倒・拘束に対するセーヴ+2")]
    public void TranslateShortDescription_TranslatesRuntimeObservedDescriptionLines(
        string source,
        string expected)
    {
        var translated = DescriptionTextTranslator.TranslateShortDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslateShortDescription_TranslatesNamedItemCaptureThroughDisplayNameRoute()
    {
        WriteHistorySpiceDictionary(("potent", "強大な"), ("ghost", "幽鬼"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "This item is a named shrine to ウーヒム II, the Potent Ghost.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("このアイテムは名前付きのウーヒム II、強大な幽鬼の祠である。"));
    }

    [Test]
    public void TranslateShortDescription_TranslatesEnergyCostReductionScopeThroughDisplayNameRoute()
    {
        WriteDictionary("ui-displayname-atomic.ja.json", ("applying tonics energy cost", "トニック適用時のエネルギー消費"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "Provides 50% reduction in applying tonics energy cost.",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("トニック適用時のエネルギー消費が50%軽減される。"));
    }

    [TestCase("Creates: biodynamic cell", "作成物：バイオダイナミック セル")]
    [TestCase("Creates: {{Y|biodynamic cell}}", "作成物：{{Y|バイオダイナミック セル}}")]
    public void TranslateShortDescription_TranslatesCreatesItemThroughDisplayNameRoute(
        string source,
        string expected)
    {
        WriteDictionary("ui-displayname-atomic.ja.json", ("biodynamic cell", "バイオダイナミック セル"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslateShortDescription_TranslatesFightingTargetThroughDisplayNameRoute()
    {
        WriteDictionary("ui-displayname-atomic.ja.json", ("bloody electric snail", "血まみれの電気カタツムリ"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "Fighting a {{Y|bloody electric snail}}",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("{{Y|血まみれの電気カタツムリ}}と交戦中"));
    }

    [Test]
    public void TranslateShortDescription_TranslatesRuntimeObservedRandomStatueSubjectThroughDisplayNameRoute()
    {
        WriteDictionary("ui-displayname-atomic.ja.json", ("snapjaw scavenger", "スナップジョーのあさり屋"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "gold で作られた細やかな彫像で、a {{Y|snapjaw scavenger}} を表現している。",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("金で作られた細やかな彫像で、{{Y|スナップジョーのあさり屋}}を表現している。"));
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
    public void TranslateLongDescription_LeavesTooltipStatNameFragmentsUnchanged()
    {
        const string source = "装備して電源を入れると、Intelligence スコアが 2 上昇したかのように遺物鑑定へボーナスを得る。";

        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo(source));
    }

    [TestCase("Regains charge when worn or held in hand, much more quickly while in combat.")]
    [TestCase("Regains charge when wornまたはheld in hand, much more quickly while in combat.")]
    public void TranslateLongDescription_TranslatesRegainsChargeWhenWornOrHeldLine(string source)
    {
        var translated = DescriptionTextTranslator.TranslateLongDescription(
            source,
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("装備中または手に持っているとチャージが回復する。戦闘中は大幅に速く回復する。"));
    }

    [Test]
    public void TranslateShortDescription_TranslatesRegainsChargeWhenWornOrHeldLinePreservingColorWrapper()
    {
        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "{{rules|Regains charge when worn or held in hand, much more quickly while in combat.}}",
            "DescriptionTextTranslatorTests");

        Assert.That(translated, Is.EqualTo("{{rules|装備中または手に持っているとチャージが回復する。戦闘中は大幅に速く回復する。}}"));
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
    public void TranslateShortDescription_TranslatesWorldModsReputationLine_WithLocalizedFaction()
    {
        WriteContextDictionary(
            "world-mods.ja.json",
            ("XRL.World.Parts.AddsRep.AppendDescription", "+{0} reputation with {1}", "{1}との評判{0:+#;-#}"));

        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "-200 reputation with 猫",
            "DescriptionShortDescriptionPatch");

        Assert.That(translated, Is.EqualTo("猫との評判-200"));
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
    public void TranslateShortDescription_TranslatesRuntimeObservedWallAndMoveSpeedRules()
    {
        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "+20 penetration vs. walls.\nDestroys  walls after 8 penetrating hits.\n+8 move speed",
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo(
                "壁に対する貫通+20。\n" +
                "8回の貫通ヒット後に壁を破壊する。\n" +
                "移動速度+8"));
    }

    [Test]
    public void TranslateShortDescription_TranslatesPoweredWallAndNegativeMoveSpeedRules()
    {
        var translated = DescriptionTextTranslator.TranslateShortDescription(
            "When powered, +20 penetration vs. walls.\nWhen powered, destroys  walls after 8 penetrating hits.\n-25 move speed",
            "DescriptionTextTranslatorTests");

        Assert.That(
            translated,
            Is.EqualTo(
                "電源投入時、壁に対する貫通+20。\n" +
                "電源投入時、8回の貫通ヒット後に壁を破壊する。\n" +
                "移動速度-25"));
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

    private void WriteJournalPatternDictionary(params (string pattern, string template)[] patterns)
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
        File.WriteAllText(journalPatternFilePath, builder.ToString(), Utf8WithoutBom);
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
    {
        WriteDictionary("ui-test.ja.json", entries);
    }

    private void WriteHistorySpiceDictionary(params (string key, string text)[] entries)
    {
        WriteDictionary(Path.Combine("Scoped", "historyspice-common.ja.json"), entries);
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
        var path = Path.Combine(dictionaryDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, builder.ToString(), Utf8WithoutBom);
    }

    private void WriteContextDictionary(string fileName, params (string context, string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"context\":\"");
            builder.Append(EscapeJson(entries[index].context));
            builder.Append("\",\"key\":\"");
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
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
