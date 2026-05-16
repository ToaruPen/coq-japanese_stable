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
