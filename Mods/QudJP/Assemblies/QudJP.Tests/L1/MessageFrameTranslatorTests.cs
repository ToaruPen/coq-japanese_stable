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

    // --- New Tier3 tests: possessive pronoun capture (DidX, objectSlotCount=0) ---

    [Test]
    public void TryTranslateXDidY_Tier3_TightenCarapace()
    {
        WriteDictionary(tier3: new[] { ("tighten", "{0} carapace", "{0}の甲殻を締めつけた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("カニ", "tighten", "its carapace", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("カニはitsの甲殻を締めつけた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_ActivateReflectiveShield()
    {
        WriteDictionary(tier3: new[] { ("activate", "{0} reflective shield", "{0}の反射シールドを起動した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("ロボット", "activate", "its reflective shield", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ロボットはitsの反射シールドを起動した。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_SpinMolecularCannon()
    {
        WriteDictionary(tier3: new[] { ("spin", "up {0} molecular cannon", "{0}の分子砲を回転させた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("変異体", "spin", "up its molecular cannon", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("変異体はitsの分子砲を回転させた！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_BreakFreeFrom()
    {
        WriteDictionary(tier3: new[] { ("break", "free from {0}", "{0}から抜け出した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("戦士", "break", "free from the hook", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士はthe hookから抜け出した！"));
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
        WriteDictionary(tier3: new[] { ("assume", "the form of {0}", "{0}の姿をとった") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("変異者", "assume", "the form of a bear", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("変異者はa bearの姿をとった。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_AreKnockedDirection()
    {
        WriteDictionary(tier3: new[] { ("are", "knocked {0}", "{0}に吹き飛ばされた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("ゴブリン", "are", "knocked to the north", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ゴブリンはto the northに吹き飛ばされた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_AreNoLonger()
    {
        WriteDictionary(tier3: new[] { ("are", "no longer {0}", "{0}状態ではなくなった") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "are", "no longer rooted", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはrooted状態ではなくなった。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_SwitchStance()
    {
        WriteDictionary(tier3: new[] { ("switch", "to {0} stance", "{0}の構えに切り替えた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("剣士", "switch", "to aggressive stance", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("剣士はaggressiveの構えに切り替えた。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_TeleportAway()
    {
        WriteDictionary(tier3: new[] { ("teleport", "{0} away", "{0}をテレポートさせた") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("念動力者", "teleport", "the bear away", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("念動力者はthe bearをテレポートさせた。"));
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
            ("fling", "{0} {1}", "{0}の{1}を飛ばした")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("ヤマアラシ", "fling", "its quills everywhere", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ヤマアラシはitsのquillsをあたりに飛ばした！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_FlingBasic()
    {
        WriteDictionary(tier3: new[] {
            ("fling", "{0} {1} everywhere", "{0}の{1}をあたりに飛ばした"),
            ("fling", "{0} {1}", "{0}の{1}を飛ばした")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("ヤマアラシ", "fling", "its quills", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("ヤマアラシはitsのquillsを飛ばした！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_ArePromoted()
    {
        WriteDictionary(tier3: new[] { ("are", "promoted to the {0} of {1}", "{1}の{0}に昇進した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "are", "promoted to the Champion of the Barathrumites", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはthe BarathrumitesのChampionに昇進した。"));
        });
    }

    // --- Tier3 tests for XDidYToZ frame (objectSlotCount=1) ---

    [Test]
    public void TryTranslateXDidYToZ_Tier3_TryBeatFlames()
    {
        WriteDictionary(tier3: new[] {
            ("try", "to beat at the flames on {0}, but {1} dodges", "{0}の炎を叩こうとしたが、{1}はかわした"),
            ("try", "to beat at the flames on {0}, but {1}", "{0}の炎を叩こうとしたが、{1}")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "戦士", "try", "to beat at the flames on", "ゴブリン",
            ", but it dodges", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士はゴブリンの炎を叩こうとしたが、itはかわした！"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_Tier3_AttemptConk()
    {
        WriteDictionary(tier3: new[] {
            ("attempt", "to conk {0} on {1}", "{0}の{1}を強打しようとした")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "戦士", "attempt", "to conk", "クマ",
            "on the head", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士はクマのthe headを強打しようとした。"));
        });
    }

    [Test]
    public void TryTranslateXDidYToZ_Tier3_BeatFlamesOnTarget()
    {
        WriteDictionary(tier3: new[] {
            ("beat", "at the flames on {0} with {1}", "{0}の炎を{1}で叩いた")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidYToZ(
            "戦士", "beat", "at the flames on", "クマ",
            "with its fists", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("戦士はクマの炎をits fistsで叩いた！"));
        });
    }

    // --- Tier3 test for XDidY frame (objectSlotCount=0, same as DidX) ---

    [Test]
    public void TryTranslateXDidY_Tier3_SensePsychicPresence()
    {
        WriteDictionary(tier3: new[] {
            ("sense", "{0} foreign to this place and time", "この地と時に馴染まぬ{0}を感じ取った")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "sense", "a psychic presence foreign to this place and time", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたはこの地と時に馴染まぬa psychic presenceを感じ取った。"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_StareAtMultipleTargets()
    {
        WriteDictionary(tier3: new[] {
            ("stare", "at {0} menacingly", "{0}を睨みつけた")
        });

        var ok = MessageFrameTranslator.TryTranslateXDidY("熊", "stare", "at the yeti and the baboon menacingly", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("熊はthe yeti and the baboonを睨みつけた。"));
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
        WriteDictionary(tier3: new[] { ("begin", "{0} from another wound", "別の傷から{0}") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("あなた", "begin", "bleeding from another wound", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("あなたは別の傷からbleeding！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_SpitPuddleOf()
    {
        WriteDictionary(tier3: new[] { ("spit", "a puddle of {0}", "{0}の水溜まりを吐き出した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("変異体", "spit", "a puddle of acid", "!", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("変異体はacidの水溜まりを吐き出した！"));
        });
    }

    [Test]
    public void TryTranslateXDidY_Tier3_VibrateCoreImprint()
    {
        WriteDictionary(tier3: new[] { ("vibrate", "as the current location is imprinted in {0} geospatial core", "現在地が{0}の地理空間コアに刻み込まれ、振動した") });

        var ok = MessageFrameTranslator.TryTranslateXDidY("リコイラー", "vibrate", "as the current location is imprinted in its geospatial core", ".", out var sentence);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(sentence, Is.EqualTo("リコイラーは現在地がitsの地理空間コアに刻み込まれ、振動した。"));
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
