using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class WorldModsTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-world-mods-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslate_UsesScopedExactLookup()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Airfoil: This item can be thrown at +4 throwing range.", "エアフォイル: この品は投擲射程が+4される。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Airfoil: This item can be thrown at +4 throwing range.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("エアフォイル: この品は投擲射程が+4される。"));
        });
    }

    [Test]
    public void TryTranslate_UsesExactLookupForTinkeringModDescription()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("lacquered :: Item cannot rust.", "漆仕上げ :: 錆びない。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "lacquered :: Item cannot rust.",
            "TinkeringDetailsLineTranslationPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("漆仕上げ :: 錆びない。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesColorsForScopedExactLookup()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Scoped: This weapon has increased accuracy.", "スコープ付き: この武器は命中精度が向上する。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "{{Y|Scoped: This weapon has increased accuracy.}}",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{Y|スコープ付き: この武器は命中精度が向上する。}}"));
        });
    }

    [Test]
    public void TryTranslate_DoesNotUseContextualExactLeaf_WhenContextIsNotSpecified()
    {
        WriteDictionaryWithContext(
            "world-mods.ja.json",
            ("XRL.World.Parts.ModBiomech.GetShortDescription", "Scoped: Context-only text.", "スコープ付き: 文脈専用。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Scoped: Context-only text.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo("Scoped: Context-only text."));
        });
    }

    [TestCase("Biomech: Has biomechanical power transmission systems.", "バイオメカ: 生体機械式の動力伝達機構を備える。")]
    [TestCase("Fitted with filters: This item protects against breathing in dangerous gases.", "フィルター付き: 有害なガスを吸い込むのを防ぐ。")]
    [TestCase("Airfoil: This item can be thrown at +4 throwing range.", "エアフォイル: この品は投擲射程が+4される。")]
    [TestCase("Extradimensional: This item recently materialized in this dimension having inherited some properties from its home dimension, {{O|", "異次元由来: この品は元いた次元からいくつかの特性を持ったまま最近この次元に出現した、{{O|")]
    [TestCase("Gesticulating: This item grants +", "蠢く: この装備で筋力が+")]
    public void TryTranslate_RepositoryDictionary_TranslatesPrefixOwnedContextExactDescriptions(
        string source,
        string expected)
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(
            Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries"));
        ScopedDictionaryLookup.ResetForTests();

        var ok = WorldModsTextTranslator.TryTranslate(
            source,
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_RepositoryDictionary_TranslatesShieldRulesContextWithLeadingNewline()
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(
            Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries"));
        ScopedDictionaryLookup.ResetForTests();

        var ok = WorldModsTextTranslator.TryTranslate(
            "\n{{rules|Shields only grant their AV when you successfully block an attack.}}",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("\n{{rules|盾は攻撃をブロックしたときにのみAVを付与する。}}"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesImprovedMutationTemplate()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Grants you {0} at level {1}. If you already have {0}, its level is increased by {1}.", "{0}をレベル{1}で得る。すでに{0}を持っている場合、そのレベルが{1}上昇する。"),
            ("Temporal Fugue", "時間遁走"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Grants you Temporal Fugue at level 3. If you already have Temporal Fugue, its level is increased by 3.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("時間遁走をレベル3で得る。すでに時間遁走を持っている場合、そのレベルが3上昇する。"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesJewelEncrustedTemplateFromScopedWorldModsDictionary()
    {
        WriteDictionaryWithContext(
            "world-mods.ja.json",
            (
                "XRL.World.Parts.ModJewelEncrusted.GetShortDescription",
                "Jewel-Encrusted: This item is much more valuable than usual and grants the wearer {0} reputation with water barons.",
                "宝石象嵌: この品は通常よりはるかに高価で、装着者に水の男爵たちとの評判{0}を与える。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Jewel-Encrusted: This item is much more valuable than usual and grants the wearer +100 reputation with water barons.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("宝石象嵌: この品は通常よりはるかに高価で、装着者に水の男爵たちとの評判+100を与える。"));
        });
    }

    [TestCase(
        "Anti-gravity: When powered, this item's weight is reduced by 20% plus 2 lbs.",
        "反重力: 通電中、この品の重量は20%減り、さらに2lbs軽くなる。")]
    [TestCase(
        "Co-processor: When powered, this item grants +2 Intelligence and provides 13 units of compute power to the local lattice.",
        "共同処理装置: 通電中、知力に+2を与え、局所格子に13ユニットの演算力を供給する。")]
    [TestCase(
        "Co-Processor: When powered, this item grants bonus Intelligence and provides compute power to the local lattice.",
        "共同処理装置: 通電中、知力にボーナスを与え、局所格子に演算力を供給する。")]
    [TestCase(
        "Counterweighted: Adds +2 to hit.",
        "つり合い調整: 命中に+2のボーナスを与える。")]
    [TestCase(
        "Counterweighted: Adds a bonus to hit.",
        "つり合い調整: 命中にボーナスを与える。")]
    [TestCase(
        "Displacer: When powered, this weapon randomly teleports its target 1-6 tiles away on a successful hit.",
        "位相転移: 通電中、この武器は命中時に対象を無作為に1-6マス離れた場所へ転移させる。")]
    [TestCase(
        "Fitted with beamsplitter: This weapon has a 3-way spread with each shot at -1 penetration roll.",
        "ビームスプリッタ装着: この武器は1射撃ごとに3方向へ拡散し、各射撃の貫通判定が-1される。")]
    [TestCase(
        "Electrified: When powered, this weapon deals an additional 2-3 electrical damage on hit.",
        "帯電: 通電中、この武器は命中時に追加で2-3の電撃ダメージを与える。")]
    [TestCase(
        "Flaming: When powered, this weapon deals additional heat damage on hit.",
        "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。")]
    [TestCase(
        "Freezing: When powered, this weapon deals additional cold damage on hit.",
        "凍結: 通電中、この武器は命中時に追加の冷気ダメージを与える。")]
    [TestCase(
        "Feathered: This item grants the wearer +250 reputation with birds.",
        "羽飾り: 装着者に鳥類との評判+250を与える。")]
    [TestCase(
        "Scaled: This item grants the wearer +250 reputation with unshelled reptiles.",
        "鱗状の: 装着者に甲無し爬虫類との評判+250を与える。")]
    [TestCase(
        "Snail-Encrusted: This item is crawling with tiny snails and grants the wearer +250 reputation with mollusks.",
        "巻貝まみれ: 小さなカタツムリが這っており、装着者に軟体動物との評判+250を与える。")]
    [TestCase(
        "+200 reputation with the Issachari tribe",
        "イッサカリ族との評判+200")]
    [TestCase(
        "+200 reputation with the Mechanimists",
        "the Mechanimistsとの評判+200")]
    [TestCase(
        "+200 reputation with the イッサカリ族",
        "イッサカリ族との評判+200")]
    [TestCase(
        "{{rules|+200 reputation with the Issachari tribe}}",
        "{{rules|イッサカリ族との評判+200}}")]
    [TestCase(
        "+200 reputation with {{Y|the Issachari tribe}}",
        "{{Y|イッサカリ族}}との評判+200")]
    [TestCase(
        "-100 reputation with the Issachari tribe",
        "イッサカリ族との評判-100")]
    [TestCase(
        "{{W|Co-processor: When powered, this item grants +2 Intelligence and provides 13 units of compute power to the local lattice.}}",
        "{{W|共同処理装置: 通電中、知力に+2を与え、局所格子に13ユニットの演算力を供給する。}}")]
    [TestCase(
        "{{rules|Co-Processor: When powered, this item grants bonus Intelligence and provides compute power to the local lattice.}}",
        "{{rules|共同処理装置: 通電中、知力にボーナスを与え、局所格子に演算力を供給する。}}")]
    [TestCase(
        "Offhand Attack Chance: 15%",
        "オフハンド命中率: 15%")]
    [TestCase(
        "Microserrated: This weapon has 15% chance to dismember opponents.",
        "微鋸歯: この武器は15%の確率で敵を切断する。")]
    [TestCase(
        "Nanon: 15% chance to dismember on penetration",
        "ナノ刃: 貫通時に15%の確率で切断する。")]
    [TestCase(
        "Serrated: This weapon has 15% chance to dismember opponents.",
        "鋸歯: この武器は15%の確率で敵を切断する。")]
    [TestCase(
        "Liquid-cooled: This weapon's rate of fire is increased, but it requires pure water to function. When fired, there's a one in 7 chance that 1 dram is consumed.",
        "液冷式: この武器の連射数は増えるが、機能するには純粋な水が必要だ。発射時には7分の1の確率で1ドラム消費する。")]
    [TestCase(
        "Heartstopper: When powered, this weapon has 15% chance to put opponents into cardiac arrest.",
        "心停止: 通電中、この武器は15%の確率で敵を心停止させる。")]
    [TestCase(
        "Heartstopper: When powered, this weapon has 15% chance to put opponents into cardiac arrest if they fail a difficulty 20 Toughness save.",
        "心停止: 通電中、この武器は15%の確率で、敵が難易度20の頑健セーヴに失敗した場合に心停止させる。")]
    [TestCase(
        "Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives a bonus to hit a target aimed at.",
        "スマート: 通電して起動し、使用者がHUDかテックスキャナーを装備している場合、この武器の追尾スコープは精度を高め、照準した対象への命中にボーナスを与える。")]
    [TestCase(
        "Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives +2 to hit a target aimed at.",
        "スマート: 通電して起動し、使用者がHUDかテックスキャナーを装備している場合、この武器の追尾スコープは精度を高め、照準した対象への命中に+2のボーナスを与える。")]
    [TestCase(
        "When equipped and powered, provides 10 units of compute power to the local lattice.",
        "装備・通電中、局所格子に10ユニットの演算力を供給する。")]
    [TestCase(
        "When equipped and powered, provides 10 units of compute power to the local lattice. (unpowered)",
        "装備・通電中、局所格子に10ユニットの演算力を供給する。（無電力）")]
    [TestCase(
        "{{rules|When equipped and powered, provides 10 units of compute power to the local lattice. (unpowered)}}",
        "{{rules|装備・通電中、局所格子に10ユニットの演算力を供給する。（無電力）}}")]
    [TestCase(
        "When equipped、powered、とin use, provides light in radius 2.",
        "装備・通電・使用中、半径2に光を提供する。")]
    [TestCase(
        "{{rules|When 装備中、給電中、と使用中, provides light in radius 2.}}",
        "{{rules|装備・通電・使用中、半径2に光を提供する。}}")]
    [TestCase(
        "25% chance per turn to repel gases near its wielderまたはwearer.",
        "使用者または着用者近くのガスを毎ターン25%の確率で退ける。")]
    [TestCase(
        "{{rules|25% chance per turn to repel gases near its wielderまたはwearer.}}",
        "{{rules|使用者または着用者近くのガスを毎ターン25%の確率で退ける。}}")]
    [TestCase(
        "Gigantic: This item is much heavier than usual and can only be equipped by gigantic creatures.",
        "巨大: この品は通常より大幅に重くなり、巨大な生物しか装備できない。")]
    [TestCase(
        "Gigantic: This weapon has +3 damage and cleaves for -3 AV. It can only be equipped by gigantic creatures.",
        "巨大: この武器はダメージ+3、装甲切断でAV-3を与える。これは巨大な生物しか装備できない。")]
    [TestCase(
        "{{rules|Gigantic: This weapon has +3 damage and is twice as effective when you Slam with it. It must be wielded four-handed by non-gigantic creatures.}}",
        "{{rules|巨大: この武器はダメージ+3、スラム時の効果が2倍になる。これは巨大でない生物が扱うには四手持ちが必要。}}")]
    [TestCase(
        "Gigantic: These items hold twice as much liquid, have twice the energy capacity, contain double the tonic dosage, and dig twice as fast.",
        "巨大: これらの品は液体容量が2倍になり、エネルギー容量が2倍になり、トニック用量が2倍になり、掘削速度が2倍になる。")]
    public void TryTranslate_TranslatesDynamicWorldModsTemplates(string source, string expected)
    {
        WriteDynamicWorldModsDictionary();

        var ok = WorldModsTextTranslator.TryTranslate(
            source,
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCaseSource(nameof(RepositoryWeaponModDescriptions))]
    public void TryTranslate_RepositoryDictionary_TranslatesWeaponModDescriptions(string source, string expected)
    {
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(
            Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries"));
        ScopedDictionaryLookup.ResetForTests();

        var ok = WorldModsTextTranslator.TryTranslate(
            source,
            "TinkeringDetailsLineTranslationPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_DoesNotUseGlobalFallbackForWorldModTemplate()
    {
        WriteDictionary("world-mods.ja.json");
        WriteDictionary(
            "ui-default.ja.json",
            (
                "Displacer: When powered, this weapon randomly teleports its target {0} tiles away on a successful hit.",
                "誤った経路: {0}"));

        const string Source = "Displacer: When powered, this weapon randomly teleports its target 1-6 tiles away on a successful hit.";
        var ok = WorldModsTextTranslator.TryTranslate(
            Source,
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(Source));
        });
    }

    [Test]
    public void TryTranslate_DoesNotUseGlobalFallbackForWorldModExactDescription()
    {
        WriteDictionary("world-mods.ja.json");
        WriteDictionary(
            "ui-default.ja.json",
            ("Scoped: This weapon has increased accuracy.", "誤った経路"));

        const string Source = "Scoped: This weapon has increased accuracy.";
        var ok = WorldModsTextTranslator.TryTranslate(
            Source,
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(Source));
        });
    }

    [Test]
    public void TryTranslate_LiquidCooledUsesLiquidOwnerDictionaryForRequirement()
    {
        WriteDictionary(
            "world-mods.ja.json",
            (
                "Liquid-cooled: This weapon's rate of fire is increased, but it requires {0} to function. When fired, there's a one in {1} chance that 1 dram is consumed.",
                "液冷式: この武器の連射数は増えるが、機能するには{0}が必要だ。発射時には{1}分の1の確率で1ドラム消費する。"));
        WriteDictionary(
            "ui-default.ja.json",
            ("water", "誤った経路"));
        WriteDictionaryWithContext(
            "ui-liquids.ja.json",
            ("XRL.Liquids", "water", "水"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Liquid-cooled: This weapon's rate of fire is increased, but it requires pure {{c|water}} to function. When fired, there's a one in 7 chance that 1 dram is consumed.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(
                translated,
                Is.EqualTo("液冷式: この武器の連射数は増えるが、機能するには純粋な{{c|水}}が必要だ。発射時には7分の1の確率で1ドラム消費する。"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesStatContractLabelsInTooltipTemplates()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"));
        WriteDictionary(
            "world-mods.ja.json",
            ("Co-Processor: When powered, this item grants {0} {1} and provides {2} units of compute power to the local lattice.", "共同処理装置: 通電中、{1}に{0}を与え、局所格子に{2}ユニットの演算力を供給する。"),
            ("Intelligence", "知力"));

        var strengthOk = WorldModsTextTranslator.TryTranslate(
            "Strength Bonus Cap: 4",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var strengthTranslated);
        var intelligenceOk = WorldModsTextTranslator.TryTranslate(
            "Co-processor: When powered, this item grants +2 Intelligence and provides 13 units of compute power to the local lattice.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var intelligenceTranslated);
        var egoOk = WorldModsTextTranslator.TryTranslate(
            "Ego Bonus Cap: 2",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var egoTranslated);
        var agilityOk = WorldModsTextTranslator.TryTranslate(
            "Agility Bonus Cap: no limit",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var agilityTranslated);
        var toughnessOk = WorldModsTextTranslator.TryTranslate(
            "Toughness Bonus Cap: 5",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var toughnessTranslated);
        var willpowerOk = WorldModsTextTranslator.TryTranslate(
            "Willpower Bonus Cap: 6",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var willpowerTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(strengthOk, Is.True);
            Assert.That(strengthTranslated, Is.EqualTo("筋力ボーナス上限: 4"));
            Assert.That(intelligenceOk, Is.True);
            Assert.That(
                intelligenceTranslated,
                Is.EqualTo("共同処理装置: 通電中、知力に+2を与え、局所格子に13ユニットの演算力を供給する。"));
            Assert.That(egoOk, Is.True);
            Assert.That(egoTranslated, Is.EqualTo("自我ボーナス上限: 2"));
            Assert.That(agilityOk, Is.True);
            Assert.That(agilityTranslated, Is.EqualTo("敏捷ボーナス上限: なし"));
            Assert.That(toughnessOk, Is.True);
            Assert.That(toughnessTranslated, Is.EqualTo("頑健ボーナス上限: 5"));
            Assert.That(willpowerOk, Is.True);
            Assert.That(willpowerTranslated, Is.EqualTo("意志力ボーナス上限: 6"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesMasterworkTemplateFromScopedWorldModsDictionary()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Masterwork: This weapon scores critical hits {0} of the time instead of 5%.", "傑作: この武器のクリティカル発生率は{0}（通常は5%）。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "{{rules|Masterwork: This weapon scores critical hits 15% of the time instead of 5%.}}",
            "LookTooltipContentPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{rules|傑作: この武器のクリティカル発生率は15%（通常は5%）。}}"));
        });
    }

    [Test]
    public void TryTranslate_PreservesColoredMasterworkValueWhenTemplateMovesCapture()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Masterwork: This weapon scores critical hits {0} of the time instead of 5%.", "傑作: この武器のクリティカル発生率は{0}（通常は5%）。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "{{rules|Masterwork: This weapon scores critical hits {{W|15%}} of the time instead of 5%.}}",
            "LookTooltipContentPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{rules|傑作: この武器のクリティカル発生率は{{W|15%}}（通常は5%）。}}"));
        });
    }

    [Test]
    public void TryTranslate_PreservesColoredBeamsplitterCountInsideWholeSourceWrapper()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Fitted with beamsplitter: This weapon has a {0}-way spread with each shot at -1 penetration roll.", "ビームスプリッタ装着: この武器は1射撃ごとに{0}方向へ拡散し、各射撃の貫通判定が-1される。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "{{rules|Fitted with beamsplitter: This weapon has a {{W|3}}-way spread with each shot at -1 penetration roll.}}",
            "LookTooltipContentPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{rules|ビームスプリッタ装着: この武器は1射撃ごとに{{W|3}}方向へ拡散し、各射撃の貫通判定が-1される。}}"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesNestedDataDiskItemModificationTemplate()
    {
        WriteDynamicWorldModsDictionary();

        var ok = WorldModsTextTranslator.TryTranslate(
            "Adds item modification: Counterweighted: Adds +2 to hit.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("アイテム改造: つり合い調整: 命中に+2のボーナスを与える。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesColorsForDynamicWorldModsTemplate()
    {
        WriteDynamicWorldModsDictionary();

        var ok = WorldModsTextTranslator.TryTranslate(
            "{{Y|Electrified: When powered, this weapon deals an additional 2-3 electrical damage on hit.}}",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{Y|帯電: 通電中、この武器は命中時に追加で2-3の電撃ダメージを与える。}}"));
        });
    }

    [Test]
    public void TryTranslate_FallbackToEnglishForUntranslatedKey()
    {
        WriteDictionary("world-mods.ja.json");

        var ok = WorldModsTextTranslator.TryTranslate(
            "This is an untranslated English phrase.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo("This is an untranslated English phrase."));
        });
    }

    [Test]
    public void TryTranslate_HandlesEmptyInput()
    {
        WriteDictionary("world-mods.ja.json");

        var ok = WorldModsTextTranslator.TryTranslate(
            "",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(""));
        });
    }

    [Test]
    public void TryTranslate_PreservesMarkerAndColorTagsCombined()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Flaming: When powered, this weapon deals additional heat damage on hit.", "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "\x01{{r|Flaming: When powered, this weapon deals additional heat damage on hit.}}",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("\x01{{r|火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。}}"));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_TranslatesBowsAndRifles()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class:", "武器カテゴリ:"),
            ("Bows && Rifles", "弓・ライフル"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weapon Class: Bows && Rifles",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("武器カテゴリ: 弓・ライフル"));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_TranslatesScopedWeaponClassValue()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class:", "武器カテゴリ:"),
            ("Cudgel (dazes on critical hit)", "棍棒（クリティカル時に朦朧付与）"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weapon Class: Cudgel (dazes on critical hit)",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("武器カテゴリ: 棍棒（クリティカル時に朦朧付与）"));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_UsesScopedWholeWeaponClassLineBeforeValueLookup()
    {
        WriteDictionaryWithContext(
            "world-mods.ja.json",
            ("XRL.World.Parts.MeleeWeapon.GetShortDescription",
                "Weapon Class: Long Blades (increased penetration on critical hit)",
                "武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weapon Class: Long Blades (increased penetration on critical hit)",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Long Blades (increased penetration on critical hit)"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_TranslatesRequiresAndWeightPrefixes()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("Requires:", "要件："),
            ("Weight:", "重量："),
            ("Tinker I", "ティンカーI"));

        var requiresOk = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Requires: Tinker I",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var requiresTranslated);
        var weightOk = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weight: 1 lbs.",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var weightTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(requiresOk, Is.True);
            Assert.That(requiresTranslated, Is.EqualTo("要件： ティンカーI"));
            Assert.That(weightOk, Is.True);
            Assert.That(weightTranslated, Is.EqualTo("重量： 1 lbs."));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_RecordsValueMissAgainstProvidedRoute()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("Weapon Class:", "武器カテゴリ:"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weapon Class: Unknown Weapon Family",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("武器カテゴリ: Unknown Weapon Family"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Unknown Weapon Family"), Is.EqualTo(1));
            Assert.That(Translator.GetMissingRouteHitCountForTests("DescriptionShortDescriptionPatch"), Is.EqualTo(1));
            Assert.That(Translator.GetMissingRouteHitCountForTests("<no-context>"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_TranslatesValueWithoutMissingRouteNoise()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class:", "武器カテゴリ:"),
            ("Bows && Rifles", "弓・ライフル"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weapon Class: Bows && Rifles",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("武器カテゴリ: 弓・ライフル"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Bows && Rifles"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("DescriptionShortDescriptionPatch"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("<no-context>"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateCompareStatusSequence_TranslatesAllPartsWithoutMissingRouteNoise()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Friendly", "友好"),
            ("Neutral", "中立"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusSequence(
            "Friendly, Neutral",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatusSequence",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("友好、中立"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Friendly"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Neutral"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("DescriptionShortDescriptionPatch"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("<no-context>"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateActiveEffectsLine_PartiallyTranslatesKnownEffectsAndKeepsMissingEffectsVisible()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wet", "濡れている"));

        var ok = StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(
            "ACTIVE EFFECTS: unknown, wet",
            "TestRoute",
            "Description.ActiveEffects",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("発動中の効果: unknown、濡れている"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("unknown"), Is.EqualTo(1));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests("TestRoute", "Description.ActiveEffects"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void TryTranslateActiveEffectsLine_TranslatesKnownEffectsWithoutMissingRouteNoise()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wet", "濡れている"));

        var ok = StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(
            "ACTIVE EFFECTS: wet",
            "DescriptionShortDescriptionPatch",
            "Description.ActiveEffects",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("発動中の効果: 濡れている"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("ACTIVE EFFECTS:"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("wet"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("DescriptionShortDescriptionPatch"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("<no-context>"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateActiveEffectsLine_TranslatesGeneratedDisplayNameEffectParts()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wading", "浅瀬を進んでいる"));
        WriteDictionary(
            "ui-displayname-adjectives.ja.json",
            ("tarry", "べとつく"),
            ("wet", "{{B|濡れた}}"));

        var ok = StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(
            "ACTIVE EFFECTS: tarry wet, wading",
            "DescriptionShortDescriptionPatch",
            "Description.ActiveEffects",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("発動中の効果: べとつく{{B|濡れた}}、浅瀬を進んでいる"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("tarry wet"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateActiveEffectsLine_UsesDisplayNameAdjectiveContextForGeneratedEffectParts()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("ACTIVE EFFECTS:", "発動中の効果:"));
        WriteDictionaryWithContext(
            "ui-displayname-adjectives.ja.json",
            ("GetDisplayName.Adjective", "bloody", "{{r|血まみれの}}"),
            ("GetDisplayName.Adjective", "salty", "{{W|塩辛い}}"),
            ("GetDisplayName.Adjective", "slimy", "{{slimy|粘液質の}}"));

        var ok = StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(
            "ACTIVE EFFECTS: bloody salty slimy",
            "AbilityBarAfterRenderTranslationPatch",
            "AbilityBar.ActiveEffects",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("発動中の効果: {{r|血まみれの}}{{W|塩辛い}}{{slimy|粘液質の}}"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("bloody salty slimy"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateActiveEffectsLine_TranslatesLiquidCoveredPrefixChains()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("ACTIVE EFFECTS:", "発動中の効果:"));
        WriteDictionaryWithContext(
            "ui-displayname-adjectives.ja.json",
            ("GetDisplayName.Adjective", "bloody", "{{r|血まみれの}}"),
            ("GetDisplayName.Adjective", "slimy", "{{slimy|粘液質の}}"),
            ("GetDisplayName.Adjective", "wet", "{{B|濡れた}}"));

        var ok = StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(
            "ACTIVE EFFECTS: bloody slimy wet",
            "AbilityBarAfterRenderTranslationPatch",
            "AbilityBar.ActiveEffects",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("発動中の効果: {{r|血まみれの}}{{slimy|粘液質の}}{{B|濡れた}}"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("bloody slimy wet"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateActiveEffectsLine_TranslatesGeneratedStuckInEffectPart()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("ACTIVE EFFECTS:", "発動中の効果:"));
        WriteDictionary(
            "ui-displayname-adjectives.ja.json",
            ("tar", "タール"));

        var ok = StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(
            "ACTIVE EFFECTS: tarry, stuck in tar",
            "AbilityBarAfterRenderTranslationPatch",
            "AbilityBar.ActiveEffects",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("発動中の効果: tarry、タールにはまっている"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("stuck in tar"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateLevelExpLine_TranslatesLevelLabelWithoutMissingRouteNoise()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("LVL", "LV"));

        var ok = StatusLineTranslationHelpers.TryTranslateLevelExpLine(
            "LVL: 12 Exp: 345 / 678",
            "DescriptionShortDescriptionPatch",
            "Description.LevelExp",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("LV: 12 Exp: 345 / 678"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("LVL"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("DescriptionShortDescriptionPatch"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("<no-context>"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateHpStatusLine_TranslatesStatusWithoutMissingRouteNoise()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Wounded", "負傷"));

        var ok = StatusLineTranslationHelpers.TryTranslateHpStatusLine(
            "HP: Wounded",
            "DescriptionShortDescriptionPatch",
            "Description.HpStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("HP: 負傷"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Wounded"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("DescriptionShortDescriptionPatch"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingRouteHitCountForTests("<no-context>"), Is.EqualTo(0));
        });
    }

    private void WriteDynamicWorldModsDictionary()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Adds item modification: {0}", "アイテム改造: {0}"),
            ("Anti-gravity: When powered, this item's weight is reduced by {0}% plus {1} {2}.", "反重力: 通電中、この品の重量は{0}%減り、さらに{1}{2}軽くなる。"),
            ("Co-Processor: When powered, this item grants {0} {1} and provides compute power to the local lattice.", "共同処理装置: 通電中、{1}に{0}を与え、局所格子に演算力を供給する。"),
            ("Co-Processor: When powered, this item grants {0} {1} and provides {2} units of compute power to the local lattice.", "共同処理装置: 通電中、{1}に{0}を与え、局所格子に{2}ユニットの演算力を供給する。"),
            ("When {0}, provides {1} {2} of compute power to the local lattice.", "{0}、局所格子に{1}{2}の演算力を供給する。"),
            ("When {0}, provides {1} in radius {2}.", "{0}、半径{2}に{1}を提供する。"),
            ("When {0}, provides {1}.", "{0}、{1}を提供する。"),
            ("{0}% chance per turn to repel gases {1} {2}.", "{2}{1}ガスを毎ターン{0}%の確率で退ける。"),
            ("Repels gases {0} {1}.", "{1}{0}ガスを退ける。"),
            ("Counterweighted: Adds a bonus to hit.", "つり合い調整: 命中にボーナスを与える。"),
            ("Counterweighted: Adds {0} to hit.", "つり合い調整: 命中に{0}のボーナスを与える。"),
            ("Displacer: When powered, this weapon randomly teleports its target {0} tiles away on a successful hit.", "位相転移: 通電中、この武器は命中時に対象を無作為に{0}マス離れた場所へ転移させる。"),
            ("Fitted with beamsplitter: This weapon has a {0}-way spread with each shot at -1 penetration roll.", "ビームスプリッタ装着: この武器は1射撃ごとに{0}方向へ拡散し、各射撃の貫通判定が-1される。"),
            ("Electrified: When powered, this weapon deals additional electrical damage on hit.", "帯電: 通電中、この武器は命中時に追加の電撃ダメージを与える。"),
            ("Electrified: When powered, this weapon deals an additional {0} electrical damage on hit.", "帯電: 通電中、この武器は命中時に追加で{0}の電撃ダメージを与える。"),
            ("Flaming: When powered, this weapon deals additional heat damage on hit.", "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。"),
            ("Flaming: When powered, this weapon deals an additional {0} heat damage on hit.", "火炎: 通電中、この武器は命中時に追加で{0}の熱ダメージを与える。"),
            ("Freezing: When powered, this weapon deals additional cold damage on hit.", "凍結: 通電中、この武器は命中時に追加の冷気ダメージを与える。"),
            ("Freezing: When powered, this weapon deals an additional {0} cold damage on hit.", "凍結: 通電中、この武器は命中時に追加で{0}の冷気ダメージを与える。"),
            ("Liquid-cooled: This weapon's rate of fire is increased, but it requires {0} to function. When fired, there's a one in {1} chance that 1 dram is consumed.", "液冷式: この武器の連射数は増えるが、機能するには{0}が必要だ。発射時には{1}分の1の確率で1ドラム消費する。"),
            ("Heartstopper: When powered, this weapon has {0}% chance to put opponents into cardiac arrest.", "心停止: 通電中、この武器は{0}%の確率で敵を心停止させる。"),
            ("Heartstopper: When powered, this weapon has {0}% chance to put opponents into cardiac arrest if they fail a difficulty {1} {2} save.", "心停止: 通電中、この武器は{0}%の確率で、敵が難易度{1}の{2}セーヴに失敗した場合に心停止させる。"),
            ("Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives a bonus to hit a target aimed at.", "スマート: 通電して起動し、使用者がHUDかテックスキャナーを装備している場合、この武器の追尾スコープは精度を高め、照準した対象への命中にボーナスを与える。"),
            ("Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives {0} to hit a target aimed at.", "スマート: 通電して起動し、使用者がHUDかテックスキャナーを装備している場合、この武器の追尾スコープは精度を高め、照準した対象への命中に{0}のボーナスを与える。"),
            ("Microserrated: This weapon has {0}% chance to dismember opponents.", "微鋸歯: この武器は{0}%の確率で敵を切断する。"),
            ("Nanon: {0}% chance to dismember on penetration.", "ナノ刃: 貫通時に{0}%の確率で切断する。"),
            ("Serrated: This weapon has {0}% chance to dismember opponents.", "鋸歯: この武器は{0}%の確率で敵を切断する。"),
            ("Feathered: This item grants the wearer {0} reputation with birds.", "羽飾り: 装着者に鳥類との評判{0}を与える。"),
            ("+{0} reputation with {1}", "{1}との評判{0:+#;-#}"),
            ("Offhand Attack Chance: {0}%", "オフハンド命中率: {0}%"),
            ("Scaled: This item grants the wearer {0} reputation with unshelled reptiles.", "鱗状の: 装着者に甲無し爬虫類との評判{0}を与える。"),
            ("Snail-Encrusted: This item is crawling with tiny snails and grants the wearer {0} reputation with mollusks.", "巻貝まみれ: 小さなカタツムリが這っており、装着者に軟体動物との評判{0}を与える。"),
            ("Issachari tribe", "イッサカリ族"),
            ("Intelligence", "知力"));
        WriteDictionaryWithContext(
            "ui-liquids.ja.json",
            ("XRL.Liquids", "water", "水"));
    }

    private static IEnumerable<TestCaseData> RepositoryWeaponModDescriptions()
    {
        yield return new TestCaseData("Counterweighted: Adds a bonus to hit.", "つり合い調整: 命中にボーナスを与える。");
        yield return new TestCaseData("Counterweighted: Adds +2 to hit.", "つり合い調整: 命中に+2のボーナスを与える。");
        yield return new TestCaseData("Displacer: When powered, this weapon randomly teleports its target 1-6 tiles away on a successful hit.", "位相転移: 通電中、この武器は命中時に対象を無作為に1-6マス離れた場所へ転移させる。");
        yield return new TestCaseData("Drum-loaded: This weapon may hold 20% additional ammo.", "ドラム弾倉: 装弾数が20%増える。");
        yield return new TestCaseData("Fitted with beamsplitter: This weapon has a 3-way spread with each shot at -1 penetration roll.", "ビームスプリッタ装着: この武器は1射撃ごとに3方向へ拡散し、各射撃の貫通判定が-1される。");
        yield return new TestCaseData("Electrified: When powered, this weapon deals additional electrical damage on hit.", "帯電: 通電中、この武器は命中時に追加の電撃ダメージを与える。");
        yield return new TestCaseData("Electrified: When powered, this weapon deals an additional 2-3 electrical damage on hit.", "帯電: 通電中、この武器は命中時に追加で2-3の電撃ダメージを与える。");
        yield return new TestCaseData("Flaming: When powered, this weapon deals additional heat damage on hit.", "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。");
        yield return new TestCaseData("Flaming: When powered, this weapon deals an additional 2-3 heat damage on hit.", "火炎: 通電中、この武器は命中時に追加で2-3の熱ダメージを与える。");
        yield return new TestCaseData("Freezing: When powered, this weapon deals additional cold damage on hit.", "凍結: 通電中、この武器は命中時に追加の冷気ダメージを与える。");
        yield return new TestCaseData("Freezing: When powered, this weapon deals an additional 2-3 cold damage on hit.", "凍結: 通電中、この武器は命中時に追加で2-3の冷気ダメージを与える。");
        yield return new TestCaseData("Heartstopper: When powered, this weapon has a chance to put opponents into cardiac arrest.", "心停止: 通電中、この武器は敵を心停止させる可能性がある。");
        yield return new TestCaseData("Heartstopper: When powered, this weapon has 15% chance to put opponents into cardiac arrest.", "心停止: 通電中、この武器は15%の確率で敵を心停止させる。");
        yield return new TestCaseData("Heartstopper: When powered, this weapon has 15% chance to put opponents into cardiac arrest if they fail a difficulty 20 Toughness save.", "心停止: 通電中、この武器は15%の確率で、敵が難易度20の頑健セーヴに失敗した場合に心停止させる。");
        yield return new TestCaseData("Homing: This weapon ignores DV.", "自動誘導: この武器はDVを無視する。");
        yield return new TestCaseData("Hypervelocity: When powered, this weapon matches its penetration to its target's armor and penetrates creatures.", "超高速: 通電中、この武器は目標の装甲に合わせて貫通力を調整し、生物を貫く。");
        yield return new TestCaseData("Keen: +2 to penetration rolls", "鋭利: 貫通判定+2");
        yield return new TestCaseData("Liquid-cooled: This weapon's rate of fire is increased, but it requires pure water to function. When fired, there's a one in 7 chance that 1 dram is consumed.", "液冷式: この武器の連射数は増えるが、機能するには純粋な水が必要だ。発射時には7分の1の確率で1ドラム消費する。");
        yield return new TestCaseData("Liquid-cooled: This weapon's rate of fire is increased by 2, but it requires pure water to function. When fired, there's a one in 7 chance that 1 dram is consumed.", "液冷式: この武器の連射数は2増えるが、機能するには純粋な水が必要だ。発射時には7分の1の確率で1ドラム消費する。");
        yield return new TestCaseData("Masterwork: This weapon scores critical hits 15% of the time instead of 5%.", "傑作: この武器のクリティカル発生率は15%（通常は5%）。");
        yield return new TestCaseData("Metallized: +1 AV or penetration", "金属化: AVか貫通に+1");
        yield return new TestCaseData("Microserrated: This weapon has a chance to dismember opponents.", "微鋸歯: この武器は敵の部位を切断することがある。");
        yield return new TestCaseData("Microserrated: This weapon has 15% chance to dismember opponents.", "微鋸歯: この武器は15%の確率で敵を切断する。");
        yield return new TestCaseData("Mighty: This weapon has no strength bonus penetration cap.", "剛力: 筋力による貫通ボーナスに上限がない。");
        yield return new TestCaseData("Morphogenetic: When powered and used to perform a successful, damaging hit, this weapon attempts to daze all other creatures of the same species as your target on the local map. Compute power on the local lattice increases the strength of this effect.", "形態同調: 通電してダメージを与えると、ローカルマップ内の同種個体をすべて朦朧させようとする。局所格子の算術能力が高いほど効果が強くなる。");
        yield return new TestCaseData("Nanon: This weapon has a chance to dismember on penetration.", "ナノ刃: 貫通時に切断を引き起こすことがある。");
        yield return new TestCaseData("Nanon: 15% chance to dismember on penetration", "ナノ刃: 貫通時に15%の確率で切断する。");
        yield return new TestCaseData("Nulling: When powered, this weapon astrally burdens its target on hit. Compute power on the local lattice increases the effectiveness of this effect.", "無効化: 通電中、この武器は命中した対象に霊的負荷を与える。ローカルラティス上の計算力が高いほど効果が増す。");
        yield return new TestCaseData("Phase-Harmonic: This weapon can affect both in-phase and out-of-phase objects.", "位相調和: この武器は同位相・逆位相の対象の両方に作用する。");
        yield return new TestCaseData("Psionic: This weapon uses the wielder's Ego modifier for penetration bonus instead of Strength mod and attacks MA instead of AV. It will dissipate from the corporeal realm after some use.", "サイオニック: 貫通ボーナスに筋力でなく自我修正を用い、AVではなくMAを攻撃する。一定回数で現世から消散する。");
        yield return new TestCaseData("Quantum reverb: When fired, this weapon creates a hologram of its wielder who continues to fire along the same path.", "量子残響: 発射時に射手のホログラムを作り、同じ軌道で射撃を続ける。");
        yield return new TestCaseData("Scoped: This weapon has increased accuracy.", "スコープ付き: この武器は命中精度が向上する。");
        yield return new TestCaseData("Serrated: This weapon has a chance to dismember opponents.", "鋸歯: この武器は敵を切断することがある。");
        yield return new TestCaseData("Serrated: This weapon has 15% chance to dismember opponents.", "鋸歯: この武器は15%の確率で敵を切断する。");
        yield return new TestCaseData("Sharp: +1 to penetration rolls", "鋭利: 貫通判定+1");
        yield return new TestCaseData("Sirocco: Drains 1 Toughness from any organic target this weapon damages for 3-4 turns.", "熱風: この武器が与えた有機標的から頑健性を1奪い、3-4ターン続く。");
        yield return new TestCaseData("Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives a bonus to hit a target aimed at.", "スマート: 通電して起動し、使用者がHUDかテックスキャナーを装備している場合、この武器の追尾スコープは精度を高め、照準した対象への命中にボーナスを与える。");
        yield return new TestCaseData("Smart: When powered and started up and the wielder has a HUD or techscanner equipped, this weapon's tracking scope makes it more accurate and gives +2 to hit a target aimed at.", "スマート: 通電して起動し、使用者がHUDかテックスキャナーを装備している場合、この武器の追尾スコープは精度を高め、照準した対象への命中に+2のボーナスを与える。");
        yield return new TestCaseData("Small chance to transmute an enemy into a gemstone on hit.", "命中時、ごく低確率で敵を宝石に変成させる。");
        yield return new TestCaseData("5% chance to transmute an enemy into a gemstone on hit.", "命中時、宝石に変成させる確率は5%。");
    }

    private void WriteDictionary(string fileName, params (string key, string text)[] entries)
    {
        using var writer = new StreamWriter(Path.Combine(tempDirectory, fileName), append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private void WriteDictionaryWithContext(string fileName, params (string context, string key, string text)[] entries)
    {
        using var writer = new StreamWriter(Path.Combine(tempDirectory, fileName), append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"context\":\"");
            writer.Write(EscapeJson(entries[index].context));
            writer.Write("\",\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
