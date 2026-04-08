using System.Reflection;
using System.Text;
using System.Collections.Generic;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupTranslationPatchTests
{
    private static readonly Type[] ShowConversationNonObsoleteParameterTypes =
    {
        typeof(string),
        typeof(object),
        typeof(string),
        typeof(List<string>),
        typeof(bool),
        typeof(bool),
        typeof(bool),
    };

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesShowBlockPayload_WhenPatched()
    {
        WriteDictionary(
            ("Warning!", "警告！"),
            ("Options", "設定"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("{{R|Warning!}}", "Options");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{R|警告！}}"));
                Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.EqualTo("設定"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupTranslationPatch),
                        "Popup.ProducerText.Exact"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(PopupTranslationPatch),
                        nameof(PopupTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "{{R|Warning!}}",
                        "Warning!"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_AttackPromptReturnsSourceUnchanged()
    {
        WriteDictionary(("Do you really want to attack the {0}?", "本当に{0}を攻撃しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("Do you really want to attack the ウォーターヴァイン農家?", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("Do you really want to attack the ウォーターヴァイン農家?"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_AttackPromptWithoutArticleReturnsSourceUnchanged()
    {
        WriteDictionary(("Do you really want to attack {0}?", "本当に{0}を攻撃しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("Do you really want to attack タム?", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("Do you really want to attack タム?"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_RefusesToSpeakReturnsSourceUnchanged()
    {
        WriteDictionary(("The {0} refuses to speak to you.", "{0}はあなたと話そうとしない。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("The ウォーターヴァイン農家 refuses to speak to you.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("The ウォーターヴァイン農家 refuses to speak to you."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_RefusesToSpeakWithoutArticleReturnsSourceUnchanged()
    {
        WriteDictionary(("The {0} refuses to speak to you.", "{0}はあなたと話そうとしない。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("監視官イラメ refuses to speak to you.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("監視官イラメ refuses to speak to you."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeleteSavePrompt_WhenPatched()
    {
        WriteDictionary(("Are you sure you want to delete the save game for {0}?", "{0}のセーブデータを本当に削除しますか？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("Are you sure you want to delete the save game for Yashur?", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("Yashurのセーブデータを本当に削除しますか？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeleteTitle_WhenPatched()
    {
        WriteDictionary(("Delete {0}", "{0}を削除"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("Prompt", "Delete Yashur");

            Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.EqualTo("Yashurを削除"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDuplicateBuildCodeMessage_WhenPatched()
    {
        WriteDictionary(("That code is already in your library. It's named {0}.", "そのコードはすでにライブラリにあります。名前は{0}です。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("That code is already in your library. It's named Salt Dunes.");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("そのコードはすでにライブラリにあります。名前はSalt Dunesです。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesManageBuildTitle_WhenPatched()
    {
        WriteDictionary(("Manage Build: {0}", "ビルド管理：{0}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("Prompt", "Manage Build: Salt Dunes");

            Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.EqualTo("ビルド管理：Salt Dunes"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesSifrahChosenCorrectMessage_WhenPatched()
    {
        WriteDictionary(("You have already chosen the correct option for {0}.", "{0}の正解はすでに選択済みだ。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You have already chosen the correct option for {{C|Salt Dunes}}.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{C|Salt Dunes}}の正解はすでに選択済みだ。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesSifrahUseWhichTitle_WhenPatched()
    {
        WriteDictionary(("Use which option for {0}?", "{0}にどのオプションを使う？"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowOptionList)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowOptionList(
                Title: "Use which option for {{Y|Salt Dunes}}?",
                Options: new List<string> { "Option" },
                Intro: "Prompt",
                SpacingText: "Prompt",
                Buttons: new List<DummyPopupMenuItem>());

            Assert.That(DummyPopupTarget.LastOptionListTitle, Is.EqualTo("{{Y|Salt Dunes}}にどのオプションを使う？"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesSifrahEliminatedMessage_WhenPatched()
    {
        WriteDictionary(("You have already eliminated {0} as a possibility.", "{0}はすでに候補から除外済みだ。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You have already eliminated {{M|Salt Dunes}} as a possibility.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{M|Salt Dunes}}はすでに候補から除外済みだ。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesSifrahDisabledMessage_WhenPatched()
    {
        WriteDictionary(("Choosing {0} is disabled for this turn.", "{0}の選択はこのターン無効になっている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("Choosing {{G|Salt Dunes}} is disabled for this turn.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{G|Salt Dunes}}の選択はこのターン無効になっている。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesSifrahInsightMessage_WhenPatched()
    {
        WriteDictionary((
            "You have gained insight into {0}. In a future Sifrah task of this kind, you can use this insight to determine which of your game options are not correct for any requirement. This will expend your insight, unless there are no such options.",
            "{0}についての洞察を得た。今後この種のシフラで、この洞察を使って条件に合致しないオプションを判定できる。合致しないオプションがなければ洞察は消費されない。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You have gained insight into {{B|Salt Dunes}}. In a future Sifrah task of this kind, you can use this insight to determine which of your game options are not correct for any requirement. This will expend your insight, unless there are no such options.", "Warning");

            Assert.That(
                DummyPopupTarget.LastShowBlockMessage,
                Is.EqualTo("{{B|Salt Dunes}}についての洞察を得た。今後この種のシフラで、この洞察を使って条件に合致しないオプションを判定できる。合致しないオプションがなければ洞察は消費されない。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXRLCoreHPWarningMessage_WhenPatched()
    {
        WriteDictionary(("{{R|Your health has dropped below {{C|{0}%}}!}}", "{{R|HPが{{C|{0}%}}を下回った！}}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("{{R|Your health has dropped below {{C|23%}}!}}", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{R|HPが{{C|23%}}を下回った！}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_PrefersXRLCoreHPWarningTemplateOverStrippedExactTranslation()
    {
        WriteDictionary(
            ("Your health has dropped below 40%!", "体力が40%を下回った！"),
            ("{{R|Your health has dropped below {{C|{0}%}}!}}", "{{R|HPが{{C|{0}%}}を下回った！}}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("{{R|Your health has dropped below {{C|40%}}!}}", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{R|HPが{{C|40%}}を下回った！}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXRLCoreFleeMessage_WhenPatched()
    {
        WriteDictionary(("You can't find a way to flee from {0}.", "{0}から逃げる経路が見つからない。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You can't find a way to flee from {{C|salt kraken}}.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{C|salt kraken}}から逃げる経路が見つからない。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXRLCoreReachMessage_WhenPatched()
    {
        WriteDictionary(("You can't find a way to reach {0}.", "{0}に到達する経路が見つからない。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You can't find a way to reach {{Y|the stairs}}.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{Y|the stairs}}に到達する経路が見つからない。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXRLCoreAutoattackMessage_WhenPatched()
    {
        WriteDictionary(("You do not autoattack {0} because it is not hostile to you.", "{0}は敵対していないため自動攻撃しない。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You do not autoattack {{G|snapjaw scavenger}} because it is not hostile to you.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{G|snapjaw scavenger}}は敵対していないため自動攻撃しない。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXRLCoreReloadMessage_WhenPatched()
    {
        WriteDictionary(("You need to reload! ({0})", "リロードが必要だ！ ({0})"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You need to reload! (Ctrl+R)", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("リロードが必要だ！ (Ctrl+R)"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesXRLCoreOldSaveMessage_WhenPatched()
    {
        WriteDictionary((
            "That save file looks like it's from an older save format revision ({0}). Sorry!\nYou can probably change to a previous branch in your game client and get it to load if you want to finish it off.",
            "このセーブデータは古いフォーマット（{0}）のようです。\nゲームクライアントで以前のブランチに切り替えれば読み込める可能性があります。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock(
                "That save file looks like it's from an older save format revision (2.0.3). Sorry!\n\nYou can probably change to a previous branch in your game client and get it to load if you want to finish it off.",
                "Warning");

            Assert.That(
                DummyPopupTarget.LastShowBlockMessage,
                Is.EqualTo("このセーブデータは古いフォーマット（2.0.3）のようです。\nゲームクライアントで以前のブランチに切り替えれば読み込める可能性があります。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TranslatePopupTextForProducerRoute_TranslatesXRLCoreGameInfoBlock()
    {
        WriteDictionary((
            "\n\n           {0} mode.\n\n           Turn {1}\n\n          World seed: {2}     \n\n\n   ",
            "\n\n           {0}モード\n\n           ターン{1}\n\n          ワールドシード: {2}     \n\n\n   "));

        const string source = "\n\n           Classic mode.\n\n           Turn 12345\n\n          World seed: QUD-SEED     \n\n\n   ";

        var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, nameof(PopupTranslationPatch));

        Assert.That(
            translated,
            Is.EqualTo("\n\n           Classicモード\n\n           ターン12345\n\n          ワールドシード: QUD-SEED     \n\n\n   "));
    }

    [TestCase(
        "You try to staunch the wounds of {0}, but your limbs pass through them.",
        "{0}の傷を止血しようとするが、手が体をすり抜ける。",
        "You try to staunch the wounds of {{C|salt kraken}}, but your limbs pass through them.",
        "{{C|salt kraken}}の傷を止血しようとするが、手が体をすり抜ける。")]
    [TestCase(
        "You try to staunch the wounds of {0}, but cannot affect them.",
        "{0}の傷を止血しようとするが、影響を与えられない。",
        "You try to staunch the wounds of frozen cherub, but cannot affect them.",
        "frozen cherubの傷を止血しようとするが、影響を与えられない。")]
    [TestCase(
        "You staunch the wounds of {0}, though some are too deep to treat.",
        "{0}の傷を止血したが、深すぎて処置できないものもある。",
        "You staunch the wounds of {{Y|warden}}, though some are too deep to treat.",
        "{{Y|warden}}の傷を止血したが、深すぎて処置できないものもある。")]
    [TestCase(
        "You staunch the wounds of {0}.",
        "{0}の傷を止血した。",
        "You staunch the wounds of goatfolk pariah.",
        "goatfolk pariahの傷を止血した。")]
    [TestCase(
        "{0} are too deep to treat.",
        "{0}は深すぎて処置できない。",
        "your wounds are too deep to treat.",
        "your woundsは深すぎて処置できない。")]
    [TestCase(
        "Neither you nor {0} are bleeding.",
        "あなたも{0}も出血していない。",
        "Neither you nor {{M|Eskhind}} are bleeding.",
        "あなたも{{M|Eskhind}}も出血していない。")]
    [TestCase(
        "You have no medicinal ingredients with which to treat the poison coursing through {0}.",
        "{0}を蝕む毒を治療する薬用素材がない。",
        "You have no medicinal ingredients with which to treat the poison coursing through snapjaw scavenger.",
        "snapjaw scavengerを蝕む毒を治療する薬用素材がない。")]
    [TestCase(
        "You try to cure the poison coursing through {0}, but your limbs pass through them.",
        "{0}を蝕む毒を治そうとするが、手が体をすり抜ける。",
        "You try to cure the poison coursing through {{G|salt weep}}, but your limbs pass through them.",
        "{{G|salt weep}}を蝕む毒を治そうとするが、手が体をすり抜ける。")]
    [TestCase(
        "You try to cure the poison coursing through {0}, but cannot affect them.",
        "{0}を蝕む毒を治そうとするが、影響を与えられない。",
        "You try to cure the poison coursing through frozen cherub, but cannot affect them.",
        "frozen cherubを蝕む毒を治そうとするが、影響を与えられない。")]
    [TestCase(
        "You try to cure the poison coursing through {0}, but your cures are ineffective.",
        "{0}を蝕む毒を治そうとするが、治療が効かない。",
        "You try to cure the poison coursing through goatfolk hero, but your cures are ineffective.",
        "goatfolk heroを蝕む毒を治そうとするが、治療が効かない。")]
    [TestCase(
        "You create a new recipe for {{|{0}}}!",
        "{{|{0}}}の新しいレシピを生み出した！",
        "You create a new recipe for {{|starapple stew}}!",
        "{{|starapple stew}}の新しいレシピを生み出した！")]
    [TestCase(
        "You start to metabolize the meal, gaining the following effect for the rest of the day:\n\n{{W|{0}}}",
        "食事を消化し始め、今日の残りの間、以下の効果を得る:\n\n{{W|{0}}}",
        "You start to metabolize the meal, gaining the following effect for the rest of the day:\n\n{{W|+5 to heat resistance}}",
        "食事を消化し始め、今日の残りの間、以下の効果を得る:\n\n{{W|+5 to heat resistance}}")]
    public void TranslatePopupTextForProducerRoute_TranslatesCampfireSinglePlaceholderPatterns(
        string templateKey,
        string templateText,
        string source,
        string expected)
    {
        WriteDictionary((templateKey, templateText));

        var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, nameof(PopupTranslationPatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslatePopupTextForProducerRoute_TranslatesCampfireCurePoisonPattern()
    {
        WriteDictionary((
            "You cure the {0} coursing through {1} with a balm made from {2}.",
            "{2}で作った塗り薬で{1}を蝕む毒を治した。"));

        const string source =
            "You cure the poisons coursing through {{G|snapjaw scavenger}} with a balm made from {{Y|witchwood bark}}.";

        var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, nameof(PopupTranslationPatch));

        Assert.That(
            translated,
            Is.EqualTo("{{Y|witchwood bark}}で作った塗り薬で{{G|snapjaw scavenger}}を蝕む毒を治した。"));
    }

    [Test]
    public void TranslatePopupTextForProducerRoute_TranslatesWaterRitualLowReputationPattern()
    {
        WriteDictionary((
            "You don't have a high enough reputation with {0}.",
            "{0}との評判が十分に高くない。"));

        const string source = "You don't have a high enough reputation with {{C|Barathrumites}}.";

        var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, nameof(PopupTranslationPatch));

        Assert.That(translated, Is.EqualTo("{{C|Barathrumites}}との評判が十分に高くない。"));
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarkerAndSkipsTranslation()
    {
        // Set up a dictionary entry that would match the stripped text if translation were applied.
        WriteDictionary(("{{R|熊は防いだ。}}", "TRAP: should not be reached"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("\u0001{{R|熊は防いだ。}}", "Warning");

            // Marker stripped AND trap dictionary entry NOT applied.
            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("{{R|熊は防いだ。}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_HotkeyLabelReturnsSourceUnchanged()
    {
        WriteDictionary(("desecrate", "冒涜する"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("[D] Desecrate", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("[D] Desecrate"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_InventoryHotkeyLabelsReturnSourceUnchanged()
    {
        WriteDictionary(
            ("drop", "落とす"),
            ("mark important", "重要にする"),
            ("learn", "習得"),
            ("add notes", "メモを追加"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowOptionList)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowOptionList(
                Title: "Choose",
                Options: new List<string> { "[d] drop", "[i] mark important", "[n] learn", "[N] add Notes" },
                Intro: "Prompt",
                SpacingText: "Prompt",
                Buttons: new List<DummyPopupMenuItem>());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListOptions, Is.Not.Null);
                Assert.That(DummyPopupTarget.LastOptionListOptions![0], Is.EqualTo("[d] drop"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[1], Is.EqualTo("[i] mark important"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[2], Is.EqualTo("[n] learn"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[3], Is.EqualTo("[N] add Notes"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_HotkeyLabelCaseFallbackReturnsSourceUnchanged()
    {
        WriteDictionary(("disassemble all", "すべて分解"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("[M] disasseMble all", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("[M] disasseMble all"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeathPopup_WhenPatched()
    {
        WriteDictionary(
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.KilledBy.Bare", "{killer}に殺された。"),
            ("dromad merchant", "ドロマド商人"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You died.\n\nYou were killed by a dromad merchant.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは死んだ。\n\nドロマド商人に殺された。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeathPopupWithFromPreposition_WhenPatched()
    {
        WriteDictionary(
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.DiedOfPoisonFrom.Bare", "{killer}の毒で死亡した。"),
            ("viper", "毒蛇"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You died.\n\nYou died of poison from a viper.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは死んだ。\n\n毒蛇の毒で死亡した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesBittenToDeathPopup_WhenPatched()
    {
        WriteDictionary(
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.BittenToDeathBy.Bare", "{killer}に噛み殺された。"),
            ("snapjaw", "スナップジョー"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You died.\n\nYou were bitten to death by a snapjaw.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは死んだ。\n\nスナップジョーに噛み殺された。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesExplosionDeathPopup_WhenPatched()
    {
        WriteDictionary(
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.DiedInExplosionOf.Bare", "{killer}の爆発で死んだ。"),
            ("grenade", "グレネード"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You died.\n\nYou died in the explosion of a grenade.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは死んだ。\n\nグレネードの爆発で死んだ。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ObservationOnly_JournalLocationPopupReturnsSourceUnchanged()
    {
        WriteMessagePatternDictionary((
            "^You note the location of (.+?) in the Locations > (.+?) section of your journal\\.[.!]?$",
            "ジャーナルの「場所 > {t1}」欄に{0}の場所を記録した。"));
        WriteDictionary(("Historic Sites", "史跡"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock(
                "You note the location of Shagganip in the Locations > Historic Sites section of your journal.",
                "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("You note the location of Shagganip in the Locations > Historic Sites section of your journal."));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeathMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("View final messages", "最後のメッセージを見る"),
            ("Reload from checkpoint", "チェックポイントから再開"),
            ("Retire character", "キャラクターを引退"),
            ("Quit to main menu", "メインメニューに戻る"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowOptionList)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowOptionList(
                Title: "Choose",
                Options: new List<string>
                {
                    "View final messages",
                    "Reload from checkpoint",
                    "Retire character",
                    "Quit to main menu",
                },
                Intro: "Prompt",
                SpacingText: "Prompt",
                Buttons: new List<DummyPopupMenuItem>());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListOptions, Is.Not.Null);
                Assert.That(DummyPopupTarget.LastOptionListOptions![0], Is.EqualTo("最後のメッセージを見る"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[1], Is.EqualTo("チェックポイントから再開"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[2], Is.EqualTo("キャラクターを引退"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[3], Is.EqualTo("メインメニューに戻る"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupTranslationPatch),
                        "Popup.ProducerText.Exact"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesShowOptionListPayload_WhenPatched()
    {
        WriteDictionary(
            ("Choose", "選択"),
            ("Continue", "続行"),
            ("Quit", "終了"),
            ("Prompt", "案内"),
            ("Cancel", "キャンセル"));

        var buttons = new List<DummyPopupMenuItem>
        {
            new DummyPopupMenuItem("{{W|Cancel}}"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowOptionList)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowOptionList(
                Title: "Choose",
                Options: new List<string> { "Continue", "{{G|Quit}}" },
                Intro: "Prompt",
                SpacingText: "Prompt",
                Buttons: buttons);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListTitle, Is.EqualTo("選択"));
                Assert.That(DummyPopupTarget.LastOptionListIntro, Is.EqualTo("案内"));
                Assert.That(DummyPopupTarget.LastOptionListSpacingText, Is.EqualTo("案内"));
                Assert.That(DummyPopupTarget.LastOptionListOptions, Is.Not.Null);
                Assert.That(DummyPopupTarget.LastOptionListOptions![0], Is.EqualTo("続行"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[1], Is.EqualTo("{{G|終了}}"));
                Assert.That(DummyPopupTarget.LastOptionListButtons, Is.Not.Null);
                Assert.That(DummyPopupTarget.LastOptionListButtons![0].text, Is.EqualTo("{{W|キャンセル}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupTranslationPatch),
                        "Popup.ProducerMenuItem.Exact"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(PopupTranslationPatch),
                        nameof(PopupTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Continue",
                        "Continue"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("You already have that power.", "その能力はすでに習得している。")]
    [TestCase("You are frozen solid!", "あなたは完全に凍りついた！")]
    [TestCase("There are hostiles nearby!", "近くに敵対者がいる！")]
    [TestCase("Would you like to walk to the nearest stairway down?", "最寄りの下り階段まで移動しますか？")]
    public void Prefix_TranslatesStaticPopupPrompts(string source, string expected)
    {
        WriteDictionary((source, expected));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock(source, "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_LeavesMarkedUpPopupButtonsUnchanged()
    {
        WriteDictionary(
            ("Choose", "選択"),
            ("Prompt", "案内"),
            ("Continue", "続ける"),
            ("Cancel", "キャンセル"));

        var buttons = new List<DummyPopupMenuItem>
        {
            new DummyPopupMenuItem("{{W|[space]}} {{y|Continue}}"),
            new DummyPopupMenuItem("{{W|[Esc]}} {{y|Cancel}}"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowOptionList)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowOptionList(
                Title: "Choose",
                Options: new List<string> { "Continue" },
                Intro: "Prompt",
                SpacingText: "Prompt",
                Buttons: buttons);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListButtons, Is.Not.Null);
                Assert.That(DummyPopupTarget.LastOptionListButtons![0].text, Is.EqualTo("{{W|[space]}} {{y|Continue}}"));
                Assert.That(DummyPopupTarget.LastOptionListButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|Cancel}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCaseSource(typeof(QudJP.Tests.L1.ColorRouteInvariantCases), nameof(QudJP.Tests.L1.ColorRouteInvariantCases.PopupMenuItemCases))]
    public void TranslatePopupMenuItemText_PreservesUnmatchedHotkeyMarkup(QudJP.Tests.L1.ColorTranslationCase testCase)
    {
        WriteDictionary(testCase.Entries.ToArray());

        var translated = PopupTranslationPatch.TranslatePopupMenuItemText(testCase.Source);

        Assert.That(translated, Is.EqualTo(testCase.Expected));
    }

    [Test]
    public void TranslatePopupMenuItemText_DoesNotReTranslateAlreadyLocalizedHotkeyLabel()
    {
        var source = "{{W|[Esc]}} {{y|キャンセル}}";

        var translated = PopupTranslationPatch.TranslatePopupMenuItemText(source);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests("[Esc] キャンセル"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("キャンセル"), Is.EqualTo(0));
        });
    }

    [TestCase("Enter 送信")]
    [TestCase("Esc キャンセル")]
    [TestCase("Tab 長押しで決定")]
    public void IsAlreadyLocalizedPopupText_TreatsPlainLocalizedHotkeyLabelsAsLocalized(string source)
    {
        var localized = PopupTranslationPatch.IsAlreadyLocalizedPopupText(source);

        Assert.Multiple(() =>
        {
            Assert.That(localized, Is.True);
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void NormalizeItemTexts_PreservesBottomContextHotkeyItems()
    {
        WriteDictionary(("Continue", "続ける"));

        var context = new DummyQudMenuBottomContext(new List<DummyQudMenuItem>
        {
            new DummyQudMenuItem("{{W|[space]}} {{y|Continue}}", hotkey: "Accept,Cancel"),
        });

        QudMenuBottomContextTranslationPatch.NormalizeItemTexts(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.items[0].text, Is.EqualTo("{{W|[space]}} {{y|Continue}}"));
        });
    }

    [Test]
    public void NormalizeItemTexts_SkipsAlreadyLocalizedItemsForBottomContext()
    {
        WriteDictionary(("[Tab] 取引", "[Tab] 取引"));

        var context = new DummyQudMenuBottomContext(new List<DummyQudMenuItem>
        {
            new DummyQudMenuItem("{{W|[Tab]}} {{y|取引}}", hotkey: "CmdStartTrade"),
        });

        QudMenuBottomContextTranslationPatch.NormalizeItemTexts(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.items[0].text, Is.EqualTo("{{W|[Tab]}} {{y|取引}}"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("[Tab] 取引"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("取引"), Is.EqualTo(0));
        });
    }

    [Test]
    public void Prefix_TranslatesShowConversationPayload_OnNonObsoleteOverload_WhenPatched()
    {
        WriteDictionary(
            ("Trade", "取引"),
            ("Choose your response.", "返答を選択してください。"),
            ("Ask about water", "水について尋ねる"),
            ("Leave", "立ち去る"));

        var original = RequireMethod(
            typeof(DummyPopupTarget),
            nameof(DummyPopupTarget.ShowConversation),
            ShowConversationNonObsoleteParameterTypes);
        Assert.That(original.IsDefined(typeof(ObsoleteAttribute), inherit: false), Is.False);

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: original,
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowConversation(
                Title: "Trade",
                Intro: "Choose your response.",
                Options: new List<string> { "Ask about water", "{{G|Leave}}" });

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowConversationTitle, Is.EqualTo("取引"));
                Assert.That(DummyPopupTarget.LastShowConversationIntro, Is.EqualTo("返答を選択してください。"));
                Assert.That(DummyPopupTarget.LastShowConversationOptions, Is.Not.Null);
                Assert.That(DummyPopupTarget.LastShowConversationOptions![0], Is.EqualTo("水について尋ねる"));
                Assert.That(DummyPopupTarget.LastShowConversationOptions[1], Is.EqualTo("{{G|立ち去る}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesShowConversationPayload_WhenPatched()
    {
        WriteDictionary(
            ("Trade", "取引"),
            ("Choose your response.", "返答を選択してください。"),
            ("Ask about water", "水について尋ねる"),
            ("Leave", "立ち去る"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyPopupTarget),
                    nameof(DummyPopupTarget.ShowConversation),
                    ShowConversationNonObsoleteParameterTypes),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowConversation(
                Title: "Trade",
                Intro: "Choose your response.",
                Options: new List<string> { "Ask about water", "{{G|Leave}}" });

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowConversationTitle, Is.EqualTo("取引"));
                Assert.That(DummyPopupTarget.LastShowConversationIntro, Is.EqualTo("返答を選択してください。"));
                Assert.That(DummyPopupTarget.LastShowConversationOptions, Is.Not.Null);
                Assert.That(DummyPopupTarget.LastShowConversationOptions![0], Is.EqualTo("水について尋ねる"));
                Assert.That(DummyPopupTarget.LastShowConversationOptions[1], Is.EqualTo("{{G|立ち去る}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_ShowConversation_TranslatesPayload_WithoutPopupSinkObservation_WhenPatched()
    {
        WriteDictionary(
            ("Trade", "取引"),
            ("Choose your response.", "返答を選択してください。"),
            ("Ask about water", "水について尋ねる"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyPopupTarget),
                    nameof(DummyPopupTarget.ShowConversation),
                    ShowConversationNonObsoleteParameterTypes),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyConversationElement), nameof(DummyConversationElement.GetDisplayText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(ConversationDisplayTextPatch), nameof(ConversationDisplayTextPatch.Postfix))));

            const string titleSource = "Trade";
            const string introSource = "Choose your response.";
            const string optionSource = "Ask about water";
            DummyPopupTarget.ShowConversation(
                Title: titleSource,
                Intro: introSource,
                Options: new List<string> { optionSource });

            var translatedTitle = new DummyConversationElement(DummyPopupTarget.LastShowConversationTitle).GetDisplayText(withColor: false);
            var translatedIntro = new DummyConversationElement(DummyPopupTarget.LastShowConversationIntro).GetDisplayText(withColor: false);
            var translatedOption = new DummyConversationElement(DummyPopupTarget.LastShowConversationOptions![0]).GetDisplayText(withColor: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowConversationTitle, Is.EqualTo("取引"));
                Assert.That(DummyPopupTarget.LastShowConversationIntro, Is.EqualTo("返答を選択してください。"));
                Assert.That(DummyPopupTarget.LastShowConversationOptions![0], Is.EqualTo("水について尋ねる"));
                Assert.That(translatedTitle, Is.EqualTo("取引"));
                Assert.That(translatedIntro, Is.EqualTo("返答を選択してください。"));
                Assert.That(translatedOption, Is.EqualTo("水について尋ねる"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupTranslationPatch),
                        "Popup.ProducerText.Exact"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(PopupTranslationPatch),
                        nameof(PopupTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        titleSource,
                        titleSource),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_RecordsProducerRouteTransform_ForShowBlock_WhenPatched()
    {
        WriteDictionary(("Test message", "テストメッセージ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            const string source = "Test message";
            DummyPopupTarget.ShowBlock(source, "Warning");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("テストメッセージ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupTranslationPatch),
                        "Popup.ProducerText.Exact"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(PopupTranslationPatch),
                        nameof(PopupTranslationPatch),
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
    public void Prefix_RecordsProducerRouteTransform_ForShowOptionList_WhenPatched()
    {
        WriteDictionary(("Continue", "続行"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowOptionList)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowOptionList(
                Title: "Choose",
                Options: new List<string> { "Continue" },
                Intro: "Prompt",
                SpacingText: "Prompt",
                Buttons: new List<DummyPopupMenuItem>());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListOptions![0], Is.EqualTo("続行"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupTranslationPatch),
                        "Popup.ProducerText.Exact"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(PopupTranslationPatch),
                        nameof(PopupTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Continue",
                        "Continue"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TranslatePopupTextForRoute_ObservationOnly_ReturnsSourceUnchanged()
    {
        var source = "Do you really want to attack the bear?";
        var result = PopupTranslationPatch.TranslatePopupTextForRoute(source, "TestRoute");
        Assert.That(result, Is.EqualTo(source));
    }

    [Test]
    public void TranslatePopupTextForRoute_ObservationOnly_LogsUnclaimed()
    {
        var source = "Some untranslated popup text";
        PopupTranslationPatch.TranslatePopupTextForRoute(source, "TestRoute");
        var hitCount = SinkObservation.GetHitCountForTests(
            nameof(PopupTranslationPatch), "TestRoute", SinkObservation.ObservationOnlyDetail, source, source);
        Assert.That(hitCount, Is.GreaterThan(0));
    }

    [Test]
    public void TranslatePopupTextForRoute_DirectMarker_StillStripped()
    {
        var source = "\u0001Already translated text";
        var result = PopupTranslationPatch.TranslatePopupTextForRoute(source, "TestRoute");
        Assert.That(result, Is.EqualTo("Already translated text"));
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

    private static MethodInfo RequireMethod(Type type, string methodName, Type[] parameterTypes)
    {
        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException(
                $"Method not found: {type.FullName}.{methodName}({string.Join(", ", Array.ConvertAll(parameterTypes, static type => type.Name))})");
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

        var path = Path.Combine(dictionaryDirectory, "ui-popup.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteMessagePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

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

internal sealed class DummyQudMenuBottomContext
{
    public DummyQudMenuBottomContext(List<DummyQudMenuItem> items)
    {
        this.items = items;
    }

    public List<DummyQudMenuItem> items;
}

internal struct DummyQudMenuItem
{
    public DummyQudMenuItem(string text, string hotkey)
    {
        this.text = text;
        command = string.Empty;
        this.hotkey = hotkey;
    }

    public string text;

    public string command;

    public string hotkey;
}
