using System.Runtime.Serialization;
using System.Text;
using QudJP.Patches;

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
        ChargenStructuredTextTranslator.ResetForTests();
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
    public void Translate_UsesScopedHistorySpiceCaptureWithoutGlobalExactLookup()
    {
        WriteScopedHistorySpiceDictionary(("someone", "誰か"), ("gather", "集う"));
        WritePatternDictionary(("^(.+?) (.+?) here\\.$", "{t0}がここで{t1}。"));

        var translated = MessagePatternTranslator.Translate("someone gather here.");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("誰かがここで集う。"));
            Assert.That(Translator.Translate("someone"), Is.EqualTo("someone"));
        });
    }

    [Test]
    public void Translate_TranslatesGeneratedActivatedAbilityCapture_FromMutationDisplayName()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        ChargenStructuredTextTranslator.ResetForTests();
        WriteMutationsXml(("Corrosive Gas Generation", "腐食性ガス生成"));
        WritePatternDictionary(("^You toggle (.+?) on\\.$", "{t0}をオンにした。"));

        var translated = MessagePatternTranslator.Translate("You toggle {{c|Release Corrosive Gas}} on.");

        Assert.That(translated, Is.EqualTo("{{c|腐食性ガス放出}}をオンにした。"));
    }

    [TestCase("You are a snapjaw.", "あなたはスナップジョー。")]
    [TestCase("You are a スナップジョー.", "あなたはスナップジョー。")]
    public void Translate_TranslatedCaptureStripsLeadingArticleBeforeLogicResolution(string source, string expected)
    {
        WriteExactDictionary(("snapjaw", "スナップジョー"));
        WritePatternDictionary(("^You are (.+?)[.!]?$", "あなたは{t0}。"));

        var translated = MessagePatternTranslator.Translate(source);

        Assert.That(translated, Is.EqualTo(expected), source);
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
        WritePatternDictionary(("^You are stunned[.!]?$", "あなたは気絶している"));

        var first = MessagePatternTranslator.Translate("You are stunned");
        var second = MessagePatternTranslator.Translate("You are stunned!");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("あなたは気絶している"));
            Assert.That(second, Is.EqualTo("あなたは気絶している"));
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
    public void Translate_PreservesAmpersandWholeLineColor_WhenInnerWrappersArePresent()
    {
        WritePatternDictionary(("^You hit (.+) for (\\d+) damage[.!]?$", "{0}に{1}ダメージを与えた"));

        var translated = MessagePatternTranslator.Translate("&GYou hit {{R|snapjaw}} for 7 damage^k!");

        Assert.That(translated, Is.EqualTo("&G{{R|snapjaw}}に7ダメージを与えた^k"));
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
    public void Translate_DoesNotReapplySourceCaptureMarkup_WhenTranslatedCaptureOwnsMarkup()
    {
        WritePatternDictionary(("^You were killed by (.+?)[.!]?$", "{t0}に殺された。"));
        WriteExactDictionary(
            ("bloody Tam, dromad merchant [sitting]", "{{r|血まみれの}}Tam、ドロマド商人 [座っている]"));

        var translated = MessagePatternTranslator.Translate(
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
        WritePatternDictionary(("^You see (.+?)[.!]?$", "{t0}が見える。"));
        WriteExactDictionary(("bloody Tam", "{{r|血まみれの}}Tam"));

        var translated = MessagePatternTranslator.Translate("You see {{C|bloody Tam}}.");

        Assert.That(translated, Is.EqualTo("{{C|{{r|血まみれの}}Tam}}が見える。"));
    }

    [Test]
    public void Translate_AppliesJournalNotesPattern()
    {
        WritePatternDictionary(("^Notes: (.+)$", "備考: {0}"));

        var translated = MessagePatternTranslator.Translate("Notes: Damur");

        Assert.That(translated, Is.EqualTo("備考: Damur"));
    }

    [Test]
    public void Translate_DoesNotReapplyPartialSourceMarkupInSegmentedTranslatedCapture()
    {
        WritePatternDictionary(("^Notes: (.+)$", "備考: {t0}"));
        WriteExactDictionary(("bloody Tam", "{{r|血まみれの}}Tam"));

        var translated = MessagePatternTranslator.Translate("Notes: {{r|bloody}} Tam");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("備考: {{r|血まみれの}}Tam"));
            Assert.That(translated, Does.Not.Contain("{{r|{{r|"));
            Assert.That(translated, Does.Not.Match("血ま.*}}.*みれ"));
        });
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
                "{t1}の{t0}日、あなたは{t2}の村に到着した。\n\n地平線では、クッドのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。"));
        WriteExactDictionary(
            ("5th", "第5"),
            ("Ut yara Ux", "ウト・ヤラ・ウクス"),
            ("Damur and fungus patch", "ダムールと菌類地帯"));

        var source = "On the 5th of Ut yara Ux, you arrive at the village of Damur and fungus patch.\n\n" +
            "On the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky.";

        var translated = MessagePatternTranslator.Translate(source);

        var expected = "ウト・ヤラ・ウクスの第5日、あなたはダムールと菌類地帯の村に到着した。\n\n" +
            "地平線では、クッドのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。";

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void Translate_AppliesCookingTossPattern()
    {
        WritePatternDictionary(("^You toss (.+?) into a pot and stir\\.$", "{0}を鍋に入れてかき混ぜた。"));

        var translated = MessagePatternTranslator.Translate("You toss aluminum dram of water、smidgen of brinestalk、とsheathed スナップジョーの戦士の left hand into a pot and stir.");

        Assert.That(translated, Is.EqualTo("aluminum dram of water、smidgen of brinestalk、とsheathed スナップジョーの戦士の left handを鍋に入れてかき混ぜた。"));
    }

    [Test]
    public void Translate_AppliesBookExcerptPattern()
    {
        WritePatternDictionary(("^You read one of the few legible excerpts from (.+?):\\n\\n\"(.+?)\"$", "{0}から数少ない判読可能な抜粋を読んだ。\n\n\"{1}\""));

        var translated = MessagePatternTranslator.Translate("You read one of the few legible excerpts from する 力 も:\n\n\"...\"");

        Assert.That(translated, Is.EqualTo("する 力 もから数少ない判読可能な抜粋を読んだ。\n\n\"...\""));
    }

    [TestCase("Poisonous goo burns your eyes.", "有毒な粘液が目に染みた。")]
    [TestCase("Putrid ooze splashes into your mouth. You gag at the awful taste.", "腐った軟泥が口に入った。ひどい味に吐き気を催した。")]
    [TestCase("Brown sludge splashes into your mouth. You wince at the metallic taste.", "茶色い汚泥が口に入った。金属の味に顔をしかめた。")]
    [TestCase("The liquids stop reacting.", "液体の反応が止まった")]
    [TestCase("The reacting liquids congeal into a SoupSludge.", "反応した液体が凝固しSoupSludgeになった")]
    [TestCase("The primordial soup nearby starts reacting with the water.", "近くの原初のスープが水と反応を始めた")]
    [TestCase("You receive tinkering bits <{{|AB}}>.", "修理ビット<{{|AB}}>を受け取った。")]
    [TestCase("You make some progress disarming 地雷.", "地雷の解除が少し進んだ。")]
    [TestCase("An image of タム disappears.", "タムの映像が消えた。")]
    [TestCase("An image of タム appears.", "タムの映像が現れた。")]
    [TestCase("Some dimensional interlopers attempt to enter this region of spacetime, but the ambient normality field keeps them at bay.", "異次元からの侵入者がこの時空領域に入り込もうとするが、周囲の常在性場がそれを食い止める。")]
    [TestCase("The 熊's carapace loosens.", "熊の甲殻が緩んだ")]
    [TestCase("熊の carapace loosens.", "熊の甲殻が緩んだ")]
    [TestCase("濡れた気難しいカメの 甲殻 loosens.", "濡れた気難しいカメの甲殻が緩んだ")]
    [TestCase("猫飼いの glow dims until it's extinguished.", "猫飼いの輝きが消えるまで薄れた")]
    [TestCase("The zealot mumbles inaudibly, encased in ice.", "氷に閉じ込められた狂信者が、聞き取れないほどに呟いた。")]
    [TestCase("The infected crust of skin on 熊の left arm loosens and breaks away.", "熊の left armの感染した皮膚の痂皮が緩んで剥がれ落ちた。")]
    [TestCase("The ヒンドレンの村人 harvests some ラーの花弁.", "ヒンドレンの村人はラーの花弁を収穫した。")]
    [TestCase("An ヒンドレンの村人 harvests some ラーの花弁.", "ヒンドレンの村人はラーの花弁を収穫した。")]
    [TestCase("Westからsome 魔樹の樹皮を収穫した", "西から魔樹の樹皮を収穫した")]
    [TestCase("Southwestからsome ラーの花弁を収穫した", "南西からラーの花弁を収穫した")]
    [TestCase("ゴミ to the southwestを漁ったが、何も見つからなかった", "南西でゴミを漁ったが、何も見つからなかった")]
    [TestCase("ゴミ to the northwestを漁り、歪んだ金属板を見つけた", "北西でゴミを漁り、歪んだ金属板を見つけた")]
    [TestCase("説教者は言う、'今日は、子らよ、啓発の儀について語ろう。」", "説教者は言う、「今日は、子らよ、啓発の儀について語ろう。」")]
    [TestCase("An electrical arc leaps toward you!", "電弧があなたへ走った！")]
    [TestCase("Your カービン is already fully loaded.", "カービンはすでに完全に装填されている。")]
    [TestCase("The 凍結した タールまみれの 結合ギルシュリング は二つに分裂した！", "凍結した タールまみれの 結合ギルシュリングは二つに分裂した！")]
    [TestCase("Exodus launch in 7...", "エクソダス発射まで7…")]
    [TestCase("Something hits タム (x2) with a 鉛スラッグ for 6 damage!", "何かが鉛スラッグでタムに6ダメージを与えた！ (x2)")]
    [TestCase("The タレット hits you with a 鉛スラッグ, but your mental attack has no effect.", "タレットの鉛スラッグが命中したが、精神攻撃は効果がない")]
    [TestCase("The タレット hits タム with a 鉛スラッグ, but their mental attack has no effect.", "タレットは鉛スラッグでタムに命中させたが、精神攻撃は効果がない")]
    [TestCase("The タレット hits タム (x2) with a 鉛スラッグ!", "タレットは鉛スラッグでタムに命中した (x2)")]
    [TestCase("The タールまみれの結合ギルシュリング miss you with their 牙! [5 vs 7]", "タールまみれの結合ギルシュリングの牙は外れた。[5 vs 7]")]
    [TestCase("A loud buzz is emitted. The unauthorized glyph flashes on the display.", "大きなブザー音が鳴った。認証されていないグリフがディスプレイに点滅した。")]
    [TestCase("&rA loud buzz is emitted. The unauthorized glyph flashes on the display.", "&r大きなブザー音が鳴った。認証されていないグリフがディスプレイに点滅した。")]
    [TestCase("{{r|A loud buzz is emitted. The unauthorized glyph flashes on the display.}}", "{{r|大きなブザー音が鳴った。認証されていないグリフがディスプレイに点滅した。}}")]
    [TestCase("{{y|&rA loud buzz is emitted. The unauthorized glyph flashes on the display.}}", "{{y|&r大きなブザー音が鳴った。認証されていないグリフがディスプレイに点滅した。}}")]
    [TestCase("Youは氷で滑った！", "あなたは氷で滑った！")]
    [TestCase("The 濡れた ジュースサップは氷で滑った！", "濡れた ジュースサップは氷で滑った！")]
    [TestCase("濡れたチェーンレーザー砲座の shot goes wild!", "濡れたチェーンレーザー砲座の弾が逸れた！")]
    [TestCase("鉛スラッグ hits you to the east! (x2)", "鉛スラッグがあなたに東側に命中！ (x2)")]
    [TestCase("鉛スラッグ critically hits you to the east! (x2)", "鉛スラッグが会心であなたに東側に命中！ (x2)")]
    [TestCase("鉛スラッグ hits you to the east, but your mental attack has no effect.", "鉛スラッグがあなたに東側に命中したが、精神攻撃は効果がない")]
    [TestCase("鉛スラッグ critically hits you to the east.", "鉛スラッグが会心であなたに東側に命中した。")]
    [TestCase("鉛スラッグ hits タム to the east, but your mental attack has no effect.", "鉛スラッグがタムに東側に命中したが、精神攻撃は効果がない")]
    [TestCase("鉛スラッグ critically hits タム to the east.", "鉛スラッグが会心でタムに東側に命中した。")]
    [TestCase("鉛スラッグ hits you (x2) to the east!", "鉛スラッグがあなたに東側に命中！ (x2)")]
    [TestCase("鉛スラッグ critically hits you (x2) to the east!", "鉛スラッグが会心であなたに東側に命中！ (x2)")]
    [TestCase("鉛スラッグ hits タム (x2) to the east!", "鉛スラッグがタムに東側に命中！ (x2)")]
    [TestCase("鉛スラッグ critically hits タム (x2) to the east!", "鉛スラッグが会心でタムに東側に命中！ (x2)")]
    [TestCase("The スナップジョー is destroyed.", "スナップジョーは破壊された")]
    [TestCase("An スナップジョー dies.", "スナップジョーは死んだ")]
    [TestCase("You hit the 熊 for 3 damage.", "熊に3ダメージを与えた")]
    [TestCase("the 熊 hits you for 2 damage.", "熊の攻撃で2ダメージを受けた")]
    [TestCase("an ironshank misses you.", "ironshankの攻撃は外れた")]
    public void Translate_RepositoryDictionary_AppliesEmitMessageSweepPatterns(string source, string expected)
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(source);

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesPlayerStuckInMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You are stuck in a 蜘蛛の巣!");

        Assert.That(translated, Is.EqualTo("蜘蛛の巣にはまっている！"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesColoredLevelGainCompoundMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "&yYou have gained a level! You are now level &C12&y!\n"
            + "You gain &C4&y hitpoints\n"
            + "You gain &C98&y Skill Points\n"
            + "You gain &C1&y Mutation Point\n"
            + "You gain &C1&y to each attribute");

        Assert.That(
            translated,
            Is.EqualTo(
                "&yレベルが上がった！現在レベル&C12&y！\n"
                + "ヒットポイントを&C4&y得た\n"
                + "スキルポイントを&C98&y得た\n"
                + "変異ポイントを&C1&y得た\n"
                + "各能力値が&C1&y上昇した"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesWarmingDraughtCompoundMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("&yYou flush with the warming draught! You are now &gQuenched&y.");

        Assert.That(translated, Is.EqualTo("&y温まる一口が全身を巡った！あなたは今、&g潤っている&y。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesElectricalArcTowardDirectionMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "The {{electrical|electrical arc}} leaps toward the {{freezing|凍結した}} 塩水の水たまり to the southwest!");

        Assert.That(translated, Is.EqualTo("{{electrical|電弧}}が{{freezing|凍結した}} 塩水の水たまり（南西側に）へ走った！"));
    }

    [TestCase("You pass by a 編みかご.", "編みかごのそばを通り過ぎた。")]
    [TestCase("You pass by an 編みかご.", "編みかごのそばを通り過ぎた。")]
    [TestCase("You pass by the ウォーターヴァイン.", "ウォーターヴァインのそばを通り過ぎた。")]
    [TestCase("You pass by ウォーターヴァイン.", "ウォーターヴァインのそばを通り過ぎた。")]
    public void Translate_RepositoryDictionary_StripsEnglishArticlesFromPassByCapture(string source, string expected)
    {
        UseRepositoryPatternDictionary();

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(expected));
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
    public void Translate_AppliesHybridPossessiveCrackedPattern()
    {
        WritePatternDictionary(("^Your (.+?)にひびが入った。?$", "{0}にひびが入った。"));

        var translated = MessagePatternTranslator.Translate("Your 鋼鉄のブーツにひびが入った");

        Assert.That(translated, Is.EqualTo("鋼鉄のブーツにひびが入った。"));
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
            ("^Your mental attack does not affect (.+?)\\.$", "あなたの精神攻撃は{t0}に効かない。"));
        WriteExactDictionary(("turret", "タレット"));

        var translated = MessagePatternTranslator.Translate("Your mental attack does not affect the turret.");

        Assert.That(translated, Is.EqualTo("あなたの精神攻撃はタレットに効かない。"));
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
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("タレット hits something to the east!");

        Assert.That(translated, Is.EqualTo("タレットは東側の何かに命中させた"));
    }

    [Test]
    public void Translate_AppliesFreezingEffectDamagePattern()
    {
        WritePatternDictionary(("^You take (\\d+) damage from (.+?)の freezing effect![.!]?$", "{1}の凍結効果で{0}ダメージを受けた！"));

        var translated = MessagePatternTranslator.Translate("You take 14 damage from 監視官イラメの freezing effect!");

        Assert.That(translated, Is.EqualTo("監視官イラメの凍結効果で14ダメージを受けた！"));
    }

    [Test]
    public void Translate_AppliesThornsDamagePattern()
    {
        WritePatternDictionary(("^You take (\\d+) damage from (?:the )?(.+?)の thorns\\.[.!]?$", "{1}の棘で{0}ダメージを受けた。"));

        var translated = MessagePatternTranslator.Translate("You take 1 damage from the フラクタスの thorns.");

        Assert.That(translated, Is.EqualTo("フラクタスの棘で1ダメージを受けた。"));
    }

    [Test]
    public void Translate_AppliesCannotFindPathToTargetPattern()
    {
        WritePatternDictionary(("^You cannot find a path to (?:the )?(.+?)\\.[.!]?$", "{0}への経路が見つからない。"));

        var translated = MessagePatternTranslator.Translate("You cannot find a path to the イッサカリの銃兵.");

        Assert.That(translated, Is.EqualTo("イッサカリの銃兵への経路が見つからない。"));
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
    public void Translate_AppliesIncomingCriticalWeaponHitPatternBeforeGenericHitPattern()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("イジル critically hits (x1) for 1 damage with his 棍棒. [21]");

        Assert.That(translated, Is.EqualTo("イジルの棍棒が会心し、1ダメージを受けた。(x1) [21]"));
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
    public void Translate_DisplayNamePlaceholderTranslatesWeaponCapture()
    {
        WriteExactDictionary(("chain pistol", "チェーンピストル"));
        WritePatternDictionary(
            ("^You hit \\((x\\d+)\\) for (\\d+) damage with (?:your |the |a |an )?(.+?)[.!] \\[(.+?)\\]$", "{d2}で{1}ダメージを与えた。({0}) [{3}]"));

        var translated = MessagePatternTranslator.Translate("You hit (x1) for 1 damage with the {{Y|chain pistol}}! [18]");

        Assert.That(translated, Is.EqualTo("{{Y|チェーンピストル}}で1ダメージを与えた。(x1) [18]"));
    }

    [Test]
    public void Translate_DisplayNamePlaceholderTranslatesPlainDisplayNameCapture()
    {
        WriteExactDictionary(("chain pistol", "チェーンピストル"));
        WritePatternDictionary(("^You equip (.+?)[.!]?$", "{d0}を装備した"));

        var translated = MessagePatternTranslator.Translate("You equip a chain pistol.");

        Assert.That(translated, Is.EqualTo("チェーンピストルを装備した"));
    }

    [Test]
    public void Translate_DisplayNamePlaceholderPreservesColorWrappedDisplayNameCapture()
    {
        WriteExactDictionary(("chain pistol", "チェーンピストル"));
        WritePatternDictionary(("^You equip (.+?)[.!]?$", "{d0}を装備した"));

        var translated = MessagePatternTranslator.Translate("You equip {{Y|a chain pistol}}.");

        Assert.That(translated, Is.EqualTo("{{Y|チェーンピストル}}を装備した"));
    }

    [Test]
    public void Translate_DisplayNamePlaceholderFallsBackSafelyWhenDisplayNameMissing()
    {
        WritePatternDictionary(("^You equip (.+?)[.!]?$", "{d0}を装備した"));

        var translated = MessagePatternTranslator.Translate("You equip a gleaming trinket.");

        Assert.That(translated, Is.EqualTo("gleaming trinketを装備した"));
    }

    [Test]
    public void Translate_DisplayNamePlaceholderStripsDirectTranslationMarkerInsideCapture()
    {
        WritePatternDictionary(("^You receive (.+?)!$", "{d0}を受け取った"));

        var translated = MessagePatternTranslator.Translate(
            "You receive " + MessageFrameTranslator.MarkDirectTranslation("奇妙な小物") + "!");

        Assert.That(translated, Is.EqualTo("奇妙な小物を受け取った"));
    }

    [Test]
    public void Translate_DisplayNamePlaceholderStripsDirectTranslationMarkerBeforeLeadingArticle()
    {
        WriteExactDictionary(("chain pistol", "チェーンピストル"));
        WritePatternDictionary(("^You equip (.+?)[.!]?$", "{d0}を装備した"));

        var translated = MessagePatternTranslator.Translate(
            "You equip " + MessageFrameTranslator.MarkDirectTranslation("the chain pistol") + ".");

        Assert.That(translated, Is.EqualTo("チェーンピストルを装備した"));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesNestedColorWrappersForPlayerHitWithRoll()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("{{g|You hit {{&w|(x1)}} for 1 damage with your {{fiery|燃え盛る}} {{w|青銅の短剣}}! [9]}}");

        Assert.That(
            translated,
            Is.EqualTo("{{g|{{fiery|燃え盛る}} {{w|青銅の短剣}}で1ダメージを与えた。({{&w|x1}}) [9]}}"));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesNestedColorWrappersForPlayerHitWithTheWeaponAndRoll()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "{{g|You hit {{&W|(x2)}} for 21 damage with the {{g|{{Y|塩気のある}} {{slimy|粘液質の}} Point of the Commanding Woe}}! [19]}}");

        Assert.That(
            translated,
            Is.EqualTo("{{g|{{g|{{Y|塩気のある}} {{slimy|粘液質の}} 威厳ある嘆きの尖端}}で21ダメージを与えた。({{&W|x2}}) [19]}}"));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesNestedColorWrappersForPlayerCriticalHitWithTheWeaponAndRoll()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "{{g|You critically hit {{&W|(x3)}} for 28 damage with the {{Y-R-Y-Y-Y-Y-Y-r-Y sequence|Point of the Commanding Woe}}! [21]}}");

        Assert.That(
            translated,
            Is.EqualTo("{{g|{{Y-R-Y-Y-Y-Y-Y-r-Y sequence|威厳ある嘆きの尖端}}で会心の一撃、28ダメージを与えた。({{&W|x3}}) [21]}}"));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesOuterWrapperForPlayerWeaponMiss()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("{{r|You miss with your {{fiery|燃え盛る}} {{w|青銅の短剣}}! [0 vs 0]}}");

        Assert.That(
            translated,
            Is.EqualTo("{{r|{{fiery|燃え盛る}} {{w|青銅の短剣}}での攻撃は外れた。[0 vs 0]}}"));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesOuterWrapperForPlayerWeaponMissWithTheWeapon()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("{{r|You miss with the {{Y-R-Y-Y-Y-Y-Y-r-Y sequence|Point of the Commanding Woe}}! [8 vs 12]}}");

        Assert.That(
            translated,
            Is.EqualTo("{{r|{{Y-R-Y-Y-Y-Y-Y-r-Y sequence|威厳ある嘆きの尖端}}での攻撃は外れた。[8 vs 12]}}"));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesOuterWrapperForPlayerWeaponMiss_WhenRollIsOutsideWrapper()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("{{r|You miss with your {{w|青銅の短剣}}!}} [7 vs 12]");

        Assert.That(translated, Is.EqualTo("{{r|{{w|青銅の短剣}}での攻撃は外れた。[7 vs 12]}}"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesIncomingMissWhenWeaponNameIsEmpty()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("The 山羊人の暴漢 misses you with his ! [7 vs 10]");

        Assert.That(translated, Is.EqualTo("山羊人の暴漢の攻撃は外れた。[7 vs 10]"));
    }

    [Test]
    public void Translate_RepositoryDictionary_RemovesPossessivePronounFromIncomingArmorPenetrationWeapon()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "The ウォーターヴァイン農家 doesn't penetrate your armor with his 鉄の蔓刈り斧! [7]");

        Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家は鉄の蔓刈り斧であなたの装甲を貫けなかった！ [7]"));
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

    [TestCase("The {{B|濡れた}}光葉 begins leaking.", "{{B|濡れた}}光葉は液漏れし始めた。")]
    [TestCase("an 樹液まみれの濡れた光葉 begins oozing from another wound.", "樹液まみれの濡れた光葉は別の傷から滲出し始めた。")]
    [TestCase("A {{B|濡れた}}光葉 stops fluxing.", "{{B|濡れた}}光葉のフラックス漏れは止まった。")]
    [TestCase("The {{B|濡れた}}光葉 takes 1 damage from leaking.", "{{B|濡れた}}光葉は液漏れで1ダメージを受けた。")]
    [TestCase("the 樹液まみれの濡れた光葉 takes no damage from oozing.", "樹液まみれの濡れた光葉は滲出でダメージを受けなかった。")]
    [TestCase("One of タムの wounds stops leaking.", "タムの傷のひとつの液漏れが止まった。")]
    [TestCase("One of the 樹液まみれの濡れた光葉の wounds stops leaking.", "樹液まみれの濡れた光葉の傷のひとつの液漏れが止まった。")]
    public void Translate_RepositoryDictionary_TranslatesCirculatoryLossEventMessages(
        string source,
        string expected)
    {
        UseRepositoryPatternDictionary();

        var actual = MessagePatternTranslator.Translate(source);

        Assert.Multiple(() =>
        {
            Assert.That(actual, Is.EqualTo(expected), source);
            Assert.That(actual, Does.Not.Contain("You gain"));
        });
    }

    [Test]
    public void Translate_RepositoryDictionary_UsesSpecificWoundStopBleedingPatternBeforeGenericBleedingStopPattern()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("One of タムの wounds stops bleeding.");

        Assert.That(translated, Is.EqualTo("タムの傷のひとつの出血が止まった。"));
    }

    [TestCase("You cannot reach タム to bandage their wounds.", "タムには届かず、傷に包帯を巻けない。")]
    [TestCase("There's no one there.", "そこには誰もいない。")]
    [TestCase("All of タムの wounds that can be staunched have been already.", "タムの止血できる傷はすべて処置済みだ。")]
    [TestCase("All of your wounds that can be staunched have been already.", "あなたの止血できる傷はすべて処置済みだ。")]
    [TestCase("タムの wounds have been bandaged.", "タムの傷は包帯処置済みだ。")]
    [TestCase("Your wounds have been bandaged.", "あなたの傷は包帯処置済みだ。")]
    [TestCase("タムの wounds are too deep to bandage.", "タムの傷は深すぎて包帯では処置できない。")]
    [TestCase("Your wounds are too deep to bandage.", "あなたの傷は深すぎて包帯では処置できない。")]
    public void Translate_RepositoryDictionary_TranslatesBandageMedicationFailureMessages(
        string source,
        string expected)
    {
        UseRepositoryPatternDictionary();

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(expected));
    }

    [TestCase(
        "You have gained a level! You are now level {{C|2}}!\nYou gain {{rules|1}} hitpoint\nYou gain {{rules|1}} Skill Point",
        "レベルが上がった！現在レベル{{C|2}}！\nヒットポイントを{{rules|1}}得た\nスキルポイントを{{rules|1}}得た")]
    [TestCase(
        "You have gained a level! You are now level {{C|6}}!\nYou gain {{rules|7}} hitpoints\nYou gain {{rules|50}} Skill Points\nYou gain {{rules|1}} Mutation Point\nYou gain {{rules|1}} to each attribute",
        "レベルが上がった！現在レベル{{C|6}}！\nヒットポイントを{{rules|7}}得た\nスキルポイントを{{rules|50}}得た\n変異ポイントを{{rules|1}}得た\n各能力値が{{rules|1}}上昇した")]
    public void Translate_RepositoryDictionary_TranslatesVariableLevelUpPopup(
        string source,
        string expected)
    {
        UseRepositoryPatternDictionary();

        var actual = MessagePatternTranslator.Translate(source);

        Assert.That(actual, Is.EqualTo(expected), source);
    }

    [TestCase("熊 nose begins leaking more heavily.", "熊の鼻が激しく液漏れし始めた。")]
    [TestCase("熊 noses begin oozing more heavily.", "熊の鼻が激しく滲出し始めた。")]
    public void Translate_RepositoryDictionary_TranslatesCirculatoryLossNoseFallbackPatterns(
        string source,
        string expected)
    {
        UseRepositoryPatternDictionary();

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(expected), () => source);
    }

    [Test]
    public void Translate_RepositoryDictionary_StillTranslatesCombatWoundsPattern()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("The 熊 wounds タム.");

        Assert.That(translated, Is.EqualTo("熊はタムに深手を負わせた"));
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
    public void Translate_RepositoryDictionary_TranslatesDirectionalSeeAndStopWithLocalizedAutoActDescription()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You see 巨大トンボ and {{r|クッズー}}共生体 to the northeast and stop 移動中.");

        Assert.That(translated, Is.EqualTo("北東に巨大トンボ and {{r|クッズー}}共生体が見えたので移動をやめた。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesDirectionalSeeAndRefrainWithLocalizedAutoActDescription()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You see 塩気のある血まみれのクローンリング to the east, so you refrain from 休息中.");

        Assert.That(translated, Is.EqualTo("東に塩気のある血まみれのクローンリングが見えたので休息を控えた。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesStopBecauseWithLocalizedAutoActDescription()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You stop 移動中 because you can go no further.");

        Assert.That(translated, Is.EqualTo("これ以上進めないので移動をやめた。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_LeavesUnknownLocalizedAutoActStopSeeMessageUnchanged()
    {
        UseRepositoryPatternDictionary();

        const string source = "You see 巨大トンボ to the northeast and stop 鑑賞中.";

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [TestCase("You stop 瞑想中 because you can go no further.")]
    [TestCase("{{y|You stop 瞑想中 because you can go no further.}}")]
    public void Translate_RepositoryDictionary_FallsBackForUnknownLocalizedAutoActDescription(string source)
    {
        UseRepositoryPatternDictionary();

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesWholeLineColorForUnknownLocalizedAutoActStopSeeMessage()
    {
        UseRepositoryPatternDictionary();

        const string source = "{{y|You see 巨大トンボ to the northeast and stop 鑑賞中.}}";

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_RepositoryDictionary_LeavesEmptyAutoActInputUnchanged()
    {
        UseRepositoryPatternDictionary();

        Assert.That(MessagePatternTranslator.Translate(string.Empty), Is.Empty);
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesWholeLineColorForLocalizedAutoActStopBecause()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("{{y|You stop 移動中 because you can go no further.}}");

        Assert.That(translated, Is.EqualTo("{{y|これ以上進めないので移動をやめた。}}"));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesDirectMarkerForLocalizedAutoActStopBecause()
    {
        UseRepositoryPatternDictionary();

        var source = MessageFrameTranslator.MarkDirectTranslation("You stop 移動中 because you can go no further.");

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesDirectMarkerForUnknownLocalizedAutoActStopBecause()
    {
        UseRepositoryPatternDictionary();

        var source = MessageFrameTranslator.MarkDirectTranslation(
            "You stop 瞑想中 because you can go no further.");

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesDirectMarkerForLocalizedAutoActStopSeeMessage()
    {
        UseRepositoryPatternDictionary();

        var source = MessageFrameTranslator.MarkDirectTranslation(
            "You see 巨大トンボ to the northeast and stop 移動中.");

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_RepositoryDictionary_PreservesCaptureColorForLocalizedDirectionalSeeAndStop()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You see {{r|巨大トンボ}} and {{G|q蓮の共生体}} to the northeast and stop 移動中.");

        Assert.That(translated, Is.EqualTo("北東に{{r|巨大トンボ}} and {{G|q蓮の共生体}}が見えたので移動をやめた。"));
    }

    [Test]
    public void Translate_AppliesGenericSultanHistoriesJournalPattern()
    {
        WritePatternDictionary(("^You note this piece of information in the Sultan Histories > (.+?) section of your journal\\.[.!]?$", "この情報をジャーナルの「スルタン史 > {0}」欄に記録した。"));

        var translated = MessagePatternTranslator.Translate("You note this piece of information in the Sultan Histories > Nashid I section of your journal.");

        Assert.That(translated, Is.EqualTo("この情報をジャーナルの「スルタン史 > Nashid I」欄に記録した。"));
    }

    [Test]
    public void Translate_AppliesVillageHistoriesJournalPattern()
    {
        WritePatternDictionary(("^You note this piece of information in the Village Histories > (.+?) section of your journal\\.[.!]?$", "この情報をジャーナルの「村の歴史 > {0}」欄に記録した。"));

        var translated = MessagePatternTranslator.Translate("You note this piece of information in the Village Histories > テッガトゥム section of your journal.");

        Assert.That(translated, Is.EqualTo("この情報をジャーナルの「村の歴史 > テッガトゥム」欄に記録した。"));
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
        WriteExactDictionary(("5th", "第5"), ("Ut yara Ux", "ウト・ヤラ・ウクス"));
        WritePatternDictionary(("^Last visited on the (.+?) of (.+?)$", "{t1}の{t0}日に最後に訪れた。"));

        var translated = MessagePatternTranslator.Translate("Last visited on the 5th of Ut yara Ux");

        Assert.That(translated, Is.EqualTo("ウト・ヤラ・ウクスの第5日に最後に訪れた。"));
    }

    [Test]
    public void Translate_AppliesDisassembleAndBitsReceiptPattern()
    {
        WritePatternDictionary(("^You disassemble (?:the |your )(.+?)\\. You receive tinkering bits <(.+?)>\\.[.!]?$", "{0}を分解し、修理ビット<{1}>を受け取った。"));

        var translated = MessagePatternTranslator.Translate("You disassemble the 奇妙な遺物. You receive tinkering bits <CD>.");

        Assert.That(translated, Is.EqualTo("奇妙な遺物を分解し、修理ビット<CD>を受け取った。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesRuntimeObservedDisassembleYourBitsReceipt()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You disassemble your 焦げたコンデンサ x2. You receive tinkering bits <AA>.");

        Assert.That(translated, Is.EqualTo("焦げたコンデンサ x2を分解し、修理ビット<AA>を受け取った。"));
    }

    [TestCase("You notice some ruins nearby. Would you like to investigate?", "近くに遺跡があることに気づいた。調査しますか？")]
    [TestCase("You smell roasted boar nearby. Would you like to investigate?", "近くで焼いたイノシシの匂いがする。調査しますか？")]
    [TestCase("You are carrying too much to move!", "持ちすぎて動けない！")]
    [TestCase("There's nothing in that. Would you like to store an item?", "その中には何も入っていない。アイテムを預けるか？")]
    [TestCase("イサッカリライフル to the southを分解し、修理ビット<A2>を受け取った。", "イサッカリライフル（南側）を分解し、修理ビット<A2>を受け取った。")]
    [TestCase("イサッカリライフル hereを分解し、修理ビット<A2>を受け取った。", "イサッカリライフル（ここ）を分解し、修理ビット<A2>を受け取った。")]
    public void Translate_RepositoryDictionary_TranslatesRuntimeObservedFrames(
        string source,
        string expected)
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(source);

        Assert.That(translated, Is.EqualTo(expected));
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
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("The ウォーターヴァイン農家のメカニマス教徒改宗者 yells, 'Is it a dybbuk that possesses the robot? It should be sacred and still.'");

        Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家のメカニマス教徒改宗者は「Is it a dybbuk that possesses the robot? It should be sacred and still.」と叫んだ。"));
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
        WriteExactDictionary(("27th", "第27"), ("Uru Ux", "ウル・ウクス"));
        WritePatternDictionary((
            "^On the (.+?) of (.+?), you arrive at the oasis-hamlet of Joppa, along the far rim of Moghra'yi, the Great Salt Desert\\.\\n\\nAll around you, moisture farmers tend to groves of viridian watervine\\. There are huts wrought from rock salt and brinestalk\\.\\n\\nOn the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth\\. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky\\.$",
            "{t1}の{t0}日、あなたは大塩砂漠モグライィの遥かな縁にあるオアシス集落ジョッパに到着した。\n\nあたりではウォーターヴァインの茂みを水耕農家たちが世話している。岩塩とブラインストークで組まれた小屋が建っている。\n\n地平線では、クッドのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。"));

        var source = "On the 27th of Uru Ux, you arrive at the oasis-hamlet of Joppa, along the far rim of Moghra'yi, the Great Salt Desert.\n\n" +
            "All around you, moisture farmers tend to groves of viridian watervine. There are huts wrought from rock salt and brinestalk.\n\n" +
            "On the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky.";

        var translated = MessagePatternTranslator.Translate(source);

        var expected = "ウル・ウクスの第27日、あなたは大塩砂漠モグライィの遥かな縁にあるオアシス集落ジョッパに到着した。\n\n" +
            "あたりではウォーターヴァインの茂みを水耕農家たちが世話している。岩塩とブラインストークで組まれた小屋が建っている。\n\n" +
            "地平線では、クッドのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。";

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void Translate_TranslatesCapitalizedYourselfCapture()
    {
        WritePatternDictionary(("^(.+?) repairs\\.$", "{t0}を修理した。"));

        var translated = MessagePatternTranslator.Translate("Yourself repairs.");

        Assert.That(translated, Is.EqualTo("自分自身を修理した。"));
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
        WritePatternDictionary(("^(?:The |the )?(.+?) harvests a (.+?)[.!]?$", "{0}は{1}を収穫した。"));

        var translated = MessagePatternTranslator.Translate("ウォーターヴァイン農家 harvests a ヴァインウェハー.");

        Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家はヴァインウェハーを収穫した。"));
    }

    [Test]
    public void Translate_AppliesHarvestPatternWithArticle()
    {
        WritePatternDictionary(("^(?:The |the )?(.+?) harvests a (.+?)[.!]?$", "{0}は{1}を収穫した。"));

        var translated = MessagePatternTranslator.Translate("The ウォーターヴァイン農家 harvests a ヴァインウェハー.");

        Assert.That(translated, Is.EqualTo("ウォーターヴァイン農家はヴァインウェハーを収穫した。"));
    }

    [Test]
    public void Translate_DoesNotTreatNonDishPhraseWithDishWordAsHistoricSpiceGeneratedName()
    {
        WriteExactDictionary(("ancient", "古代"), ("bread", "パン"), ("farm", "農場"));
        WritePatternDictionary(("^You inspect (.+?)[.!]?$", "{t0}を調べた。"));

        var translated = MessagePatternTranslator.Translate("You inspect Ancient Bread Farm.");

        Assert.That(translated, Is.EqualTo("Ancient Bread Farmを調べた。"));
    }

    [Test]
    public void Translate_TranslatesHistoricSpiceFakedDeathCognomenCapture()
    {
        WriteScopedHistorySpiceDictionary(("desiccated", "乾ききった"), ("spectre", "亡霊"));
        WritePatternDictionary(("^You remember (.+?)[.!]?$", "{t0}を思い出した。"));

        var translated = MessagePatternTranslator.Translate("You remember the Desiccated Spectre.");

        Assert.That(translated, Is.EqualTo("乾ききった亡霊を思い出した。"));
    }

    [Test]
    public void Translate_HistoricSpiceFakedDeathCognomenCapture_FallsBackToEnglishCaptureWhenPiecesAreMissing()
    {
        WriteScopedHistorySpiceDictionary(("desiccated", "乾ききった"));
        WritePatternDictionary(("^You remember (.+?)[.!]?$", "{t0}を思い出した。"));

        var translated = MessagePatternTranslator.Translate("You remember the Desiccated Spectre.");

        Assert.That(translated, Is.EqualTo("Desiccated Spectreを思い出した。"));
    }

    [Test]
    public void Translate_HistoricSpiceFakedDeathCognomenCapture_PreservesColorMarkup()
    {
        WriteScopedHistorySpiceDictionary(("desiccated", "乾ききった"), ("spectre", "亡霊"));
        WritePatternDictionary(("^You remember (.+?)[.!]?$", "{t0}を思い出した。"));

        var translated = MessagePatternTranslator.Translate("{{W|You remember the Desiccated Spectre.}}");

        Assert.That(translated, Is.EqualTo("{{W|乾ききった亡霊を思い出した。}}"));
    }

    [Test]
    public void Translate_HistoricSpiceFakedDeathCognomenCapture_DoesNotReapplyWhenMarkedDirect()
    {
        WriteScopedHistorySpiceDictionary(("desiccated", "乾ききった"), ("spectre", "亡霊"));
        WritePatternDictionary(("^You remember (.+?)[.!]?$", "{t0}を思い出した。"));
        var source = MessageFrameTranslator.MarkDirectTranslation("You remember the Desiccated Spectre.");

        var translated = MessagePatternTranslator.Translate(source);

        Assert.That(translated, Is.EqualTo(source));
    }

    [Test]
    public void Translate_HistoricSpiceFakedDeathCognomenCapture_ReturnsEmptyInput()
    {
        WriteScopedHistorySpiceDictionary(("desiccated", "乾ききった"), ("spectre", "亡霊"));
        WritePatternDictionary(("^You remember (.+?)[.!]?$", "{t0}を思い出した。"));

        Assert.That(MessagePatternTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_AppliesBeginFlyingPatternWithoutArticle()
    {
        WritePatternDictionary(("^(?:The |the )?(.+?) begins flying[.!]?$", "{0}が飛翔し始めた。"));

        var translated = MessagePatternTranslator.Translate("カロク begins flying.");

        Assert.That(translated, Is.EqualTo("カロクが飛翔し始めた。"));
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
    public void Translate_NoPatternLog_AppendsStructuredPhaseFSuffixInExactOrder()
    {
        WritePatternDictionary(("^You equip (.+)[.!]?$", "{0}を装備した"));

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                MessagePatternTranslator.Translate("You catch fire", "MessagePattern"),
                Is.EqualTo("You catch fire")));

        Assert.That(
            output,
            Does.Contain(
                "[QudJP] MessagePatternTranslator: no pattern for 'You catch fire' (hit 1). (context: MessagePattern); route=MessagePattern; family=message_pattern; template_id=<missing>; rendered_text_sample=You catch fire"));
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
    public void Translate_ReusesLoadedPatternFile_WhenSamePathIsSelectedAgain()
    {
        WritePatternDictionary(("^You hear (.+?)[.!]?$", "あなたは{0}を聞いた"));
        _ = MessagePatternTranslator.Translate("You hear thunder.");
        Assert.That(MessagePatternTranslator.LoadInvocationCount, Is.EqualTo(1));

        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        var translated = string.Empty;
        var output = TestTraceHelper.CaptureTrace(() =>
            translated = MessagePatternTranslator.Translate("You hear rain."));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("あなたはrainを聞いた"));
            Assert.That(MessagePatternTranslator.LoadInvocationCount, Is.EqualTo(0));
            Assert.That(output, Does.Not.Contain("loaded 1 pattern(s)"));
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

    private void WriteScopedHistorySpiceDictionary(params (string key, string text)[] entries)
    {
        var scopedDirectory = Path.Combine(dictionaryDirectory, "Scoped");
        Directory.CreateDirectory(scopedDirectory);

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
        File.WriteAllText(
            Path.Combine(scopedDirectory, "historyspice-common.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
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
        MessagePatternTranslator.InvalidatePatternFileCacheForTests(patternFilePath);
    }

    private void WriteMutationsXml(params (string name, string displayName)[] entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<mutations>");
        foreach (var entry in entries)
        {
            builder.Append("  <mutation Name=\"");
            builder.Append(EscapeXml(entry.name));
            builder.Append("\" DisplayName=\"");
            builder.Append(EscapeXml(entry.displayName));
            builder.AppendLine("\" />");
        }

        builder.AppendLine("</mutations>");
        File.WriteAllText(Path.Combine(tempDirectory, "Mutations.jp.xml"), builder.ToString(), Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesAttackConfirmationWithLocalizedTarget()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("Do you really want to attack the レシェフの神殿?");

        Assert.That(translated, Is.EqualTo("レシェフの神殿を本当に攻撃しますか？"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesReloadMessageWithoutYourCapture()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You reload your クローム・リボルバー with 鉛スラッグ x6.");

        Assert.That(translated, Is.EqualTo("クローム・リボルバーに鉛スラッグ x6を装填した"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesToggleAbilityCapture()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You toggle {{c|Akimbo}} on.");

        Assert.That(translated, Is.EqualTo("{{c|二挺拳銃}}をオンにした。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesGainedActivatedAbilityCapture()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You have gained the activated ability {{Y|Rifle through Trash}}.");

        Assert.That(translated, Is.EqualTo("{{Y|ゴミ漁り}}を有効化能力として獲得した。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesRuntimeObservedDeactivateActivatedAbilityCapture()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You have gained the activated ability {{Y|Deactivate ナインフォールドのブーツ}}.");

        Assert.That(translated, Is.EqualTo("{{Y|ナインフォールドのブーツを停止}}を有効化能力として獲得した。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesColorizedXpGain()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You gain {{C|75}} XP!");

        Assert.That(translated, Is.EqualTo("あなたは経験値を{{C|75}}獲得した"));
    }

    [Test]
    public void Translate_RepositoryDictionary_XpGainFallbackToEnglish()
    {
        UseRepositoryPatternDictionary();

        const string source = "You gain renown!";

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_RepositoryDictionary_XpGainEmptyInputEdgeCase()
    {
        UseRepositoryPatternDictionary();

        Assert.That(MessagePatternTranslator.Translate(string.Empty), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_RepositoryDictionary_XpGainDirectMarkerEdgeCase()
    {
        UseRepositoryPatternDictionary();

        var source = MessageFrameTranslator.MarkDirectTranslation("You gain {{C|75}} XP!");

        Assert.That(MessagePatternTranslator.Translate(source), Is.EqualTo(source));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesPyrokinesisDamage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You take 6 damage from ドリンクスの pyrokinesis!");

        Assert.That(translated, Is.EqualTo("ドリンクスの熱念動で6ダメージを受けた！"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesElectricalArcGeneratedCaptures()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "An electrical arc leaps from you toward the 凍結した 山羊人の暴漢 to the northwest!");

        Assert.That(translated, Is.EqualTo("電弧があなたから凍結した 山羊人の暴漢（北西）へ走った！"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesBootSequenceReadoutDescription()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "Its readout indicates that its startup sequence will take an estimated 7 more rounds.");

        Assert.That(translated, Is.EqualTo("表示には、起動シーケンス完了まであとおよそ7ラウンドかかると示されている。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesDisassembleOnlyAutoActMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("You disassemble your HEミサイル x4.");

        Assert.That(translated, Is.EqualTo("HEミサイル x4を分解した。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesCapitalizedDirectionQualifiedCapture()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "An electrical arc leaps from you toward the 凍結した 山羊人の暴漢 to the North!");

        Assert.That(translated, Is.EqualTo("電弧があなたから凍結した 山羊人の暴漢（北）へ走った！"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesHitFromDirectionDamageCapture()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "A レーザービーム hits you (x1) from the northwest for 3 damage!");

        Assert.That(translated, Is.EqualTo("レーザービームが北西からあなたに命中、3ダメージ！ (x1)"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesHitFromCapitalizedDirectionDamageCapture()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "A レーザービーム hits you (x1) from the North for 3 damage!");

        Assert.That(translated, Is.EqualTo("レーザービームが北からあなたに命中、3ダメージ！ (x1)"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesRuntimeMessageLogActionSiblings()
    {
        UseRepositoryPatternDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessagePatternTranslator.Translate("You butcher the 目なし蟹の死体 to the east into a set of 無眼蟹の脚."),
                Is.EqualTo("東にある目なし蟹の死体を解体して無眼蟹の脚一組にした。"));
            Assert.That(
                MessagePatternTranslator.Translate("You extinguish the キャンプファイヤー."),
                Is.EqualTo("キャンプファイヤーを消した。"));
            Assert.That(
                MessagePatternTranslator.Translate("You light the キャンプファイヤー."),
                Is.EqualTo("キャンプファイヤーに火をつけた。"));
            Assert.That(
                MessagePatternTranslator.Translate("The ウォーターヴァイン農家 sits down on the フロアクッション."),
                Is.EqualTo("ウォーターヴァイン農家はフロアクッションに座った。"));
            Assert.That(
                MessagePatternTranslator.Translate("The 目なし蟹 is stuck in a アスファルトの水たまり!"),
                Is.EqualTo("目なし蟹はアスファルトの水たまりにはまっている！"));
        });
    }

    [Test]
    public void Translate_RepositoryDictionary_RestrictsStopActionHearingPatternsToKnownActions()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You stop moving because you hear タム fighting to the north.");
        var translatedLocalizedAction = MessagePatternTranslator.Translate(
            "You stop 分解中 because you hear タム fighting to the north.");
        var unknownAction = "You stop meditating because you hear タム fighting to the north.";
        var unknownTranslated = MessagePatternTranslator.Translate(unknownAction);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("北でタムが戦っている音が聞こえたので移動をやめた。"));
            Assert.That(translatedLocalizedAction, Is.EqualTo("北でタムが戦っている音が聞こえたので分解をやめた。"));
            Assert.That(unknownTranslated, Is.EqualTo(unknownAction));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(unknownAction), Is.EqualTo(1));
        });
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesGenericReceiveItem()
    {
        UseRepositoryPatternDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessagePatternTranslator.Translate("You receive 奇妙な小物!"),
                Is.EqualTo("奇妙な小物を受け取った"));
            Assert.That(
                MessagePatternTranslator.Translate("You receive {{W|奇妙な小物}}!"),
                Is.EqualTo("{{W|奇妙な小物}}を受け取った"));
            Assert.That(
                MessagePatternTranslator.Translate("You receive " + MessageFrameTranslator.MarkDirectTranslation("奇妙な小物") + "!"),
                Is.EqualTo("奇妙な小物を受け取った"));
        });
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesStatuePrayerMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You voice a short prayer beneath the marble statue of オボロコル.");

        Assert.That(translated, Is.EqualTo("あなたはオボロコルの大理石の像の下で短い祈りを唱えた。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesGeneratedRandomStatueDescriptionLine()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "This statue worked from stone intricately depicts a 山羊人の種播き:");

        Assert.That(translated, Is.EqualTo("石から作られたこの像には山羊人の種播きが精巧に描かれている:"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesStoneStatuePrayerMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You voice a short prayer beneath the 冒涜された stone statue of a 山羊人の種播き.");

        Assert.That(translated, Is.EqualTo("あなたは山羊人の種播きの冒涜された石像の下で短い祈りを唱えた。"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesPlayerProjectileArmorFailureMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "Your 鉛スラッグ fails to penetrate the フォームクリートの armor!");

        Assert.That(translated, Is.EqualTo("あなたの鉛スラッグはフォームクリートの装甲を貫けなかった！"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesPlayerWeaponArmorFailureWithLocalizedPossessive()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate(
            "You don't penetrate 珪岩の armor with the Point of the Commanding Woe. [23]");

        Assert.That(translated, Is.EqualTo("威厳ある嘆きの尖端では珪岩の装甲を貫けなかった！ [23]"));
    }

    [Test]
    public void Translate_RepositoryDictionary_TranslatesSlimyMessage()
    {
        UseRepositoryPatternDictionary();

        var translated = MessagePatternTranslator.Translate("It's disgustingly slimy!");

        Assert.That(translated, Is.EqualTo("吐き気を催すほどぬめっている！"));
    }

    private static void UseRepositoryPatternDictionary()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        MessagePatternTranslator.SetPatternFileForTests(null);
    }
}
