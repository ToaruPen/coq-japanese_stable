using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class CookingRecipeDisplayNameTranslatorTests
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

    [TestCase("{{W|Fried Wafers}}", "{{W|揚げウェハー}}")]
    [TestCase("{{R|Fried Wafers}}", "{{R|揚げウェハー}}")]
    [TestCase("Fried Wafers", "揚げウェハー")]
    [TestCase("{{W|Honeyed Salt Stew}}", "{{W|ハチミツ風味の塩シチュー}}")]
    [TestCase("{{W|Salt Bread}}", "{{W|塩パン}}")]
    [TestCase("{{W|Marrow Fillet}}", "{{W|髄切り身}}")]
    [TestCase("{{W|Slaw Leaves}}", "{{W|コールスロー葉}}")]
    [TestCase("{{W|Bush Shrubs}}", "{{W|茂み低木}}")]
    [TestCase("{{W|Grass Root}}", "{{W|草根}}")]
    [TestCase("{{W|Seeds Thorns}}", "{{W|種棘}}")]
    [TestCase("{{W|Hay Bugs}}", "{{W|干し草虫}}")]
    [TestCase("{{W|Larvae Loaf}}", "{{W|幼生ローフ}}")]
    [TestCase("{{W|Kebab Hash}}", "{{W|ケバブハッシュ}}")]
    [TestCase("{{W|Goulash Schnitzel}}", "{{W|グヤーシュシュニッツェル}}")]
    [TestCase("{{W|Roast Shawarma}}", "{{W|ローストシャワルマ}}")]
    [TestCase("{{W|Meatballs Tajine}}", "{{W|ミートボールタジン}}")]
    [TestCase("{{W|Alloy Wire}}", "{{W|合金ワイヤー}}")]
    [TestCase("{{W|Diodes Circuitry}}", "{{W|ダイオード回路}}")]
    [TestCase("{{W|Figs Stems}}", "{{W|イチジク茎}}")]
    [TestCase("{{W|Shoots Bark}}", "{{W|新芽樹皮}}")]
    [TestCase("{{W|Rocks Pebbles}}", "{{W|岩小石}}")]
    [TestCase("{{W|Boulder Humus}}", "{{W|巨石腐植土}}")]
    [TestCase("{{W|Rot Corpse}}", "{{W|腐敗死体}}")]
    [TestCase("{{W|Shredded Clams}}", "{{W|細切りハマグリ}}")]
    [TestCase("{{W|Sliced Mussels}}", "{{W|薄切りムール貝}}")]
    [TestCase("{{W|Chopped Snails}}", "{{W|刻みカタツムリ}}")]
    [TestCase("{{W|Minced Worms}}", "{{W|みじん切りワーム}}")]
    [TestCase("{{W|Crumbled Clams}}", "{{W|砕きハマグリ}}")]
    [TestCase("{{W|Diced Mussels}}", "{{W|角切りムール貝}}")]
    [TestCase("{{W|Marinated Snails}}", "{{W|マリネカタツムリ}}")]
    [TestCase("{{W|Aged Worms}}", "{{W|熟成ワーム}}")]
    [TestCase("{{W|Boiled Bread}}", "{{W|茹でパン}}")]
    [TestCase("{{W|Flaky Wafers}}", "{{W|フレーク状ウェハー}}")]
    [TestCase("{{W|Glazed Stew}}", "{{W|照りシチュー}}")]
    [TestCase("{{W|Grilled Fillet}}", "{{W|グリル切り身}}")]
    [TestCase("{{W|Mild Bread}}", "{{W|まろやかパン}}")]
    [TestCase("{{W|Rich Hash}}", "{{W|濃厚ハッシュ}}")]
    [TestCase("{{W|Sauteed Roast}}", "{{W|ソテーロースト}}")]
    [TestCase("{{W|Savory Tajine}}", "{{W|旨味タジン}}")]
    [TestCase("{{W|Sizzling Kebab}}", "{{W|ジュージューケバブ}}")]
    [TestCase("{{W|Smothered Loaf}}", "{{W|たっぷりローフ}}")]
    [TestCase("{{W|Thick Goulash}}", "{{W|とろみグヤーシュ}}")]
    [TestCase("{{W|Tossed Slaw}}", "{{W|和えコールスロー}}")]
    [TestCase("{{W|Velvety Porridge}}", "{{W|なめらか粥}}")]
    [TestCase("{{W|Stuffed Matz}}", "{{W|詰め物マッツァ}}")]
    [TestCase("{{W|Herbed Cookies}}", "{{W|ハーブクッキー}}")]
    [TestCase("{{W|Mashed Yogurt}}", "{{W|すり潰しヨーグルト}}")]
    [TestCase("{{W|Baked Rice}}", "{{W|焼き飯}}")]
    [TestCase("{{W|Velvety Hummus}}", "{{W|なめらかフムス}}")]
    [TestCase("{{W|Stuffed Knish}}", "{{W|詰め物クニッシュ}}")]
    [TestCase("{{W|Boiled Broth}}", "{{W|茹でブロス}}")]
    [TestCase("{{W|Flaky Kugel}}", "{{W|フレーク状クーゲル}}")]
    [TestCase("{{W|Glazed Latkes}}", "{{W|照りラトケス}}")]
    [TestCase("{{W|Grilled Pancake}}", "{{W|グリルパンケーキ}}")]
    [TestCase("{{W|Mild Flatbread}}", "{{W|まろやかフラットブレッド}}")]
    [TestCase("{{W|Rich Pastry}}", "{{W|濃厚ペイストリー}}")]
    [TestCase("{{W|Sauteed Casserole}}", "{{W|ソテーキャセロール}}")]
    [TestCase("{{W|Savory Cake}}", "{{W|旨味ケーキ}}")]
    [TestCase("{{W|Sizzling Dumpling}}", "{{W|ジュージュー団子}}")]
    [TestCase("{{W|Smothered Doughnut}}", "{{W|たっぷりドーナツ}}")]
    [TestCase("{{W|Thick Couscous}}", "{{W|とろみクスクス}}")]
    [TestCase("{{W|Tossed Dolma}}", "{{W|和えドルマ}}")]
    [TestCase("{{W|Boiled Borscht}}", "{{W|茹でボルシチ}}")]
    [TestCase("{{W|Flaky Dip}}", "{{W|フレーク状ディップ}}")]
    [TestCase("{{W|Glazed Baklava}}", "{{W|照りバクラヴァ}}")]
    [TestCase("{{W|Grilled Compote}}", "{{W|グリルコンポート}}")]
    [TestCase("{{W|Mild Bone Meal}}", "{{W|まろやか骨粉}}")]
    [TestCase("{{W|Rich Mazebeard Gland Paste}}", "{{W|濃厚迷髭腺ペースト}}")]
    [TestCase("{{W|Sauteed Mazebeard}}", "{{W|ソテーメイズビアード}}")]
    [TestCase("{{W|Savory Blaze}}", "{{W|旨味ブレイズ}}")]
    [TestCase("{{W|Sizzling Open Flame}}", "{{W|ジュージュー直火}}")]
    [TestCase("{{W|Smothered Char}}", "{{W|たっぷり炭}}")]
    [TestCase("{{W|Thick Smoke}}", "{{W|とろみ煙}}")]
    [TestCase("{{W|Tossed Hulk Honey}}", "{{W|和えハルクハニー}}")]
    [TestCase("{{W|Boiled Rubbergum}}", "{{W|茹でラバーガム}}")]
    [TestCase("{{W|Flaky Gum}}", "{{W|フレーク状ゴム}}")]
    [TestCase("{{W|Glazed Poultice}}", "{{W|照り湿布}}")]
    [TestCase("{{W|Grilled Anodyne}}", "{{W|グリル鎮痛剤}}")]
    [TestCase("{{W|Mild Shade Oil}}", "{{W|まろやかシェードオイル}}")]
    [TestCase("{{W|Rich Shade Oil Tonic}}", "{{W|濃厚シェードオイルトニック}}")]
    [TestCase("{{W|Sauteed Tonic}}", "{{W|ソテートニック}}")]
    [TestCase("{{W|Savory Skulk}}", "{{W|旨味スカルク}}")]
    [TestCase("{{W|Sizzling Tartbeard Gland Paste}}", "{{W|ジュージュー酸髭腺ペースト}}")]
    [TestCase("{{W|Smothered Tartbeard}}", "{{W|たっぷり酸髭}}")]
    [TestCase("{{W|Thick Grave Moss}}", "{{W|とろみ墓苔}}")]
    [TestCase("{{W|Tossed Cracker}}", "{{W|和えクラッカー}}")]
    [TestCase("{{W|Boiled Dawnglider Tail}}", "{{W|茹でドーングライダーの尾}}")]
    [TestCase("{{W|Flaky Scales}}", "{{W|フレーク状鱗}}")]
    [TestCase("{{W|Glazed Scale}}", "{{W|照り鱗}}")]
    [TestCase("{{W|Grilled Dream Smoke}}", "{{W|グリル夢の煙}}")]
    [TestCase("{{W|Mild Dreams}}", "{{W|まろやか夢}}")]
    [TestCase("{{W|Rich Daydreams}}", "{{W|濃厚白昼夢}}")]
    [TestCase("{{W|Sauteed Daydream}}", "{{W|ソテー白昼夢}}")]
    [TestCase("{{W|Savory Petals}}", "{{W|旨味花びら}}")]
    [TestCase("{{W|Sizzling Petal}}", "{{W|ジュージュー花びら}}")]
    [TestCase("{{W|Smothered Vanta}}", "{{W|たっぷりヴァンタ}}")]
    [TestCase("{{W|Thick Nectar}}", "{{W|とろみネクター}}")]
    [TestCase("{{W|Tossed Greens}}", "{{W|和え青菜}}")]
    [TestCase("{{W|Velvety Dream}}", "{{W|なめらか夢}}")]
    [TestCase("{{W|Stuffed Flamebeard Gland Paste}}", "{{W|詰め物炎髭腺ペースト}}")]
    [TestCase("{{W|Herbed Flamebeard}}", "{{W|ハーブ炎髭}}")]
    [TestCase("{{W|Mashed Concentrated Flamebeard Gland Paste}}", "{{W|すり潰し濃縮炎髭腺ペースト}}")]
    [TestCase("{{W|Baked Elder Flamebeard}}", "{{W|焼き年老いた炎髭}}")]
    [TestCase("{{W|Velvety Sleetbeard Gland Paste}}", "{{W|なめらか霙髭腺ペースト}}")]
    [TestCase("{{W|Stuffed Sleetbeard}}", "{{W|詰め物霙髭}}")]
    [TestCase("{{W|Herbed Concentrated Sleetbeard Gland Paste}}", "{{W|ハーブ濃縮霙髭腺ペースト}}")]
    [TestCase("{{W|Mashed Elder Sleetbeard}}", "{{W|すり潰し年老いた霙髭}}")]
    [TestCase("{{W|Baked Concentrated Tartbeard Gland Paste}}", "{{W|焼き濃縮酸髭腺ペースト}}")]
    [TestCase("{{W|Velvety Elder Tartbeard}}", "{{W|なめらか年老いた酸髭}}")]
    [TestCase("{{W|Stuffed Nullity}}", "{{W|詰め物無}}")]
    [TestCase("{{W|Herbed Nullbeard Gland Paste}}", "{{W|ハーブ虚髭腺ペースト}}")]
    [TestCase("{{W|Mashed Nullbeard}}", "{{W|すり潰し虚髭}}")]
    [TestCase("{{W|Baked Concentrated Nullbeard Gland Paste}}", "{{W|焼き濃縮虚髭腺ペースト}}")]
    [TestCase("{{W|Velvety Elder Nullbeard}}", "{{W|なめらか年老いた虚髭}}")]
    [TestCase("{{W|Stuffed Gallbeard Gland Paste}}", "{{W|詰め物胆髭腺ペースト}}")]
    [TestCase("{{W|Herbed Gallbeard}}", "{{W|ハーブ胆髭}}")]
    [TestCase("{{W|Mashed Concentrated Gallbeard Gland Paste}}", "{{W|すり潰し濃縮胆髭腺ペースト}}")]
    [TestCase("{{W|Baked Elder Gallbeard}}", "{{W|焼き年老いた胆髭}}")]
    [TestCase("{{W|Velvety Dreambeard Gland Paste}}", "{{W|なめらか夢髭腺ペースト}}")]
    [TestCase("{{W|Stuffed Dreambeard}}", "{{W|詰め物夢髭}}")]
    [TestCase("{{W|Herbed Concentrated Dreambeard Gland Paste}}", "{{W|ハーブ濃縮夢髭腺ペースト}}")]
    [TestCase("{{W|Mashed Elder Dreambeard}}", "{{W|すり潰し年老いた夢髭}}")]
    [TestCase("{{W|Baked Stillbeard Gland Paste}}", "{{W|焼き静髭腺ペースト}}")]
    [TestCase("{{W|Velvety Stillbeard}}", "{{W|なめらか静髭}}")]
    [TestCase("{{W|Stuffed Concentrated Stillbeard Gland Paste}}", "{{W|詰め物濃縮静髭腺ペースト}}")]
    [TestCase("{{W|Herbed Elder Stillbeard}}", "{{W|ハーブ年老いた静髭}}")]
    [TestCase("{{W|Mashed Concentrated Mazebeard Gland Paste}}", "{{W|すり潰し濃縮迷髭腺ペースト}}")]
    [TestCase("{{W|Baked Elder Mazebeard}}", "{{W|焼き年老いた迷髭}}")]
    [TestCase("{{W|Boiled Yondercane}}", "{{W|茹でヨンダーケーン}}")]
    [TestCase("{{W|Flaky Fermented Yondercane}}", "{{W|フレーク状発酵ヨンダーケーン}}")]
    [TestCase("{{W|Glazed Air}}", "{{W|照り空気}}")]
    [TestCase("{{W|Grilled Cane}}", "{{W|グリルケーン}}")]
    [TestCase("{{W|Mild Fermented Yuckwheat}}", "{{W|まろやか発酵ヤックウィート}}")]
    [TestCase("{{W|Rich Medicine}}", "{{W|濃厚薬}}")]
    [TestCase("{{W|Sauteed Stem}}", "{{W|ソテー茎}}")]
    [TestCase("{{W|Savory Fire Ant Gaster Paste}}", "{{W|旨味火蟻の腹嚢ペースト}}")]
    [TestCase("{{W|Sizzling Gaster Paste}}", "{{W|ジュージュー腹嚢ペースト}}")]
    [TestCase("{{W|Smothered Fire Ant}}", "{{W|たっぷり火蟻}}")]
    [TestCase("{{W|Thick Gaster}}", "{{W|とろみ腹部}}")]
    [TestCase("{{W|Tossed Ant}}", "{{W|和え蟻}}")]
    [TestCase("{{W|Boiled Hoarshrooms}}", "{{W|茹でホアシュルーム}}")]
    [TestCase("{{W|Flaky Mushrooms}}", "{{W|フレーク状キノコ}}")]
    [TestCase("{{W|Glazed Motes of Light}}", "{{W|照り光の微塵}}")]
    [TestCase("{{W|Grilled Hoarshroom}}", "{{W|グリルホアシュルーム}}")]
    [TestCase("{{W|Mild Mushroom}}", "{{W|まろやかキノコ}}")]
    [TestCase("{{W|Rich Fungus}}", "{{W|濃厚菌類}}")]
    [TestCase("{{W|Sauteed Jerky}}", "{{W|ソテージャーキー}}")]
    [TestCase("{{W|Savory Meat}}", "{{W|旨味肉}}")]
    [TestCase("{{W|Sizzling Flesh}}", "{{W|ジュージュー肉}}")]
    [TestCase("{{W|Smothered Extremity}}", "{{W|たっぷり肢端}}")]
    [TestCase("{{W|Thick Limb}}", "{{W|とろみ四肢}}")]
    [TestCase("{{W|Tossed Appendage}}", "{{W|和え付属肢}}")]
    [TestCase("{{W|Velvety Lagroot}}", "{{W|なめらかラグルート}}")]
    [TestCase("{{W|Stuffed Mashed Lag}}", "{{W|詰め物マッシュドラグ}}")]
    [TestCase("{{W|Herbed Mirror Dust}}", "{{W|ハーブ鏡粉}}")]
    [TestCase("{{W|Mashed Dust}}", "{{W|すり潰し砂塵}}")]
    [TestCase("{{W|Boiled Fiber}}", "{{W|茹で繊維}}")]
    [TestCase("{{W|Flaky Pickled Mushrooms}}", "{{W|フレーク状きのこの酢漬け}}")]
    [TestCase("{{W|Glazed Pickled Mushroom Water}}", "{{W|照りきのこの酢漬け液}}")]
    [TestCase("{{W|Grilled Brineshroom}}", "{{W|グリルブラインシュルーム}}")]
    [TestCase("{{W|Mild Pickles}}", "{{W|まろやかピクルス}}")]
    [TestCase("{{W|Rich Pickle Water}}", "{{W|濃厚ピクルス液}}")]
    [TestCase("{{W|Sauteed Cucumber}}", "{{W|ソテーキュウリ}}")]
    [TestCase("{{W|Savory Pickle}}", "{{W|旨味ピクルス}}")]
    [TestCase("{{W|Sizzling Psychal Gland Paste}}", "{{W|ジュージューサイカル腺ペースト}}")]
    [TestCase("{{W|Smothered Psychal Gland}}", "{{W|たっぷりサイカル腺}}")]
    [TestCase("{{W|Boiled Memories}}", "{{W|茹で記憶}}")]
    [TestCase("{{W|Flaky Psyche}}", "{{W|フレーク状精神}}")]
    [TestCase("{{W|Glazed Memory}}", "{{W|照り記憶}}")]
    [TestCase("{{W|Grilled Chips}}", "{{W|グリルチップ}}")]
    [TestCase("{{W|Mild Bop Sponge}}", "{{W|まろやかボップスポンジ}}")]
    [TestCase("{{W|Rich Bop Cheek}}", "{{W|濃厚ボップ頬肉}}")]
    [TestCase("{{W|Sauteed Bop}}", "{{W|ソテーボップ}}")]
    [TestCase("{{W|Savory Cheek}}", "{{W|旨味頬肉}}")]
    [TestCase("{{W|Sizzling Soul Curd}}", "{{W|ジュージュー魂の凝乳}}")]
    [TestCase("{{W|Smothered Spark Tick Plasma}}", "{{W|たっぷりスパークティックのプラズマ}}")]
    [TestCase("{{W|Boiled Sparks}}", "{{W|茹で火花}}")]
    [TestCase("{{W|Flaky Electric Current}}", "{{W|フレーク状電流}}")]
    [TestCase("{{W|Glazed Electricity}}", "{{W|照り電気}}")]
    [TestCase("{{W|Grilled Lightning}}", "{{W|グリル稲妻}}")]
    [TestCase("{{W|Mild Electron}}", "{{W|まろやか電子}}")]
    [TestCase("{{W|Rich Volt}}", "{{W|濃厚ボルト}}")]
    [TestCase("{{W|Sauteed Nettles}}", "{{W|ソテーイラクサ}}")]
    [TestCase("{{W|Savory Spines}}", "{{W|旨味棘}}")]
    [TestCase("{{W|Sizzling Spine Fruit Jam}}", "{{W|ジュージュースパインフルーツのジャム}}")]
    [TestCase("{{W|Smothered Nettle}}", "{{W|たっぷりイラクサ}}")]
    [TestCase("{{W|Boiled Thorn}}", "{{W|茹で棘}}")]
    [TestCase("{{W|Flaky Apples}}", "{{W|フレーク状リンゴ}}")]
    [TestCase("{{W|Glazed Starapples}}", "{{W|照りスターアップル}}")]
    [TestCase("{{W|Grilled Jam}}", "{{W|グリルジャム}}")]
    [TestCase("{{W|Mild Apple Jam}}", "{{W|まろやかアップルジャム}}")]
    [TestCase("{{W|Rich Bananas}}", "{{W|濃厚バナナ}}")]
    [TestCase("{{W|Sauteed Banana}}", "{{W|ソテーバナナ}}")]
    [TestCase("{{W|Savory Vinewafers}}", "{{W|旨味ヴァインウェハー}}")]
    [TestCase("{{W|Sizzling Freshwater}}", "{{W|ジュージュー真水}}")]
    [TestCase("{{W|Smothered Voider Gland Paste}}", "{{W|たっぷり虚空腺ペースト}}")]
    [TestCase("{{W|Rich Voider Gland}}", "{{W|濃厚ヴォイダー腺}}")]
    [TestCase("{{W|Sauteed Voider}}", "{{W|ソテーヴォイダー}}")]
    [TestCase("{{W|Savory Paste}}", "{{W|旨味ペースト}}")]
    [TestCase("{{W|Baked Wild Rice}}", "{{W|焼き野生米}}")]
    [TestCase("{{W|Boiled Glue}}", "{{W|茹で糊}}")]
    [TestCase("{{W|Flaky Cloning Draught}}", "{{W|フレーク状クローン薬液}}")]
    [TestCase("{{W|Glazed Elixir}}", "{{W|照り霊薬}}")]
    [TestCase("{{W|Grilled Magma}}", "{{W|グリルマグマ}}")]
    [TestCase("{{W|Mild Neutrons}}", "{{W|まろやか中性子}}")]
    [TestCase("{{W|Salt-Cured Bread}}", "{{W|塩漬けパン}}")]
    [TestCase("{{W|Honey-Rubbed Bread}}", "{{W|ハチミツまぶしパン}}")]
    [TestCase("{{W|Bread With Salt}}", "{{W|パン：塩入り}}")]
    [TestCase("{{W|Salt In Bread}}", "{{W|塩入りパン}}")]
    [TestCase("{{W|Salt Inside Of Bread}}", "{{W|塩入りパン}}")]
    [TestCase("{{W|Salt On Top Of Bread}}", "{{W|塩のせパン}}")]
    [TestCase("{{W|Salt Over Bread}}", "{{W|塩がけパン}}")]
    [TestCase("{{W|Bread With Salt And Meat}}", "{{W|パン：塩と肉入り}}")]
    [TestCase("{{W|Salt And Meat With Bread}}", "{{W|パン：塩と肉入り}}")]
    [TestCase("{{W|Bread With Salt, Meat, And Wild Rice}}", "{{W|パン：塩、肉、野生米入り}}")]
    public void TryProcessDisplayName_TranslatesGeneratedDishName(string source, string expected)
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            source,
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(actualTranslation, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("{{W|Bread With Salt}}", "{{W|パン：塩入り}}")]
    [TestCase("{{W|Bread With Salt And Meat}}", "{{W|パン：塩と肉入り}}")]
    [TestCase("{{W|Bread With Salt, Meat, And Wild Rice}}", "{{W|パン：塩、肉、野生米入り}}")]
    public void TryProcessDisplayName_DoesNotJoinDishComponentsWithMiddleDots(string source, string expected)
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            source,
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(actualTranslation, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(translated, Does.Not.Contain("・"));
        });
    }

    [Test]
    public void TryProcessDisplayName_TranslatesChefPossessiveDishPart()
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            "{{W|Argyve's Fried Wafers}}",
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(actualTranslation, Is.True);
            Assert.That(translated, Is.EqualTo("{{W|Argyveの揚げウェハー}}"));
        });
    }

    [Test]
    public void TryProcessDisplayName_TranslatesChefPossessiveDishPrepositionPart()
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            "{{W|Argyve's Bread With Salt}}",
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(actualTranslation, Is.True);
            Assert.That(translated, Is.EqualTo("{{W|Argyveのパン：塩入り}}"));
        });
    }

    [Test]
    public void TryProcessDisplayName_StripsDirectMarkerWithoutRetranslating()
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            "\x01{{W|Fried Wafers}}",
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(actualTranslation, Is.False);
            Assert.That(translated, Is.EqualTo("{{W|Fried Wafers}}"));
        });
    }

    [TestCase("")]
    [TestCase("{{W|Qwern Wafers}}")]
    public void TryProcessDisplayName_LeavesUnsupportedDisplayNamesUnchanged(string source)
    {
        var ok = CookingRecipeDisplayNameTranslationPatch.TryProcessDisplayName(
            source,
            out var translated,
            out var actualTranslation);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(actualTranslation, Is.False);
            Assert.That(translated, Is.EqualTo(source));
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
