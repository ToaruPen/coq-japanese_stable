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
        "電化: 通電中、この武器は命中時に追加で2-3の電撃ダメージを与える。")]
    [TestCase(
        "Flaming: When powered, this weapon deals additional heat damage on hit.",
        "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。")]
    [TestCase(
        "Freezing: When powered, this weapon deals additional cold damage on hit.",
        "冷却: 通電中、この武器は命中時に追加の冷気ダメージを与える。")]
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
            Assert.That(translated, Is.EqualTo("{{Y|電化: 通電中、この武器は命中時に追加で2-3の電撃ダメージを与える。}}"));
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
            ("Electrified: When powered, this weapon deals additional electrical damage on hit.", "電化: 通電中、この武器は命中時に追加の電撃ダメージを与える。"),
            ("Electrified: When powered, this weapon deals an additional {0} electrical damage on hit.", "電化: 通電中、この武器は命中時に追加で{0}の電撃ダメージを与える。"),
            ("Flaming: When powered, this weapon deals additional heat damage on hit.", "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。"),
            ("Flaming: When powered, this weapon deals an additional {0} heat damage on hit.", "火炎: 通電中、この武器は命中時に追加で{0}の熱ダメージを与える。"),
            ("Freezing: When powered, this weapon deals additional cold damage on hit.", "冷却: 通電中、この武器は命中時に追加の冷気ダメージを与える。"),
            ("Freezing: When powered, this weapon deals an additional {0} cold damage on hit.", "冷却: 通電中、この武器は命中時に追加で{0}の冷気ダメージを与える。"),
            ("Feathered: This item grants the wearer {0} reputation with birds.", "羽飾り: 装着者に鳥類との評判{0}を与える。"),
            ("+{0} reputation with {1}", "{1}との評判{0:+#;-#}"),
            ("Offhand Attack Chance: {0}%", "オフハンド命中率: {0}%"),
            ("Scaled: This item grants the wearer {0} reputation with unshelled reptiles.", "鱗状の: 装着者に甲無し爬虫類との評判{0}を与える。"),
            ("Snail-Encrusted: This item is crawling with tiny snails and grants the wearer {0} reputation with mollusks.", "巻貝まみれ: 小さなカタツムリが這っており、装着者に軟体動物との評判{0}を与える。"),
            ("Issachari tribe", "イッサカリ族"),
            ("Intelligence", "知力"));
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
