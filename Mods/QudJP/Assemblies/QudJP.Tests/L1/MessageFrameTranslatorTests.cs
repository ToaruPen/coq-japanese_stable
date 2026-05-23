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
            Assert.That(freezingRayFromHands, Is.EqualTo("変異体は凍結光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a flaming ray from its face", "!", out var flamingRay),
                Is.True);
            Assert.That(flamingRay, Is.EqualTo("変異体は火炎光線を放った！"));

            Assert.That(
                MessageFrameTranslator.TryTranslateXDidY("変異体", "emit", "a vast freezing ray from its mouth", "!", out var vastFreezingRay),
                Is.True);
            Assert.That(vastFreezingRay, Is.EqualTo("変異体は巨大な凍結光線を放った！"));

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
        "あなたはタムの傷に包帯を巻こうとしたが、包帯はすり抜けた。")]
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
        "あなたは包帯でタムの傷を止血しようとしたが、包帯はすり抜けた。")]
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
