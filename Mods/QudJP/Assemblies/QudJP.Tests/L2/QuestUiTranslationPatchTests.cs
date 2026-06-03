using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QuestUiTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-quests-ui-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyQuestsLineTarget.ResetStaticMenuOptions();
        DummyQuestLogTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyQuestsLineTarget.ResetStaticMenuOptions();
        DummyQuestLogTarget.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void QuestsLinePostfix_TranslatesNoQuestFallbackAndMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("You have no active quests.", "進行中のクエストがない。"),
            ("Expand", "展開"),
            ("Collapse", "折りたたむ"),
            ("<unknown>", "<不明>"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestsLineTarget), nameof(DummyQuestsLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestsLineTranslationPatch), nameof(QuestsLineTranslationPatch.Postfix))));

            var emptyTarget = new DummyQuestsLineTarget();
            emptyTarget.setData(new DummyQuestsLineDataTarget { quest = null });

            var questTarget = new DummyQuestsLineTarget();
            questTarget.setData(
                new DummyQuestsLineDataTarget
                {
                    quest = new DummyQuestTarget
                    {
                        DisplayName = "A Signal in the Noise",
                        QuestGiverName = null,
                        QuestGiverLocationName = null,
                    },
                    expanded = false,
                });

            Assert.Multiple(() =>
            {
                Assert.That(emptyTarget.titleText.Text, Is.EqualTo("進行中のクエストがない。"));
                Assert.That(questTarget.giverText.Text, Is.EqualTo("<不明> / <不明>"));
                Assert.That(DummyQuestsLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyQuestsLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsLineTranslationPatch), "QuestsLine.TitleText"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsLineTranslationPatch), "QuestsLine.GiverText"),
                    Is.EqualTo(2));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsLineTranslationPatch), "QuestsLine.MenuOption"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestsLinePostfix_TranslatesGeneratedFindItemQuestTitle_WhenPatched()
    {
        WriteDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestsLineTarget), nameof(DummyQuestsLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestsLineTranslationPatch), nameof(QuestsLineTranslationPatch.Postfix))));

            var target = new DummyQuestsLineTarget();
            target.setData(
                new DummyQuestsLineDataTarget
                {
                    quest = new DummyQuestTarget
                    {
                        DisplayName = "Aiding {{&Y|ドリンクス}} to Find the ポリセフian 祖父角の角笛",
                        QuestGiverName = "ドリンクス",
                        QuestGiverLocationName = "キヤクキャ",
                    },
                    expanded = false,
                });

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.titleText.Text,
                    Is.EqualTo("[+] {{&Y|ドリンクス}}がポリセフian 祖父角の角笛を探すのを助ける"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsLineTranslationPatch), "GeneratedQuestTitle.FindSpecificItem"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestsLinePostfix_TranslatesQuestLogBodyStepNames_WhenPatched()
    {
        WriteDictionary(
            ("Travel to Red Rock", "レッドロックへ向かう"),
            ("Find the Vermin", "害獣を見つける"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestsLineTarget), nameof(DummyQuestsLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestsLineTranslationPatch), nameof(QuestsLineTranslationPatch.Postfix))));

            var target = new DummyQuestsLineTarget();
            target.setData(
                new DummyQuestsLineDataTarget
                {
                    quest = new DummyQuestTarget
                    {
                        DisplayName = "What's Eating the Watervine?",
                        QuestGiverName = "Mehmet",
                        QuestGiverLocationName = "Joppa",
                    },
                    BodyText = "{{white|ù Travel to Red Rock}}\n   {{y|Find the Vermin}}\n   {{y|Unregistered Step}}",
                });

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.bodyText.Text,
                    Is.EqualTo("{{white|ù レッドロックへ向かう}}\n   {{y|害獣を見つける}}\n   {{y|Unregistered Step}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsLineTranslationPatch), "QuestLog.StepName"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestsStatusScreenPostfix_TranslatesQuestMapPinTitleAndPrefix_WhenPatched()
    {
        WriteDictionary(
            ("Joppa", "ジョッパ"),
            ("quest:", "クエスト:"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestsStatusScreenTarget), nameof(DummyQuestsStatusScreenTarget.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestsStatusScreenTranslationPatch), nameof(QuestsStatusScreenTranslationPatch.Postfix))));

            var target = new DummyQuestsStatusScreenTarget();
            target.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.mapController.pins[0].pinItem.titleText.Text,
                    Is.EqualTo("{{W|ジョッパ}}"));
                Assert.That(
                    target.mapController.pins[0].pinItem.detailsText.Text,
                    Is.EqualTo("{{B|クエスト:}} Find Mehmet\n{{B|クエスト:}} Return to Argyve"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsStatusScreenTranslationPatch), "ZoneDisplayName"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsStatusScreenTranslationPatch), "QuestsStatusScreen.MapPinDetails"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestsStatusScreenPostfix_TranslatesGeneratedFindItemQuestTitle_WhenPatched()
    {
        WriteDictionary(("quest:", "クエスト:"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestsStatusScreenTarget), nameof(DummyQuestsStatusScreenTarget.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestsStatusScreenTranslationPatch), nameof(QuestsStatusScreenTranslationPatch.Postfix))));

            var target = new DummyQuestsStatusScreenTarget
            {
                PinDataOverride = new List<DummyMapPinData>
                {
                    new DummyMapPinData
                    {
                        title = "{{W|Joppa}}",
                        details = "{{B|quest:}} Aiding {{&Y|ドリンクス}} to Find the ポリセフian 祖父角の角笛",
                    },
                },
            };
            target.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.mapController.pins[0].pinItem.detailsText.Text,
                    Is.EqualTo("{{B|クエスト:}} {{&Y|ドリンクス}}がポリセフian 祖父角の角笛を探すのを助ける"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsStatusScreenTranslationPatch), "QuestsStatusScreen.MapPinDetails"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestsStatusScreenPostfix_TranslatesRuntimeObservedVisitMapPinDetails_WhenPatched()
    {
        WriteDictionary(("quest:", "クエスト:"));
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("stargazer", "星見"),
            ("home", "家"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestsStatusScreenTarget), nameof(DummyQuestsStatusScreenTarget.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestsStatusScreenTranslationPatch), nameof(QuestsStatusScreenTranslationPatch.Postfix))));

            var target = new DummyQuestsStatusScreenTarget
            {
                PinDataOverride = new List<DummyMapPinData>
                {
                    new DummyMapPinData
                    {
                        title = "{{W|トゥキスフ, Stargazerhome, 地表}}",
                        details = "{{B|quest:}} Visit カルクヘタラ, Stargazerhome",
                    },
                },
            };
            target.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.mapController.pins[0].pinItem.titleText.Text,
                    Is.EqualTo("{{W|トゥキスフ, 星見の家, 地表}}"));
                Assert.That(
                    target.mapController.pins[0].pinItem.detailsText.Text,
                    Is.EqualTo("{{B|クエスト:}} カルクヘタラ, 星見の家を訪問"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestsStatusScreenTranslationPatch), "QuestsStatusScreen.MapPinVisitDetails"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestLogPostfix_TranslatesOptionalPrefixAndBonusReward_WhenPatched()
    {
        WriteDictionary(
            ("Optional: ", "任意: "),
            ("Bonus reward for completing this quest by level &C{0}&y.", "レベル&C{0}&yまでにクエストを完了するとボーナス報酬。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestLogTarget), nameof(DummyQuestLogTarget.GetLinesForQuest)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestLogTranslationPatch), nameof(QuestLogTranslationPatch.Postfix))));

            var lines = DummyQuestLogTarget.GetLinesForQuest(null);

            Assert.Multiple(() =>
            {
                Assert.That(lines[0], Is.EqualTo("{{white|{{white|ù 任意: Find Mehmet}}}"));
                Assert.That(lines[1], Is.EqualTo("  レベル&C12&yまでにクエストを完了するとボーナス報酬。"));
                Assert.That(lines[2], Is.EqualTo("   {{y|Unchanged line}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestLogTranslationPatch), "QuestLog.OptionalPrefix"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestLogTranslationPatch), "QuestLog.BonusReward"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void QuestLogPostfix_TranslatesAuthoredQuestStepNames_WhenPatched()
    {
        WriteDictionary(
            ("Optional: ", "任意: "),
            ("Travel to Red Rock", "レッドロックへ向かう"),
            ("Return with the Corpse", "死体を持って戻る"));
        DummyQuestLogTarget.LinesOverride = new List<string>
        {
            "{{white|ù Travel to Red Rock}}",
            "{{white|ù Optional: Return with the Corpse}}",
            "{{white|ù Unregistered Step}}",
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestLogTarget), nameof(DummyQuestLogTarget.GetLinesForQuest)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestLogTranslationPatch), nameof(QuestLogTranslationPatch.Postfix))));

            var lines = DummyQuestLogTarget.GetLinesForQuest(null);

            Assert.Multiple(() =>
            {
                Assert.That(
                    lines,
                    Is.EqualTo(new[]
                    {
                        "{{white|ù レッドロックへ向かう}}",
                        "{{white|ù 任意: 死体を持って戻る}}",
                        "{{white|ù Unregistered Step}}",
                    }));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(QuestLogTranslationPatch), "QuestLog.StepName"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase(
        "Aiding {{&Y|ドリンクス}} to Find the ポリセフian 祖父角の角笛",
        "{{&Y|ドリンクス}}がポリセフian 祖父角の角笛を探すのを助ける")]
    [TestCase(
        "Aiding {{&Y|ドリンクス}} to Find {{W|the ポリセフian 祖父角の角笛}}",
        "{{&Y|ドリンクス}}が{{W|ポリセフian 祖父角の角笛}}を探すのを助ける")]
    public void QuestLogPostfix_TranslatesGeneratedFindItemQuestTitle_WhenPatched(
        string source,
        string expected)
    {
        WriteDictionary();
        DummyQuestLogTarget.LinesOverride = new List<string> { source };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQuestLogTarget), nameof(DummyQuestLogTarget.GetLinesForQuest)),
                postfix: new HarmonyMethod(RequireMethod(typeof(QuestLogTranslationPatch), nameof(QuestLogTranslationPatch.Postfix))));

            var lines = DummyQuestLogTarget.GetLinesForQuest(null);

            Assert.That(lines, Is.EqualTo(new[] { expected }));
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

    private static MethodInfo RequireMethod(Type type, string methodName, Type[]? parameterTypes = null)
    {
        var method = parameterTypes is null
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
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

        var path = Path.Combine(tempDirectory, "ui-quests.ja.json");
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
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
