using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class MessageFrameTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryPath = null!;
    private string exactDictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-message-frame-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryPath = Path.Combine(tempDirectory, "verbs.ja.json");
        exactDictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(exactDictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(exactDictionaryDirectory);
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(dictionaryPath);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslateXDidY_UsesTier1Verb()
    {
        WriteDictionary(
            tier1: new[] { ("block", "防いだ") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("クマ", "block", extra: null, endMark: ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("クマは防いだ。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_UsesTier2VerbExtraPair()
    {
        WriteDictionary(
            tier2: new[] { ("are", "stunned", "気絶した") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("ゴア", "are", "stunned", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("ゴアは気絶した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_UsesTier3Template()
    {
        WriteDictionary(
            tier3: new[] { ("gain", "{{rules|{0}}} XP", "{{rules|{0}}}XPを獲得した") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("あなた", "gain", "{{rules|150}} XP", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは{{rules|150}}XPを獲得した。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_UsesExactObjectPair()
    {
        WriteDictionary(
            tier2: new[] { ("stare", "at {0} menacingly", "{0}を睨みつけた") });

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ("熊", "stare", "at", "タム", "menacingly", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("熊はタムを睨みつけた。"));
        });
    }

    [Test]
    public void TryTranslateWDidXToYWithZ_UsesTier3TemplateWithTwoObjects()
    {
        WriteDictionary(
            tier3: new[] { ("strike", "{0} with {1} for {2} damage", "{1}で{0}に{2}ダメージを与えた") });

        var translated = MessageFrameTranslator.TryTranslateWDidXToYWithZ(
            "熊",
            "strike",
            directPreposition: null,
            directObject: "スナップジョー",
            indirectPreposition: "with",
            indirectObject: "青銅の短剣",
            extra: "for 5 damage",
            endMark: "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("熊は青銅の短剣でスナップジョーに5ダメージを与えた！"));
        });
    }

    [Test]
    public void TryTranslateWDidXToYWithZ_FallsBackToGenericParticleOrdering()
    {
        WriteDictionary(
            tier1: new[] { ("strike", "攻撃した") });

        var translated = MessageFrameTranslator.TryTranslateWDidXToYWithZ(
            "熊",
            "strike",
            directPreposition: null,
            directObject: "スナップジョー",
            indirectPreposition: "with",
            indirectObject: "青銅の短剣",
            extra: null,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("熊はスナップジョーを青銅の短剣で攻撃した。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_ReturnsFalseWhenVerbIsUnknown()
    {
        WriteDictionary(
            tier1: new[] { ("block", "防いだ") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("熊", "teleport", extra: null, endMark: ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(sentence, Is.Empty);
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_UsesStunnedEntry()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidY("スナップジョー", "are", "stunned", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("スナップジョーは気絶した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_UsesBeepEntry()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidY("端末", "beep", extra: null, endMark: ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("端末はビープ音を鳴らした。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier1_UsesClickEntry()
    {
        WriteDictionary(tier1: new[] { ("click", "カチッと鳴った") });

        var translated = MessageFrameTranslator.TryTranslateXDidY("端末", "click", extra: null, endMark: ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("端末はカチッと鳴った。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_UsesBloopEntry()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidY("端末", "bloop", extra: null, endMark: ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("端末は低い電子音を鳴らした。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_UsesShrinePrayerEntry()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "voice",
            "a short prayer beneath",
            "山羊人の種播きの石の像",
            extra: null,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは山羊人の種播きの石の像の下で短い祈りを唱えた。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_UsesRepairEntry()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "整備ロボット",
            "repair",
            preposition: null,
            objectText: "損傷したアーム",
            extra: null,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("整備ロボットは損傷したアームを修理した。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_UsesPetEntry()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "ウォーターヴァイン農家のメカニマス教徒改宗者",
            "pet",
            preposition: null,
            objectText: "クテシフス",
            extra: null,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("ウォーターヴァイン農家のメカニマス教徒改宗者はクテシフスを撫でた。"));
        });
    }


    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TryTouchEvadesYou()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "try",
            preposition: "to touch",
            objectText: "帯電セル",
            extra: ", but it evades you",
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは帯電セルに触れようとしたが、かわされた。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_FlinchesOutOfWayOfProjectile()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "ドリンクス",
            "flinch",
            preposition: "out of the way of",
            objectText: "木の矢",
            extra: null,
            endMark: "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("ドリンクスは木の矢をかわした！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_FlinchesAwayAsProjectilePassesFromDirection()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "flinch",
            preposition: "away as",
            objectText: "{{w|木の矢}}",
            extra: "whistles past from the east",
            endMark: "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは東から飛んできた{{w|木の矢}}をかわした！"));
        });
    }

    [TestCase(
        "巡礼者",
        "throw",
        "古代の遺物 down",
        "井戸",
        null,
        ".",
        "巡礼者は古代の遺物を井戸に投げ込んだ。")]
    [TestCase(
        "ゼラチン状の塊",
        "melt",
        "through the floor and descends with",
        "あなた",
        null,
        ".",
        "ゼラチン状の塊はあなたを飲み込んだまま床を溶かして下っていった。")]
    [TestCase(
        "あなた",
        "bump",
        "into",
        "変位装置",
        null,
        ".",
        "あなたは変位装置にぶつかった。")]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesResidualPopupFrameRoutes(
        string subject,
        string verb,
        string preposition,
        string objectText,
        string? extra,
        string endMark,
        string expected)
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            subject,
            verb,
            preposition,
            objectText,
            extra,
            endMark,
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_FlinchesAwayAsProjectileWhizzesPastFromDirection()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "flinch",
            preposition: "away as",
            objectText: "{{w|木の矢}}",
            extra: "whizzes past from the west",
            endMark: "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは西から飛んできた{{w|木の矢}}をかわした！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_ButchersCorpseIntoSingleYield()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "butcher",
            preposition: null,
            objectText: "ドーングライダーの死体",
            extra: "into a {{G|ドーングライダーの尾}}",
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはドーングライダーの死体を解体して{{G|ドーングライダーの尾}}を得た。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_ButchersCorpseIntoSetYield()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "butcher",
            preposition: null,
            objectText: "目なし蟹の死体",
            extra: "into a set of 無眼蟹の脚",
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは目なし蟹の死体を解体して無眼蟹の脚を得た。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_PressesPowerButtonOnObject()
    {
        UseRepositoryDictionary();

        var translated = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "press",
            preposition: "the power button on",
            objectText: "バネ仕掛けのナインフォールドのブーツ",
            extra: null,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはバネ仕掛けのナインフォールドのブーツの電源ボタンを押した。"));
        });
    }

    [Test]
    public void TryTranslateRepositoryDictionary_TranslatesResidualDoesMessageFrameRoutes()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "巡礼者",
                    "take",
                    preposition: "a puff on",
                    objectText: "水ギセル",
                    extra: null,
                    endMark: ".",
                    out var puff),
                Is.True);
            Assert.That(puff, Is.EqualTo("巡礼者は水ギセルを一服した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("術者", "blur", "through spacetime", ".", out var blur),
                Is.True);
            Assert.That(blur, Is.EqualTo("術者は時空をぼやけるように進んだ。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("術者", "multiply", null, ".", out var multiply),
                Is.True);
            Assert.That(multiply, Is.EqualTo("術者は増殖した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateWDidXToYWithZ(
                    "医者",
                    "use",
                    directPreposition: null,
                    directObject: "除細動器",
                    indirectPreposition: "on",
                    indirectObject: "患者",
                    extra: null,
                    endMark: ".",
                    out var defibrillate),
                Is.True);
            Assert.That(defibrillate, Is.EqualTo("医者は患者に除細動器を使った。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateWDidXToYWithZ(
                    "医者",
                    "try",
                    directPreposition: "to use",
                    directObject: "除細動器",
                    indirectPreposition: "on",
                    indirectObject: "敵",
                    extra: ", but it dodges",
                    endMark: "!",
                    out var dodge),
                Is.True);
            Assert.That(dodge, Is.EqualTo("医者は敵に除細動器を使おうとしたが、かわされた！"));
        });
    }

    // --- New Tier2 tests (Task 1: #82 DidX verb entries) ---

    [Test]
    public void TryTranslateXDidY_Tier2_BeginRunning()
    {
        WriteDictionary(tier2: new[] { ("begin", "running", "走り始めた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("クマ", "begin", "running", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("クマは走り始めた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier2_StopSprinting()
    {
        WriteDictionary(tier2: new[] { ("stop", "sprinting", "全力疾走をやめた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "stop", "sprinting", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは全力疾走をやめた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier2_ReleaseSteam()
    {
        WriteDictionary(tier2: new[] { ("release", "a cloud of steam to cool off", "蒸気の雲を放出して冷却した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("装置", "release", "a cloud of steam to cool off", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("装置は蒸気の雲を放出して冷却した！"));
        });
    }

    // --- Tier3 tests: possessive pronoun phrases (DidX, objectSlotCount=0) ---

    [Test]
    public void TryTranslateXDidY_Tier3_TightenCarapace()
    {
        WriteDictionary(tier3: new[]
        {
            ("tighten", "{0} carapace", "{0}の甲殻を締めつけた"),
            ("tighten", "(?:your|his|her|its|their) carapace", "甲殻を締めつけた")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("カニ", "tighten", "its carapace", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("カニは甲殻を締めつけた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_ActivateReflectiveShield()
    {
        WriteDictionary(tier3: new[]
        {
            ("activate", "{0} reflective shield", "{0}の反射シールドを起動した"),
            ("activate", "(?:your|his|her|its|their) reflective shield", "反射シールドを起動した")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("ロボット", "activate", "its reflective shield", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ロボットは反射シールドを起動した。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_SpinMolecularCannon()
    {
        WriteDictionary(tier3: new[]
        {
            ("spin", "up {0} molecular cannon", "{0}の分子砲を回転させた"),
            ("spin", "up (?:your|his|her|its|their) molecular cannon", "分子砲を回転させた")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("変異体", "spin", "up its molecular cannon", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("変異体は分子砲を回転させた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesRuntimeRestockedInventoryWithoutPossessivePronoun()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidY(
            "ドロマドの行商人、メカニミスト改宗者 [{{B|座っている}}]",
            "have",
            "restocked his inventory",
            "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ドロマドの行商人、メカニミスト改宗者 [{{B|座っている}}]は在庫を補充した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_BreakFreeFrom()
    {
        WriteExactDictionary(("hook", "鉤"));
        WriteDictionary(tier3: new[] { ("break", "free from {0}", "{t0}から抜け出した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("戦士", "break", "free from the hook", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士は鉤から抜け出した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_DischargeElectricalCharge()
    {
        WriteDictionary(tier3: new[] { ("discharge", "{0} units of electrical charge", "{0}ユニットの電荷を放電した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("電気ウナギ", "discharge", "500 units of electrical charge", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("電気ウナギは500ユニットの電荷を放電した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_AssumeFormOf()
    {
        WriteExactDictionary(("bear", "熊"));
        WriteDictionary(tier3: new[] { ("assume", "the form of {0}", "{t0}の姿をとった") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("変異者", "assume", "the form of a bear", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("変異者は熊の姿をとった。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_AreKnockedDirection()
    {
        WriteExactDictionary(("to the north", "北側"));
        WriteDictionary(tier3: new[] { ("are", "knocked {0}", "{t0}に吹き飛ばされた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("ゴブリン", "are", "knocked to the north", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ゴブリンは北側に吹き飛ばされた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_AreNoLonger()
    {
        WriteExactDictionary(("rooted", "根付いた"));
        WriteDictionary(tier3: new[] { ("are", "no longer {0}", "{t0}状態ではなくなった") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "are", "no longer rooted", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは根付いた状態ではなくなった。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_SwitchStance()
    {
        WriteExactDictionary(("aggressive", "攻撃的"));
        WriteDictionary(tier3: new[] { ("switch", "to {0} stance", "{t0}の構えに切り替えた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("剣士", "switch", "to aggressive stance", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("剣士は攻撃的の構えに切り替えた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_TeleportAway()
    {
        WriteExactDictionary(("bear", "熊"));
        WriteDictionary(tier3: new[] { ("teleport", "{0} away", "{t0}をテレポートさせた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("念動力者", "teleport", "the bear away", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("念動力者は熊をテレポートさせた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_StartReleasing()
    {
        WriteDictionary(tier3: new[] { ("start", "releasing {0}", "{0}を放出し始めた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("変異体", "start", "releasing {{G|poison gas}}", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("変異体は{{G|poison gas}}を放出し始めた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_FlingEverywhere()
    {
        WriteDictionary(tier3: new[] {
            ("fling", "{0} {1} everywhere", "{0}の{1}をあたりに飛ばした"),
            ("fling", "{0} {1}", "{0}の{1}を飛ばした"),
            ("fling", "(?:your|his|her|its|their) quills everywhere", "棘をあたりに飛ばした")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("ヤマアラシ", "fling", "its quills everywhere", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ヤマアラシは棘をあたりに飛ばした！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_FlingBasic()
    {
        WriteDictionary(tier3: new[] {
            ("fling", "{0} {1} everywhere", "{0}の{1}をあたりに飛ばした"),
            ("fling", "{0} {1}", "{0}の{1}を飛ばした"),
            ("fling", "(?:your|his|her|its|their) quills", "棘を飛ばした")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("ヤマアラシ", "fling", "its quills", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ヤマアラシは棘を飛ばした！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_ArePromoted()
    {
        WriteExactDictionary(("Champion", "チャンピオン"), ("Barathrumites", "バラサラム派"));
        WriteDictionary(tier3: new[] { ("are", "promoted to the {0} of {1}", "{t1}の{t0}に昇進した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "are", "promoted to the Champion of the Barathrumites", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはバラサラム派のチャンピオンに昇進した。"));
        });
    }

    // --- Tier3 tests for XDidYToZ frame (objectSlotCount=1) ---

    [Test]
    public void TryTranslateXDidYToZ_Tier3_TryBeatFlames()
    {
        WriteDictionary(
            tier2: new[] {
                ("try", "to beat at the flames on {0}, but it dodges", "{0}の炎を叩こうとしたが、かわされた")
            },
            tier3: new[] {
                ("try", "to beat at the flames on {0}, but {1} dodges", "{0}の炎を叩こうとしたが、{1}はかわした"),
                ("try", "to beat at the flames on {0}, but {1}", "{0}の炎を叩こうとしたが、{1}")
            });

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "戦士", "try", "to beat at the flames on", "ゴブリン",
            ", but it dodges", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士はゴブリンの炎を叩こうとしたが、かわされた！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_Tier3_AttemptConk()
    {
        WriteDictionary(
            tier2: new[] {
                ("attempt", "to conk {0} on the head", "{0}の頭を強打しようとした")
            },
            tier3: new[] {
                ("attempt", "to conk {0} on {1}", "{0}の{1}を強打しようとした")
            });

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "戦士", "attempt", "to conk", "クマ",
            "on the head", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士はクマの頭を強打しようとした。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_Tier3_BeatFlamesOnTarget()
    {
        WriteDictionary(
            tier2: new[] {
                ("beat", "at the flames on {0} with its fists", "{0}の炎を拳で叩いた")
            },
            tier3: new[] {
                ("beat", "at the flames on {0} with {1}", "{0}の炎を{1}で叩いた")
            });

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "戦士", "beat", "at the flames on", "クマ",
            "with its fists", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士はクマの炎を拳で叩いた！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_BeatFlamesOnTarget()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "戦士", "beat", "at the flames on", "クマ",
            "with its fists", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士はクマの炎を拳で叩いた！"));
            Assert.That(sentence, Does.Not.Contain("its"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_BeatFlamesWithPossessiveHands()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidY(
            "あなた",
            "beat",
            "at the flames with your hands",
            "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは手で炎を叩いた！"));
            Assert.That(sentence, Does.Not.Contain("your"));
            Assert.That(sentence, Does.Not.Contain("hands"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_BeatFlamesOnTargetWithPossessiveHands()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "beat",
            "at the flames on",
            "クマ",
            "with your hands",
            "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはクマの炎を手で叩いた！"));
            Assert.That(sentence, Does.Not.Contain("your"));
            Assert.That(sentence, Does.Not.Contain("hands"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TryBeatFlamesPassThroughPossessiveHands()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "try",
            "to beat at the flames on",
            "幽霊",
            ", but your hands pass through them",
            "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは幽霊の炎を叩こうとしたが、手はすり抜けた！"));
            Assert.That(sentence, Does.Not.Contain("your"));
            Assert.That(sentence, Does.Not.Contain("hands"));
            Assert.That(sentence, Does.Not.Contain("them"));
        });
    }

    // --- Tier3 test for XDidY frame (objectSlotCount=0, same as DidX) ---

    [Test]
    public void TryTranslateXDidY_Tier3_SensePsychicPresence()
    {
        WriteDictionary(
            tier2: new[] {
                ("sense", "a psychic presence foreign to this place and time", "この地と時に馴染まぬサイキックな気配を感じ取った")
            },
            tier3: new[] {
                ("sense", "{0} foreign to this place and time", "この地と時に馴染まぬ{0}を感じ取った")
            });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "sense", "a psychic presence foreign to this place and time", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはこの地と時に馴染まぬサイキックな気配を感じ取った。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_StareAtMultipleTargets()
    {
        WriteExactDictionary(("yeti", "イエティ"), ("baboon", "ヒヒ"));
        WriteDictionary(tier3: new[] {
            ("stare", "at {0} menacingly", "{t0}を睨みつけた")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("熊", "stare", "at the yeti and the baboon menacingly", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("熊はイエティとヒヒを睨みつけた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_EmitCatchAll()
    {
        WriteDictionary(tier3: new[] { ("emit", "{0}", "{0}を発射した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("砲台", "emit", "3 iron slugs", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("砲台は3 iron slugsを発射した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_ChargeBroughtUpShort()
    {
        WriteDictionary(tier3: new[] { ("charge", ", but{0} brought up short", "突撃したが、阻まれた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("戦士", "charge", ", but is brought up short", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士は突撃したが、阻まれた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_BeginBleedingFromAnotherWound()
    {
        WriteDictionary(
            tier2: new[] { ("begin", "bleeding from another wound", "別の傷から出血し始めた") },
            tier3: new[] { ("begin", "{0} from another wound", "別の傷から{0}") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "begin", "bleeding from another wound", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは別の傷から出血し始めた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_TranslatesCirculatoryLossTerms()
    {
        WriteDictionary(
            tier3: new[]
            {
                ("begin", "{0}", "{t0}が始まった"),
                ("begin", "{0} from another wound", "別の傷から{t0}が始まった"),
                ("begin", "acting like {0} {1}", "{t0} {t1}のふりをし始めた"),
                ("begin", "acting like {0} {1} from another wound", "{t0} {t1}のふりをし始めた（別の傷から）"),
                ("stop", "{0}", "{t0}が止まった"),
                ("stop", "acting like {0} {1}", "{t0} {t1}のふりをやめた"),
                ("stop", "acting like {0} {1} so much", "{t0} {t1}のふりをするのをやめた")
            });

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("光葉", "begin", "leaking", "!", out var leaking),
                Is.True);
            Assert.That(leaking, Is.EqualTo("光葉は液漏れが始まった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("粘液", "begin", "oozing from another wound", "!", out var oozing),
                Is.True);
            Assert.That(oozing, Is.EqualTo("粘液は別の傷から滲出が始まった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("幻影", "begin", "acting like chrome hoverer", null, out var holographic),
                Is.True);
            Assert.That(holographic, Is.EqualTo("幻影はchrome hovererのふりをし始めた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "幻影",
                    "begin",
                    "acting like chrome hoverer from another wound",
                    null,
                    out var holographicWound),
                Is.True);
            Assert.That(holographicWound, Is.EqualTo("幻影はchrome hovererのふりをし始めた（別の傷から）。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("機械", "stop", "leaking", ".", out var stopped),
                Is.True);
            Assert.That(stopped, Is.EqualTo("機械は液漏れが止まった。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("幻影", "stop", "acting like chrome hoverer", ".", out var stoppedActing),
                Is.True);
            Assert.That(stoppedActing, Is.EqualTo("幻影はchrome hovererのふりをやめた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "幻影",
                    "stop",
                    "acting like chrome hoverer so much",
                    ".",
                    out var stoppedActingSoMuch),
                Is.True);
            Assert.That(stoppedActingSoMuch, Is.EqualTo("幻影はchrome hovererのふりをするのをやめた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_SpitPuddleOf()
    {
        WriteDictionary(
            tier2: new[] { ("spit", "a puddle of acid", "酸の水溜まりを吐き出した") },
            tier3: new[] { ("spit", "a puddle of {0}", "{0}の水溜まりを吐き出した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("変異体", "spit", "a puddle of acid", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("変異体は酸の水溜まりを吐き出した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_VibrateCoreImprint()
    {
        WriteDictionary(tier3: new[]
        {
            ("vibrate", "as the current location is imprinted in {0} geospatial core", "現在地が{0}の地理空間コアに刻み込まれ、振動した"),
            ("vibrate", "as the current location is imprinted in (?:your|his|her|its|their) geospatial core", "現在地を地理空間コアに刻み込み、振動した")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("リコイラー", "vibrate", "as the current location is imprinted in its geospatial core", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("リコイラーは現在地を地理空間コアに刻み込み、振動した。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_PossessiveMutationMessages_DoNotLeakEnglishPronouns()
    {
        UseRepositoryDictionary();

        var cases = new[]
        {
            ("カニ", "tighten", "your carapace", ".", "カニは甲殻を締めつけた。"),
            ("カニ", "tighten", "his carapace", ".", "カニは甲殻を締めつけた。"),
            ("カニ", "tighten", "her carapace", ".", "カニは甲殻を締めつけた。"),
            ("カニ", "tighten", "its carapace", ".", "カニは甲殻を締めつけた。"),
            ("カニ", "tighten", "their carapace", ".", "カニは甲殻を締めつけた。"),
            ("ロボット", "activate", "its reflective shield", ".", "ロボットは反射シールドを起動した。"),
            ("変異体", "spin", "up its molecular cannon", "!", "変異体は分子砲を回転させた！"),
            ("ヤマアラシ", "fling", "its quills everywhere", "!", "ヤマアラシは棘をあたりに飛ばした！"),
            ("リコイラー", "vibrate", "as the current location is imprinted in its geospatial core", ".", "リコイラーは現在地を地理空間コアに刻み込み、振動した。")
        };

        foreach (var (subject, verb, extra, endMark, expected) in cases)
        {
            var ok = MessageFrameTranslator.TryTranslateXDidY(subject, verb, extra, endMark, out var sentence);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True, $"{verb} {extra}");
                Assert.That(sentence, Is.EqualTo(expected));
                Assert.That(sentence, Does.Not.Contain("your"));
                Assert.That(sentence, Does.Not.Contain("his"));
                Assert.That(sentence, Does.Not.Contain("her"));
                Assert.That(sentence, Does.Not.Contain("its"));
                Assert.That(sentence, Does.Not.Contain("their"));
            });
        }
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_BreatherConeMessages()
    {
        UseRepositoryDictionary();

        var cases = new[]
        {
            ("あなた", "fire", "あなたは火を円錐状に吐き出した！"),
            ("熊", "poison gas", "熊は毒ガスを円錐状に吐き出した！"),
            ("スナップジョー", "normality gas", "スナップジョーは正常空間ガスを円錐状に吐き出した！")
        };

        foreach (var (subject, breath, expected) in cases)
        {
            var ok = MessageFrameTranslator.TryTranslateXDidY(subject, "breath", "a cone of " + breath, "!", out var sentence);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True, breath);
                Assert.That(sentence, Is.EqualTo(expected));
            });
        }
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesMutationActionFrames()
    {
        UseRepositoryDictionary();
        WriteExactDictionary(("laser beam", "レーザービーム"));

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "emit", "a freezing ray", "!", out var freezingRay),
                Is.True);
            Assert.That(freezingRay, Is.EqualTo("あなたは凍結光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a freezing ray from its hands", "!", out var freezingRayFromHands),
                Is.True);
            Assert.That(freezingRayFromHands, Is.EqualTo("変異体は手から凍結光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a flaming ray from its face", "!", out var flamingRay),
                Is.True);
            Assert.That(flamingRay, Is.EqualTo("変異体は顔から火炎光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a flaming ray from its feet", "!", out var flamingRayFromFeet),
                Is.True);
            Assert.That(flamingRayFromFeet, Is.EqualTo("変異体は足から火炎光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a flaming ray from its forefeet", "!", out var flamingRayFromForefeet),
                Is.True);
            Assert.That(flamingRayFromForefeet, Is.EqualTo("変異体は前足から火炎光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a freezing ray from its hindfeet", "!", out var freezingRayFromHindfeet),
                Is.True);
            Assert.That(freezingRayFromHindfeet, Is.EqualTo("変異体は後足から凍結光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a freezing ray from its midfeet", "!", out var freezingRayFromMidfeet),
                Is.True);
            Assert.That(freezingRayFromMidfeet, Is.EqualTo("変異体は中足から凍結光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a vast freezing ray from its mouth", "!", out var vastFreezingRay),
                Is.True);
            Assert.That(vastFreezingRay, Is.EqualTo("変異体は口から巨大な凍結光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("磁気変異体", "emit", "a powerful magnetic pulse", "!", out var magneticPulse),
                Is.True);
            Assert.That(magneticPulse, Is.EqualTo("磁気変異体は強力な磁気パルスを放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "an electromagnetic pulse", "!", out var electromagneticPulse),
                Is.True);
            Assert.That(electromagneticPulse, Is.EqualTo("変異体は電磁パルスを放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "shoot", "a swatch of frost webs", "!", out var frostWebs),
                Is.True);
            Assert.That(frostWebs, Is.EqualTo("変異体は霜の網を一面に撃ち出した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "refract", "the laser beam", "!", out var refractDidY),
                Is.True);
            Assert.That(refractDidY, Is.EqualTo("変異体はレーザービームを屈折させた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ("変異体", "refract", null, "the laser beam", null, "!", out var refractDidYToZ),
                Is.True);
            Assert.That(refractDidYToZ, Is.EqualTo("変異体はレーザービームを屈折させた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ("ミラー", "reflect", null, "レーザー", null, "!", out var reflectDidYToZ),
                Is.True);
            Assert.That(reflectDidYToZ, Is.EqualTo("ミラーはレーザーを反射した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "invoke", "a concussive blast around you", "!", out var concussiveSelf),
                Is.True);
            Assert.That(concussiveSelf, Is.EqualTo("あなたは自分の周囲に衝撃波を呼び起こした！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "feel", "a concussive blast around them", "!", out var concussiveAround),
                Is.True);
            Assert.That(concussiveAround, Is.EqualTo("変異体は周囲に衝撃波を感じた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "feel", "a concussive blast from the west", "!", out var concussiveDirection),
                Is.True);
            Assert.That(concussiveDirection, Is.EqualTo("変異体は西からの衝撃波を感じた！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesBandageMedicationSuccessFrame()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "bandage",
            preposition: null,
            objectText: "タムの",
            extra: "wounds",
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはタムの傷に包帯を巻いた。"));
        });
    }

    [TestCase(
        "to bandage",
        "wounds, but 包帯 pass through them",
        "あなたはタムの傷に包帯を巻こうとしたが、包帯はそれらをすり抜けた。")]
    [TestCase(
        "to bandage",
        "wounds, but cannot affect them",
        "あなたはタムの傷に包帯を巻こうとしたが、影響を与えられなかった。")]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesBandageMedicationFailedPhaseFrames(
        string preposition,
        string extra,
        string expected)
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "あなた",
            "try",
            preposition,
            objectText: "タムの",
            extra,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, extra);
            Assert.That(sentence, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateWDidXToYWithZ_RepositoryDictionary_TranslatesBandageMedicationStaunchFrame()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateWDidXToYWithZ(
            "あなた",
            "staunch",
            directPreposition: null,
            directObject: "タムの",
            indirectPreposition: "wounds with",
            indirectObject: "包帯",
            extra: null,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは包帯でタムの傷を止血した。"));
        });
    }

    [TestCase(
        ", but 包帯 pass through them",
        "あなたは包帯でタムの傷を止血しようとしたが、包帯はそれらをすり抜けた。")]
    [TestCase(
        ", but cannot affect them",
        "あなたは包帯でタムの傷を止血しようとしたが、影響を与えられなかった。")]
    public void TryTranslateWDidXToYWithZ_RepositoryDictionary_TranslatesBandageMedicationFailedStaunchFrames(
        string extra,
        string expected)
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateWDidXToYWithZ(
            "あなた",
            "try",
            directPreposition: "to staunch",
            directObject: "タムの",
            indirectPreposition: "wounds with",
            indirectObject: "包帯",
            extra,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, extra);
            Assert.That(sentence, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesMultiHornsStoppedInTracksFrame()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "多重角の変異体",
            "are",
            "stopped in its tracks by",
            "壁",
            extra: null,
            endMark: "!",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("多重角の変異体は壁に進路を阻まれた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesMultiHornsShovedByChargeFrame()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidY(
            "スナップジョー",
            "are",
            "shoved by 多重角の変異体の charge!",
            ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("スナップジョーは多重角の変異体の突撃に押し飛ばされた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesMultiHornsPossessiveChargeFrame()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidY(
            "スナップジョー",
            "are",
            "shoved by your charge!",
            ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("スナップジョーはあなたの突撃に押し飛ばされた！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesClonelingProduceCloneFrame()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "クローニング獣",
            "produce",
            "a clone of",
            "スナップジョー",
            "in a flurry of {{C|flashing chrome}} and {{cloning|spurting liquid}}",
            ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("クローニング獣は{{C|煌めくクローム}}と{{cloning|噴き出す液体}}の中からスナップジョーのクローンを生み出した。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesReviewedProducerFrames()
    {
        UseRepositoryDictionary();

        var level = MessageFrameTranslator.TryTranslateXDidY("レベラー", "gain", "a level", "!", out var levelSentence);
        var teleport = MessageFrameTranslator.TryTranslateXDidY("熊", "teleport", null, ".", out var teleportSentence);
        var startGas = MessageFrameTranslator.TryTranslateXDidY(
            "変異体",
            "start",
            "releasing {{G|poison gas}}",
            ".",
            out var startGasSentence);
        var stopGas = MessageFrameTranslator.TryTranslateXDidY(
            "変異体",
            "stop",
            "releasing {{G|poison gas}}",
            ".",
            out var stopGasSentence);

        Assert.Multiple(() =>
        {
            Assert.That(level, Is.True);
            Assert.That(levelSentence, Is.EqualTo("レベラーはレベルが上がった！"));
            Assert.That(teleport, Is.True);
            Assert.That(teleportSentence, Is.EqualTo("熊はテレポートした。"));
            Assert.That(startGas, Is.True);
            Assert.That(startGasSentence, Is.EqualTo("変異体は{{G|poison gas}}を放出し始めた。"));
            Assert.That(stopGas, Is.True);
            Assert.That(stopGasSentence, Is.EqualTo("変異体は{{G|poison gas}}の放出をやめた。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesReviewedProducerObjectFrames()
    {
        UseRepositoryDictionary();

        var filch = MessageFrameTranslator.TryTranslateXDidYToZ(
            "フェレット",
            "filch",
            preposition: null,
            objectText: "銅塊",
            extra: null,
            endMark: "!",
            out var filchSentence);
        var place = MessageFrameTranslator.TryTranslateXDidYToZ(
            "技師",
            "place",
            preposition: null,
            objectText: "タレット",
            extra: null,
            endMark: ".",
            out var placeSentence);
        var passThrough = MessageFrameTranslator.TryTranslateXDidYToZ(
            "円盤",
            "pass",
            preposition: "through",
            objectText: "ドア",
            extra: null,
            endMark: "!",
            out var passThroughSentence);

        Assert.Multiple(() =>
        {
            Assert.That(filch, Is.True);
            Assert.That(filchSentence, Is.EqualTo("フェレットは銅塊をかすめ取った！"));
            Assert.That(place, Is.True);
            Assert.That(placeSentence, Is.EqualTo("技師はタレットを置いた。"));
            Assert.That(passThrough, Is.True);
            Assert.That(passThroughSentence, Is.EqualTo("円盤はドアを通り抜けた！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesResidualMutationStickyTongueFrames()
    {
        UseRepositoryDictionary();

        var failedPull = MessageFrameTranslator.TryTranslateXDidYToZ(
            "舌使い",
            "try",
            preposition: "to pull",
            objectText: "snapjaw",
            extra: "toward them, but cannot",
            endMark: "!",
            out var failedPullSentence);
        var pullToSelf = MessageFrameTranslator.TryTranslateXDidYToZ(
            "舌使い",
            "pull",
            preposition: null,
            objectText: "snapjaw",
            extra: "to them",
            endMark: "!",
            out var pullToSelfSentence);
        var pullTowardSelf = MessageFrameTranslator.TryTranslateXDidYToZ(
            "舌使い",
            "pull",
            preposition: null,
            objectText: "snapjaw",
            extra: "toward them",
            endMark: "!",
            out var pullTowardSelfSentence);

        Assert.Multiple(() =>
        {
            Assert.That(failedPull, Is.True);
            Assert.That(failedPullSentence, Is.EqualTo("舌使いはsnapjawを自分の方へ引き寄せようとしたが、できなかった！"));
            Assert.That(pullToSelf, Is.True);
            Assert.That(pullToSelfSentence, Is.EqualTo("舌使いはsnapjawを自分の近くまで引き寄せた！"));
            Assert.That(pullTowardSelf, Is.True);
            Assert.That(pullTowardSelfSentence, Is.EqualTo("舌使いはsnapjawを自分の方へ引き寄せた！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesReviewedCombatProducerObjectFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "迎撃砲",
                    "intercept",
                    preposition: null,
                    objectText: "ロケット",
                    extra: null,
                    endMark: "!",
                    out var intercept),
                Is.True);
            Assert.That(intercept, Is.EqualTo("迎撃砲はロケットを迎撃した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "迎撃砲",
                    "intercept",
                    preposition: null,
                    objectText: "ロケット",
                    extra: ", but 弾丸 passes through them",
                    endMark: "!",
                    out var interceptPassThrough),
                Is.True);
            Assert.That(interceptPassThrough, Is.EqualTo("迎撃砲はロケットを迎撃したが、弾丸は対象を通り抜けた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "迎撃砲",
                    "intercept",
                    preposition: null,
                    objectText: "ロケット",
                    extra: ", but 弾丸 fails to affect them",
                    endMark: "!",
                    out var interceptNoEffect),
                Is.True);
            Assert.That(interceptNoEffect, Is.EqualTo("迎撃砲はロケットを迎撃したが、弾丸は対象に影響を与えなかった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "大いなる虚無者",
                    "teleport",
                    preposition: null,
                    objectText: "侵入者",
                    extra: "to its lair",
                    endMark: "!",
                    out var teleportToLair),
                Is.True);
            Assert.That(teleportToLair, Is.EqualTo("大いなる虚無者は侵入者を巣穴へテレポートさせた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "巨獣",
                    "run",
                    preposition: "over",
                    objectText: "瓦礫",
                    extra: null,
                    endMark: ".",
                    out var runOver),
                Is.True);
            Assert.That(runOver, Is.EqualTo("巨獣は瓦礫を轢いた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "巨獣",
                    "are",
                    preposition: "stopped in their tracks by",
                    objectText: "壁",
                    extra: null,
                    endMark: "!",
                    out var stoppedInTracks),
                Is.True);
            Assert.That(stoppedInTracks, Is.EqualTo("巨獣は壁に進路を阻まれた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateWDidXToYWithZ(
                    "番兵",
                    "disarm",
                    directPreposition: null,
                    directObject: "侵入者",
                    indirectPreposition: "of",
                    indirectObject: "剣",
                    extra: null,
                    endMark: "!",
                    out var disarm),
                Is.True);
            Assert.That(disarm, Is.EqualTo("番兵は侵入者から剣を奪って武装解除した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "複製体",
                    "refract",
                    preposition: null,
                    objectText: "対象",
                    extra: "into two additional clones",
                    endMark: ".",
                    out var refractClone),
                Is.True);
            Assert.That(refractClone, Is.EqualTo("複製体は対象を追加のクローンへ屈折させた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "複製体",
                    "try",
                    preposition: "to refract",
                    objectText: "対象",
                    extra: "but fails to push through the normality lattice in the local region of spacetime",
                    endMark: ".",
                    out var tryRefract),
                Is.True);
            Assert.That(tryRefract, Is.EqualTo("複製体は対象を屈折させようとしたが、局所時空の正常化格子を突破できなかった。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "羽虫",
                    "resist",
                    preposition: "being blown back by",
                    objectText: "送風機",
                    extra: null,
                    endMark: ".",
                    out var resistFan),
                Is.True);
            Assert.That(resistFan, Is.EqualTo("羽虫は送風機に吹き戻されるのに抵抗した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "羽虫",
                    "are",
                    preposition: "blown forcefully back by",
                    objectText: "送風機",
                    extra: null,
                    endMark: ".",
                    out var blownBack),
                Is.True);
            Assert.That(blownBack, Is.EqualTo("羽虫は送風機に強く吹き戻された。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "羽虫",
                    "are",
                    preposition: "blown into the air by",
                    objectText: "送風機",
                    extra: null,
                    endMark: "!",
                    out var blownIntoAir),
                Is.True);
            Assert.That(blownIntoAir, Is.EqualTo("羽虫は送風機に空中へ吹き上げられた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "標的",
                    "are",
                    preposition: "dragged toward",
                    objectText: "フック",
                    extra: null,
                    endMark: ".",
                    out var draggedToward),
                Is.True);
            Assert.That(draggedToward, Is.EqualTo("標的はフックに引き寄せられた。"));

        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesReviewedCombatProducerSubjectFrames()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidY(
            "アジの巻き貝",
            "blow",
            "into the conch of the Aji",
            ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("アジの巻き貝はアジの巻き貝を吹いた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesReviewedPhysicalProducerFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "精神術師",
                    "attempt",
                    preposition: "to burrow a channel through the psychic aether and sunder",
                    objectText: "敵の",
                    extra: "mind, but the attack has no effect",
                    endMark: ".",
                    out var sunderNoEffect),
                Is.True);
            Assert.That(sunderNoEffect, Is.EqualTo("精神術師は精神のエーテルに通路を掘って敵の精神を切り裂こうとしたが、攻撃は効果がなかった。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("岩", "are", "knocked north", ".", out var knocked),
                Is.True);
            Assert.That(knocked, Is.EqualTo("岩は北側に吹き飛ばされた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "岩",
                    "collide",
                    preposition: "with",
                    objectText: "壁",
                    extra: null,
                    endMark: ".",
                    out var collide),
                Is.True);
            Assert.That(collide, Is.EqualTo("岩は壁に衝突した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "butcher",
                    preposition: null,
                    objectText: "イボイノシシの死体",
                    extra: "into 肉3個",
                    endMark: ".",
                    out var butcherMany),
                Is.True);
            Assert.That(butcherMany, Is.EqualTo("あなたはイボイノシシの死体を解体して肉3個にした。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateWDidXToYWithZ(
                    "あなた",
                    "butcher",
                    directPreposition: null,
                    directObject: "イボイノシシの死体",
                    indirectPreposition: "into",
                    indirectObject: "肉",
                    extra: null,
                    endMark: ".",
                    out var butcherOne),
                Is.True);
            Assert.That(butcherOne, Is.EqualTo("あなたはイボイノシシの死体を解体して肉にした。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "fail",
                    preposition: "to butcher anything useful from",
                    objectText: "イボイノシシの死体",
                    extra: null,
                    endMark: ".",
                    out var butcherFail),
                Is.True);
            Assert.That(butcherFail, Is.EqualTo("あなたはイボイノシシの死体から有用なものを解体できなかった。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "あなた",
                    "pluck",
                    "a coral polyp off the strut and toss it aside",
                    ".",
                    out var pluckPolyp),
                Is.True);
            Assert.That(pluckPolyp, Is.EqualTo("あなたは支柱からサンゴポリプを摘み取り、脇へ投げ捨てた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("技師", "project", "a stasis field", ".", out var stasis),
                Is.True);
            Assert.That(stasis, Is.EqualTo("技師は静止フィールドを投射した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "時間",
                    "begins",
                    preposition: "to distort around",
                    objectText: "あなた",
                    extra: null,
                    endMark: ".",
                    out var timeDistort),
                Is.True);
            Assert.That(timeDistort, Is.EqualTo("時間はあなたの周囲で歪み始めた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "are",
                    preposition: "out of phase with",
                    objectText: "小屋",
                    extra: null,
                    endMark: ".",
                    out var outOfPhase),
                Is.True);
            Assert.That(outOfPhase, Is.EqualTo("あなたは小屋と位相がずれている。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "小屋",
                    "refuse",
                    preposition: null,
                    objectText: "あなた",
                    extra: "entry",
                    endMark: ".",
                    out var refuseEntry),
                Is.True);
            Assert.That(refuseEntry, Is.EqualTo("小屋はあなたの進入を拒んだ。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "cannot",
                    preposition: "reach",
                    objectText: "小屋",
                    extra: null,
                    endMark: ".",
                    out var cannotReach),
                Is.True);
            Assert.That(cannotReach, Is.EqualTo("あなたは小屋に届かない。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "are",
                    preposition: "too large to enter",
                    objectText: "小屋",
                    extra: null,
                    endMark: ".",
                    out var tooLarge),
                Is.True);
            Assert.That(tooLarge, Is.EqualTo("あなたは大きすぎて小屋に入れない。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "are",
                    preposition: "unable to enter",
                    objectText: "小屋",
                    extra: null,
                    endMark: ".",
                    out var unableEnter),
                Is.True);
            Assert.That(unableEnter, Is.EqualTo("あなたは小屋に入れない。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "次元歪曲器",
                    "swap",
                    preposition: "positions with",
                    objectText: "標的",
                    extra: null,
                    endMark: "!",
                    out var swapPositions),
                Is.True);
            Assert.That(swapPositions, Is.EqualTo("次元歪曲器は標的と位置を入れ替えた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesEnvironmentalProducerFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "胞子嚢",
                    "cause",
                    "several plants to germinate with the force of its spores",
                    "!",
                    out var germinate),
                Is.True);
            Assert.That(germinate, Is.EqualTo("胞子嚢はsporesの力で複数の植物を芽吹かせた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "熱装置",
                    "burn",
                    preposition: "off",
                    objectText: "ガス雲",
                    extra: null,
                    endMark: ".",
                    out var burnOff),
                Is.True);
            Assert.That(burnOff, Is.EqualTo("熱装置はガス雲を焼き払った。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "異次元の狩人",
                    "emerge",
                    preposition: "from",
                    objectText: "裂け目",
                    extra: null,
                    endMark: ".",
                    out var emerge),
                Is.True);
            Assert.That(emerge, Is.EqualTo("異次元の狩人は裂け目から現れた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "鉄茸",
                    "are",
                    preposition: "impaled by",
                    objectText: "棘",
                    extra: null,
                    endMark: "!",
                    out var impaled),
                Is.True);
            Assert.That(impaled, Is.EqualTo("鉄茸は棘に串刺しにされた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "被害者",
                    "are",
                    preposition: "shamed by",
                    objectText: "鏡像",
                    extra: "reflection",
                    endMark: "!",
                    out var shamed),
                Is.True);
            Assert.That(shamed, Is.EqualTo("被害者は鏡像の反射で恥辱状態になった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "探索者",
                    "spot",
                    "a sewage eel to the north",
                    "!",
                    out var spot),
                Is.True);
            Assert.That(spot, Is.EqualTo("探索者は北側にいる下水ウナギを見つけた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "つかみ腕",
                    "grab",
                    preposition: null,
                    objectText: "あなた",
                    extra: "and holds you in place",
                    endMark: ".",
                    out var grab),
                Is.True);
            Assert.That(grab, Is.EqualTo("つかみ腕はあなたをつかんでその場に押さえつけた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "調査者",
                    "accidentally prick",
                    "itself with tonic applicator",
                    ".",
                    out var prick),
                Is.True);
            Assert.That(prick, Is.EqualTo("調査者は誤ってtonic applicatorで自分を刺した。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesEnvironmentalProducerObjectFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "作業員",
                    "place",
                    preposition: null,
                    objectText: "地雷",
                    extra: null,
                    endMark: ".",
                    out var place),
                Is.True);
            Assert.That(place, Is.EqualTo("作業員は地雷を置いた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "ロボット",
                    "consume",
                    preposition: null,
                    objectText: "残骸",
                    extra: null,
                    endMark: ".",
                    out var consume),
                Is.True);
            Assert.That(consume, Is.EqualTo("ロボットは残骸を消費した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "技師",
                    "activate",
                    preposition: null,
                    objectText: "霊素体",
                    extra: null,
                    endMark: ".",
                    out var activate),
                Is.True);
            Assert.That(activate, Is.EqualTo("技師は霊素体を起動した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("霊素体", "appear", null, ".", out var appear),
                Is.True);
            Assert.That(appear, Is.EqualTo("霊素体は現れた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "操縦者",
                    "eject",
                    preposition: "from",
                    objectText: "座席",
                    extra: null,
                    endMark: "!",
                    out var ejectFrom),
                Is.True);
            Assert.That(ejectFrom, Is.EqualTo("操縦者は座席から射出された！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "操縦者",
                    "eject",
                    preposition: "with",
                    objectText: "座席",
                    extra: null,
                    endMark: "!",
                    out var ejectWith),
                Is.True);
            Assert.That(ejectWith, Is.EqualTo("操縦者は座席ごと射出された！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "操作者",
                    "flip",
                    preposition: "the thermal polarity on the",
                    objectText: "装置",
                    extra: null,
                    endMark: ".",
                    out var flip),
                Is.True);
            Assert.That(flip, Is.EqualTo("操作者は装置の熱極性を反転させた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "標的",
                    "get",
                    preposition: "entangled in",
                    objectText: "粘液",
                    extra: null,
                    endMark: "!",
                    out var entangled),
                Is.True);
            Assert.That(entangled, Is.EqualTo("標的は粘液に絡め取られた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesUtilityProducerFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("装置", "cool", "into a block of shale", ".", out var cool),
                Is.True);
            Assert.That(cool, Is.EqualTo("装置は冷えて頁岩の塊になった。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("肉体", "revert", "to its original form", ".", out var revert),
                Is.True);
            Assert.That(revert, Is.EqualTo("肉体は元の姿に戻った。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("姿", "blink", "away from the danger", ".", out var blink),
                Is.True);
            Assert.That(blink, Is.EqualTo("姿は危険から瞬間移動した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("階段", "are", "locked, and you don't have the key", ".", out var locked),
                Is.True);
            Assert.That(locked, Is.EqualTo("階段は鍵がかかっているが、鍵を持っていない。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("何か", "prevents", "you from dominating 標的", ".", out var prevents),
                Is.True);
            Assert.That(prevents, Is.EqualTo("何か標的を支配できないよう妨げた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "術者",
                    "take",
                    preposition: "control of",
                    objectText: "標的",
                    extra: null,
                    endMark: "!",
                    out var takeControl),
                Is.True);
            Assert.That(takeControl, Is.EqualTo("術者は標的を支配した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "標的",
                    "resist",
                    preposition: null,
                    objectText: "術者",
                    extra: "domination",
                    endMark: "!",
                    out var resistDomination),
                Is.True);
            Assert.That(resistDomination, Is.EqualTo("標的は術者の支配に抵抗した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "標的",
                    "slip",
                    preposition: "away from",
                    objectText: "攻撃者",
                    extra: "long sword",
                    endMark: "!",
                    out var slip),
                Is.True);
            Assert.That(slip, Is.EqualTo("標的は攻撃者のlong swordから逃れた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "stand",
                    preposition: null,
                    objectText: "仲間",
                    extra: "up",
                    endMark: ".",
                    out var standOther),
                Is.True);
            Assert.That(standOther, Is.EqualTo("あなたは仲間を立ち上がらせた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "try",
                    preposition: "to stand",
                    objectText: "仲間",
                    extra: "up, but cannot",
                    endMark: ".",
                    out var tryStand),
                Is.True);
            Assert.That(tryStand, Is.EqualTo("あなたは仲間を立ち上がらせようとしたが、できなかった。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "参拝者",
                    "incense",
                    preposition: null,
                    objectText: "香炉",
                    extra: null,
                    endMark: ".",
                    out var incense),
                Is.True);
            Assert.That(incense, Is.EqualTo("参拝者は香炉に香を焚いた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "空気",
                    "vibrates",
                    preposition: "destructively around",
                    objectText: "標的",
                    extra: null,
                    endMark: "!",
                    out var vibrates),
                Is.True);
            Assert.That(vibrates, Is.EqualTo("空気は標的の周囲で破壊的に振動した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "巡礼者",
                    "desecrate",
                    preposition: null,
                    objectText: "祠",
                    extra: null,
                    endMark: ".",
                    out var desecrate),
                Is.True);
            Assert.That(desecrate, Is.EqualTo("巡礼者は祠を冒涜した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "機械",
                    "lock",
                    preposition: "onto",
                    objectText: "標的",
                    extra: null,
                    endMark: ".",
                    out var lockOnto),
                Is.True);
            Assert.That(lockOnto, Is.EqualTo("機械は標的を捕捉した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("複製体", "cohere", null, ".", out var cohere),
                Is.True);
            Assert.That(cohere, Is.EqualTo("複製体は凝集した。"));
        });
    }

    [Test]
    public void TryTranslateWDidXToYWithZ_RepositoryDictionary_TranslatesEnergyCellFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "can't", "remove 動力セル", "!", out var cannotRemove),
                Is.True);
            Assert.That(cannotRemove, Is.EqualTo("あなたは動力セルを取り外せない！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateWDidXToYWithZ(
                    "あなた",
                    "pop",
                    directPreposition: null,
                    directObject: "動力セル",
                    indirectPreposition: "out of",
                    indirectObject: "ライフル",
                    extra: null,
                    endMark: ".",
                    out var popOut),
                Is.True);
            Assert.That(popOut, Is.EqualTo("あなたは動力セルをライフルから取り外した。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesSapOnPenetrationStatDrainFrames()
    {
        UseRepositoryDictionary();
        Translator.SetDictionaryDirectoryForTests(
            Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries"));

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "爪",
                    "permanently drain",
                    null,
                    "あなた",
                    "Strength by 2 points",
                    "!",
                    out var strength),
                Is.True);
            Assert.That(strength, Is.EqualTo("爪はあなたの筋力を2ポイント永久に吸い取った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "爪",
                    "permanently drain",
                    null,
                    "snapjaw",
                    "Hitpoints by 1 point",
                    "!",
                    out var hitpoints),
                Is.True);
            Assert.That(hitpoints, Is.EqualTo("爪はsnapjawのヒットポイントを1ポイント永久に吸い取った！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesResidualPureMessageFrameFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("時間", "begin", "to distort around snapjaw", ".", out var timeDilation),
                Is.True);
            Assert.That(timeDilation, Is.EqualTo("時間はsnapjawの周囲で歪み始めた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("操縦者", "start", "dashing in a plume of flame and smoke", "!", out var skybear),
                Is.True);
            Assert.That(skybear, Is.EqualTo("操縦者は炎と煙を噴き上げて疾走し始めた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("黒オパールの座", "shear", "the fiber of spacetime and burrow to another place", "!", out var blackOpal),
                Is.True);
            Assert.That(blackOpal, Is.EqualTo("黒オパールの座は時空の繊維を切り裂いて別の場所へ潜った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "白オパールの座",
                    "start",
                    "streaming ribbons of {{glittering|glitter}}",
                    "!",
                    out var whiteOpal),
                Is.True);
            Assert.That(whiteOpal, Is.EqualTo("白オパールの座は{{glittering|きらめく}}帯を流し始めた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("snapjaw", "resist", "the effects of your venom", "!", out var venom),
                Is.True);
            Assert.That(venom, Is.EqualTo("snapjawは毒の効果に抵抗した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("襲撃者", "impale", "itself on your psychic barbs", ".", out var barbs),
                Is.True);
            Assert.That(barbs, Is.EqualTo("襲撃者は精神の棘に突き刺さった。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "棘罠",
                    "prick",
                    null,
                    "あなた",
                    "with its neuronal thorns",
                    ".",
                    out var thorns),
                Is.True);
            Assert.That(thorns, Is.EqualTo("棘罠は神経棘であなたを刺した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "放射器",
                    "hand",
                    null,
                    "捕縛者",
                    "off",
                    ".",
                    out var handOff),
                Is.True);
            Assert.That(handOff, Is.EqualTo("放射器は捕縛者を引き渡した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "レーザー標的",
                    "take",
                    "27 damage from",
                    "プリズムビーム",
                    null,
                    ".",
                    out var damage),
                Is.True);
            Assert.That(damage, Is.EqualTo("レーザー標的はプリズムビームから27ダメージを受けた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "敵",
                    "decide",
                    "it isn't angry at",
                    "あなた",
                    null,
                    ".",
                    out var feeling),
                Is.True);
            Assert.That(feeling, Is.EqualTo("敵はあなたへの怒りを収めた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesTranche37ActiveEffectFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "stand", "up", ".", out var standUp),
                Is.True);
            Assert.That(standUp, Is.EqualTo("あなたは立ち上がった。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "enter", "a berserk fury", "!", out var berserk),
                Is.True);
            Assert.That(berserk, Is.EqualTo("あなたは狂暴な激怒状態に入った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "shake", "the spores off", ".", out var spores),
                Is.True);
            Assert.That(spores, Is.EqualTo("あなたは胞子を振り払った。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("snapjaw", "go", "into cardiac arrest", "!", out var cardiacArrest),
                Is.True);
            Assert.That(cardiacArrest, Is.EqualTo("snapjawは心停止に陥った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "snapjaw",
                    "go",
                    "into another cardiac arrest",
                    "!",
                    out var anotherCardiacArrest),
                Is.True);
            Assert.That(anotherCardiacArrest, Is.EqualTo("snapjawは再び心停止に陥った！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesTranche38ActiveEffectFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "stop", "sprinting", ".", out var sprinting),
                Is.True);
            Assert.That(sprinting, Is.EqualTo("あなたは全力疾走をやめた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "stop", "power skating", ".", out var powerSkating),
                Is.True);
            Assert.That(powerSkating, Is.EqualTo("あなたはパワースケートをやめた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("グローミング", "reappear", extra: null, endMark: ".", out var reappear),
                Is.True);
            Assert.That(reappear, Is.EqualTo("グローミングは再び現れた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "flush",
                    "with understanding of",
                    "奇妙なアーティファクト",
                    extra: null,
                    endMark: ".",
                    out var understanding),
                Is.True);
            Assert.That(understanding, Is.EqualTo("あなたは奇妙なアーティファクトへの理解で満たされた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesTranche39ActiveEffectFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "ライフドレイン使い",
                    "bond",
                    "with",
                    "スナップジョー",
                    extra: null,
                    endMark: ".",
                    out var bond),
                Is.True);
            Assert.That(bond, Is.EqualTo("ライフドレイン使いはスナップジョーと絆を結んだ。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "ライフドレイン使い",
                    "begin",
                    "to drain life essence from",
                    "スナップジョー",
                    extra: null,
                    endMark: "!",
                    out var drain),
                Is.True);
            Assert.That(drain, Is.EqualTo("ライフドレイン使いはスナップジョーから生命力を吸い取り始めた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "release",
                    preposition: null,
                    objectText: "snapjaw",
                    extra: "from your life drain",
                    endMark: ".",
                    out var releaseYour),
                Is.True);
            Assert.That(releaseYour, Is.EqualTo("あなたはsnapjawをあなたの生命吸収から解放した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "snapjaw",
                    "release",
                    preposition: null,
                    objectText: "あなた",
                    extra: "from its life drain",
                    endMark: ".",
                    out var releaseIts),
                Is.True);
            Assert.That(releaseIts, Is.EqualTo("snapjawはあなたをその生命吸収から解放した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "ライフドレイン使い",
                    "release",
                    preposition: null,
                    objectText: "スナップジョー",
                    extra: "from their life drain",
                    endMark: ".",
                    out var releaseTheir),
                Is.True);
            Assert.That(releaseTheir, Is.EqualTo("ライフドレイン使いはスナップジョーをそれらの生命吸収から解放した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "begin", "bleeding", "!", out var bleeding),
                Is.True);
            Assert.That(bleeding, Is.EqualTo("あなたは出血が始まった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "あなた",
                    "begin",
                    "bleeding from another wound",
                    "!",
                    out var anotherBleeding),
                Is.True);
            Assert.That(anotherBleeding, Is.EqualTo("あなたは別の傷から出血し始めた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("光葉", "begin", "leaking", "!", out var leaking),
                Is.True);
            Assert.That(leaking, Is.EqualTo("光葉は液漏れが始まった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "粘液",
                    "begin",
                    "oozing from another wound",
                    "!",
                    out var oozing),
                Is.True);
            Assert.That(oozing, Is.EqualTo("粘液は別の傷から滲出が始まった！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesTranche40BeguiledLoseInterestFrame()
    {
        UseRepositoryDictionary();

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "snapjaw",
            "lose",
            "interest in",
            "あなた",
            extra: null,
            endMark: ".",
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("snapjawはあなたへの関心を失った。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesTranche41ActiveEffectFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("snapjaw", "are", "immobilized", "!", out var immobilized),
                Is.True);
            Assert.That(immobilized, Is.EqualTo("snapjawは動けなくなった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("snapjaw", "are", "stuck", "!", out var stuck),
                Is.True);
            Assert.That(stuck, Is.EqualTo("snapjawは動けなくなった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "snapjaw",
                    "are",
                    "stuck in adhesive foam",
                    "!",
                    out var stuckIn),
                Is.True);
            Assert.That(stuckIn, Is.EqualTo("snapjawはadhesive foamにはまって動けなくなった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "snapjaw",
                    "are",
                    "grabbed by chrome pyramid",
                    "!",
                    out var grabbed),
                Is.True);
            Assert.That(grabbed, Is.EqualTo("snapjawはchrome pyramidにつかまれた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "snapjaw",
                    "break",
                    "free from your steel battle axe",
                    "!",
                    out var breakFreeWeapon),
                Is.True);
            Assert.That(breakFreeWeapon, Is.EqualTo("snapjawはあなたのsteel battle axeから抜け出した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY(
                    "snapjaw",
                    "break",
                    "free from being latched onto",
                    "!",
                    out var breakFreeFallback),
                Is.True);
            Assert.That(breakFreeFallback, Is.EqualTo("snapjawはbeing latched ontoから抜け出した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("snapjaw", "look", "less stricken", ".", out var lessStricken),
                Is.True);
            Assert.That(lessStricken, Is.EqualTo("snapjawは苦痛がやわらいだ。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_RepositoryDictionary_TranslatesTranche42SocialActiveEffectFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "snapjaw",
                    "fall",
                    "in love with",
                    "chrome idol",
                    extra: null,
                    endMark: "!",
                    out var lovesick),
                Is.True);
            Assert.That(lovesick, Is.EqualTo("snapjawはchrome idolに恋をした！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "snapjaw",
                    "ogle",
                    preposition: null,
                    objectText: "you",
                    extra: "lovingly",
                    endMark: ".",
                    out var beguiled),
                Is.True);
            Assert.That(beguiled, Is.EqualTo("snapjawはあなたをうっとりと見つめた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "you",
                    "convince",
                    preposition: null,
                    objectText: "snapjaw",
                    extra: "to join you",
                    endMark: "!",
                    out var proselytized),
                Is.True);
            Assert.That(proselytized, Is.EqualTo("youはsnapjawを説得して仲間に加えた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "you",
                    "rebuke",
                    preposition: null,
                    objectText: "clockwork beetle",
                    extra: "into submission",
                    endMark: ".",
                    out var rebuked),
                Is.True);
            Assert.That(rebuked, Is.EqualTo("youはclockwork beetleを叱責して従わせた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_TakeAdvantageRefresh()
    {
        WriteDictionary(tier3: new[]
        {
            ("take", "advantage of (?:your|his|her|its|their) opponent's reaction to (?:your|his|her|its|their) attack! (.+?) (?:is|are) refreshed", "相手の反応の隙を突き、{0}が再使用可能になった")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY(
            "あなた",
            "take",
            "advantage of your opponent's reaction to your attack! Long Blades and Short Blades are refreshed",
            null,
            out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは相手の反応の隙を突き、Long Blades and Short Bladesが再使用可能になった。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_RawRegex()
    {
        WriteDictionary(tier3: new[] { ("kick", "(.+?) (?:backward|backwards)", "{0}を後ろに蹴った") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("熊", "kick", "スナップジョー backward", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("熊はスナップジョーを後ろに蹴った。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_TranslatedPlaceholder()
    {
        WriteExactDictionary(("water", "水"));
        WriteDictionary(tier3: new[] { ("have", "no room for more (.+?)", "{subject}にはこれ以上の{t0}を入れる余地がない") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("水筒", "have", "no room for more water", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("水筒にはこれ以上の水を入れる余地がない"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_PrefersSpecificHarvestPattern()
    {
        WriteDictionary(
            tier3: new[]
            {
                ("harvest", "(?:a |an )?(.+?)", "{subject}は{0}を収穫した"),
                ("harvest", "(?:a |an )?(.+?) from (?:the |a |an )?(.+?)", "{subject}は{1}から{0}を収穫した")
            });

        var ok = MessageFrameTranslator.TryTranslateXDidY("熊", "harvest", "a ウィッチウッド from the 木", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("熊は木からウィッチウッドを収穫した"));
        });
    }

    [Test]
    public void TryTranslateXDidY_RepositoryDictionary_TranslatesIssue747SkillFrames()
    {
        UseRepositoryDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "dive",
                    "at",
                    "スナップジョー",
                    null,
                    "!",
                    out var deathFromAboveDive),
                Is.True);
            Assert.That(deathFromAboveDive, Is.EqualTo("あなたはスナップジョーへ急降下した！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "leap",
                    "at",
                    "スナップジョー",
                    null,
                    "!",
                    out var deathFromAboveLeap),
                Is.True);
            Assert.That(deathFromAboveLeap, Is.EqualTo("あなたはスナップジョーへ跳びかかった！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "juke",
                    "north, moving",
                    "スナップジョー",
                    "out of your way",
                    null,
                    out var jukeTarget),
                Is.True);
            Assert.That(jukeTarget, Is.EqualTo("あなたはスナップジョーの進路から北へ飛び退いた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateWDidXToYWithZ(
                    "あなた",
                    "hook",
                    null,
                    "スナップジョー",
                    "with",
                    "鋼の斧",
                    null,
                    "!",
                    out var hookAndDrag),
                Is.True);
            Assert.That(hookAndDrag, Is.EqualTo("あなたは鋼の斧でスナップジョーを引っ掛けた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "make", "camp", ".", out var makeCamp),
                Is.True);
            Assert.That(makeCamp, Is.EqualTo("あなたは野営した。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "attempt",
                    "to hobble",
                    "スナップジョー",
                    null,
                    ".",
                    out var hobble),
                Is.True);
            Assert.That(hobble, Is.EqualTo("あなたはスナップジョーの足を狙おうとした。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "shame",
                    null,
                    "スナップジョー",
                    "with your words",
                    ".",
                    out var berate),
                Is.True);
            Assert.That(berate, Is.EqualTo("あなたはスナップジョーを言葉で辱めた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("あなた", "work", "yourself into a blood frenzy", "!", out var berserk),
                Is.True);
            Assert.That(berserk, Is.EqualTo("あなたは血の狂乱へ身を駆り立てた！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("snapjaw", "reel", "from the force of your bludgeoning", ".", out var bludgeon),
                Is.True);
            Assert.That(bludgeon, Is.EqualTo("snapjawは殴打の衝撃でよろめいた。"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidYToZ(
                    "あなた",
                    "accidentally",
                    "destroy",
                    "修理対象",
                    null,
                    "!",
                    out var repairCriticalFailure),
                Is.True);
            Assert.That(repairCriticalFailure, Is.EqualTo("あなたは修理対象をうっかり破壊した！"));
        });
    }

    [Test]
    public void MarkerHelpers_AddAndStripDirectTranslationMarker()
    {
        var marked = MessageFrameTranslator.MarkDirectTranslation("熊は防いだ。");

        var stripped = MessageFrameTranslator.TryStripDirectTranslationMarker(marked, out var unmarked);

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.EqualTo("\u0001熊は防いだ。"));
            Assert.That(stripped, Is.True);
            Assert.That(unmarked, Is.EqualTo("熊は防いだ。"));
        });
    }

    private void WriteDictionary(
        IEnumerable<(string verb, string text)>? tier1 = null,
        IEnumerable<(string verb, string extra, string text)>? tier2 = null,
        IEnumerable<(string verb, string extra, string text)>? tier3 = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"entries\": [],");
        builder.AppendLine("  \"tier1\": [");
        WriteTier1(builder, tier1);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier2\": [");
        WriteTier2(builder, tier2);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier3\": [");
        WriteTier2(builder, tier3);
        builder.AppendLine("  ]");
        builder.AppendLine("}");

        File.WriteAllText(dictionaryPath, builder.ToString(), Utf8WithoutBom);
    }

    private static void UseRepositoryDictionary()
    {
        var repositoryDictionaryPath = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "MessageFrames",
                "verbs.ja.json"));

        MessageFrameTranslator.SetDictionaryPathForTests(repositoryDictionaryPath);
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

            builder.Append("{\"key\":\"")
                .Append(EscapeJson(entries[index].key))
                .Append("\",\"text\":\"")
                .Append(EscapeJson(entries[index].text))
                .Append("\"}");
        }

        builder.AppendLine("]}");
        File.WriteAllText(Path.Combine(exactDictionaryDirectory, "ui-test.ja.json"), builder.ToString(), Utf8WithoutBom);
    }

    private static void WriteTier1(StringBuilder builder, IEnumerable<(string verb, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
    }

    private static void WriteTier2(StringBuilder builder, IEnumerable<(string verb, string extra, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"extra\": \"")
                .Append(EscapeJson(entry.extra))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
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
