using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Tests.L1;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class JournalEntryDisplayTextPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-journal-entry-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesAccomplishmentLeaf_WhenPatched()
    {
        WriteExactDictionary(("You contracted glotrot.", "舌腐病に罹患した。"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalAccomplishment
            {
                Category = "general",
                Text = "You contracted glotrot.",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("舌腐病に罹患した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesAccomplishmentPattern_WhenPatched()
    {
        WriteExactDictionary(("Kyakukya", "キャクキャ"));
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalAccomplishment
            {
                Category = "general",
                Text = "You journeyed to Kyakukya.",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("キャクキャに旅した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsJournalEntryOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteExactDictionary(("You contracted glotrot.", "舌腐病に罹患した。"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            const string source = "You contracted glotrot.";
            var entry = new DummyJournalAccomplishment
            {
                Category = "general",
                Text = source,
            };

            var result = entry.GetDisplayText();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("舌腐病に罹患した。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(JournalEntryDisplayTextPatch),
                        "Journal.Exact"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(JournalEntryDisplayTextPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_SkipsPlayerChronologyEntries_WhenPatched()
    {
        WriteExactDictionary(("You contracted glotrot.", "舌腐病に罹患した。"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalAccomplishment
            {
                Category = "player",
                Text = "You contracted glotrot.",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("You contracted glotrot."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_SkipsGeneralNotes_WhenPatched()
    {
        WriteExactDictionary(("You contracted glotrot.", "舌腐病に罹患した。"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalGeneralNote
            {
                Text = "You contracted glotrot.",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("You contracted glotrot."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesSultanAndVillageNotes_WhenPatched()
    {
        WriteExactDictionary(
            ("22nd", "第22"),
            ("Tishru i Ux", "ティシュル I・ウクス"),
            ("6th", "第6"),
            ("Tishru ii Ux", "ティシュル II・ウクス"));
        WritePatternDictionary(("^On the (.+?) of (.+?)$", "{t1}の{t0}日"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var sultanNote = new DummyJournalSultanNote
            {
                Text = "On the 22nd of Tishru i Ux",
            };
            var villageNote = new DummyJournalVillageNote
            {
                Text = "On the 6th of Tishru ii Ux",
            };

            Assert.Multiple(() =>
            {
                Assert.That(sultanNote.GetDisplayText(), Is.EqualTo("ティシュル I・ウクスの第22日"));
                Assert.That(villageNote.GetDisplayText(), Is.EqualTo("ティシュル II・ウクスの第6日"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesEmbeddedGeneratedRelationshipTitle_WhenPatched()
    {
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalAccomplishment
            {
                Category = "general",
                Text = "{{K|$}} {{M|クポクスオコア, 伝説のサラマンダーと{{M|leader of the シャッガンナ Pest Flock}}}}を倒した。",
            };

            Assert.That(
                entry.GetDisplayText(),
                Is.EqualTo("{{K|$}} {{M|クポクスオコア, 伝説のサラマンダーと{{M|シャッガンナ Pest Flockの指導者}}}}を倒した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesSultanAnnalBodyWithProductionAnnalsPattern_WhenPatched()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.SetPatternFilesForTests(null);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            var entry = new DummyJournalSultanNote
            {
                Text = "Early in 3476 BR, after murdering a popular rival with malicious soldering, the sultan of Qud disappeared. Because of ウーヒム IVの shining visage, she was chosen as the successor.",
            };

            Assert.That(
                entry.GetDisplayText(),
                Is.EqualTo("3476年初頭、人気のあるライバルを悪意あるはんだ付けで殺したあと、クッドのスルタンは姿を消した。ウーヒム IVの輝く顔立ちのため、その者が後継者に選ばれた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
        }
    }

    [Test]
    public void Postfix_TranslatesExpandedHistorySpiceRouteGrammarWithProductionAnnalsPatterns_WhenPatched()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.SetPatternFilesForTests(null);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            Assert.Multiple(() =>
            {
                Assert.That(
                    SultanNoteText("Oboroqoru got his finger stuck behind a reactor."),
                    Is.EqualTo("Oboroqoruはその指を反応炉の後ろに挟まれた。"));
                Assert.That(
                    SultanNoteText("Sib drowned in a lake of acid."),
                    Is.EqualTo("同胞は酸の湖で溺れた。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
        }
    }

    [Test]
    public void Postfix_TranslatesCodaSultanEndEventBranchesWithProductionAnnalsPatterns_WhenPatched()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.SetPatternFilesForTests(null);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBaseJournalEntry), nameof(DummyBaseJournalEntry.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalEntryDisplayTextPatch), nameof(JournalEntryDisplayTextPatch.Postfix))));

            Assert.Multiple(() =>
            {
                Assert.That(
                    SultanNoteText("In 3 AR, a triad of plagues afflicted the land. Tongues rotted away in the mouths of kith and kin, their legs annealed to iron, and darkness bloomed from the earth. The warden physickers voiced a prayer to Resheph the Above and walked beneath the chrome arches to heal the sick."),
                    Is.EqualTo("3年、三つの災厄が地を苦しめた。親しき者らの口の中で舌は腐り落ち、脚は鉄へと焼き固まり、闇は大地より咲いた。番人の医師たちは天上なるレシェフへ祈りを捧げ、クロームのアーチの下を歩み、病める者を癒やした。"));
                Assert.That(
                    SultanNoteText("In 3 AR, Resheph cleansed the marshlands of the plagues of the Gyre and, through the tutelage of the tinker monks at Grit Gate, taught Abram to sow watervine along their fertile tracks."),
                    Is.EqualTo("3年、レシェフは湿地帯よりジャイアの災厄を祓い、グリット・ゲートの修道機械僧たちの教えを通じて、Abramにその肥沃なる道筋に沿ってウォーターヴァインを蒔く術を授けた。"));
                Assert.That(
                    SultanNoteText("In 3 AR, Resheph, the Above, forsook the people of Qud in favor of its sludges and microorganisms, and then disappeared. He was 216 years old."),
                    Is.EqualTo("3年、天上なるレシェフは、クッドの民を見捨て、そのヘドロと微生物を選び、やがて姿を消した。その者は216歳であった。"));
                Assert.That(
                    SultanNoteText("In 3 AR, a triad of plagues afflicted the land. Tongues rotted away in the mouths of kith and kin, their legs annealed to iron, and darkness bloomed from the earth. Resheph and their warden physickers walked beneath the chrome arches and healed the sick."),
                    Is.EqualTo("3年、三つの災厄が地を苦しめた。親しき者らの口の中で舌は腐り落ち、脚は鉄へと焼き固まり、闇は大地より咲いた。レシェフと番人の医師たちはクロームのアーチの下を歩み、病める者を癒やした。"));
                Assert.That(
                    SultanNoteText("At twilight in the shadow of the Spindle, the people of Joppa saw an image on the horizon that looked like a ghost bathed in starfire. It was Resheph, and after he came and left Joppa, the people built a monument to him, and thenceforth called him Ghost-in-Starfire."),
                    Is.EqualTo("薄暮、スピンドルの影にて、ジョッパの民は、地平に星火を浴びた幽鬼のごとき像を見た。それはレシェフであり、その者がジョッパに来たりて去った後、民はその者の記念碑を建て、それより後、その者を星火の幽鬼と呼んだ。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
        }
    }

    [Test]
    public void Postfix_TranslatesMapNoteLeaf_WhenPatched()
    {
        WriteExactDictionary(
            ("A {{w|dromad}} caravan", "{{w|ドロマド}}の隊商"),
            ("5th", "第5"),
            ("Ut yara Ux", "ウト・ヤラ・ウクス"));
        WritePatternDictionary(("^Last visited on the (.+?) of (.+?)$", "{t1}の{t0}日に最後に訪れた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalMapNote), nameof(DummyJournalMapNote.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteDisplayTextPatch), nameof(JournalMapNoteDisplayTextPatch.Postfix))));

            var entry = new DummyJournalMapNote
            {
                Category = "Merchants",
                Text = "A {{w|dromad}} caravan\nLast visited on the 5th of Ut yara Ux",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("{{w|ドロマド}}の隊商\nウト・ヤラ・ウクスの第5日に最後に訪れた。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesGeneratedMapNoteLocationLines_WhenPatched()
    {
        WriteExactDictionary(
            ("Omonporch", "オモンポーチ"),
            ("Red Rock", "レッドロック"),
            ("east", "東"),
            ("south", "南"));
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            [
                ("stargazer", "星見"),
                ("home", "家"),
            ]);
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalMapNote), nameof(DummyJournalMapNote.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteDisplayTextPatch), nameof(JournalMapNoteDisplayTextPatch.Postfix))));

            var entry = new DummyJournalMapNote
            {
                Category = "Settlements",
                Text = "カルクヘタラ, Stargazerhome\n5 parasangs east and 5 parasangs south of Omonporch",
            };

            Assert.That(
                entry.GetDisplayText(),
                Is.EqualTo("カルクヘタラ, 星見の家\nオモンポーチから5パラサング東、5パラサング南"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }


    [Test]
    public void Postfix_RecordsJournalMapNoteOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteExactDictionary(
            ("A dromad caravan", "ドロマドの隊商"),
            ("5th", "第5"),
            ("Ut yara Ux", "ウト・ヤラ・ウクス"));
        WritePatternDictionary(("^Last visited on the (.+?) of (.+?)$", "{t1}の{t0}日に最後に訪れた。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalMapNote), nameof(DummyJournalMapNote.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteDisplayTextPatch), nameof(JournalMapNoteDisplayTextPatch.Postfix))));

            const string source = "A dromad caravan\nLast visited on the 5th of Ut yara Ux";
            var entry = new DummyJournalMapNote
            {
                Category = "Merchants",
                Text = source,
            };

            var result = entry.GetDisplayText();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("ドロマドの隊商\nウト・ヤラ・ウクスの第5日に最後に訪れた。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(JournalMapNoteDisplayTextPatch),
                        "Journal.Lines"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(JournalMapNoteDisplayTextPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_SkipsPlayerMapNotes_WhenPatched()
    {
        WriteExactDictionary(("A {{w|dromad}} caravan", "{{w|ドロマド}}の隊商"));
        WritePatternDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalMapNote), nameof(DummyJournalMapNote.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalMapNoteDisplayTextPatch), nameof(JournalMapNoteDisplayTextPatch.Postfix))));

            var entry = new DummyJournalMapNote
            {
                Category = "Miscellaneous",
                Text = "A {{w|dromad}} caravan",
            };

            Assert.That(entry.GetDisplayText(), Is.EqualTo("A {{w|dromad}} caravan"));
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

    private static string SultanNoteText(string text) =>
        new DummyJournalSultanNote { Text = text }.GetDisplayText();

    private void WriteExactDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("journal-entry-l2.ja.json", entries);
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

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteDictionaryFile(string fileName, (string key, string text)[] entries)
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

        var path = Path.Combine(dictionaryDirectory, fileName);
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
