using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupShowTranslationPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-show-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase("We're not quite ready to leave yet.", "まだ出発の準備が整っていない。")]
    [TestCase("{{C|Your golem is ready for use.}}", "{{C|ゴーレムは使用可能だ。}}")]
    [TestCase("You feel some ambient astral friction here.", "ここでは周囲の星界摩擦を感じる。")]
    [TestCase("{{r|Your domination is broken!}}", "{{r|支配が破られた！}}")]
    public void Prefix_TranslatesPopupShowMessage(string source, string expected)
    {
        WriteDictionary((source, expected));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDiscoverLocationTemplate()
    {
        WriteDictionary(("You discover {0}!", "{0}を発見した！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You discover Rust Wells!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("Rust Wellsを発見した！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesGenericReceiveItemPattern()
    {
        WriteMessagePatternDictionary(("^You receive (.+?)[.!]?$", "{t0}を受け取った"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You receive 奇妙な小物!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("奇妙な小物を受け取った"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DoesNotRecordMessagePatternMiss_WhenNoPopupPatternMatches()
    {
        const string source = "This popup is owned by another route.";

        var translated = PopupShowSemanticPipeline.TranslateMessage(source, nameof(PopupShowTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(source), Is.EqualTo(0));
            Assert.That(
                MessagePatternTranslator.GetMissingRouteHitCountForTests(nameof(PopupShowTranslationPatch)),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void Prefix_TranslatesDiscoverLocationTemplateWithColorWrappedTarget()
    {
        WriteDictionary(("You discover {0}!", "{0}を発見した！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You discover {{Y|Rust Wells}}!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|Rust Wells}}を発見した！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesExaminerHiddenDiscoveryTemplate()
    {
        WriteDictionary(("You discover something about {0} that was hidden!", "{0}について隠されていたことを発見した！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You discover something about phase cannon that was hidden!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("phase cannonについて隠されていたことを発見した！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesQuestReceivedPopupWithQuestTitleExclamation()
    {
        WriteDictionary(("You have received a new quest, {0}!", "新しいクエスト「{0}」を受けた！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You have received a new quest, {{W|O Glorious Shekhinah!}}!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("新しいクエスト「{{W|O Glorious Shekhinah!}}」を受けた！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPerformOfferTradeWaterMessage_WithoutDictionaryEntry()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You don't have 50 drams of fresh water to even up the trade!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("取引を釣り合わせるための50ドラムの真水が足りない！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("There are no creatures in range.", "範囲内に生き物がいない。")]
    [TestCase("Your bilge sphincter is missing.", "ビルジ括約筋がない。")]
    [TestCase("There is no liquid here for you to spew.", "ここには噴き出せる液体がない。")]
    [TestCase("That is out of range! (10 squares)", "射程外だ！(10マス)")]
    [TestCase("There is no one there to sting.", "そこに刺す相手がいない。")]
    [TestCase("There is no one there you can sting.", "そこに刺せる相手がいない。")]
    [TestCase("You may not use this mutation on the world map.", "ワールドマップではこの変異は使えない。")]
    [TestCase("You may not perform temporal fugue on the world map.", "ワールドマップでは時間遁走を実行できない。")]
    [TestCase("It is impossible to duplicate {{Y|the turret}}.", "{{Y|タレット}}は複製できない。")]
    [TestCase("That target is out of range! (5 squares)", "その対象は射程外だ！(5マス)")]
    [TestCase("You are already burrowed.", "もう潜伏している。")]
    [TestCase("You cannot burrow on the world map.", "ワールドマップでは潜伏できない。")]
    [TestCase(
        "The sealing mechanisms inside this sarcophagus will certainly kill you if you close itself inside. Are you sure you want to enter the sarcophagus?\n\nType 'ENTOMB' to confirm.",
        "この石棺の内部の封印機構は、中に入ったまま閉じれば確実にあなたを殺す。本当に石棺に入りますか？\n\n確認するには「ENTOMB」と入力してください。")]
    [TestCase(
        "Choose artifacts to throw down the well.",
        "井戸に投げ込むアーティファクトを選択してください。")]
    [TestCase(
        "You have run out of {{B|water}}! Do you want to stop travelling?",
        "{{B|水}}が尽きた！移動を止めますか？")]
    [TestCase(
        "You are dying of thirst! Do you want to stop travelling?",
        "渇きで死にかけている！移動を止めますか？")]
    [TestCase(
        "Your abysmal ritual performance deeply shames you.",
        "惨憺たる儀式の出来に、深く恥じ入る。")]
    [TestCase(
        "Your performance of the formal water ritual was sublime and inspiring.",
        "正式な水儀は崇高で感動的だった。")]
    [TestCase(
        "Your performance of the naming ritual was solemn and dignified.",
        "命名の儀式は厳粛で威厳があった。")]
    [TestCase(
        "You have no more usable options, so your performance so far will determine the outcome.",
        "使える手段が尽きたので、ここまでの成果で結果が決まる。")]
    [TestCase(
        "You do not have a leather whip.",
        "革鞭がない。")]
    [TestCase(
        "You do not have a farmer's token.",
        "農夫の証票がない。")]
    [TestCase(
        "You do not have a merchant's token.",
        "商人の証票がない。")]
    [TestCase(
        "You do not have a minstrel's token.",
        "吟遊詩人の証票がない。")]
    [TestCase(
        "You are leaving the ambient broadcast power grid and transitioning to backup power. Are you sure?",
        "環境放送電力網を離れ、予備電源に切り替えますか？")]
    [TestCase(
        "Death has no meaning here.",
        "ここでは死に意味はない。")]
    [TestCase(
        "Just before your demise, your health is restored!",
        "死の寸前で体力が回復する！")]
    [TestCase(
        "You are suddenly elsewhere!",
        "気がつくと別の場所にいた！")]
    [TestCase(
        "[{{R|!!! ERROR: REMOTE MANAGEMENT OFFLINE !!!}}]\n[{{R|!!! CHAIN LASER EMPLACEMENTS MUST BE ACTIVATED MANUALLY !!!}}]",
        "[{{R|!!! エラー: リモート管理はオフライン !!!}}]\n[{{R|!!! チェーンレーザー設置は手動で起動する必要がある !!!}}]")]
    [TestCase(
        "[{{R|!!! ERROR: REMOTE MANAGEMENT OFFLINE !!!}}]\n[{{R|!!! FORCE PROJECTORS MUST BE ACTIVATED MANUALLY !!!}}]",
        "[{{R|!!! エラー: リモート管理はオフライン !!!}}]\n[{{R|!!! フォース・プロジェクターは手動で起動する必要がある !!!}}]")]
    [TestCase(
        "You do not have any recoilers.",
        "リコイラーを持っていない。")]
    [TestCase(
        "You have no devices that use energy cells.",
        "エネルギーセルを使う装置を持っていない。")]
    public void Prefix_TranslatesReviewedResidualFixedPopups_FromRepositoryDictionary(
        string source,
        string expected)
    {
        Translator.SetDictionaryDirectoryForTests(Path.Combine(GetLocalizationRoot(), "Dictionaries"));

        Assert.That(RunShowFailWithPopupPatch(source), Is.EqualTo(expected));
    }

    [Test]
    public void Prefix_UsesPerformOfferTradeWaterTemplate_IgnoresDictionaryEntriesAndPreservesColorTags()
    {
        WriteDictionary(
            ("You don't have 50 drams of fresh water to even up the trade!", "dictionary exact fallback should not be used"),
            ("You don't have {0} to even up the trade!", "dictionary template should not be used: {0}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("{{y|You don't have {{C|50}} drams of fresh water to even up the trade!}}");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("{{y|取引を釣り合わせるための{{C|50}}ドラムの真水が足りない！}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_UsesTradeUiOwnerTemplateForHasNothingToTrade_BeforeGenericMessagePattern()
    {
        WriteMessagePatternDictionary(("^(?:The |the |[Aa]n? )?(.+?) (?:has|have) nothing to trade[.!]?$", "{0}には取引するものがない"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("\u0002have\u001F15\u001F19\u001F\u0003The ウォーターヴァイン農家 has nothing to trade.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("ウォーターヴァイン農家には取引するものがない"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_UsesTradeOwnerHandoffBeforeProducerRoute()
    {
        WriteDictionary(
            ("You don't have 50 drams of fresh water to even up the trade!", "dictionary exact fallback should not be used"),
            ("You don't have {0} to even up the trade!", "dictionary template should not be used: {0}"));

        var translated = RunShowWithPopupPatch(
            "{{y|You don't have {{C|50}} drams of fresh water to even up the trade!}}");

        Assert.That(
            translated,
            Is.EqualTo("{{y|取引を釣り合わせるための{{C|50}}ドラムの真水が足りない！}}"));
    }

    [Test]
    public void Prefix_FallsBackToProducerRoute()
    {
        WriteDictionary(("Delete save game?", "セーブデータを削除しますか？"));

        var translated = RunShowWithPopupPatch("Delete save game?");

        Assert.That(translated, Is.EqualTo("セーブデータを削除しますか？"));
    }

    [Test]
    public void Prefix_TranslatesEvilTwinFiniteDefectPopup()
    {
        WriteDictionary(("{{c|You sense a sinister presence nearby.}}", "{{c|近くに邪悪な気配を感じる。}}"));

        var translated = RunShowWithPopupPatch("{{c|You sense a sinister presence nearby.}}");

        Assert.That(translated, Is.EqualTo("{{c|近くに邪悪な気配を感じる。}}"));
    }

    [Test]
    public void Prefix_ReturnsEmptyInputUnchanged()
    {
        var translated = RunShowWithPopupPatch(string.Empty);

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Prefix_LeavesUnknownPopupShowMessageUnchanged()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            const string source = "Untranslated popup message";
            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DirectMarker_StillStripped()
    {
        WriteDictionary(("既に翻訳済み", "別訳"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("\u0001既に翻訳済み");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("既に翻訳済み"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesHistoricPopupMessagePattern()
    {
        WriteMessagePatternDictionary(("^You eat the meal\\.$", "食事をとった。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show("You eat the meal.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("食事をとった。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.ProducerText.Pattern"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesStartupJoppaIntroPattern()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        MessagePatternTranslator.SetPatternFileForTests(null);
        WriteDictionary(("10th", "第10"), ("Iyur Ut", "イユル・ウト"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        var source = "On the 10th of Iyur Ut, you arrive at the oasis-hamlet of Joppa, along the far rim of Moghra'yi, the Great Salt Desert.\n\n"
            + "All around you, moisture farmers tend to groves of viridian watervine. There are huts wrought from rock salt and brinestalk.\n\n"
            + "On the horizon, Qud's jungles strangle chrome steeples and rusted archways to the earth. Further and beyond, the fabled Spindle rises above the fray and pierces the cloud-ribboned sky.";
        var expected = "イユル・ウトの第10日、あなたは大塩砂漠モグラヤイの遥かな縁にあるオアシス集落ジョッパに到着した。\n\n"
            + "あたりではウォーターヴァインの茂みを水耕農家たちが世話している。岩塩とブラインストークで組まれた小屋が建っている。\n\n"
            + "地平線では、Qudのジャングルがクロームの尖塔と錆びたアーチを大地に絡みつかせている。さらにその彼方では、伝説のスピンドルが乱景の上にそびえ、雲の帯を貫いて空へ伸びている。";

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.ProducerText.Pattern"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupShowFailMessage()
    {
        WriteDictionary(("You can't excavate with hostiles nearby.", "敵対者が近くにいると掘削できない。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.ShowFail("You can't excavate with hostiles nearby.");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("敵対者が近くにいると掘削できない。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("The spaceship's launch sequence has already begun.", "宇宙船の発射シーケンスはすでに始まっている。")]
    [TestCase("The spaceship is already traversing the void.", "宇宙船はすでに虚空を航行している。")]
    [TestCase("The spaceship can't launch from here.", "宇宙船はここから発射できない。")]
    [TestCase("There is no starship to enter. The docking bay is empty.", "入るべき星船はない。ドッキングベイは空だ。")]
    [TestCase("The protective force of the cherubim prevents you from opening the ark.", "ケルビムの守護力があなたに方舟を開かせない。")]
    public void Prefix_TranslatesShipArkExactPopupLeaves(string source, string expected)
    {
        WriteDictionary((source, expected));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.ShowFail(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPhysicsAttackConfirmationPopupWithoutDictionaryEntry()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.ShowYesNo("Do you really want to attack the ウォーターヴァイン農家?");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("本当にウォーターヴァイン農家を攻撃しますか？"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.ProducerText.PhysicsAttackConfirm"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("The ウォーターヴァイン農家 refuses to speak to you.")]
    [TestCase("The ウォーターヴァイン農家 refuse to speak to you.")]
    public void Prefix_TranslatesConversationRefusalPopupWithoutDictionaryEntry(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.ShowFail(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("ウォーターヴァイン農家はあなたと話そうとしない。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.ProducerText.ConversationRefusal"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PhysicsAttackConfirm_LeavesNonMatchingEnglishFallback()
    {
        var source = "Do you really want to leave?";

        var translated = RunShowYesNoWithPopupPatch(source);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupShowTranslationPatch),
                    "Popup.ProducerText.PhysicsAttackConfirm"),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void Prefix_PhysicsAttackConfirm_LeavesEmptyInputUnchanged()
    {
        var translated = RunShowYesNoWithPopupPatch(string.Empty);

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Prefix_PhysicsAttackConfirm_PreservesColorTags()
    {
        var translated = RunShowYesNoWithPopupPatch("{{W|Do you really want to attack the ウォーターヴァイン農家?}}");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("{{W|本当にウォーターヴァイン農家を攻撃しますか？}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupShowTranslationPatch),
                    "Popup.ProducerText.PhysicsAttackConfirm"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Prefix_PhysicsAttackConfirm_PreservesTargetColorTagsWhenArticleIsInsideCapture()
    {
        var translated = RunShowYesNoWithPopupPatch("Do you really want to attack {{Y|the snapjaw}}?");

        Assert.That(translated, Is.EqualTo("本当に{{Y|snapjaw}}を攻撃しますか？"));
    }

    [Test]
    public void Prefix_PhysicsAttackConfirm_TranslatesGeneratedDisplayNameTarget()
    {
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("solar", "太陽光"));
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("pumping station", "ポンプステーション"),
            ("solar pumping station", "太陽光 ポンプステーション"));

        var translated = RunShowYesNoWithPopupPatch("Do you really want to attack the solar pumping station?");

        Assert.That(translated, Is.EqualTo("本当に太陽光 ポンプステーションを攻撃しますか？"));
    }

    [Test]
    public void PhysicsAttackConfirm_FallsBackToOriginalWhenTargetTranslationDictionaryIsMissing()
    {
        var source = "Do you really want to attack the solar pumping station?";
        Translator.SetDictionaryDirectoryForTests(Path.Combine(tempDirectory, "missing-dictionaries"));

        var changed = PopupTranslationPatch.TryTranslatePhysicsAttackConfirmText(
            source,
            Array.Empty<ColorSpan>(),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void Prefix_PhysicsAttackConfirm_StripsDirectMarkerWithoutRetranslating()
    {
        var source = "Do you really want to attack the ウォーターヴァイン農家?";

        var translated = RunShowYesNoWithPopupPatch(MessageFrameTranslator.MarkDirectTranslation(source));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupShowTranslationPatch),
                    "Popup.ProducerText.PhysicsAttackConfirm"),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void Prefix_ConversationRefusal_LeavesNonMatchingEnglishFallback()
    {
        var source = "The ウォーターヴァイン農家 greets you.";

        var translated = RunShowFailWithPopupPatch(source);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupShowTranslationPatch),
                    "Popup.ProducerText.ConversationRefusal"),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void Prefix_ConversationRefusal_LeavesEmptyInputUnchanged()
    {
        var translated = RunShowFailWithPopupPatch(string.Empty);

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Prefix_ConversationRefusal_PreservesColorTags()
    {
        var translated = RunShowFailWithPopupPatch("{{W|The ウォーターヴァイン農家 refuses to speak to you.}}");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("{{W|ウォーターヴァイン農家はあなたと話そうとしない。}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupShowTranslationPatch),
                    "Popup.ProducerText.ConversationRefusal"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Prefix_ConversationRefusal_PreservesTargetColorTagsWhenArticleIsInsideCapture()
    {
        var translated = RunShowFailWithPopupPatch("{{Y|The snapjaw}} refuses to speak to you.");

        Assert.That(translated, Is.EqualTo("{{Y|snapjaw}}はあなたと話そうとしない。"));
    }

    [Test]
    public void Prefix_ConversationRefusal_StripsDirectMarkerWithoutRetranslating()
    {
        var source = "The ウォーターヴァイン農家 refuses to speak to you.";

        var translated = RunShowFailWithPopupPatch(MessageFrameTranslator.MarkDirectTranslation(source));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupShowTranslationPatch),
                    "Popup.ProducerText.ConversationRefusal"),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void Prefix_DoesNotRecordMissingPattern_WhenTranslatedShowFailReentersShow()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            var prefix = new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix)));
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: prefix);
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: prefix);

            const string source = "The ウォーターヴァイン農家 refuses to speak to you.";
            const string translated = "ウォーターヴァイン農家はあなたと話そうとしない。";
            DummyPopupShow.ShowFail(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(translated));
                Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(translated), Is.EqualTo(0));
                Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(source), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupShowYesNoAsyncMessage()
    {
        WriteDictionary(("Are you sure you want to quit?", "本当に終了しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoAsync)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            _ = DummyPopupShow.ShowYesNoAsync("Are you sure you want to quit?").GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowYesNoAsyncMessage, Is.EqualTo("本当に終了しますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupShowYesNoCancelAsyncMessage()
    {
        WriteDictionary(("Would you like to save your changes?", "変更内容を保存しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNoCancelAsync)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            _ = DummyPopupShow.ShowYesNoCancelAsync("Would you like to save your changes?").GetAwaiter().GetResult();

            Assert.That(DummyPopupShow.LastShowYesNoCancelAsyncMessage, Is.EqualTo("変更内容を保存しますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PreservesTranslatedShowFailMessage_ThroughPopupMessageAndUITextSkin()
    {
        WriteDictionary(
            ("You do not have a missile weapon equipped!", "射撃武器を装備していない！"),
            ("[Esc] Cancel", "[Esc] キャンセル"));
        DummyPopupMessageTarget.Reset();

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            DummyPopupShow.ShowFail("You do not have a missile weapon equipped!");
            new DummyPopupMessageTarget().ShowPopup(DummyPopupShow.LastShowMessage!, buttons);

            var renderedMessage = DummyPopupMessageTarget.LastRenderedBodyText;
            var renderedButton = DummyPopupMessageTarget.LastButtons![0].text;
            UITextSkinTranslationPatch.Prefix(ref renderedMessage);
            UITextSkinTranslationPatch.Prefix(ref renderedButton);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("射撃武器を装備していない！"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("射撃武器を装備していない！"));
                Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
                Assert.That(renderedMessage, Is.EqualTo("{{y|射撃武器を装備していない！}}"));
                Assert.That(renderedButton, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

#if !HAS_GAME_DLL
    [Test]
    public void TargetMethods_ResolvesShowFamilyOverloads()
    {
        _ = typeof(Genkit.Location2D);
        _ = typeof(XRL.UI.DialogResult);

        var targetMethods = RequireMethod(typeof(PopupShowTranslationPatch), "TargetMethods");
        var resolved = ((IEnumerable<MethodBase>)targetMethods.Invoke(null, null)!).ToList();

        Assert.That(
            resolved.Any(method => method.DeclaringType?.FullName == "XRL.UI.Popup"
                && method.Name == nameof(DummyPopupShow.ShowFail)
                && string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))
                    == "System.String|System.Boolean|System.Boolean|System.Boolean"),
            Is.True);
        Assert.That(
            resolved.Any(method => method.DeclaringType?.FullName == "XRL.UI.Popup"
                && method.Name == nameof(DummyPopupShow.ShowYesNoAsync)
                && string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))
                    == "System.String"),
            Is.True);
        Assert.That(
            resolved.Any(method => method.DeclaringType?.FullName == "XRL.UI.Popup"
                && method.Name == nameof(DummyPopupShow.ShowYesNoCancelAsync)
                && string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))
                    == "System.String"),
            Is.True);
    }
#endif

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static string GetLocalizationRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Localization directory not found from test directory: {TestContext.CurrentContext.TestDirectory}");
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string? RunShowWithPopupPatch(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.Show(source);
            return DummyPopupShow.LastShowMessage;
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string? RunShowYesNoWithPopupPatch(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.ShowYesNo(source);
            return DummyPopupShow.LastShowYesNoMessage;
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string? RunShowFailWithPopupPatch(string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));

            DummyPopupShow.ShowFail(source);
            return DummyPopupShow.LastShowMessage;
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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

        var path = Path.Combine(tempDirectory, "popup-show.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
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

        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteMessagePatternDictionary(params (string pattern, string template)[] patterns)
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

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
