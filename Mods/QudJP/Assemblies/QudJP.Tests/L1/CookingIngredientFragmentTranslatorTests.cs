using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class CookingIngredientFragmentTranslatorTests
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

    [TestCase("a pinch of salt", "塩ひとつまみ")]
    [TestCase("a dash of algae", "藻少量")]
    [TestCase("a dram of {{C|water}}", "{{C|水}}1ドラム")]
    [TestCase("some bread", "パン少々")]
    [TestCase("a bread", "パン")]
    [TestCase("glass berries", "ガラスベリー")]
    [TestCase("a nip of joined paprika", "結ばれたパプリカ少量")]
    [TestCase("chameleon horn", "カメレオンの角")]
    [TestCase("a banana leaf", "バナナの葉")]
    [TestCase("some chipped-off chunk of the Spindle", "スピンドルの欠片少々")]
    [TestCase("chip from the horn of a goatfolk", "山羊人の角の欠片")]
    [TestCase("a sprinkle of Eater dander", "喰らう者のフケひと振り")]
    [TestCase("svardym spittle", "スヴァーディムの唾")]
    [TestCase("nachash root", "ナハシュの根")]
    [TestCase("banana peel", "バナナの皮")]
    [TestCase("diacalyptus leaf", "ディアカリプタスの葉")]
    [TestCase("some plant matter", "植物質少々")]
    [TestCase("gibbon hair", "テナガザルの毛")]
    [TestCase("asphodelyte petals", "アスフォデライトの花びら")]
    [TestCase("a magnet", "磁石")]
    [TestCase("microcontroller", "マイクロコントローラー")]
    [TestCase("pristine circuit", "無傷の回路")]
    [TestCase("vectorized fossil", "ベクトル化した化石")]
    [TestCase("astral dander", "アストラルのフケ")]
    [TestCase("abraded stone", "擦り減った石")]
    [TestCase("musa", "ムサ")]
    [TestCase("coolant", "冷却液")]
    [TestCase("antifreeze", "不凍液")]
    [TestCase("glass microtubing", "ガラス製微小管")]
    [TestCase("tetraxenonoglass", "テトラキセノノガラス")]
    [TestCase("psionic residue", "サイオニック残渣")]
    [TestCase("plasma", "プラズマ")]
    [TestCase("photonic matter", "光子物質")]
    [TestCase("vectorized pebbles", "ベクトル化した小石")]
    [TestCase("abstract form", "抽象形態")]
    [TestCase("dim memory", "かすかな記憶")]
    [TestCase("a bit", "ビット")]
    [TestCase("a byte", "バイト")]
    [TestCase("some indeterminable qualia", "判別不能なクオリア少々")]
    [TestCase("dream dust", "夢の塵")]
    [TestCase("dim light", "薄暗い光")]
    [TestCase("fulcrete brick", "フルクリートレンガ")]
    [TestCase("nimbus leaf", "光輪の葉")]
    [TestCase("prism fragment", "プリズム片")]
    [TestCase("mudroot", "泥根")]
    [TestCase("crysteel shards", "クリスタル鋼の破片")]
    [TestCase("lush acid", "瑞々しい酸")]
    [TestCase("purple membrane", "紫の膜")]
    [TestCase("some smeared urberry", "塗りつけられたウルベリー少々")]
    [TestCase("nimbus bark", "光輪の樹皮")]
    [TestCase("star orchid dust", "星蘭の塵")]
    [TestCase("displaced air", "変位した空気")]
    [TestCase("a flake of shale", "頁岩の薄片")]
    [TestCase("thorn from a jilted lover", "捨てられた恋人の棘")]
    [TestCase("boar tooth", "猪の牙")]
    [TestCase("salthopper wing", "ソルトホッパーの羽")]
    [TestCase("glowcrow feather", "グロウクロウの羽")]
    [TestCase("some seed spit", "種の唾少々")]
    [TestCase("dog hair", "犬の毛")]
    [TestCase("shavings from a tortoise shell", "亀の甲羅の削り片")]
    [TestCase("some equimax hair", "エクイマックスの毛少々")]
    [TestCase("some shale", "頁岩少々")]
    [TestCase("flower petal", "花びら")]
    [TestCase("some flower petals", "花びら少々")]
    [TestCase("flowers", "花")]
    [TestCase("mushroom stalk", "キノコの柄")]
    [TestCase("toadstool", "毒キノコ")]
    [TestCase("some lichen", "地衣類少々")]
    [TestCase("some mold", "カビ少々")]
    [TestCase("some goop", "べたつく粘液少々")]
    [TestCase("some smut", "黒穂少々")]
    [TestCase("pebble", "小石")]
    [TestCase("snapjaw fang", "スナップジョーの牙")]
    [TestCase("some limestone", "石灰岩少々")]
    [TestCase("some clay", "粘土少々")]
    [TestCase("some gravel", "砂利少々")]
    [TestCase("some silt", "沈泥少々")]
    [TestCase("some tortoise shell", "亀の甲羅少々")]
    [TestCase("some hermit dander", "隠者のフケ少々")]
    [TestCase("some goat hair", "山羊の毛少々")]
    [TestCase("quillipede quill", "クィリペードの棘")]
    [TestCase("mushroom cap", "キノコの傘")]
    [TestCase("swarmshade leaf", "スウォームシェードの葉")]
    [TestCase("swarmshade root", "スウォームシェードの根")]
    [TestCase("ziv leaf", "ジヴの葉")]
    [TestCase("ziv root", "ジヴの根")]
    [TestCase("shimscale leaf", "シムスケールの葉")]
    [TestCase("star palm frond", "スターパームの葉")]
    [TestCase("shimscale root", "シムスケールの根")]
    [TestCase("some boar hair", "猪の毛少々")]
    [TestCase("some ice frog spittle", "氷カエルの唾少々")]
    [TestCase("some albino ape hair", "アルビノエイプの毛少々")]
    [TestCase("some leech blood", "ヒルの血少々")]
    [TestCase("some beetlebum chitin", "ビートルバムのキチン質少々")]
    [TestCase("some skunk hair", "スカンクの毛少々")]
    [TestCase("some slug spit", "スラッグの唾少々")]
    [TestCase("some beetle chitin", "甲虫のキチン質少々")]
    [TestCase("prism perch", "プリズムパーチ")]
    [TestCase("perch scale", "パーチの鱗")]
    [TestCase("white esh feather", "ホワイトエッシュの羽")]
    [TestCase("junk dollar", "ジャンクダラー")]
    [TestCase("junk dollar spine", "ジャンクダラーの棘")]
    [TestCase("spongy mass", "海綿状の塊")]
    [TestCase("coral finger", "指状サンゴ")]
    [TestCase("crysteel shard", "クリスタル鋼の破片")]
    [TestCase("some algal water", "藻質の水少々")]
    [TestCase("some svardym egg shell", "スヴァーディムの卵殻少々")]
    [TestCase("some perch cheek", "パーチの頬肉少々")]
    [TestCase("eel hair", "ウナギの毛")]
    [TestCase("tunnel sponge", "トンネルスポンジ")]
    [TestCase("some svardym drool", "スヴァーディムのよだれ少々")]
    [TestCase("crystal petal", "結晶の花びら")]
    [TestCase("psychal rhythm pebble", "サイカルな律動の小石")]
    [TestCase("glitchtree leaf", "グリッチツリーの葉")]
    [TestCase("photon", "光子")]
    [TestCase("negative number", "負の数")]
    [TestCase("leaf from Chavvah, the Tree of Life", "生命の樹チャヴァの葉")]
    [TestCase("platonic solid", "プラトン立体")]
    [TestCase("starshell scute", "スターシェルの鱗板")]
    [TestCase("blade of noise grass", "ノイズグラスの葉")]
    [TestCase("imaginary number", "虚数")]
    [TestCase("dream wren feather", "ドリームレンの羽")]
    [TestCase("some entropy fungus", "エントロピー菌少々")]
    [TestCase("some glitchtree leaves", "グリッチツリーの葉少々")]
    [TestCase("some unimax tail hair", "ユニメックスの尾毛少々")]
    [TestCase("dreamcrunglem", "ドリームクランガル質")]
    [TestCase("some photons", "光子少々")]
    [TestCase("thoughtforms", "思考形態")]
    [TestCase("crystal flowers", "結晶の花")]
    [TestCase("dilute static", "希薄な静電気")]
    [TestCase("quantum fuzz", "量子の綿毛")]
    [TestCase("algebraic salt", "代数的な塩")]
    [TestCase("some quartz dust", "水晶の粉塵少々")]
    [TestCase("some hexagons", "六角形少々")]
    [TestCase("some compressed thoughtstuff", "圧縮された思考物質少々")]
    [TestCase("bone of a cannibal", "人喰いの骨")]
    [TestCase("pin from a scrying horn", "占視の角笛のピン")]
    [TestCase("holographic coupler", "ホログラフィックカプラー")]
    [TestCase("ecliptic glyph", "黄道のグリフ")]
    [TestCase("glittering spore", "煌めく胞子")]
    [TestCase("shaving of understratum", "下層ストラタの削り片")]
    [TestCase("pin from a dish horn", "ディッシュホーンのピン")]
    [TestCase("sun ribbon panel", "太陽リボンのパネル")]
    [TestCase("pin from an underhorn", "アンダーホーンのピン")]
    [TestCase("tiny crab", "小さなカニ")]
    [TestCase("shard from a glass dome", "ガラスのドームの破片")]
    [TestCase("force coupling", "フォースカップリング")]
    [TestCase("mover magnet", "ムーバー磁石")]
    [TestCase("some glittering spores", "煌めく胞子少々")]
    [TestCase("some black shale flakes", "黒い頁岩の薄片少々")]
    [TestCase("some Triangulum dust", "トリアングルムの塵少々")]
    [TestCase("some dark matter", "暗黒物質少々")]
    [TestCase("some starship exhaust", "星船の排気少々")]
    [TestCase("some shattered stratum", "砕けたストラタ少々")]
    [TestCase("some braided crysteel paper", "編まれたクリスタル鋼紙少々")]
    [TestCase("some labyrinthite flakes", "ラビリンサイトの薄片少々")]
    [TestCase("some thurible smoke", "香炉の煙少々")]
    [TestCase("some blueshifted dust of chrome", "青方偏移したクロムの塵少々")]
    [TestCase("some star dust", "星の塵少々")]
    [TestCase("some observation liquid", "観測液少々")]
    [TestCase("some forest motes", "森の微塵少々")]
    [TestCase("some dust from the Great Machine", "大いなる機械の塵少々")]
    [TestCase("some ultraviolet light", "紫外光少々")]
    [TestCase("coral polyp", "サンゴポリプ")]
    [TestCase("palladium strut", "パラジウム支柱")]
    [TestCase("some red coral", "赤サンゴ少々")]
    [TestCase("some blue coral", "青サンゴ少々")]
    [TestCase("some gold coral", "金サンゴ少々")]
    [TestCase("roasted antenna", "焼けたアンテナ")]
    [TestCase("musket muzzle", "マスケットの銃口")]
    [TestCase("bolt from a rifle turret", "ライフルタレットのボルト")]
    [TestCase("some gunpowder", "火薬少々")]
    [TestCase("some scrap", "スクラップ少々")]
    [TestCase("dawnglider feather", "ドーングライダーの羽")]
    [TestCase("dawnglider egg", "ドーングライダーの卵")]
    [TestCase("thread from an Issachari banner", "イッサカリの旗の糸")]
    [TestCase("fractus thorn", "フラクタスの棘")]
    [TestCase("some salt kraken larvae", "ソルトクラーケンの幼生少々")]
    [TestCase("some dromad hair", "ドロマドの毛少々")]
    [TestCase("some dromad dander", "ドロマドのフケ少々")]
    [TestCase("some marble chalk", "大理石のチョーク少々")]
    [TestCase("some Issachari nomad hair", "イッサカリ遊牧民の毛少々")]
    [TestCase("cube of gelatin", "ゼラチンの立方体")]
    [TestCase("fingernail", "爪")]
    [TestCase("skull", "頭蓋骨")]
    [TestCase("clump of hair", "毛の塊")]
    [TestCase("piece of marble", "大理石片")]
    [TestCase("stillvine leaf", "スティルバインの葉")]
    [TestCase("synthetic wing", "合成翼")]
    [TestCase("charred bone", "焦げた骨")]
    [TestCase("mopango scale", "モパンゴの鱗")]
    [TestCase("circuitboard", "回路基板")]
    [TestCase("some bone dust", "骨粉少々")]
    [TestCase("some petrified hair", "石化した毛少々")]
    [TestCase("some grave goods", "副葬品少々")]
    [TestCase("some digestive jelly", "消化ゼリー少々")]
    [TestCase("some sarcophagus juice", "石棺の液汁少々")]
    [TestCase("some bone brine", "骨の塩水少々")]
    [TestCase("some flakes of grave moss", "墓苔の薄片少々")]
    [TestCase("madpole tooth", "マッドポールの歯")]
    [TestCase("some madpole cheek", "マッドポールの頬肉少々")]
    [TestCase("smidgen of brass croc meat", "真鍮ワニ肉ひとつまみ")]
    [TestCase("filthy scribe's right hand", "汚らわしい書記の右手")]
    [TestCase("{{Y|filthy scribe's right hand}}", "{{Y|汚らわしい書記の右手}}")]
    [TestCase("{{R|filthy scribe}}'s right hand", "{{R|汚らわしい書記}}の右手")]
    [TestCase("filthy scribe's {{R|right hand}}", "汚らわしい書記の{{R|右手}}")]
    [TestCase("unknown pilgrim's right hand", "unknown pilgrimの右手")]
    public void TryTranslate_TranslatesMeasuredAndArticleIngredientFragments(string source, string expected)
    {
        var translated = CookingIngredientFragmentTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = CookingIngredientFragmentTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "a pinch of salt",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("a pinch of salt"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("a pinch of qwern")]
    [TestCase("qwern")]
    public void TryTranslate_LeavesUnsupportedFragmentsUnchanged(string? source)
    {
        var translated = CookingIngredientFragmentTranslator.TryTranslate(source, out var result);

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
