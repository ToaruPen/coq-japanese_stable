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
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_ReturnsSourceUnchanged_ObservationOnly()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "Hello";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("Hello"));
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
    public void Prefix_ReturnsColorWrappedSourceUnchanged_ObservationOnly()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "{{W|Hello}}";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("{{W|Hello}}"));
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarkerBeforeSinkTranslation()
    {
        var text = "\u0001{{W|熊は防いだ。}}";

        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("{{W|熊は防いだ。}}"));
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
    [TestCase("ヴァインウェハー", nameof(InventoryLocalizationPatch))]
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
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0),
                "Observation-only routes skip Translator.Translate entirely");
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsLevelExpHudLineUnchanged_ObservationOnly()
    {
        WriteDictionary(("LVL", "Lv"));

        var source = "LVL: 1 Exp: 0 / 220";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_NoTransformProbe_ForLevelExpSinkRoute_ObservationOnly()
    {
        WriteDictionary(("LVL", "Lv"));

        var source = "LVL: 1 Exp: 0 / 220";
        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(
                    source,
                    nameof(UITextSkinTranslationPatch)),
                Is.EqualTo(source)));

        Assert.That(output, Does.Not.Contain("DynamicTextProbe/v1"),
            "Observation-only routes do not emit transform probes");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsHpHudLineUnchanged_ObservationOnly()
    {
        var source = "HP: 18 / 18";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsStatusLineUnchanged_ObservationOnly()
    {
        WriteDictionary(("Sated", "満腹"), ("Quenched", "潤っている"));

        var source = "Sated Quenched";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsActiveEffectsLineUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wading", "浅瀬を進んでいる"),
            ("wet", "濡れている"));

        var source = "ACTIVE EFFECTS: wading, wet";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsExactLookupTextUnchanged_ObservationOnly()
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
            Assert.That(takeAll, Is.EqualTo("take all"));
            Assert.That(displayOptions, Is.EqualTo("Display Options"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("take all"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Display Options"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsThreatLineUnchanged_ObservationOnly()
    {
        WriteDictionary(("Perfect", "完璧"), ("Injured", "負傷"), ("Hostile", "敵対的"), ("Average", "平均"));

        var source = "Injured, Hostile, Average";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsCompareStatusLinesUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Long Blades (increased penetration on critical hit)", "長剣（クリティカル時に貫通力上昇）"),
            ("no limit", "なし"));

        var capSource = "Strength Bonus Cap: 1";
        var egoCapSource = "Ego Bonus Cap: 2";
        var noLimitSource = "Strength Bonus Cap: no limit";
        var weaponClassSource = "Weapon Class: Long Blades (increased penetration on critical hit)";

        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(capSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(capSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(egoCapSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(egoCapSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(noLimitSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(noLimitSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(weaponClassSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(weaponClassSource));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForPickTargetObservationOnlyRoute()
    {
        WriteDictionary(
            ("Look", "調べる"),
            ("lock", "固定"),
            ("interact", "インタラクト"),
            ("walk", "歩く"),
            ("select", "選択"));

        var source = "Look | ESC | (F1) lock | space interact | W walk | Enter-select";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(PickTargetWindowTextTranslator));

        Assert.That(translated, Is.EqualTo(source),
            "PickTargetWindowTextTranslator is observation-only — source must pass through unchanged");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForInventoryObservationOnlyRoute()
    {
        WriteDictionary(("Show Tooltip", "ツールチップ表示"));

        var source = "Show Tooltip";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(InventoryAndEquipmentStatusScreenTranslationPatch));

        Assert.That(translated, Is.EqualTo(source),
            "InventoryAndEquipmentStatusScreenTranslationPatch is observation-only — source must pass through unchanged");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsFactionWrappersUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"),
            ("The {0}", "{0}"),
            ("Reputation: {0}", "評判: {0}"));

        var relationshipSource = "The villagers of Abal don't care about you, but aggressive ones will attack you.";
        var labelSource = "The Arbitrarilyborn Cult";
        var reputationSource = "Reputation:     0";

        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(relationshipSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(relationshipSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(labelSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(labelSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(reputationSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(reputationSource));
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
    public void TranslatePreservingColors_ReturnsSkillStatusFamiliesUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("Learned", "習得済み"),
            ("Starting Cost [{val} sp]", "初期コスト [{val} sp]"),
            ("Skill Points (SP): {val}", "スキルポイント (SP): {val}"),
            ("Tinker I", "工匠 I"),
            ("Tinker II", "工匠 II"));

        var learnedSource = "Learned [5/10]";
        var startingCostSource = "Starting Cost [100 sp] [1/10]";
        var requirementBlockSource = ":: 100 SP ::\n:: 23 Intelligence ::\n";
        var skillLineSource = "    :Tinker II [200sp] 23 Intelligence, Tinker I";

        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(learnedSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(learnedSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(startingCostSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(startingCostSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(requirementBlockSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(requirementBlockSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(skillLineSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(skillLineSource));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSkillSectionLabelsUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("Melee", "近接戦闘"),
            ("Melee Weapons", "近接武器"),
            ("Short Blades", "短剣"));

        var sectionSource = ":Melee";
        var subsectionSource = "  :Melee Weapons";
        var skillLineSource = "    :Short Blades [100sp] 15 Agility";

        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(sectionSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(sectionSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(subsectionSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(subsectionSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(skillLineSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(skillLineSource));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsTrimmedLookupTextUnchanged_ObservationOnly()
    {
        WriteDictionary(("Joppa", "ジョッパ"));

        var source = " Joppa";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
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
    public void TranslatePreservingColors_ReturnsSourceUnchangedForFactionsObservationOnlyRoute()
    {
        WriteDictionary(
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"));

        var source = "The villagers of Abal don't care about you, but aggressive ones will attack you.";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.That(translated, Is.EqualTo(source),
            "FactionsStatusScreenTranslationPatch is observation-only — source must pass through unchanged");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForFactionsTopicListsObservationOnlyRoute()
    {
        WriteDictionary(
            ("The {0} are interested in learning about {1}.", "{0}は{1}について知ることに関心がある。"),
            ("the locations of insect lair", "昆虫の巣の場所"),
            ("the locations of ape lair", "類人猿の巣の場所"),
            ("sultan they admire or despise", "彼らが好悪を抱くスルタン"),
            ("gossip that's about them", "彼ら自身に関するうわさ話"));

        var source = "The apes are interested in learning about the locations of insect lair, the locations of ape lair, sultan they admire or despise, and gossip that's about them.";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.That(translated, Is.EqualTo(source),
            "FactionsStatusScreenTranslationPatch is observation-only — source must pass through unchanged");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForCharacterStatusObservationOnlyRoute()
    {
        WriteDictionary(
            ("Attribute Points: {0}", "能力値ポイント: {0}"),
            ("Mutated Human", "変異人間"));

        var sources = new[]
        {
            "Attribute Points: 0",
            "Mutation Points: 0",
            "Mutated Human Tinker",
            "Level: 1 ¯ HP: 18/18 ¯ XP: 0/220 ¯ Weight: 405#",
            "Force Wall (1)",
            "{{G|RANK 1/10}}",
            "{{c|[Mental Mutation]}}",
            "You see in the dark.",
        };

        Assert.Multiple(() =>
        {
            foreach (var source in sources)
            {
                var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
                    source,
                    nameof(CharacterStatusScreenTranslationPatch));

                Assert.That(translated, Is.EqualTo(source),
                    $"CharacterStatusScreenTranslationPatch is observation-only — '{source}' must pass through unchanged");
            }
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForCharacterStatusCompactLinesObservationOnlyRoute()
    {
        WriteDictionary(
            ("LVL", "Lv"),
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wading", "浅瀬を進んでいる"),
            ("wet", "濡れている"));

        var sources = new[]
        {
            "LVL: 1 Exp: 0 / 220",
            "HP: 18 / 18",
            "ACTIVE EFFECTS: wading, wet",
            "Strength Bonus Cap: no limit",
            "Ego Bonus Cap: 2",
            "Weapon Class: Long Blades (increased penetration on critical hit)",
        };

        Assert.Multiple(() =>
        {
            foreach (var source in sources)
            {
                var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
                    source,
                    nameof(CharacterStatusScreenTranslationPatch));

                Assert.That(translated, Is.EqualTo(source),
                    $"CharacterStatusScreenTranslationPatch is observation-only — '{source}' must pass through unchanged");
            }
        });
    }

    [Test]
    public void TranslateStringField_ObservationOnly_LeavesKnownTextUnchanged()
    {
        WriteDictionary(("New Game", "新しいゲーム"));

        var option = new DummyMenuOption { Text = "New Game" };

        UITextSkinTranslationPatch.TranslateStringField(
            option,
            nameof(DummyMenuOption.Text),
            "MainMenuLocalizationPatch > collection=LeftOptions");

        Assert.Multiple(() =>
        {
            Assert.That(option.Text, Is.EqualTo("New Game"));
            Assert.That(SinkObservation.GetHitCountForTests(
                nameof(UITextSkinTranslationPatch),
                "MainMenuLocalizationPatch > collection=LeftOptions",
                SinkObservation.ObservationOnlyDetail,
                "New Game",
                "New Game"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateStringField_PassesThroughUnknownText()
    {
        WriteDictionary(("Known", "既知"));

        var option = new DummyMenuOption { Text = "Unknown text" };

        UITextSkinTranslationPatch.TranslateStringField(
            option,
            nameof(DummyMenuOption.Text),
            "MainMenuLocalizationPatch > collection=LeftOptions");

        Assert.That(option.Text, Is.EqualTo("Unknown text"),
            "Unknown text passes through unchanged when not in dictionary");
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

    [Test, Category("L2")]
    [TestCase("MainMenuLocalizationPatch")]
    [TestCase("OptionsLocalizationPatch")]
    [TestCase("CharGenLocalizationPatch")]
    [TestCase("PickTargetWindowTextTranslator")]
    [TestCase("CharacterStatusScreenTranslationPatch")]
    [TestCase("FactionsStatusScreenTranslationPatch")]
    [TestCase("InventoryAndEquipmentStatusScreenTranslationPatch")]
    [TestCase("ConversationDisplayTextPatch")]
    [TestCase("DescriptionLongDescriptionPatch")]
    [TestCase("LookTooltipContentPatch")]
    [TestCase("UITextSkinTranslationPatch")]
    public void TranslatePreservingColors_AllRoutes_ObservationOnly(string context)
    {
        SinkObservation.ResetForTests();
        var source = "English text that would normally be translated";
        var result = UITextSkinTranslationPatch.TranslatePreservingColors(source, context);
        Assert.That(result, Is.EqualTo(source));
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
    public void HarmonyPatch_AppliesPrefix_ToDummyUITextSkin_ObservationOnly()
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

            Assert.That(dummy.Text, Is.EqualTo("World"),
                "Observation-only mode passes source through unchanged");
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
