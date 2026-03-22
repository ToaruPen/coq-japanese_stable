using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupTranslationPatchTests
{
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
        DummyPopupTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesShowBlockMessageAndTitle()
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
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesAttackPromptWithAlreadyLocalizedTarget()
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

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("本当にウォーターヴァイン農家を攻撃しますか？"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("ウォーターヴァイン農家"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesAttackPromptWithoutArticle()
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

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("本当にタムを攻撃しますか？"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("タム"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesRefusesToSpeakPromptWithAlreadyLocalizedTarget()
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

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("ウォーターヴァイン農家はあなたと話そうとしない。"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("ウォーターヴァイン農家"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesRefusesToSpeakPromptWithoutLeadingArticle()
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

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("監視官イラメはあなたと話そうとしない。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeleteSavePromptWithoutTranslatingSaveName()
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

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("Yashurのセーブデータを本当に削除しますか？"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Yashur"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeleteTitleWithoutTranslatingSaveName()
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

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastShowBlockTitle, Is.EqualTo("Yashurを削除"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Yashur"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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
    public void Prefix_TranslatesHotkeyLabelWithCaseFallback()
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

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("[D] 冒涜する"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesInventoryHotkeyLabels()
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
                Assert.That(DummyPopupTarget.LastOptionListOptions![0], Is.EqualTo("[d] 落とす"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[1], Is.EqualTo("[i] 重要にする"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[2], Is.EqualTo("[n] 習得"));
                Assert.That(DummyPopupTarget.LastOptionListOptions[3], Is.EqualTo("[N] メモを追加"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesHotkeyLabelWithLowerAsciiFallbackForDisassembleAll()
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

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("[M] すべて分解"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeathPopupTemplate()
    {
        WriteDictionary(
            ("QudJP.DeathWrapper.KilledBy.Wrapped", "あなたは死んだ。\n\n{killer}に殺された。"),
            ("dromad merchant", "ドロマド商人"),
            ("sitting", "座っている"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You died.\n\nYou were killed by タム, dromad merchant [sitting].", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは死んだ。\n\nタム、ドロマド商人 [座っている]に殺された。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeathPopupTemplateWithoutLeakingArticleIntoKillerName()
    {
        WriteDictionary(("QudJP.DeathWrapper.KilledBy.Wrapped", "あなたは死んだ。\n\n{killer}に殺された。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You died.\n\nYou were killed by a ウォーターヴァイン農家.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは死んだ。\n\nウォーターヴァイン農家に殺された。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesBittenToDeathPopupTemplateWithoutLeakingArticleIntoKillerName()
    {
        WriteDictionary(("QudJP.DeathWrapper.BittenToDeathBy.Wrapped", "あなたは死んだ。\n\n{killer}に噛み殺された。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowBlock)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupTranslationPatch), nameof(PopupTranslationPatch.Prefix))));

            DummyPopupTarget.ShowBlock("You died.\n\nYou were bitten to death by the ウォーターヴァイン農家.", "Warning");

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("あなたは死んだ。\n\nウォーターヴァイン農家に噛み殺された。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesJournalLocationPopup_ViaSharedMessageFamily()
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

            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("ジャーナルの「場所 > 史跡」欄にShagganipの場所を記録した。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesDeathMenuOptions()
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
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesShowOptionListPayload()
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
    public void Prefix_TranslatesMarkedUpPopupButtonsWithoutBreakingHotkeyPrefix()
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
                Assert.That(DummyPopupTarget.LastOptionListButtons![0].text, Is.EqualTo("{{W|[space]}} {{y|続ける}}"));
                Assert.That(DummyPopupTarget.LastOptionListButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCaseSource(typeof(QudJP.Tests.L1.ColorRouteInvariantCases), nameof(QudJP.Tests.L1.ColorRouteInvariantCases.PopupMenuItemCases))]
    public void TranslatePopupMenuItemText_PreservesSharedInvariantCases(QudJP.Tests.L1.ColorTranslationCase testCase)
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
    public void NormalizeItemTexts_UpdatesStructBackedItemsForBottomContext()
    {
        WriteDictionary(("Continue", "続ける"));

        var context = new DummyQudMenuBottomContext(new List<DummyQudMenuItem>
        {
            new DummyQudMenuItem("{{W|[space]}} {{y|Continue}}", hotkey: "Accept,Cancel"),
        });

        QudMenuBottomContextTranslationPatch.NormalizeItemTexts(context);

        Assert.That(context.items[0].text, Is.EqualTo("{{W|[space]}} {{y|続ける}}"));
    }

    [Test]
    public void NormalizeItemTexts_SkipsAlreadyLocalizedItemsForBottomContext()
    {
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
    public void Prefix_TranslatesShowConversationPayload()
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
                original: RequireMethod(typeof(DummyPopupTarget), nameof(DummyPopupTarget.ShowConversation)),
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
