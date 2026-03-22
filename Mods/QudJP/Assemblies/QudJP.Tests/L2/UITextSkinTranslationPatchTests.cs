using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class UITextSkinTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-uitextskin-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesKnownKey()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "Hello";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("こんにちは"));
    }

    [Test]
    public void Prefix_PassesThroughUnknownText()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "Unknown text";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("Unknown text"));
    }

    [Test]
    public void Prefix_PreservesColorCodes()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "{{W|Hello}}";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("{{W|こんにちは}}"));
    }

    [TestCaseSource(typeof(QudJP.Tests.L1.ColorRouteInvariantCases), nameof(QudJP.Tests.L1.ColorRouteInvariantCases.UiTextSkinCases))]
    public void TranslatePreservingColors_PreservesSharedInvariantCases(QudJP.Tests.L1.ColorTranslationCase testCase)
    {
        WriteDictionary(testCase.Entries.ToArray());

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(testCase.Source, testCase.Context);

        Assert.That(translated, Is.EqualTo(testCase.Expected));
    }

    [TestCase("[Esc]")]
    [TestCase("[Space]")]
    [TestCase("[]")]
    [TestCase("[Esc] Cancel")]
    [TestCase("SP: 99")]
    [TestCase("1.0.4\nbuild 2.0.210.24")]
    [TestCase("quit")]
    public void Prefix_SkipsKnownObservabilityNoiseTokens(string text)
    {
        WriteDictionary(("quit", "終了"), ("[Esc]", "[Esc-JP]"));

        var original = text;
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo(original));
            Assert.That(Translator.GetMissingKeyHitCountForTests(original), Is.EqualTo(0));
        });
    }

    [TestCase("クラシック")]
    [TestCase("チュートリアル\n[A]")]
    [TestCase("：ゲームモードを選択：")]
    [TestCase(" >{{K| . . . . . . . ■ .  . . . . . . . ■")]
    [TestCase("   ")]
    public void TranslatePreservingColors_SkipsAlreadyLocalizedUITextSinkText(string text)
    {
        WriteDictionary(("クラシック", "CLASSIC-JP"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_DoesNotSuppressJapaneseTextForNonSinkContexts()
    {
        var text = "クラシック";

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(CharGenLocalizationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
        });
    }

    [TestCase("新しいゲーム", nameof(MainMenuLocalizationPatch))]
    [TestCase("：プレイ方式を選択：", nameof(CharGenLocalizationPatch))]
    [TestCase("チュートリアル\n[A]", nameof(CharGenLocalizationPatch))]
    [TestCase("Caves of Qud の基礎を学ぶ。", nameof(CharGenLocalizationPatch))]
    [TestCase("有機生命体の史料庫と照合する微小なグラフェンアレイ。\n\n生物クリーチャーの正確なHP・AV・DVを参照できる。", nameof(CharGenLocalizationPatch))]
    [TestCase("甲殻", nameof(GetDisplayNamePatch))]
    [TestCase("木製バックラー", nameof(GetDisplayNameProcessPatch))]
    [TestCase("イッサカリ族", nameof(FactionsStatusScreenTranslationPatch))]
    [TestCase("ヴァインウェイファー", nameof(InventoryLocalizationPatch))]
    [TestCase("ウォーターヴァイン農家", nameof(PopupTranslationPatch))]
    [TestCase("[■] 効果音", nameof(OptionsLocalizationPatch))]
    [TestCase("移動", nameof(OptionsLocalizationPatch))]
    [TestCase("新しいゲーム", "MainMenuLocalizationPatch > collection=LeftOptions > itemType=MainMenuOptionData > field=Text")]
    [TestCase("[基本盾]", nameof(GetDisplayNameProcessPatch))]
    [TestCase("[R]", nameof(CharGenLocalizationPatch))]
    [TestCase("[Delete]", nameof(CharGenLocalizationPatch))]
    [TestCase("[ ][3]", nameof(CharGenLocalizationPatch))]
    [TestCase("[■][2]", nameof(CharGenLocalizationPatch))]
    [TestCase("[2pts]", nameof(CharGenLocalizationPatch))]
    public void TranslatePreservingColors_SkipsAlreadyLocalizedDirectRouteText(string text, string context)
    {
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, context);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
        });
    }

    [TestCase("光学バイオスキャナ (Face)")]
    [TestCase("+1 Toughness")]
    [TestCase("Stinger (Confusing Venom)")]
    public void TranslatePreservingColors_KeepsObservingMixedLanguageDirectRouteText(string text)
    {
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(CharGenLocalizationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(1));
        });
    }

    [Test]
    public void TranslatePreservingColors_KeepsExpLabelInLevelExpHudLineInSinkContext()
    {
        WriteDictionary(("LVL", "Lv"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "LVL: 1 Exp: 0 / 220",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("Lv: 1 Exp: 0 / 220"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("LVL: 1 Exp: 0 / 220"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_LogsDynamicTransformProbe_ForLevelExpSinkRoute()
    {
        WriteDictionary(("LVL", "Lv"));

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(
                    "LVL: 1 Exp: 0 / 220",
                    nameof(UITextSkinTranslationPatch)),
                Is.EqualTo("Lv: 1 Exp: 0 / 220")));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("DynamicTextProbe/v1"));
            Assert.That(output, Does.Contain("route='UITextSkinTranslationPatch'"));
            Assert.That(output, Does.Contain("family='UITextSink.LevelExp'"));
            Assert.That(output, Does.Contain("source='LVL: 1 Exp: 0 / 220'"));
            Assert.That(output, Does.Contain("translated='Lv: 1 Exp: 0 / 220'"));
        });
    }

    [Test]
    public void TranslatePreservingColors_KeepsHpLabelInHudLineInSinkContext()
    {
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "HP: 18 / 18",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("HP: 18 / 18"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("HP: 18 / 18"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesSpaceSeparatedStatusLineInSinkContext()
    {
        WriteDictionary(("Sated", "満腹"), ("Quenched", "潤っている"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Sated Quenched",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("満腹 潤っている"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Sated Quenched"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesActiveEffectsStatusLineInSinkContext()
    {
        WriteDictionary(
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wading", "浅瀬を進んでいる"),
            ("wet", "濡れている"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "ACTIVE EFFECTS: wading, wet",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("発動中の効果: 浅瀬を進んでいる、濡れている"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("ACTIVE EFFECTS: wading, wet"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_PrefersExactLookupOverSpaceSequenceInSinkContext()
    {
        WriteDictionary(
            ("take all", "すべて取る"),
            ("display options", "表示オプション"));

        var takeAll = UITextSkinTranslationPatch.TranslatePreservingColors(
            "take all",
            nameof(UITextSkinTranslationPatch));
        var displayOptions = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Display Options",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(takeAll, Is.EqualTo("すべて取る"));
            Assert.That(displayOptions, Is.EqualTo("表示オプション"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("take all"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Display Options"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesCommaSeparatedThreatLineInSinkContext()
    {
        WriteDictionary(("Perfect", "完璧"), ("Injured", "負傷"), ("Hostile", "敵対的"), ("Average", "平均"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Injured, Hostile, Average",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("負傷、敵対的、平均"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Injured, Hostile, Average"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesCompareStatusLinesInSinkContext()
    {
        WriteDictionary(
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Long Blades (increased penetration on critical hit)", "長剣（クリティカル時に貫通力上昇）"),
            ("no limit", "なし"));

        var cap = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Strength Bonus Cap: 1",
            nameof(UITextSkinTranslationPatch));
        var egoCap = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Ego Bonus Cap: 2",
            nameof(UITextSkinTranslationPatch));
        var noLimit = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Strength Bonus Cap: no limit",
            nameof(UITextSkinTranslationPatch));
        var weaponClass = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Weapon Class: Long Blades (increased penetration on critical hit)",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(cap, Is.EqualTo("筋力ボーナス上限: 1"));
            Assert.That(egoCap, Is.EqualTo("Ego ボーナス上限: 2"));
            Assert.That(noLimit, Is.EqualTo("筋力ボーナス上限: なし"));
            Assert.That(weaponClass, Is.EqualTo("武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesCommandBarInPickTargetRouteContext()
    {
        WriteDictionary(
            ("Look", "調べる"),
            ("lock", "固定"),
            ("interact", "インタラクト"),
            ("walk", "歩く"),
            ("select", "選択"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Look | ESC | (F1) lock | space interact | W walk | Enter-select",
            nameof(PickTargetWindowTextTranslator));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("調べる | ESC | (F1) 固定 | space インタラクト | W 歩く | Enter-選択"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Look | ESC | (F1) lock | space interact | W walk | Enter-select"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesInventoryTooltipLabelInDedicatedRouteContext()
    {
        WriteDictionary(("Show Tooltip", "ツールチップ表示"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Show Tooltip",
            nameof(InventoryAndEquipmentStatusScreenTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("ツールチップ表示"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Show Tooltip"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_LeavesFactionWrappersToDedicatedRoute()
    {
        WriteDictionary(
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"),
            ("The {0}", "{0}"),
            ("Reputation: {0}", "評判: {0}"));

        var relationship = UITextSkinTranslationPatch.TranslatePreservingColors(
            "The villagers of Abal don't care about you, but aggressive ones will attack you.",
            nameof(UITextSkinTranslationPatch));
        var label = UITextSkinTranslationPatch.TranslatePreservingColors(
            "The Arbitrarilyborn Cult",
            nameof(UITextSkinTranslationPatch));
        var reputation = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Reputation:     0",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(relationship, Is.EqualTo("The villagers of Abal don't care about you, but aggressive ones will attack you."));
            Assert.That(label, Is.EqualTo("The Arbitrarilyborn Cult"));
            Assert.That(reputation, Is.EqualTo("Reputation:     0"));
        });
    }

    [Test]
    public void TranslatePreservingColors_SkipsFactionsLineOwnedTextWithoutMissingKeyLogs()
    {
        var source = "The villagers of Abal don't care about you, but aggressive ones will attack you.";

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(FactionsLineTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesSkillStatusFamiliesInSinkContext()
    {
        WriteDictionary(
            ("Learned", "習得済み"),
            ("Starting Cost [{val} sp]", "初期コスト [{val} sp]"),
            ("Skill Points (SP): {val}", "スキルポイント (SP): {val}"),
            ("Tinker I", "工匠 I"),
            ("Tinker II", "工匠 II"));

        var learned = UITextSkinTranslationPatch.TranslatePreservingColors("Learned [5/10]", nameof(UITextSkinTranslationPatch));
        var startingCost = UITextSkinTranslationPatch.TranslatePreservingColors("Starting Cost [100 sp] [1/10]", nameof(UITextSkinTranslationPatch));
        var requirementBlock = UITextSkinTranslationPatch.TranslatePreservingColors(":: 100 SP ::\n:: 23 Intelligence ::\n", nameof(UITextSkinTranslationPatch));
        var skillLine = UITextSkinTranslationPatch.TranslatePreservingColors("    :Tinker II [200sp] 23 Intelligence, Tinker I", nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(learned, Is.EqualTo("習得済み [5/10]"));
            Assert.That(startingCost, Is.EqualTo("初期コスト [100 sp] [1/10]"));
            Assert.That(requirementBlock, Is.EqualTo(":: 100 SP ::\n:: 23 INT ::"));
            Assert.That(skillLine, Is.EqualTo("    :工匠 II [200sp] 23 INT, 工匠 I"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesSkillSectionLabelsInSinkContext()
    {
        WriteDictionary(
            ("Melee", "近接戦闘"),
            ("Melee Weapons", "近接武器"),
            ("Short Blades", "短剣"));

        var section = UITextSkinTranslationPatch.TranslatePreservingColors(
            ":Melee",
            nameof(UITextSkinTranslationPatch));
        var subsection = UITextSkinTranslationPatch.TranslatePreservingColors(
            "  :Melee Weapons",
            nameof(UITextSkinTranslationPatch));
        var skillLine = UITextSkinTranslationPatch.TranslatePreservingColors(
            "    :Short Blades [100sp] 15 Agility",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(section, Is.EqualTo(":近接戦闘"));
            Assert.That(subsection, Is.EqualTo("  :近接武器"));
            Assert.That(skillLine, Is.EqualTo("    :短剣 [100sp] 15 AGI"));
        });
    }

    [Test]
    public void TranslatePreservingColors_UsesTrimmedLookupInSinkContext()
    {
        WriteDictionary(("Joppa", "ジョッパ"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            " Joppa",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(" ジョッパ"));
            Assert.That(Translator.GetMissingKeyHitCountForTests(" Joppa"), Is.EqualTo(0));
        });
    }

    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharGenLocalizationPatch), "QudMutationsModule", "QudCyberneticsModule")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharGenLocalizationPatch), "EmbarkBuilder")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharacterStatusScreenTranslationPatch), "Qud.UI.CharacterStatusScreen")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharacterStatusScreenTranslationPatch), "Qud.UI.CharacterMutationLine")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharacterStatusScreenTranslationPatch), "Qud.UI.CharacterAttributeLine")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(FactionsStatusScreenTranslationPatch), "Qud.UI.FactionsStatusScreen")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(FactionsLineTranslationPatch), "Qud.UI.FactionsLine")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(MainMenuLocalizationPatch), "Qud.UI.MainMenu")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(OptionsLocalizationPatch), "Qud.UI.OptionsScreen")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(PickTargetWindowTextTranslator), "XRL.UI.PickTargetWindow")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(PopupTranslationPatch), "Qud.UI.Popup")]
    public void ResolveObservabilityContext_ReclassifiesKnownSinkStacks(
        string originalContext,
        string expectedContext,
        params string[] stackTypeNames)
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(originalContext, stackTypeNames);

        Assert.That(resolvedContext, Is.EqualTo(expectedContext));
    }

    [TestCase("Points Remaining: 12")]
    [TestCase("Your Strength score determines how effectively you penetrate armor.")]
    [TestCase("ù +2 Ego\nù Proselytize\n")]
    public void ResolveObservabilityContext_ReclassifiesKnownCharGenTextPatterns(string source)
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(
            nameof(UITextSkinTranslationPatch),
            source,
            "Some.Unrelated.Widget");

        Assert.That(resolvedContext, Is.EqualTo(nameof(CharGenLocalizationPatch)));
    }

    [Test]
    public void ResolveObservabilityContext_LeavesUnknownSinkStackUntouched()
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(
            nameof(UITextSkinTranslationPatch),
            "Some.Unrelated.Widget");

        Assert.That(resolvedContext, Is.EqualTo(nameof(UITextSkinTranslationPatch)));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesFactionFamiliesInDedicatedRouteContext()
    {
        WriteDictionary(
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "The villagers of Abal don't care about you, but aggressive ones will attack you.",
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.That(translated, Is.EqualTo("Abalの村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesFactionTopicListsInDedicatedRouteContext()
    {
        WriteDictionary(
            ("The {0} are interested in learning about {1}.", "{0}は{1}について知ることに関心がある。"),
            ("the locations of insect lair", "昆虫の巣の場所"),
            ("the locations of ape lair", "類人猿の巣の場所"),
            ("sultan they admire or despise", "彼らが好悪を抱くスルタン"),
            ("gossip that's about them", "彼ら自身に関するうわさ話"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "The apes are interested in learning about the locations of insect lair, the locations of ape lair, sultan they admire or despise, and gossip that's about them.",
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.That(
            translated,
            Is.EqualTo("apesは昆虫の巣の場所、類人猿の巣の場所、彼らが好悪を抱くスルタン、と彼ら自身に関するうわさ話について知ることに関心がある。"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesCharacterStatusFamiliesInDedicatedRouteContext()
    {
        WriteDictionary(
            ("Attribute Points: {0}", "能力値ポイント: {0}"),
            ("Mutation Points: {0}", "突然変異ポイント: {0}"),
            ("Mutated Human", "変異人間"),
            ("Tinker", "修理工"),
            ("LVL", "Lv"),
            ("Weight", "重量"),
            ("Force Wall", "力場壁"),
            ("RANK", "ランク"),
            ("Mental Mutation", "精神突然変異"),
            ("You see in the dark.", "暗闇でも見える。"),
            ("Your {{W|Ego}} determines the potency of your mental mutations, your ability to haggle with merchants, and your ability to dominate the wills of other living creatures.", "あなたの{{W|自我}}は精神突然変異の威力、商人との取引能力、他者の意志を支配する力を決める。"));

        var attributePoints = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Attribute Points: 0",
            nameof(CharacterStatusScreenTranslationPatch));
        var mutationPoints = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Mutation Points: 0",
            nameof(CharacterStatusScreenTranslationPatch));
        var title = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Mutated Human Tinker",
            nameof(CharacterStatusScreenTranslationPatch));
        var summary = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Level: 1 ¯ HP: 18/18 ¯ XP: 0/220 ¯ Weight: 405#",
            nameof(CharacterStatusScreenTranslationPatch));
        var mutationLine = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Force Wall (1)",
            nameof(CharacterStatusScreenTranslationPatch));
        var rank = UITextSkinTranslationPatch.TranslatePreservingColors(
            "{{G|RANK 1/10}}",
            nameof(CharacterStatusScreenTranslationPatch));
        var mutationType = UITextSkinTranslationPatch.TranslatePreservingColors(
            "{{c|[Mental Mutation]}}",
            nameof(CharacterStatusScreenTranslationPatch));
        var help = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Your Ego determines the potency of your mental mutations, your ability to haggle with merchants, and your ability to dominate the wills of other living creatures.",
            nameof(CharacterStatusScreenTranslationPatch));
        var darkVision = UITextSkinTranslationPatch.TranslatePreservingColors(
            "You see in the dark.",
            nameof(CharacterStatusScreenTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(attributePoints, Is.EqualTo("能力値ポイント: 0"));
            Assert.That(mutationPoints, Is.EqualTo("突然変異ポイント: 0"));
            Assert.That(title, Is.EqualTo("変異人間 修理工"));
            Assert.That(summary, Is.EqualTo("Lv: 1 ¯ HP: 18/18 ¯ XP: 0/220 ¯ 重量: 405#"));
            Assert.That(mutationLine, Is.EqualTo("力場壁 (1)"));
            Assert.That(rank, Is.EqualTo("{{G|ランク 1/10}}"));
            Assert.That(mutationType, Is.EqualTo("{{c|[精神突然変異]}}"));
            Assert.That(help, Is.EqualTo("あなたの{{W|自我}}は精神突然変異の威力、商人との取引能力、他者の意志を支配する力を決める。"));
            Assert.That(darkVision, Is.EqualTo("暗闇でも見える。"));
        });
    }

    [Test]
    public void TranslatePreservingColors_KeepsHpAndExpLabelsInCharacterStatusCompactLines()
    {
        WriteDictionary(
            ("LVL", "Lv"),
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wading", "浅瀬を進んでいる"),
            ("wet", "濡れている"),
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Long Blades (increased penetration on critical hit)", "長剣（クリティカル時に貫通力上昇）"),
            ("no limit", "なし"));

        var levelExp = UITextSkinTranslationPatch.TranslatePreservingColors(
            "LVL: 1 Exp: 0 / 220",
            nameof(CharacterStatusScreenTranslationPatch));
        var hp = UITextSkinTranslationPatch.TranslatePreservingColors(
            "HP: 18 / 18",
            nameof(CharacterStatusScreenTranslationPatch));
        var activeEffects = UITextSkinTranslationPatch.TranslatePreservingColors(
            "ACTIVE EFFECTS: wading, wet",
            nameof(CharacterStatusScreenTranslationPatch));
        var cap = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Strength Bonus Cap: no limit",
            nameof(CharacterStatusScreenTranslationPatch));
        var egoCap = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Ego Bonus Cap: 2",
            nameof(CharacterStatusScreenTranslationPatch));
        var weaponClass = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Weapon Class: Long Blades (increased penetration on critical hit)",
            nameof(CharacterStatusScreenTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(levelExp, Is.EqualTo("Lv: 1 Exp: 0 / 220"));
            Assert.That(hp, Is.EqualTo("HP: 18 / 18"));
            Assert.That(activeEffects, Is.EqualTo("発動中の効果: 浅瀬を進んでいる、濡れている"));
            Assert.That(cap, Is.EqualTo("筋力ボーナス上限: なし"));
            Assert.That(egoCap, Is.EqualTo("Ego ボーナス上限: 2"));
            Assert.That(weaponClass, Is.EqualTo("武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
        });
    }

    [Test]
    public void TranslateStringField_LogsRouteCollectionAndFieldDetails()
    {
        var option = new DummyMenuOption { Text = "Unknown text" };

        var output = TestTraceHelper.CaptureTrace(() =>
            UITextSkinTranslationPatch.TranslateStringField(
                option,
                nameof(DummyMenuOption.Text),
                "MainMenuLocalizationPatch > collection=LeftOptions"));

        Assert.Multiple(() =>
        {
            Assert.That(option.Text, Is.EqualTo("Unknown text"));
            Assert.That(
                output,
                Does.Contain("context: MainMenuLocalizationPatch > collection=LeftOptions > itemType=DummyMenuOption > field=Text"));
            Assert.That(Translator.GetMissingRouteHitCountForTests(nameof(MainMenuLocalizationPatch)), Is.EqualTo(1));
        });
    }

    [Test]
    public void TranslatePreservingColors_SuppressesAlreadyLocalizedMarkupWrappedJapaneseDisplayName()
    {
        var source = "{{B}}|濡れた豚農家";

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void Prefix_HandlesNullOrEmpty()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var emptyText = string.Empty;
        UITextSkinTranslationPatch.Prefix(ref emptyText);

        Assert.That(emptyText, Is.EqualTo(string.Empty));
    }

    [Test]
    public void HarmonyPatch_AppliesPrefix_ToDummyUITextSkin()
    {
        WriteDictionary(("World", "世界"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyUITextSkin), nameof(DummyUITextSkin.SetText)),
                prefix: new HarmonyMethod(RequireMethod(typeof(UITextSkinTranslationPatch), nameof(UITextSkinTranslationPatch.Prefix))));

            var dummy = new DummyUITextSkin();
            dummy.SetText("World");

            Assert.That(dummy.Text, Is.EqualTo("世界"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyMenuOption
    {
        public string Text = string.Empty;
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

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

        var path = Path.Combine(tempDirectory, "ui-textskin.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
