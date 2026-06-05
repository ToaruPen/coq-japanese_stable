using QudJP;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class RelicGeneratedNameTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase("Edge of the Dominant Sword", "支配的な剣の刃")]
    [TestCase("Adventurer's Dominant Edge", "冒険者の支配的な刃")]
    [TestCase("Dominant-Edge", "支配的な・刃")]
    [TestCase("Dominant Edge", "支配的な刃")]
    [TestCase("Dominant Sword of Bethesda Susa", "Bethesda Susaの支配的な剣")]
    [TestCase("The Dominant Sword of Bethesda Susa", "Bethesda Susaの支配的な剣")]
    [TestCase("The Point of the Commanding Woe", "威厳ある嘆きの尖端")]
    [TestCase("the Point of the Commanding Woe", "威厳ある嘆きの尖端")]
    [TestCase("the Breast of the Embraced Telescope", "受け入れた望遠鏡の胸甲")]
    [TestCase("Chain of the Analog Sand", "アナログの砂の鎖")]
    public void TryTranslate_TranslatesFiniteRelicNameShapes(string source, string expected)
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate("{{Y|Edge of the Dominant Sword}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y|支配的な剣の刃}}"));
        });
    }

    [Test]
    public void TryTranslate_StripsLeadingArticleOutsideColorWrapper()
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(
            "the {{Y-R-Y-Y-Y-Y-Y-r-Y sequence|Point of the Commanding Woe}}",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{Y-R-Y-Y-Y-Y-Y-r-Y sequence|威厳ある嘆きの尖端}}"));
        });
    }

    [TestCase("Hatchet", "手斧")]
    [TestCase("Fell", "伐斧")]
    [TestCase("Hew", "斫斧")]
    [TestCase("Edge", "刃")]
    [TestCase("Dirk", "ダーク")]
    [TestCase("Point", "尖端")]
    [TestCase("Shiv", "シヴ")]
    [TestCase("Shank", "シャンク")]
    [TestCase("Kris", "クリス")]
    [TestCase("Brand", "剣")]
    [TestCase("Glaive", "グレイブ")]
    [TestCase("Mace", "メイス")]
    [TestCase("Rod", "棍")]
    [TestCase("Staff", "杖")]
    [TestCase("Cosh", "ブラックジャック")]
    [TestCase("Rifle", "ライフル")]
    [TestCase("Cannon", "大砲")]
    [TestCase("Boomstick", "ブームスティック")]
    [TestCase("Long Arm", "長銃")]
    [TestCase("Pistol", "ピストル")]
    [TestCase("Gun", "銃")]
    [TestCase("Sidearm", "サイドアーム")]
    [TestCase("Helm", "兜")]
    [TestCase("Cap", "帽子")]
    [TestCase("Lid", "兜")]
    [TestCase("Hood", "フード")]
    [TestCase("Veil", "ヴェール")]
    [TestCase("Mask", "面")]
    [TestCase("Guise", "仮面")]
    [TestCase("Breast", "胸甲")]
    [TestCase("Vest", "ベスト")]
    [TestCase("Mail", "鎖帷子")]
    [TestCase("Chain", "鎖")]
    [TestCase("Link", "連環")]
    [TestCase("Band", "腕帯")]
    [TestCase("Clogs", "木靴")]
    [TestCase("Cleats", "スパイク付き")]
    [TestCase("Sneaks", "スニーカー")]
    [TestCase("Mitts", "ミット")]
    [TestCase("Muffs", "マフ")]
    [TestCase("Gloves", "手袋")]
    [TestCase("Orb", "宝珠")]
    [TestCase("Sphere", "球体")]
    [TestCase("Frill", "飾り")]
    [TestCase("Toy", "玩具")]
    [TestCase("Gaw", "珍品")]
    [TestCase("Guard", "護り")]
    [TestCase("Aegis", "アイギス")]
    [TestCase("Ward", "守り")]
    [TestCase("Shield", "盾")]
    [TestCase("Bread", "パン")]
    [TestCase("Meat", "肉")]
    [TestCase("Organ", "器官")]
    [TestCase("Chrome", "クロム")]
    [TestCase("Gear", "歯車")]
    [TestCase("Ware", "ウェア")]
    [TestCase("Wire", "ワイヤー")]
    [TestCase("Becoming", "変容")]
    [TestCase("Enhancement", "強化体")]
    public void TryTranslate_TranslatesHistorySpiceItemTypeComponents(string item, string expected)
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(item + " of the Dominant Sword", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("支配的な剣の" + expected));
        });
    }

    [Test]
    public void TryTranslate_TranslatesMultiwordItemTypeInsidePossessiveDescriptor()
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate("Adventurer's Dominant Long Arm", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("冒険者の支配的な長銃"));
        });
    }

    [TestCase("Feed", "飼料")]
    [TestCase("Chow", "食事")]
    [TestCase("Codex", "写本")]
    [TestCase("Tome", "大冊")]
    [TestCase("Volume", "巻")]
    [TestCase("Atlas", "地図帳")]
    [TestCase("Lexicon", "語彙録")]
    [TestCase("Folio", "フォリオ")]
    [TestCase("Omnibus", "大全")]
    [TestCase("Opus", "作品")]
    public void TryTranslate_TranslatesOwnerKnownBookAndFoodItemTypes_WhenEnabled(string item, string expected)
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(
            item + " of the Dominant Sword",
            out var result,
            includeBroadItemTypes: true);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("支配的な剣の" + expected));
        });
    }

    [Test]
    public void TryTranslate_LeavesBookItemTypeUnchanged_WhenBroadItemTypesAreDisabled()
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate("Codex of Leaves", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Codex of Leaves"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "Edge of the Dominant Sword",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Edge of the Dominant Sword"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("Qwern of the Dominant Sword")]
    [TestCase("Edge of the Qwern Sword")]
    public void TryTranslate_LeavesUnsupportedNamesUnchanged(string? source)
    {
        var translated = RelicGeneratedNameTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }
}
