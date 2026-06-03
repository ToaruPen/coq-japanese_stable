using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationDisplayTextPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-conversation-display-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesKnownDisplayText_WhenPatched()
    {
        WriteDictionary(("Hello, traveler.", "旅人さん、こんにちは。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("Hello, traveler.");
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo("旅人さん、こんにちは。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesRuntimeObservedSixDayStiltConvertAppendLine_WhenPatched()
    {
        WriteDictionary(("May the ground shake but the Six Day Stilt never tumble!", "地は揺れても六日のスティルトは決して倒れん！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("May the ground shake but the Six Day Stilt never tumble!");
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo("地は揺れても六日のスティルトは決して倒れん！"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesDynamicQuestSiteIntroFixedFrames_WhenPatched()
    {
        WriteDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement(
                "Travelers spoke of {{Y|salt shrine}}. But they wouldn't reveal the location. We must know. Will you do it?");
            var result = element.GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result,
                    Is.EqualTo("Travelers spoke of {{Y|salt shrine}}. だが、彼らは場所を明かさなかった。 どうしても知る必要がある。 Will you do it?"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationDisplayTextPatch),
                        "ConversationDisplay.DynamicQuestSiteIntroFixedFrame"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_DoesNotTranslateStandaloneDynamicQuestSiteIntroFrame_WhenPatched()
    {
        WriteDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("We must know.");
            var result = element.GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("We must know."));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationDisplayTextPatch),
                        "ConversationDisplay.DynamicQuestSiteIntroFixedFrame"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_DoesNotTranslateStandaloneButTheyWouldntRevealLocation_WhenPatched()
    {
        WriteDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            const string source = "But they wouldn't reveal the location.";
            var element = new DummyConversationElement(source);
            var result = element.GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationDisplayTextPatch),
                        "ConversationDisplay.DynamicQuestSiteIntroFixedFrame"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_DoesNotTranslateWhenNeitherDynamicQuestSiteIntroFramePresent_WhenPatched()
    {
        WriteDictionary();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            const string source = "Travelers spoke of {{Y|salt shrine}}. Will you do it?";
            var element = new DummyConversationElement(source);
            var result = element.GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(source));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationDisplayTextPatch),
                        "ConversationDisplay.DynamicQuestSiteIntroFixedFrame"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_DoesNotOwnGeneratedVillageTinkerConversationTemplateAfterPlayerVariableReplacement()
    {
        WriteDictionary(
            (
                "Need a gadget repaired or identified, =player.formalAddressTerm=? Or if you're a tinker =player.reflexive=, perhaps you'd like to peruse my schematics?",
                "修理や鑑定が必要なガジェットはあるかい？ それとも君自身が工匠なら、設計図を見ていくかい？"),
            ("Live and drink, tinker.", "生きて飲め、工匠。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var body = new DummyConversationElement(
                "Need a gadget repaired or identified, friend? Or if you're a tinker yourself, perhaps you'd like to peruse my schematics?")
                .GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(
                    body,
                    Is.EqualTo("Need a gadget repaired or identified, friend? Or if you're a tinker yourself, perhaps you'd like to peruse my schematics?"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationDisplayTextPatch),
                        "ConversationDisplay.ExactLeaf"),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(ConversationDisplayTextPatch),
                        SinkObservation.ObservationOnlyDetail,
                        body,
                        body),
                    Is.EqualTo(0));
            });

            var choice = new DummyConversationElement("Live and drink, tinker. [End]")
                .GetDisplayText(withColor: false);

            Assert.That(choice, Is.EqualTo("生きて飲め、工匠。 [終了]"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(("Hello, traveler.", "旅人さん、こんにちは。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            const string source = "Hello, traveler.";
            var element = new DummyConversationElement(source);
            var result = element.GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("旅人さん、こんにちは。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationDisplayTextPatch),
                        "ConversationDisplay.ExactLeaf"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(ConversationDisplayTextPatch),
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
    public void Postfix_PassesThroughUnknownDisplayText_WhenPatched()
    {
        WriteDictionary(("Known line", "既知の文"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("Unknown runtime line");
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo("Unknown runtime line"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_UnknownText_DoesNotRecordRouteOrTriggerSinkObservation_WhenPatched()
    {
        WriteDictionary(("Known line", "既知の文"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            const string source = "Unknown runtime line";
            var element = new DummyConversationElement(source);
            element.GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(ConversationDisplayTextPatch),
                        "ConversationDisplay.ExactLeaf"),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(ConversationDisplayTextPatch),
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
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Farewell", "さらば"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("Farewell");
            var result = element.GetDisplayText(withColor: true);

            Assert.That(result, Is.EqualTo("{{W|さらば}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesEmptyResultUnchanged_WhenPatched()
    {
        WriteDictionary(("Placeholder", "プレースホルダー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement(string.Empty);
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo(string.Empty));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesNullResultUnchanged_WhenPatched()
    {
        WriteDictionary(("Placeholder", "プレースホルダー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement(null!);
            string? result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.Null);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughAlreadyJapaneseText_WhenPatched()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("すでに日本語です");
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo("すでに日本語です"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("生きて飲め。 [End]", "生きて飲め。 [終了]")]
    [TestCase("取引しよう。 [begin trade]", "取引しよう。 [取引を始める]")]
    [TestCase("{{G|取引しよう。}} {{g|[begin trade]}}", "{{G|取引しよう。}} {{g|[取引を始める]}}")]
    [TestCase("取引しよう。\n{{g|[begin trade]}}\n", "取引しよう。\n{{g|[取引を始める]}}")]
    [TestCase(
        "お前の渇きは私の渇き、私の水はお前のものだ。 [begin water ritual; 1 dram of water]",
        "お前の渇きは私の渇き、私の水はお前のものだ。 [水儀式を始める; 1ドラムの水]")]
    [TestCase(
        "お前の渇きは私の渇き、私の水はお前のものだ。 {{K|[begin water ritual; {{C|1}} dram of water]}}",
        "お前の渇きは私の渇き、私の水はお前のものだ。 {{K|[水儀式を始める; {{C|1}}ドラムの水]}}")]
    [TestCase(
        "{{G|お前の渇きは私の渇き、私の水はお前のものだ。}} {{g|[begin water ritual; {{C|1}} dram of {{B|fresh water}}]}}",
        "{{G|お前の渇きは私の渇き、私の水はお前のものだ。}} {{g|[水儀式を始める; {{C|1}}ドラムの{{B|真水}}]}}")]
    [TestCase(
        "お前の渇きは私の渇き、私の水はお前のものだ。 {{K|[begin water ritual]}}",
        "お前の渇きは私の渇き、私の水はお前のものだ。 {{K|[水儀式を始める]}}")]
    [TestCase(
        "秘密を打ち明けてくれ、水のきょうだい。 {{g|[{{C|75}} reputation]}}",
        "秘密を打ち明けてくれ、水のきょうだい。 {{g|[評判 {{C|75}}]}}")]
    [TestCase(
        "秘密を話そう。 {{g|[{{C|+50}}{{c|+10}} reputation]}}",
        "秘密を話そう。 {{g|[評判 {{C|+50}}{{c|+10}}]}}")]
    [TestCase(
        "お前たちの好物の料理を教えてくれないか？ {{g|[learn to cook {{W|starapple jam}}: {{C|50}} reputation]}}",
        "お前たちの好物の料理を教えてくれないか？ {{g|[{{W|starapple jam}}の料理を習う: 評判 {{C|50}}]}}")]
    [TestCase(
        "共に来てくれ。 {{g|[gain {{C|2}} {{W|skill points}}: {{C|100}} reputation]}}",
        "共に来てくれ。 {{g|[{{C|2}}スキルポイントを得る: 評判 {{C|100}}]}}")]
    [TestCase(
        "変異を教えてくれ。 {{g|[gain {{M|Light Manipulation}}: {{C|200}} reputation]}}",
        "変異を教えてくれ。 {{g|[{{M|Light Manipulation}}を得る: 評判 {{C|200}}]}}")]
    [TestCase(
        "感染させてくれ。 {{g|[become infected with brooding goldpuff: {{C|75}} reputation]}}",
        "感染させてくれ。 {{g|[brooding goldpuffに感染する: 評判 {{C|75}}]}}")]
    [TestCase(
        "剣術を教えてくれ。 {{g|[learn {{W|Long Blades}}: {{C|200}} reputation, {{C|-50}} SP]}}",
        "剣術を教えてくれ。 {{g|[{{W|Long Blades}}を習う: 評判 {{C|200}}, SP {{C|-50}}]}}")]
    [TestCase("話を聞こう。 {{W|[Accept Quest]}}", "話を聞こう。 {{W|[クエストを受ける]}}")]
    [TestCase("話を聞こう。 {{W|[Accept Quest - level-based reward]}}", "話を聞こう。 {{W|[クエストを受ける - レベル基準報酬]}}")]
    [TestCase("報告しよう。 {{W|[Complete Quest]}}", "報告しよう。 {{W|[クエストを完了する]}}")]
    [TestCase("進めよう。 {{W|[Complete Quest Step]}}", "進めよう。 {{W|[クエスト段階を完了する]}}")]
    [TestCase("戦うしかない。 {{R|[Fight]}}", "戦うしかない。 {{R|[戦う]}}")]
    [TestCase("本を渡そう。 {{g|[Give Books]}}", "本を渡そう。 {{g|[本を渡す]}}")]
    [TestCase("秘密を共有しよう。 {{g|[Share secrets from Resheph's life]}}", "秘密を共有しよう。 {{g|[レシェフの生涯の秘密を共有する]}}")]
    [TestCase("候補を確認しよう。 {{W|[confirm {{C|Kyakuukya}} as a sanctuary option]}}", "候補を確認しよう。 {{W|[{{C|Kyakuukya}}を聖域候補として確認する]}}")]
    [TestCase("条件を満たしている。 {{C|[Loved by {{Y|the Farmers' Guild}}]}}", "条件を満たしている。 {{C|[{{Y|the Farmers' Guild}}に愛されている]}}")]
    [TestCase("条件を満たしていない。 {{r|[Hated by {{Y|the Farmers' Guild}}]}}", "条件を満たしていない。 {{r|[{{Y|the Farmers' Guild}}に憎まれている]}}")]
    public void Postfix_TranslatesKnownTrailingActionMarkers_WhenPatched(string source, string expected)
    {
        AssertPatchedText(source, expected);
    }

    [TestCase(
        "Live and drink. [custom authored tag]",
        "生きて飲め。 [custom authored tag]")]
    [TestCase(
        "Live and drink. {{W|[custom authored tag]}}",
        "生きて飲め。 {{W|[custom authored tag]}}")]
    [TestCase(
        "Live and drink. {{W|[custom authored tag: {{C|42}}]}}",
        "生きて飲め。 {{W|[custom authored tag: {{C|42}}]}}")]
    public void Postfix_PreservesUnknownAuthoredTrailingActionMarkers_WhenPatched(string source, string expected)
    {
        WriteDictionary(("Live and drink.", "生きて飲め。"));

        AssertPatchedText(source, expected);
    }

    [Test]
    public void Postfix_TranslatesColoredChoiceBody_AfterStrippingColoredActionMarker()
    {
        WriteDictionary(("Live and drink.", "生きて飲め。"));

        AssertPatchedText("{{G|Live and drink.}} {{g|[begin trade]}}", "{{G|生きて飲め。}} {{g|[取引を始める]}}");
    }

    [Test]
    public void Postfix_TranslatesWaterRitualReputationSummary_WhenPatched()
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(
            "生きて飲め。\n\n{{C|-----}}\n{{y|Your reputation with {{C|Issachari}} is {{C|100}}.\nTam can award an additional {{C|50}} reputation.}}",
            "生きて飲め。\n\n{{C|-----}}\n{{y|{{C|Issachari}}との評判は{{C|100}}。\nTamから追加で{{C|50}}の評判を得られる。}}");
    }

    [TestCase("生きて飲め、water-sib。", "生きて飲め、水のきょうだい。")]
    [TestCase("秘密を打ち明けてくれ、waterのきょうだい。", "秘密を打ち明けてくれ、水のきょうだい。")]
    [TestCase("waterのきょうだい、共に来てくれないか。", "水のきょうだい、共に来てくれないか。")]
    [TestCase("{{K|waterのきょうだい、共に来てくれないか。}}", "{{K|水のきょうだい、共に来てくれないか。}}")]
    public void Postfix_TranslatesWaterRitualKinshipTerms_WhenPatched(string source, string expected)
    {
        AssertPatchedText(source, expected);
    }

    [TestCase("まだ！ soon に戻って。", "まだ！ もうすぐ に戻って。")]
    [TestCase("まだ！ in one day に戻って。", "まだ！ 1日後 に戻って。")]
    [TestCase("まだ！ in three days に戻って。", "まだ！ 3日後 に戻って。")]
    public void Postfix_TranslatesMoundCountdown_WhenPatched(string source, string expected)
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(source, expected);
    }

    [Test]
    public void Postfix_TranslatesQuestSignpostDirections_WhenPatched()
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(
            "{{y|Tam}}, to the northeast, or {{y|Ara}} also to the south に会いに行くといい。",
            "{{y|Tam}}, 北東側、または {{y|Ara}} も南側 に会いに行くといい。");
    }

    [Test]
    public void Postfix_TranslatesRepeatedQuestSignpostDiagonalDirections_WhenPatched()
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(
            "{{y|Tam}}, to the northeast, or {{y|Ara}} also to the northeast に会いに行くといい。",
            "{{y|Tam}}, 北東側、または {{y|Ara}} も北東側 に会いに行くといい。");
    }

    [TestCase(
        "{{y|Tam}}, to the north と話してみてくれ。",
        "{{y|Tam}}, 北側 と話してみてくれ。")]
    [TestCase(
        "{{y|Tam}}, to the west を探してみてくれ。",
        "{{y|Tam}}, 西側 を探してみてくれ。")]
    [TestCase(
        "{{y|Tam}}, somewhere を探してみてくれ。",
        "{{y|Tam}}, どこか を探してみてくれ。")]
    public void Postfix_TranslatesQuestSignpostDirections_ForAllLocalizedTemplates(
        string source,
        string expected)
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(source, expected);
    }

    [Test]
    public void Postfix_DoesNotTranslateDirectionFragmentsOutsideQuestSignpostText()
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(
            "{{y|Tam}} says you can rest here or travel to the north.",
            "{{y|Tam}} says you can rest here or travel to the north.");
    }

    [Test]
    public void Postfix_TranslatesInitiatorySkillPrompt_WhenPatched()
    {
        WriteDictionary(("Long Blades", "長剣"));

        AssertPatchedText("I seek Long Blades.", "長剣を求めている。");
    }

    [Test]
    public void Postfix_TranslatesInitiatorySkillPrompt_WithEnglishFallback_WhenPatched()
    {
        WriteDictionary(("Long Blades", "長剣"));

        AssertPatchedText("I seek Chronomancy.", "Chronomancyを求めている。");
    }

    [Test]
    public void Postfix_TranslatesWaterRitualTinkeringRecipeModLabel_WhenPatched()
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(
            "[{{W|Item mod}}] - {{C|serrated}}について学ぶ。",
            "[{{W|アイテム改造}}] - {{C|serrated}}について学ぶ。");
    }

    [Test]
    public void Postfix_TranslatesHermitOathFallback_WhenPatched()
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(
            "hermit、もう二度と邪魔しないと誓う。",
            "隠者、もう二度と邪魔しないと誓う。");
    }

    private static void AssertPatchedText(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement(source);
            var result = element.GetDisplayText(withColor: false);

            Assert.That(result, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PassesThroughAlreadyJapaneseChoice_WhenPatched()
    {
        WriteDictionary(("Dummy", "ダミー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            var element = new DummyConversationElement("スティルトとは？");
            var result = element.GetDisplayText(withColor: false);
            Assert.That(result, Is.EqualTo("スティルトとは？"));
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

        var path = Path.Combine(tempDirectory, "conversation-display-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
