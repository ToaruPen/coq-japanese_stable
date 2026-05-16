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

            Assert.That(choice, Is.EqualTo("生きて飲め、工匠。"));
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

    [TestCase("生きて飲め。 [End]", "生きて飲め。")]
    [TestCase("取引しよう。 [begin trade]", "取引しよう。")]
    [TestCase("お前の渇きは私の渇き、私の水はお前のものだ。 [begin water ritual; 1 dram of water]", "お前の渇きは私の渇き、私の水はお前のものだ。")]
    [TestCase("お前の渇きは私の渇き、私の水はお前のものだ。 {{K|[begin water ritual; {{C|1}} dram of water]}}", "お前の渇きは私の渇き、私の水はお前のものだ。")]
    [TestCase("{{G|お前の渇きは私の渇き、私の水はお前のものだ。}} {{g|[begin water ritual; {{C|1}} dram of water]}}", "{{G|お前の渇きは私の渇き、私の水はお前のものだ。}}")]
    [TestCase("{{G|取引しよう。}} {{g|[begin trade]}}", "{{G|取引しよう。}}")]
    [TestCase("取引しよう。\n{{g|[begin trade]}}\n", "取引しよう。")]
    public void Postfix_StripsTrailingActionMarkers_WhenPatched(string source, string expected)
    {
        WriteDictionary(("Dummy", "ダミー"));

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
    public void Postfix_TranslatesColoredChoiceBody_AfterStrippingColoredActionMarker()
    {
        WriteDictionary(("Live and drink.", "生きて飲め。"));

        AssertPatchedText("{{G|Live and drink.}} {{g|[begin trade]}}", "{{G|生きて飲め。}}");
    }

    [Test]
    public void Postfix_TranslatesWaterRitualReputationSummary_WhenPatched()
    {
        WriteDictionary(("Dummy", "ダミー"));

        AssertPatchedText(
            "生きて飲め。\n\n{{C|-----}}\n{{y|Your reputation with {{C|Issachari}} is {{C|100}}.\nTam can award an additional {{C|50}} reputation.}}",
            "生きて飲め。\n\n{{C|-----}}\n{{y|{{C|Issachari}}との評判は{{C|100}}。\nTamから追加で{{C|50}}の評判を得られる。}}");
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
