using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class JournalApiAddTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-journal-api-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        DummyJournalApi.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyJournalApi.Reset();
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesStoredTexts_WhenPatched()
    {
        WriteExactDictionary(("Kyakukya", "キャクキャ"));
        WritePatternDictionary(
            ("^You journeyed to (.+?)\\.$", "{t0}に旅した。"),
            ("^Notes: (.+)$", "備考: {t0}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "You journeyed to Kyakukya.",
                "Notes: Kyakukya",
                "Notes: Kyakukya",
                category: "general");

            var entry = DummyJournalApi.Accomplishments.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001キャクキャに旅した。"));
                Assert.That(entry.MuralText, Is.EqualTo("\u0001備考: キャクキャ"));
                Assert.That(entry.GospelText, Is.EqualTo("\u0001備考: キャクキャ"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesGameObjectDieDeathJournalText_WhenPatched()
    {
        WriteExactDictionary(
            ("5th", "第5"),
            ("Ut yara Ux", "ウト・ヤラ・ウクス"));
        WritePatternDictionary(("^On the (.+?) of (.+?)$", "{t1}の{t0}日"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            GameObjectDieTranslationPatch.Prefix();
            try
            {
                DummyJournalApi.AddAccomplishment(
                    "On the 5th of Ut yara Ux, 蒸発した。",
                    category: "general");
            }
            finally
            {
                _ = GameObjectDieTranslationPatch.Finalizer(null);
            }

            var entry = DummyJournalApi.Accomplishments.Single();
            Assert.That(entry.Text, Is.EqualTo("\u0001ウト・ヤラ・ウクスの第5日、蒸発した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesWakingDreamGospel_WhenPatched()
    {
        WriteExactDictionary(("You woke from a peaceful dream.", "安らかな夢から目覚めた。"));
        WritePatternDictionary(
            (
                "^<spice\\.commonPhrases\\.blessed\\.!random\\.capitalize> =name= dreamed of a thousand years of peace, and the people of Qud <spice\\.history\\.gospels\\.Celebration\\.LateSultanate\\.!random> in <spice\\.commonPhrases\\.celebration\\.!random>\\.$",
                "<spice.commonPhrases.blessed.!random.capitalize>=name=は千年の平和を夢見、クッドの民は<spice.commonPhrases.celebration.!random>で<spice.history.gospels.Celebration.LateSultanate.!random>した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "You woke from a peaceful dream.",
                gospelText: "<spice.commonPhrases.blessed.!random.capitalize> =name= dreamed of a thousand years of peace, and the people of Qud <spice.history.gospels.Celebration.LateSultanate.!random> in <spice.commonPhrases.celebration.!random>.");

            var entry = DummyJournalApi.Accomplishments.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001安らかな夢から目覚めた。"));
                Assert.That(
                    entry.GospelText,
                    Is.EqualTo("\u0001<spice.commonPhrases.blessed.!random.capitalize>=name=は千年の平和を夢見、クッドの民は<spice.commonPhrases.celebration.!random>で<spice.history.gospels.Celebration.LateSultanate.!random>した。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesAbsorbablePsycheGospel_WhenPatched()
    {
        WritePatternDictionary(
            (
                "^In the month of (.+?) of (.+?), =name= was challenged by <spice\\.commonPhrases\\.pretender\\.!random\\.article> to a duel over the rights of (.+?)\\. =name= won and had the pretender's psyche kibbled and absorbed into (.+?) own\\.$",
                "{1}年{0}、=name= は {t2}の権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name= は勝利し、偽者の精神を刻んで吸収した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "placeholder",
                gospelText: "In the month of Ut yara Ux of 1012, =name= was challenged by <spice.commonPhrases.pretender.!random.article> to a duel over the rights of the Mechanimists. =name= won and had the pretender's psyche kibbled and absorbed into their own.");

            var entry = DummyJournalApi.Accomplishments.Single();
            Assert.That(
                entry.GospelText,
                Is.EqualTo("\u00011012年Ut yara Ux、=name= は Mechanimistsの権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name= は勝利し、偽者の精神を刻んで吸収した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesGivesRepMuralAndGospelVariants_WhenPatched()
    {
        WritePatternDictionary(
            (
                "^You slew your bonded kith (.+?), violating the covenant of the water ritual and earning the emnity of all\\.$",
                "水の儀式で結ばれた同胞{t0}を殺し、その誓約を破って全ての者の敵意を買った。"),
            (
                "^Blasphemously, the traitor (.+?) attacked =name=, (?:his|her|their|its) water-sib, and =name= was forced to slay (?:him|her|them|it)\\. Deep in grief, =name= wept for one year\\.$",
                "冒涜的にも、裏切り者の{t0}は水の同胞である=name=を襲い、=name=は{t0}を殺さざるを得なかった。深い悲しみの中、=name=は一年間泣き続けた。"),
            (
                "^In the month of (.+?) of (.+?), =name= was challenged by <spice\\.commonPhrases\\.pretender\\.!random\\.article> to a duel over the rights of (.+?)\\. =name= won and murdered the pretender before tragically realizing <spice\\.pronouns\\.subject\\.!random> was (?:your|his|her|their|its) water-sib\\.$",
                "{1}年{0}、=name= は {t2}の権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name= は勝利し、偽者を殺した後、悲劇的にも<spice.pronouns.subject.!random>が水の同胞だったと気づいた。"),
            (
                "^You slew (.+?)\\.$",
                "{t0}を倒した。"),
            (
                "^In the month of (.+?) of (.+?), brave =name= slew (.+?) in single combat\\.$",
                "{1}年{0}、勇敢なる=name=は一騎打ちで{t2}を倒した。"),
            (
                "^In the month of (.+?) of (.+?), =name= was challenged by <spice\\.commonPhrases\\.pretender\\.!random\\.article> to a duel over the rights of (.+?)\\. =name= won and murdered the pretender <spice\\.elements\\.(.+?)\\.murdermethods\\.!random>\\.$",
                "{1}年{0}、=name= は {t2}の権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name= は勝利し、<spice.elements.{3}.murdermethods.!random>で偽者を殺した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "You slew your bonded kith a snapjaw scavenger, violating the covenant of the water ritual and earning the emnity of all.",
                "Blasphemously, the traitor a snapjaw scavenger attacked =name=, his water-sib, and =name= was forced to slay him. Deep in grief, =name= wept for one year.",
                "In the month of Ut yara Ux of 1012, =name= was challenged by <spice.commonPhrases.pretender.!random.article> to a duel over the rights of the Mechanimists. =name= won and murdered the pretender before tragically realizing <spice.pronouns.subject.!random> was your water-sib.",
                category: "general");

            DummyJournalApi.AddAccomplishment(
                "You slew a snapjaw scavenger.",
                "In the month of Ut yara Ux of 1012, brave =name= slew loathsome a snapjaw scavenger in single combat.",
                "In the month of Ut yara Ux of 1012, =name= was challenged by <spice.commonPhrases.pretender.!random.article> to a duel over the rights of the Mechanimists. =name= won and murdered the pretender <spice.elements.salt.murdermethods.!random>.",
                category: "general");

            Assert.Multiple(() =>
            {
                Assert.That(DummyJournalApi.Accomplishments[0].Text, Is.EqualTo("\u0001水の儀式で結ばれた同胞snapjaw scavengerを殺し、その誓約を破って全ての者の敵意を買った。"));
                Assert.That(DummyJournalApi.Accomplishments[0].MuralText, Is.EqualTo("\u0001冒涜的にも、裏切り者のsnapjaw scavengerは水の同胞である=name=を襲い、=name=はsnapjaw scavengerを殺さざるを得なかった。深い悲しみの中、=name=は一年間泣き続けた。"));
                Assert.That(DummyJournalApi.Accomplishments[0].GospelText, Is.EqualTo("\u00011012年Ut yara Ux、=name= は Mechanimistsの権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name= は勝利し、偽者を殺した後、悲劇的にも<spice.pronouns.subject.!random>が水の同胞だったと気づいた。"));
                Assert.That(DummyJournalApi.Accomplishments[1].Text, Is.EqualTo("\u0001snapjaw scavengerを倒した。"));
                Assert.That(DummyJournalApi.Accomplishments[1].MuralText, Is.EqualTo("\u00011012年Ut yara Ux、勇敢なる=name=は一騎打ちでloathsome a snapjaw scavengerを倒した。"));
                Assert.That(DummyJournalApi.Accomplishments[1].GospelText, Is.EqualTo("\u00011012年Ut yara Ux、=name= は Mechanimistsの権利を巡り<spice.commonPhrases.pretender.!random.article>に決闘を挑まれた。=name= は勝利し、<spice.elements.salt.murdermethods.!random>で偽者を殺した。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesReputationBecameLovedVariants_WhenPatched()
    {
        WriteExactDictionary(
            ("the Farmers' Guild", "農夫ギルド"),
            ("Salt Dunes", "塩砂丘"),
            ("his", "その"),
            ("him", "その者"),
            ("chromatic aura", "色彩のオーラ"));
        WritePatternDictionary(
            (
                "^You became loved among (.+?) and were treated as one of their own\\.$",
                "{t0}に愛され、その一員として扱われるようになった。"),
            (
                "^While wandering around (.+?), =name= stumbled upon a clan of (.+?) performing a secret ritual\\. Because of (.+?) (.+?), they accepted (.+?) into their fold and taught (.+?) their secrets\\.$",
                "{t0}の辺りをさまよううち、=name=は秘密の儀式を行う{t1}の一族に出くわした。{t2}{t3}ゆえ、彼らは{t4}を仲間に迎え入れ、{t5}に彼らの秘密を授けた。"),
            (
                "^Deep in the wilds of (.+?), =name= stumbled upon a clan of (.+?) performing a secret ritual\\. Because of (.+?) <spice\\.elements\\.(.+?)\\.quality\\.!random>, they accepted (.+?) into their fold and taught (.+?) their secrets\\.$",
                "{t0}の荒野深くにて、=name=は秘密の儀式を行う{t1}の一族に出くわした。{t2}<spice.elements.{3}.quality.!random>ゆえ、彼らは{t4}を仲間に迎え入れ、{t5}に彼らの秘密を授けた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "You became loved among the Farmers' Guild and were treated as one of their own.",
                "While wandering around Salt Dunes, =name= stumbled upon a clan of the Farmers' Guild performing a secret ritual. Because of his chromatic aura, they accepted him into their fold and taught him their secrets.",
                "Deep in the wilds of Salt Dunes, =name= stumbled upon a clan of the Farmers' Guild performing a secret ritual. Because of his <spice.elements.salt.quality.!random>, they accepted him into their fold and taught him their secrets.",
                category: "general");

            var entry = DummyJournalApi.Accomplishments.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001農夫ギルドに愛され、その一員として扱われるようになった。"));
                Assert.That(
                    entry.MuralText,
                    Is.EqualTo("\u0001塩砂丘の辺りをさまよううち、=name=は秘密の儀式を行う農夫ギルドの一族に出くわした。その色彩のオーラゆえ、彼らはその者を仲間に迎え入れ、その者に彼らの秘密を授けた。"));
                Assert.That(
                    entry.GospelText,
                    Is.EqualTo("\u0001塩砂丘の荒野深くにて、=name=は秘密の儀式を行う農夫ギルドの一族に出くわした。その<spice.elements.salt.quality.!random>ゆえ、彼らはその者を仲間に迎え入れ、その者に彼らの秘密を授けた。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddAccomplishment_ReputationBecameLovedEdges_WhenPatched()
    {
        WriteExactDictionary(
            ("the Farmers' Guild", "農夫ギルド"),
            ("Salt Dunes", "塩砂丘"),
            ("his", "その"),
            ("him", "その者"),
            ("chromatic aura", "色彩のオーラ"));
        WritePatternDictionary(
            (
                "^You became loved among (.+?) and were treated as one of their own\\.$",
                "{t0}に愛され、その一員として扱われるようになった。"),
            (
                "^While wandering around (.+?), =name= stumbled upon a clan of (.+?) performing a secret ritual\\. Because of (.+?) (.+?), they accepted (.+?) into their fold and taught (.+?) their secrets\\.$",
                "{t0}の辺りをさまよううち、=name=は秘密の儀式を行う{t1}の一族に出くわした。{t2}{t3}ゆえ、彼らは{t4}を仲間に迎え入れ、{t5}に彼らの秘密を授けた。"));

        WithPatchedJournalApi(() =>
        {
            DummyJournalApi.AddAccomplishment(
                "You became loved among the Unknown Guild and were treated as one of their own.",
                muralText: "While wandering around Nowhere, =name= stumbled upon a clan of the Unknown Guild performing a secret ritual. Because of his chromatic aura, they accepted him into their fold and taught him their secrets.",
                category: "general");
            DummyJournalApi.AddAccomplishment(
                string.Empty,
                muralText: "   ",
                gospelText: null,
                category: "general");
            DummyJournalApi.AddAccomplishment(
                "You became loved among {{Y|the Farmers' Guild}} and were treated as one of their own.",
                category: "general");
            DummyJournalApi.AddAccomplishment(
                MessageFrameTranslator.MarkDirectTranslation(
                    "You became loved among the Farmers' Guild and were treated as one of their own."),
                category: "general");
        });

        Assert.Multiple(() =>
        {
            Assert.That(DummyJournalApi.Accomplishments, Has.Count.EqualTo(3));
            Assert.That(
                DummyJournalApi.Accomplishments[0].Text,
                Is.EqualTo("\u0001Unknown Guildに愛され、その一員として扱われるようになった。"));
            Assert.That(
                DummyJournalApi.Accomplishments[0].MuralText,
                Is.EqualTo("\u0001Nowhereの辺りをさまよううち、=name=は秘密の儀式を行うUnknown Guildの一族に出くわした。その色彩のオーラゆえ、彼らはその者を仲間に迎え入れ、その者に彼らの秘密を授けた。"));
            Assert.That(
                DummyJournalApi.Accomplishments[1].Text,
                Is.EqualTo("\u0001{{Y|農夫ギルド}}に愛され、その一員として扱われるようになった。"));
            Assert.That(
                DummyJournalApi.Accomplishments[2].Text,
                Is.EqualTo("\u0001You became loved among the Farmers' Guild and were treated as one of their own."));
        });
    }

    [Test]
    public void AddAccomplishment_TranslatesDynamicQuestCompletionVariants_FromAssets_WhenPatched()
    {
        WriteExactDictionary(
            ("Grit Gate", "グリット・ゲート"),
            ("your", "あなたの"),
            ("shining", "輝く"),
            ("the Barathrumites", "バラサルマイト"),
            ("the glass lens", "ガラスレンズ"),
            ("Joppa", "ジョッパ"),
            ("Stopsvalinn", "ストップスヴァリン"));
        var localizationRoot = Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "You located Grit Gate.",
                "Through the use of your divinely shining eyes, =name= discovered the lost location of Grit Gate.",
                "Acting against the persecution of the Barathrumites, =name= led an army to the lost gates of Grit Gate. They liberated its citizens, who together in your honor <spice.history.gospels.Celebration.LateSultanate.!random>.",
                category: "general");
            DummyJournalApi.AddAccomplishment(
                "You recovered the glass lens.",
                "While exploring Joppa, =name= recovered the fabled artifact called the glass lens.",
                "While visiting an obscure <spice.professions.apothecary.guildhall>, =name= met with a group of <spice.professions.apothecary.plural> and commissed what came to be known as the the glass lens.",
                category: "general");
            DummyJournalApi.AddAccomplishment(
                "You prayed at the glass lens.",
                "While exploring Joppa, =name= prayed at the fabled contraption called the glass lens.",
                "While visiting an obscure <spice.professions.apothecary.guildhall>, =name= met with a group of <spice.professions.apothecary.plural> and commissed what came to be known as the the glass lens.",
                category: "general");
            DummyJournalApi.AddAccomplishment(
                "You recovered the historic relic, Stopsvalinn.",
                "<spice.commonPhrases.intrepid.!random.capitalize> =name= recovered Stopsvalinn, a historic relic once thought lost to the sands of time.",
                "In an excavation at a site of deep history near Joppa, =name= recovered Stopsvalinn, the historic relic once thought lost to the sands of time.",
                category: "general");

            Assert.Multiple(() =>
            {
                Assert.That(DummyJournalApi.Accomplishments[0].Text, Is.EqualTo("\u0001グリット・ゲートを発見した。"));
                Assert.That(DummyJournalApi.Accomplishments[0].MuralText, Is.EqualTo("\u0001あなたの神々しい輝く目を用いて、=name=は失われたグリット・ゲートの場所を発見した。"));
                Assert.That(DummyJournalApi.Accomplishments[0].GospelText, Is.EqualTo("\u0001バラサルマイトへの迫害に抗し、=name=は軍勢を率いて失われたグリット・ゲートの門へ至った。彼らはその市民を解放し、あなたの栄誉のもと<spice.history.gospels.Celebration.LateSultanate.!random>した。"));
                Assert.That(DummyJournalApi.Accomplishments[1].Text, Is.EqualTo("\u0001ガラスレンズを回収した。"));
                Assert.That(DummyJournalApi.Accomplishments[1].MuralText, Is.EqualTo("\u0001ジョッパを探索中、=name=はガラスレンズという伝説のアーティファクトを回収した。"));
                Assert.That(DummyJournalApi.Accomplishments[1].GospelText, Is.EqualTo("\u0001とある<spice.professions.apothecary.guildhall>を訪れた際、=name=は<spice.professions.apothecary.plural>の一団と会い、のちにガラスレンズとして知られるものを依頼した。"));
                Assert.That(DummyJournalApi.Accomplishments[2].Text, Is.EqualTo("\u0001ガラスレンズで祈った。"));
                Assert.That(DummyJournalApi.Accomplishments[2].MuralText, Is.EqualTo("\u0001ジョッパを探索中、=name=はガラスレンズという伝説の仕掛けで祈った。"));
                Assert.That(DummyJournalApi.Accomplishments[2].GospelText, Is.EqualTo("\u0001とある<spice.professions.apothecary.guildhall>を訪れた際、=name=は<spice.professions.apothecary.plural>の一団と会い、のちにガラスレンズとして知られるものを依頼した。"));
                Assert.That(DummyJournalApi.Accomplishments[3].Text, Is.EqualTo("\u0001歴史的遺物ストップスヴァリンを回収した。"));
                Assert.That(DummyJournalApi.Accomplishments[3].MuralText, Is.EqualTo("\u0001<spice.commonPhrases.intrepid.!random.capitalize>=name=は、かつて時の砂に失われたと思われていた歴史的遺物ストップスヴァリンを回収した。"));
                Assert.That(DummyJournalApi.Accomplishments[3].GospelText, Is.EqualTo("\u0001ジョッパ近くの深い歴史を持つ場所での発掘において、=name=はかつて時の砂に失われたと思われていた歴史的遺物ストップスヴァリンを回収した。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesOpeningStoryAndAnimatorSprayVariants_FromAssets_WhenPatched()
    {
        WriteExactDictionary(
            ("5th", "第5"),
            ("Ut yara Ux", "ウト・ヤラ・ウクス"),
            ("Joppa", "ジョッパ"),
            ("Golgotha", "ゴルゴタ"),
            ("your", "あなたの"),
            ("cerulean", "空色"),
            ("ghost", "幽鬼"),
            ("chair", "椅子"),
            ("with ivory limbs", "象牙色の四肢を持つ"));
        var localizationRoot = Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "On the 5th of Ut yara Ux, you arrived at Joppa.",
                "On the auspicious 5th of Ut yara Ux, =name= arrived in Joppa and began your prodigious odyssey through Qud.",
                "At <spice.time.partsOfDay.!random> under <spice.commonPhrases.strange.!random.article> and cerulean sky, the people of Joppa saw an image on the horizon that looked like a ghost bathed in cerulean. It was =name=, and after he came and left, the people of Joppa built a monument to =name= and thenceforth called him Ghost-in-Cerulean.",
                category: "general");
            DummyJournalApi.AddAccomplishment(
                "You imbued a chair with life. Why?",
                "While traveling in Joppa, =name= performed a sacred ritual with a chair, imbuing it with life and arranging it with ivory limbs. Many of the local denizens declared it a miracle. Some weren't so sure.",
                "While traveling in Joppa, =name= performed a sacred ritual with a chair, imbuing it with life and arranging it with ivory limbs. Many of the local denizens declared it a miracle.",
                category: "general");
            DummyJournalApi.AddAccomplishment(
                "You journeyed to Golgotha.",
                "In the month of Ut yara Ux of 1012 AR, =name= ascended the trash chutes of Golgotha, victorious and bathed in slime.",
                "One auspicious day in the jungle, =name= descended the trash chutes of Golgotha and bathed in viscous slime. From that day forth, he always kept some wet trash on your person.",
                category: "general");

            Assert.Multiple(() =>
            {
                Assert.That(DummyJournalApi.Accomplishments[0].Text, Is.EqualTo("\u0001ウト・ヤラ・ウクスの第5日、ジョッパに到着した。"));
                Assert.That(DummyJournalApi.Accomplishments[0].MuralText, Is.EqualTo("\u0001ウト・ヤラ・ウクスの第5日、=name=はジョッパに到着し、あなたのクッドを巡る驚異的な旅路を始めた。"));
                Assert.That(DummyJournalApi.Accomplishments[0].GospelText, Is.EqualTo("\u0001<spice.time.partsOfDay.!random>、<spice.commonPhrases.strange.!random.article>と空色の空の下で、ジョッパの民は地平線に空色を浴びた幽鬼のような姿を見た。それは=name=だった。その者が来て去った後、ジョッパの民は=name=の記念碑を建て、以後その者を空色の幽鬼と呼んだ。"));
                Assert.That(DummyJournalApi.Accomplishments[1].Text, Is.EqualTo("\u0001椅子に命を吹き込んだ。なぜ？"));
                Assert.That(DummyJournalApi.Accomplishments[1].MuralText, Is.EqualTo("\u0001ジョッパを旅する中で、=name=は椅子を用いて神聖な儀式を行い、それに命を吹き込み、それを象牙色の四肢を持つよう整えた。地元の多くの住民はそれを奇跡だと宣言した。疑う者もいた。"));
                Assert.That(DummyJournalApi.Accomplishments[1].GospelText, Is.EqualTo("\u0001ジョッパを旅する中で、=name=は椅子を用いて神聖な儀式を行い、それに命を吹き込み、それを象牙色の四肢を持つよう整えた。地元の多くの住民はそれを奇跡だと宣言した。"));
                Assert.That(DummyJournalApi.Accomplishments[2].Text, Is.EqualTo("\u0001ゴルゴタに旅した。"));
                Assert.That(DummyJournalApi.Accomplishments[2].MuralText, Is.EqualTo("\u00011012年ウト・ヤラ・ウクス、=name=は勝利を得て粘液を浴びながら、ゴルゴタの廃棄物シュートを登った。"));
                Assert.That(DummyJournalApi.Accomplishments[2].GospelText, Is.EqualTo("\u0001ジャングルのある吉日、=name=はゴルゴタの廃棄物シュートを下り、粘つく粘液を浴びた。その日以来、その者は常に濡れたごみをあなたの身につけていた。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesBodyAndMutationVariants_FromAssets_WhenPatched()
    {
        WriteExactDictionary(
            ("left arm", "左腕"),
            ("shining visage", "輝く顔"),
            ("Light Manipulation", "光操作"),
            ("mutation", "変異"),
            ("him", "彼"),
            ("mutants", "変異者"),
            ("around Salt Dunes", "塩砂丘の辺り"),
            ("Player's", "プレイヤーの"));
        var localizationRoot = Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "Your left arm was dismembered.",
                "While fighting a battle to protect the practice of shining visage, =name= valorously had his left arm dismembered.",
                "While fighting a battle to protect the practice of shining visage, =name= valorously had his left arm dismembered.",
                category: "general");
            DummyJournalApi.AddAccomplishment(
                "Your genome destabilized and you gained the Light Manipulation mutation.",
                "<spice.commonPhrases.oneStarryNight.!random.capitalize>, =name= manifested a latent power inside him and joined the divine ranks of mutants.",
                "While wandering around Salt Dunes, =name= stumbled upon a clan of mutants. Because of Player's <spice.elements.salt.quality.!random>, they accepted him into their fold and taught him their secrets.",
                category: "general");

            Assert.Multiple(() =>
            {
                Assert.That(DummyJournalApi.Accomplishments[0].Text, Is.EqualTo("\u0001左腕が切断された。"));
                Assert.That(DummyJournalApi.Accomplishments[0].MuralText, Is.EqualTo("\u0001輝く顔の実践を守る戦いの中で、=name=は勇敢にも左腕を切断された。"));
                Assert.That(DummyJournalApi.Accomplishments[0].GospelText, Is.EqualTo("\u0001輝く顔の実践を守る戦いの中で、=name=は勇敢にも左腕を切断された。"));
                Assert.That(DummyJournalApi.Accomplishments[1].Text, Is.EqualTo("\u0001あなたのゲノムが不安定になり、光操作の変異を得た。"));
                Assert.That(DummyJournalApi.Accomplishments[1].MuralText, Is.EqualTo("\u0001<spice.commonPhrases.oneStarryNight.!random.capitalize>、=name=は内なる潜在能力を顕現させ、変異者の神聖なる列に加わった。"));
                Assert.That(DummyJournalApi.Accomplishments[1].GospelText, Is.EqualTo("\u0001塩砂丘の辺りをさまよううち、=name=は変異者の一族に出くわした。プレイヤーの<spice.elements.salt.quality.!random>ゆえ、彼らはその者を仲間に迎え入れ、その者に彼らの秘密を授けた。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesVillageSurfaceVisitVariants_FromAssets_WhenPatched()
    {
        WriteExactDictionary(
            ("Ut yara Ux", "ウト・ヤラ・ウクス"),
            ("Kyakukya", "キャクキャ"),
            ("his", "その"));
        var localizationRoot = Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));

            DummyJournalApi.AddAccomplishment(
                "You visited the village of Kyakukya.",
                "In the month of Ut yara Ux of 1012 AR, =name= founded the village of Kyakukya to <spice.history.gospels.HumblePractice.LateSultanate.!random>.",
                "Acting against the prohibition on the practice of <spice.elements.salt.practices.!random>, =name= led an army to the gates of Kyakukya. =name= <spice.commonPhrases.liberated.!random> its citizens, and in his honor they <spice.history.gospels.Celebration.LateSultanate.!random>.",
                category: "general");

            var entry = DummyJournalApi.Accomplishments.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001キャクキャの村を訪れた。"));
                Assert.That(entry.MuralText, Is.EqualTo("\u00011012年ウト・ヤラ・ウクス、=name=は<spice.history.gospels.HumblePractice.LateSultanate.!random>ためにキャクキャの村を建てた。"));
                Assert.That(entry.GospelText, Is.EqualTo("\u0001<spice.elements.salt.practices.!random>の実践への禁令に抗し、=name=は軍勢を率いてキャクキャの門へ至った。=name=はその市民を<spice.commonPhrases.liberated.!random>し、その栄誉のもと彼らは<spice.history.gospels.Celebration.LateSultanate.!random>した。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void AddAccomplishment_TranslatesTranche42SocialActiveEffectVariants_FromAssets_WhenPatched()
    {
        WriteExactDictionary(
            ("chrome idol", "クローム偶像"),
            ("snapjaw", "スナップジョー"),
            ("clockwork beetle", "クロックワークビートル"),
            ("5th", "第5"),
            ("Iyur Ut", "イユル・ウト"),
            ("Barathrumites", "バラサルマイト"));
        var localizationRoot = Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();

        try
        {
            WithPatchedJournalApi(() =>
            {
                DummyJournalApi.AddAccomplishment(
                    "Your heart sang at the sight of a chrome idol.",
                    "The troubadour-hero =name= rode the tides of your passions and shipwrecked on the shores of a chrome idol.",
                    "<spice.elements.salt.weddingConditions.!random.capitalize>, =name= cemented your friendship with Barathrumites by marrying a snapjaw.",
                    category: "general");
                DummyJournalApi.AddAccomplishment(
                    "A snapjaw ogled you lovingly after you employed your charm.",
                    "The storied eroticism of =name= became intimately known to a snapjaw.",
                    "<spice.elements.salt.weddingConditions.!random.capitalize>, =name= cemented your friendship with Barathrumites by marrying a snapjaw.",
                    category: "general");
                DummyJournalApi.AddAccomplishment(
                    "You convinced a snapjaw to join your cause.",
                    "Few were possessed of such potent charm as =name=, who -- on the 5th of Iyur Ut -- bent the will of a snapjaw with mere words.",
                    "<spice.elements.salt.weddingConditions.!random.capitalize>, =name= cemented your friendship with Barathrumites by marrying a snapjaw.",
                    category: "general");
                DummyJournalApi.AddAccomplishment(
                    "You rebuked a clockwork beetle into submission.",
                    "Onlookers! Remember the admonishment =name= gave a clockwork beetle when it presumed to speak the sacred tongue!",
                    "<spice.elements.salt.weddingConditions.!random.capitalize>, =name= cemented your friendship with Barathrumites by marrying a snapjaw.",
                    category: "general");
            });

            Assert.Multiple(() =>
            {
                Assert.That(DummyJournalApi.Accomplishments[0].Text, Is.EqualTo("\u0001クローム偶像を見て心が歌った。"));
                Assert.That(DummyJournalApi.Accomplishments[0].MuralText, Is.EqualTo("\u0001吟遊詩人の英雄=name=は情熱の潮に乗り、クローム偶像の岸辺に漂着した。"));
                Assert.That(DummyJournalApi.Accomplishments[0].GospelText, Is.EqualTo("\u0001<spice.elements.salt.weddingConditions.!random.capitalize>、=name=はバラサルマイトとの友情を固めるためスナップジョーと結婚した。"));
                Assert.That(DummyJournalApi.Accomplishments[1].Text, Is.EqualTo("\u0001あなたの魅了術を受けてスナップジョーがうっとりとこちらを見つめた。"));
                Assert.That(DummyJournalApi.Accomplishments[1].MuralText, Is.EqualTo("\u0001=name=の名高い色香はスナップジョーに深く知られることとなった。"));
                Assert.That(DummyJournalApi.Accomplishments[1].GospelText, Is.EqualTo("\u0001<spice.elements.salt.weddingConditions.!random.capitalize>、=name=はバラサルマイトとの友情を固めるためスナップジョーと結婚した。"));
                Assert.That(DummyJournalApi.Accomplishments[2].Text, Is.EqualTo("\u0001スナップジョーを説得し仲間に加えた。"));
                Assert.That(DummyJournalApi.Accomplishments[2].MuralText, Is.EqualTo("\u0001イユル・ウトの第5日、=name=ほど強力な魅力を備えた者は稀であり、ただ言葉だけでスナップジョーの意志を曲げた。"));
                Assert.That(DummyJournalApi.Accomplishments[2].GospelText, Is.EqualTo("\u0001<spice.elements.salt.weddingConditions.!random.capitalize>、=name=はバラサルマイトとの友情を固めるためスナップジョーと結婚した。"));
                Assert.That(DummyJournalApi.Accomplishments[3].Text, Is.EqualTo("\u0001クロックワークビートルを叱責して従わせた。"));
                Assert.That(DummyJournalApi.Accomplishments[3].MuralText, Is.EqualTo("\u0001見る者よ！=name=がクロックワークビートルに与えた戒めを思い起こせ。聖なる言葉を口にしようとしたためだ！"));
                Assert.That(DummyJournalApi.Accomplishments[3].GospelText, Is.EqualTo("\u0001<spice.elements.salt.weddingConditions.!random.capitalize>、=name=はバラサルマイトとの友情を固めるためスナップジョーと結婚した。"));
            });
        }
        finally
        {
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
            JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        }
    }

    [Test]
    public void AddMapNote_TranslatesText_WhenPatched()
    {
        WritePatternDictionary(("^A \"SATED\" baetyl$", "「満足した」ベテル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddMapNote)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteAddTranslationPatch), nameof(JournalMapNoteAddTranslationPatch.Prefix))));

            DummyJournalApi.AddMapNote("Joppa.1.1.1.1.10", "A \"SATED\" baetyl", "Baetyls");

            Assert.That(
                DummyJournalApi.MapNotes.Single().Text,
                Is.EqualTo("\u0001「満足した」ベテル"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddMapNote_SkipsMiscellaneousCategory_WhenPatched()
    {
        WritePatternDictionary(("^A \"SATED\" baetyl$", "「満足した」ベテル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddMapNote)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteAddTranslationPatch), nameof(JournalMapNoteAddTranslationPatch.Prefix))));

            DummyJournalApi.AddMapNote("Joppa.1.1.1.1.10", "A \"SATED\" baetyl", "Miscellaneous");

            Assert.That(
                DummyJournalApi.MapNotes.Single().Text,
                Is.EqualTo("A \"SATED\" baetyl"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddObservation_TranslatesTextAndRevealText_WhenPatched()
    {
        WriteExactDictionary(("Kyakukya", "キャクキャ"));
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddObservation)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalObservationAddTranslationPatch), nameof(JournalObservationAddTranslationPatch.Prefix))));

            DummyJournalApi.AddObservation(
                "You journeyed to Kyakukya.",
                "obs-1",
                "general",
                additionalRevealText: "You journeyed to Kyakukya.");

            var entry = DummyJournalApi.Observations.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001キャクキャに旅した。"));
                Assert.That(entry.RevealText, Is.EqualTo("\u0001キャクキャに旅した。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddObservation_TranslatesHistoricGossip_WhenPatched()
    {
        WriteExactDictionary(("some organization", "ある組織"), ("some party", "ある一団"));
        WritePatternDictionary(("^(.+?) repeatedly beat (.+?) at dice\\.$", "{t0}は{t1}を何度も賽子で打ち負かした。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddObservation)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalObservationAddTranslationPatch), nameof(JournalObservationAddTranslationPatch.Prefix))));

            DummyJournalApi.AddObservation(
                "some organization repeatedly beat some party at dice.",
                "gossip-1",
                "general",
                additionalRevealText: "some organization repeatedly beat some party at dice.");

            var entry = DummyJournalApi.Observations.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001ある組織はある一団を何度も賽子で打ち負かした。"));
                Assert.That(entry.RevealText, Is.EqualTo("\u0001ある組織はある一団を何度も賽子で打ち負かした。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddObservation_StripsEnglishArticlesFromAlreadyLocalizedHistoricGossipCaptures_WhenPatched()
    {
        WritePatternDictionary(("^(.+?) repeatedly beat (.+?) at dice\\.$", "{t0}は{t1}を何度も賽子で打ち負かした。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddObservation)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalObservationAddTranslationPatch), nameof(JournalObservationAddTranslationPatch.Prefix))));

            DummyJournalApi.AddObservation(
                "A スナップジョーの狩人 repeatedly beat the イッサカリ族 at dice.",
                "gossip-articles-1",
                "general",
                additionalRevealText: "A スナップジョーの狩人 repeatedly beat the イッサカリ族 at dice.");

            var entry = DummyJournalApi.Observations.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001スナップジョーの狩人はイッサカリ族を何度も賽子で打ち負かした。"));
                Assert.That(entry.RevealText, Is.EqualTo("\u0001スナップジョーの狩人はイッサカリ族を何度も賽子で打ち負かした。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddObservation_TranslatesVillagersOfCaptureAndStripsObjectArticle_WhenPatched()
    {
        WriteExactDictionary(("The villagers of {0}", "{0}の村人たち"));
        WritePatternDictionary(("^(.+?) cooked (.+?) a rancid meal\\.$", "{t0}は{t1}に腐った食事を振る舞った。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddObservation)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalObservationAddTranslationPatch), nameof(JournalObservationAddTranslationPatch.Prefix))));

            DummyJournalApi.AddObservation(
                "The villagers of スモル cooked a スナップジョーの軍主 a rancid meal.",
                "gossip-villagers-1",
                "general",
                additionalRevealText: "The villagers of スモル cooked a スナップジョーの軍主 a rancid meal.");

            var entry = DummyJournalApi.Observations.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Text, Is.EqualTo("\u0001スモルの村人たちはスナップジョーの軍主に腐った食事を振る舞った。"));
                Assert.That(entry.RevealText, Is.EqualTo("\u0001スモルの村人たちはスナップジョーの軍主に腐った食事を振る舞った。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddObservation_PreservesVillagersOfCapture_WhenVillageTemplateDictionaryEntryIsMissing()
    {
        WritePatternDictionary(("^(.+?) cooked (.+?) a rancid meal\\.$", "{t0}は{t1}に腐った食事を振る舞った。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddObservation)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalObservationAddTranslationPatch), nameof(JournalObservationAddTranslationPatch.Prefix))));

            DummyJournalApi.AddObservation(
                "The villagers of スモル cooked a スナップジョーの軍主 a rancid meal.",
                "gossip-villagers-fallback",
                "general");

            var entry = DummyJournalApi.Observations.Single();
            Assert.That(
                entry.Text,
                Is.EqualTo("\u0001The villagers of スモルはスナップジョーの軍主に腐った食事を振る舞った。"));
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

    private static void WithPatchedJournalApi(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalApi), nameof(DummyJournalApi.AddAccomplishment)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Prefix))),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalAccomplishmentAddTranslationPatch), nameof(JournalAccomplishmentAddTranslationPatch.Postfix))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[],\"patterns\":[");
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
        File.WriteAllText(patternFilePath, builder.ToString(), Utf8WithoutBom);
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

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "journal-api-l2.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
