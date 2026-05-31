using System.Collections;
using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupPickOptionTranslationPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-pickoption-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        ScopedDictionaryLookup.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        SelectableTextMenuItemTranslationPatch.ClearDisplayTranslationCacheForTests();
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DummyPopupGenericTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        SelectableTextMenuItemTranslationPatch.ClearDisplayTranslationCacheForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesPickOptionTitle()
    {
        WriteDictionary(("Save Slots", "セーブ一覧"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Title: "Save Slots");

        Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("セーブ一覧"));
    }

    [Test]
    public void Prefix_TranslatesCampfireCookingActionMenuTitle()
    {
        WriteDictionary(("The fire breathes its warmth on your bones.", "焚き火の温もりが骨身にしみる。"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Title: "{{W|The fire breathes its warmth on your bones.}}");

        Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("{{W|焚き火の温もりが骨身にしみる。}}"));
    }

    [Test]
    public void Prefix_TranslatesEnergyCellSocketPickerTitleAndOptions_WhenPatched()
    {
        WriteCommonMenuActionDictionary(("disassemble cell", "セルを分解する"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Title: "{{W|&yChoose a cell for your &W帯電&y &bカーバイドの戦斧}}",
            Options: new[]
            {
                "remove cell: {{c|ケムセル}} {{y|({{g|残量多}})}} {{y|<{{|{{G|B}}{{C|D}}{{r|1}}}}>}}",
                "disassemble cell",
            });

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionTitle,
                Is.EqualTo("{{W|&W帯電&y &bカーバイドの戦斧&y用のセルを選ぶ}}"));
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionOptions,
                Is.EqualTo(new[]
                {
                    "セルを外す: {{c|ケムセル}} {{y|({{g|残量多}})}} {{y|<{{|{{G|B}}{{C|D}}{{r|1}}}}>}}",
                    "セルを分解する",
                }));
        });
    }

    [Test]
    public void Prefix_DoesNotTreatOpeningSentenceAsContainerTitle()
    {
        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Title: "Opening the ark will expose the core to outside influence.");

        Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("Opening the ark will expose the core to outside influence."));
    }

    [Test]
    public void Prefix_TranslatesPickOptionIntro()
    {
        WriteDictionary(("Choose a destination.", "行き先を選んでください。"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Intro: "Choose a destination.");

        Assert.That(DummyPopupGenericTarget.LastPickOptionIntro, Is.EqualTo("行き先を選んでください。"));
    }

    [Test]
    public void Prefix_TranslatesWaitStyleOptions_WhenPatched()
    {
        WriteQudMenuItemDictionary(
            ("Wait 1 Turn", "QudMenuItem", "1ターン待機"),
            ("Wait N Turns", "QudMenuItem", "Nターン待機"),
            ("Wait 20 Turns", "QudMenuItem", "20ターン待機"),
            ("Wait 100 Turns", "QudMenuItem", "100ターン待機"),
            ("Wait Until Healed", "QudMenuItem", "完全回復まで待機"),
            ("Wait Until Party Healed", "QudMenuItem", "パーティー全員の回復まで待機"),
            ("Wait Until Morning", "QudMenuItem", "朝まで待機"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Title: "Select Wait Style",
            Options: new[]
            {
                "Wait 1 Turn",
                "Wait N Turns",
                "Wait 20 Turns",
                "Wait 100 Turns",
                "Wait Until Healed",
                "Wait Until Party Healed",
                "Wait Until Morning",
            },
            Buttons: new[] { new DummyPopupMenuItem("{{W|[a]}} {{y|Wait 1 Turn}}") });

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionOptions,
                Is.EqualTo(new[]
                {
                    "1ターン待機",
                    "Nターン待機",
                    "20ターン待機",
                    "100ターン待機",
                    "完全回復まで待機",
                    "パーティー全員の回復まで待機",
                    "朝まで待機",
                }));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|[a]}} {{y|1ターン待機}}"));
        });
    }

    [Test]
    public void Prefix_TranslatesRenamePopupTitleAndOptions_WhenPatched()
    {
        WriteDisplayNameDictionary(("chem cell", "ケムセル"));
        WriteQudMenuItemDictionary(
            ("Enter a name.", "QudMenuItem", "名前を入力する。"),
            ("Name it based on its qualities.", "QudMenuItem", "特徴に基づいて名前を付ける。"),
            ("Choose a random name from your own culture.", "QudMenuItem", "自分の文化からランダムな名前を選ぶ。"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Title: "Rename your {{c|chem cell}}.",
            Options: new[]
            {
                "Enter a name.",
                "Name it based on its qualities.",
                "Choose a random name from your own culture.",
                "Choose a random name from リヴィッド・クリーバーの culture.",
            },
            Buttons: new[] { new DummyPopupMenuItem("{{W|[d]}} {{y|Choose a random name from リヴィッド・クリーバーの culture.}}") });

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("{{c|ケムセル}}の名前を変更する。"));
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionOptions,
                Is.EqualTo(new[]
                {
                    "名前を入力する。",
                    "特徴に基づいて名前を付ける。",
                    "自分の文化からランダムな名前を選ぶ。",
                    "リヴィッド・クリーバーの文化からランダムな名前を選ぶ。",
                }));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionButtons![0].text,
                Is.EqualTo("{{W|[d]}} {{y|リヴィッド・クリーバーの文化からランダムな名前を選ぶ。}}"));
        });
    }

    [Test]
    public void Prefix_TranslatesPickOptionOptions()
    {
        WriteDictionary(("Continue", "続ける"), ("Cancel", "キャンセル"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Options: new[] { "Continue", "Cancel" });

        Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "続ける", "キャンセル" }));
    }

    [Test]
    public void Prefix_RepositoryDictionary_TranslatesReviewedFixedProducerPickOptionPayload()
    {
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Title: "Pick end game state",
            Options: new[]
            {
                "Return",
                "Return Ultra",
                "Covenant",
                "Covenant Ultra",
                "Accede",
                "Accede Ultra",
                "Launch",
                "Launch Ultra",
                "Marooned",
                "Marooned Ultra",
            });

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("エンドゲーム状態を選ぶ"));
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionOptions,
                Is.EqualTo(new[]
                {
                    "帰還",
                    "帰還（究極）",
                    "契約",
                    "契約（究極）",
                    "同意",
                    "同意（究極）",
                    "発射",
                    "発射（究極）",
                    "遭難",
                    "遭難（究極）",
                }));
        });
    }

    [Test]
    public void Prefix_PreservesInventoryActionMenuOptions_ForTutorialCommandGuards()
    {
        WriteDictionary(("get", "取得"), ("equip (auto)", "装備（自動）"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Options: new[] { "get", "equip (auto)" },
            PopupID: "InventoryActionMenu:ABC123");

        Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "get", "equip (auto)" }));
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_TranslatesHotkeyLabelWithoutChangingMenuData()
    {
        WriteQudMenuItemDictionary(("get", "QudMenuItem", "拾う"));

        var translated = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[g]}} {{y|get}}");

        Assert.That(translated, Is.EqualTo("{{W|[g]}} {{y|拾う}}"));
    }

    [Test]
    public void TranslateMenuItemTextForDisplay_FlattensAndTranslatesNestedBottomContextHotkeyLabel()
    {
        WriteCommonMenuActionDictionary(("back", "戻る"));

        var translated = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "{{y|{{W|[Esc]}} Back}}");

        Assert.That(translated, Is.EqualTo("{{W|[Esc]}} {{y|戻る}}"));
    }

    [Test]
    public void SelectableTextMenuItemPostfix_TranslatesDisplayOnlyAndPreservesControlIdData()
    {
        WriteCommonMenuActionDictionary(("get", "取る"));
        var menuData = new DummySelectableMenuData(
            text: "{{W|[g]}} {{y|get}}",
            simpleText: "get",
            command: "CmdGet",
            hotkey: "g");
        var target = new DummySelectableTextMenuItemTarget(menuData);

        SelectableTextMenuItemTranslationPatch.Postfix(target, newState: true);

        Assert.Multiple(() =>
        {
            Assert.That(menuData.text, Is.EqualTo("{{W|[g]}} {{y|get}}"));
            Assert.That(menuData.simpleText, Is.EqualTo("get"));
            Assert.That(menuData.command, Is.EqualTo("CmdGet"));
            Assert.That(menuData.hotkey, Is.EqualTo("g"));
            Assert.That(target.ControlId, Is.EqualTo("QudTextMenuItem:get"));
            Assert.That(target.item.Text, Is.EqualTo("{{W|{{W|[g]}} {{y|取る}}}}"));
        });
    }

    [Test]
    public void BottomContextDisplayTranslation_ReappliesWhenPopupRouteChangesWithoutItemTextChange()
    {
        WriteQudMenuItemDictionary(("remove", "QudMenuItem", "一般の外す"));
        WriteInventoryActionDictionary(("remove", "XRL.World.IInventoryActionsEvent", "装備から外す"));
        var menuData = new DummySelectableMenuData(
            text: "{{W|[r]}} {{y|remove}}",
            simpleText: "remove",
            command: "CmdRemove",
            hotkey: "r");
        var target = new DummySelectableTextMenuItemTarget(menuData)
        {
            selected = true,
        };

        SelectableTextMenuItemTranslationPatch.ApplyDisplayTranslationIfChanged(
            target,
            selected: true,
            popupIdOverride: null);
        QudMenuBottomContextTranslationPatch.ApplyButtonDisplayTranslations(
            new DummyQudMenuBottomContext(target),
            popupIdOverride: "InventoryActionMenu:ABC123");

        Assert.Multiple(() =>
        {
            Assert.That(menuData.text, Is.EqualTo("{{W|[r]}} {{y|remove}}"));
            Assert.That(target.item.Text, Is.EqualTo("{{W|{{W|[r]}} {{y|装備から外す}}}}"));
            Assert.That(target.item.SetTextCallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void TranslateMenuItemTextForDisplay_CachesRepeatedSourceAndPopupId()
    {
        WriteCommonMenuActionDictionary(("get", "取る"));

        var first = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "{{W|[g]}} {{y|get}}",
            "InventoryActionMenu:test");
        var second = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "{{W|[g]}} {{y|get}}",
            "InventoryActionMenu:test");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("{{W|[g]}} {{y|取る}}"));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(SelectableTextMenuItemTranslationPatch.GetDisplayTranslationCacheCountForTests(), Is.EqualTo(1));
        });
    }

    [Test]
    public void TranslateMenuItemTextForDisplay_CacheSeparatesPopupIds()
    {
        WriteCommonMenuActionDictionary(("get", "取る"));

        var first = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "{{W|[g]}} {{y|get}}",
            "InventoryActionMenu:a");
        var second = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "{{W|[g]}} {{y|get}}",
            "InventoryActionMenu:b");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("{{W|[g]}} {{y|取る}}"));
            Assert.That(second, Is.EqualTo("{{W|[g]}} {{y|取る}}"));
            Assert.That(SelectableTextMenuItemTranslationPatch.GetDisplayTranslationCacheCountForTests(), Is.EqualTo(2));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_UsesInventoryActionOwnerDictionaryForInventoryActionMenu()
    {
        WriteDictionary(
            ("remove", "GLOBAL-REMOVE-POISON"),
            ("drop", "GLOBAL-DROP-POISON"),
            ("detonate", "GLOBAL-DETONATE-POISON"));
        WriteQudMenuItemDictionary(
            ("remove", "QudMenuItem", "QUD-MENU-REMOVE-POISON"),
            ("drop", "QudMenuItem", "QUD-MENU-DROP-POISON"),
            ("detonate", "QudMenuItem", "QUD-MENU-DETONATE-POISON"));
        WriteInventoryActionDictionary(
            ("equip (manual)", "XRL.World.IInventoryActionsEvent", "手動で装備"),
            ("fight fire", "XRL.World.IInventoryActionsEvent", "消火する"),
            ("mark important", "XRL.World.IInventoryActionsEvent", "重要にする"),
            ("add notes", "XRL.World.IInventoryActionsEvent", "メモを追加"),
            ("remove", "XRL.World.IInventoryActionsEvent", "外す"),
            ("drop", "XRL.World.IInventoryActionsEvent", "落とす"),
            ("detonate", "XRL.World.IInventoryActionsEvent", "起爆する"),
            ("seal", "XRL.World.IInventoryActionsEvent", "封をする"),
            ("unseal", "XRL.World.IInventoryActionsEvent", "封を解く"),
            ("pray", "XRL.World.IInventoryActionsEvent", "祈る"),
            ("desecrate", "XRL.World.IInventoryActionsEvent", "冒涜する"),
            ("deactivate", "XRL.World.IInventoryActionsEvent", "停止する"),
            ("repair", "XRL.World.IInventoryActionsEvent", "修理する"),
            ("recharge", "XRL.World.IInventoryActionsEvent", "充電する"),
            ("drink charge", "XRL.World.IInventoryActionsEvent", "チャージを飲む"),
            ("treat these as scrap", "XRL.World.IInventoryActionsEvent", "スクラップ扱いにする"),
            ("stop treating these as scrap", "XRL.World.IInventoryActionsEvent", "スクラップ扱いをやめる"),
            ("disassemble all", "XRL.World.IInventoryActionsEvent", "すべて分解"),
            ("clean", "XRL.World.IInventoryActionsEvent", "掃除する"),
            ("show internals", "XRL.World.IInventoryActionsEvent", "内部構造を表示"),
            ("mod with tinkering", "XRL.World.IInventoryActionsEvent", "工作で改造"),
            ("drop all", "XRL.World.IInventoryActionsEvent", "すべて落とす"),
            ("replace cell", "XRL.World.IInventoryActionsEvent", "セルを交換"),
            ("install cell", "XRL.World.IInventoryActionsEvent", "セルを装着"),
            ("direct to attack target", "XRL.World.IInventoryActionsEvent", "攻撃対象を指示"),
            ("direct to engage aggressively", "XRL.World.IInventoryActionsEvent", "攻撃的に交戦させる"),
            ("direct to engage defensively only", "XRL.World.IInventoryActionsEvent", "防御的に交戦させる"),
            ("direct to come along", "XRL.World.IInventoryActionsEvent", "ついて来させる"),
            ("give items", "XRL.World.IInventoryActionsEvent", "アイテムを渡してもらう"),
            ("direct to move", "XRL.World.IInventoryActionsEvent", "移動を指示"),
            ("direct to change follow distance", "XRL.World.IInventoryActionsEvent", "追従距離を変更"),
            ("direct to stay there", "XRL.World.IInventoryActionsEvent", "その場で待機させる"),
            ("direct ability use", "XRL.World.IInventoryActionsEvent", "能力使用設定を変更"),
            ("rename", "XRL.World.IInventoryActionsEvent", "名前を変更"),
            ("untarget", "XRL.World.IInventoryActionsEvent", "ターゲット解除"),
            ("telekinetically pull toward you", "XRL.World.IInventoryActionsEvent", "念動力で引き寄せる"),
            ("telekinetically pull one toward you", "XRL.World.IInventoryActionsEvent", "念動力で1つ引き寄せる"),
            ("telekinetically pull towards you and take", "XRL.World.IInventoryActionsEvent", "念動力で引き寄せて拾う"),
            ("telekinetically pull one towards you and take", "XRL.World.IInventoryActionsEvent", "念動力で1つ引き寄せて拾う"),
            ("telekinetically hurl", "XRL.World.IInventoryActionsEvent", "念動力で投げる"),
            ("telekinetically move", "XRL.World.IInventoryActionsEvent", "念動力で移動"),
            ("telekinetically hurl one", "XRL.World.IInventoryActionsEvent", "念動力で1つ投げる"),
            ("telekinetically move one", "XRL.World.IInventoryActionsEvent", "念動力で1つ移動"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[E]}} {{y|Equip (manual)}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[E]}} {{y|手動で装備}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[E]}} {{y|equip (manual)}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[E]}} {{y|手動で装備}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[f]}} {{y|{{hotkey|f}}ight fire}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[f]}} {{y|消火する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[i]}} {{y|mark important}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[i]}} {{y|重要にする}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[n]}} {{y|add notes}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[n]}} {{y|メモを追加}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[r]}} {{y|remove}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[r]}} {{y|外す}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[d]}} {{y|{{hotkey|d}}rop}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[d]}} {{y|落とす}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[n]}} {{y|deto{{hotkey|n}}ate}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[n]}} {{y|起爆する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[s]}} {{y|{{hotkey|s}}eal}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[s]}} {{y|封をする}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[u]}} {{y|{{hotkey|u}}nseal}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[u]}} {{y|封を解く}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[y]}} {{y|pra{{hotkey|y}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[y]}} {{y|祈る}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[D]}} {{y|{{hotkey|D}}esecrate}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[D]}} {{y|冒涜する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|de{{hotkey|a}}ctivate}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|停止する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[r]}} {{y|{{hotkey|r}}epair}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[r]}} {{y|修理する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[R]}} {{y|{{hotkey|R}}echarge}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[R]}} {{y|充電する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[R]}} {{y|{{hotkey|R}}echarge ケムセル}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[R]}} {{y|ケムセルを充電する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[k]}} {{y|drin{{hotkey|k}} charge}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[k]}} {{y|チャージを飲む}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[S]}} {{y|treat these as {{hotkey|S}}crap}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[S]}} {{y|スクラップ扱いにする}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[D]}} {{y|drop all}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[D]}} {{y|すべて落とす}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "    {{y|disassemble all}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("    {{y|すべて分解}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|clean}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|掃除する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[n]}} {{y|clea{{hotkey|n}} all your items [1 dram]}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[n]}} {{y|手持ちのアイテムをすべて洗う [{{rules|1}}ドラム]}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[W]}} {{y|show internals}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[W]}} {{y|内部構造を表示}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[t]}} {{y|mod with tinkering}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[t]}} {{y|工作で改造}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[c]}} {{y|replace cell}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[c]}} {{y|セルを交換}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[c]}} {{y|install cell}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[c]}} {{y|セルを装着}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|direct to {{hotkey|a}}ttack target}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|攻撃対象を指示}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[e]}} {{y|direct to engage aggressively}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[e]}} {{y|攻撃的に交戦させる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[e]}} {{y|direct to engage defensively only}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[e]}} {{y|防御的に交戦させる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[c]}} {{y|direct to come along}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[c]}} {{y|ついて来させる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[g]}} {{y|give items}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[g]}} {{y|アイテムを渡してもらう}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[m]}} {{y|direct to move}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[m]}} {{y|移動を指示}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[f]}} {{y|direct to change follow distance}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[f]}} {{y|追従距離を変更}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[f]}} {{y|direct to change {{hotkey|f}}ollow distance}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[f]}} {{y|追従距離を変更}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[s]}} {{y|direct to stay there}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[s]}} {{y|その場で待機させる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[u]}} {{y|direct ability use}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[u]}} {{y|能力使用設定を変更}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[u]}} {{y|direct ability {{hotkey|u}}se}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[u]}} {{y|能力使用設定を変更}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[r]}} {{y|rename}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[r]}} {{y|名前を変更}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[t]}} {{y|untarget}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[t]}} {{y|ターゲット解除}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[p]}} {{y|telekinetically pull toward you}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[p]}} {{y|念動力で引き寄せる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[P]}} {{y|telekinetically pull one toward you}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[P]}} {{y|念動力で1つ引き寄せる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[t]}} {{y|telekinetically pull towards you and take}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[t]}} {{y|念動力で引き寄せて拾う}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[T]}} {{y|telekinetically pull one towards you and take}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[T]}} {{y|念動力で1つ引き寄せて拾う}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[h]}} {{y|telekinetically hurl}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[h]}} {{y|念動力で投げる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[m]}} {{y|telekinetically move}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[m]}} {{y|念動力で移動}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[H]}} {{y|telekinetically hurl one}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[H]}} {{y|念動力で1つ投げる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[M]}} {{y|telekinetically move one}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[M]}} {{y|念動力で1つ移動}}"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_TranslatesCampfireCookingInventoryActionMenuRows()
    {
        WriteInventoryActionDictionary(
            ("Eat fresh apple matz.", "XRL.World.IInventoryActionsEvent", "新鮮なアップルマッツァを食べる。"),
            ("Drink mulled mushroom cider.", "XRL.World.IInventoryActionsEvent", "温めたマッシュルームサイダーを飲む。"),
            ("Eat goat in sweet leaf.", "XRL.World.IInventoryActionsEvent", "甘葉包みのヤギ肉を食べる。"),
            ("Eat some Tongue and Cheek.", "XRL.World.IInventoryActionsEvent", "タングアンドチークを食べる。"),
            ("Eat bone babka.", "XRL.World.IInventoryActionsEvent", "ボーンバブカを食べる。"),
            ("Eat some Hot and Spiny.", "XRL.World.IInventoryActionsEvent", "ホットアンドスパイニーを食べる。"),
            ("Eat mah lah soup.", "XRL.World.IInventoryActionsEvent", "マーラースープを食べる。"),
            ("Eat the Porridge.", "XRL.World.IInventoryActionsEvent", "粥を食べる。"),
            ("Whip up a meal.", "XRL.World.IInventoryActionsEvent", "手早く食事を作る。"),
            ("Choose ingredients to cook with.", "XRL.World.IInventoryActionsEvent", "料理に使う材料を選ぶ。"),
            ("Cook from a recipe.", "XRL.World.IInventoryActionsEvent", "レシピから料理する。"),
            ("Preserve your fresh foods.", "XRL.World.IInventoryActionsEvent", "新鮮な食材を保存食にする。"),
            ("Preserve your exotic foods.", "XRL.World.IInventoryActionsEvent", "珍味を保存食にする。"),
            ("Stop bleeding.", "XRL.World.IInventoryActionsEvent", "出血を止める。"),
            ("Treat poison.", "XRL.World.IInventoryActionsEvent", "毒を治療する。"),
            ("Treat illness.", "XRL.World.IInventoryActionsEvent", "病気を治療する。"),
            ("Treat disease onset.", "XRL.World.IInventoryActionsEvent", "発症前の病を治療する。"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat fresh apple matz.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|新鮮なアップルマッツァを食べる。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[m]}} {{y|Whip up a meal.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[m]}} {{y|手早く食事を作る。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Drink mulled mushroom cider.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|温めたマッシュルームサイダーを飲む。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat goat in sweet leaf.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|甘葉包みのヤギ肉を食べる。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat some Tongue and Cheek.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|タングアンドチークを食べる。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat bone babka.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|ボーンバブカを食べる。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat some Hot and Spiny.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|ホットアンドスパイニーを食べる。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat mah lah soup.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|マーラースープを食べる。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat the Porridge.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|粥を食べる。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[i]}} {{y|Choose ingredients to cook with.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[i]}} {{y|料理に使う材料を選ぶ。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[r]}} {{K|Cook from a recipe.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[r]}} {{K|レシピから料理する。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[r]}} {{y|&KCook from a recipe.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[r]}} {{y|&Kレシピから料理する。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[f]}} {{K|Preserve your fresh foods.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[f]}} {{K|新鮮な食材を保存食にする。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[f]}} {{y|&KPreserve your fresh foods.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[f]}} {{y|&K新鮮な食材を保存食にする。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[x]}} {{y|Preserve your exotic foods.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[x]}} {{y|珍味を保存食にする。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[b]}} {{y|Stop bleeding.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[b]}} {{y|出血を止める。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[p]}} {{y|Treat poison.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[p]}} {{y|毒を治療する。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[l]}} {{y|Treat illness.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[l]}} {{y|病気を治療する。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[d]}} {{y|Treat disease onset.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[d]}} {{y|発症前の病を治療する。}}"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_TranslatesDynamicEatRecipeInventoryActionMenuRows()
    {
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        ScopedDictionaryLookup.ResetForTests();

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat {{Y|Velvety Porridge}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|{{Y|なめらか粥}}を食べる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat {{Y|なめらか粥}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|{{Y|なめらか粥}}を食べる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat {{Y|クダング's Jewel}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|Eat {{Y|クダング's Jewel}}}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat Apple Matz.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|アップルマッツァを食べる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat {{Y|Apple Matz}}.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|{{Y|アップルマッツァ}}を食べる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|Eat {{Y|Uncatalogued Feast}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|Eat {{Y|Uncatalogued Feast}}}}"));
        });
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_DoesNotUseInventoryActionOwnerDictionaryWithoutInventoryActionMenuRoute()
    {
        WriteInventoryActionDictionary(("remove", "XRL.World.IInventoryActionsEvent", "外す"));

        var translated = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[r]}} {{y|remove}}");

        Assert.That(translated, Is.EqualTo("{{W|[r]}} {{y|remove}}"));
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_UsesCampfireActionOwnerDictionaryForEmbeddedHotkeyRows()
    {
        WriteInventoryActionDictionary(
            ("Whip up a meal.", "XRL.World.IInventoryActionsEvent", "手早く食事を作る。"),
            ("Choose ingredients to cook with.", "XRL.World.IInventoryActionsEvent", "料理に使う材料を選ぶ。"),
            ("Cook from a recipe.", "XRL.World.IInventoryActionsEvent", "レシピから料理する。"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "Whip up a {{hotkey|m}}eal.",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("手早く食事を作る。"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[i]}} {{y|Choose {{hotkey|i}}ngredients to cook with.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[i]}} {{y|料理に使う材料を選ぶ。}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[r]}} {{K|Cook f{{hotkey|r}}om a recipe.}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[r]}} {{K|レシピから料理する。}}"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_DoesNotFallbackToQudMenuItemDictionaryInsideInventoryActionMenu()
    {
        WriteCommonMenuActionDictionary(("get", "拾う"));
        WriteQudMenuItemDictionary(("get", "QudMenuItem", "拾う"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[g]}} {{y|get}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[g]}} {{y|拾う}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "    {{y|get}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("    {{y|拾う}}"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_DoesNotFallbackToUnscopedInventoryActionDictionaryEntries()
    {
        WriteInventoryActionDictionaryContents(
            "{\"entries\":[" +
            "{\"key\":\"get\",\"text\":\"UNSCOPED-GET-POISON\"}" +
            "]}\n");

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[g]}} {{y|get}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[g]}} {{y|get}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "    {{y|get}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("    {{y|get}}"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_UsesCommonMenuActionDictionaryAcrossRoutes()
    {
        WriteCommonMenuActionDictionary(
            ("attack", "攻撃"),
            ("chat", "話す"),
            ("Close Menu", "メニューを閉じる"),
            ("collect liquid", "液体を採取"),
            ("get", "拾う"),
            ("look", "見る"),
            ("examine", "調べる"),
            ("open", "開ける"),
            ("drop", "落とす"),
            ("eat", "食べる"),
            ("fill", "満たす"),
            ("pour", "注ぐ"),
            ("apply", "使用する"),
            ("read", "読む"),
            ("target", "狙う"),
            ("show effects", "効果を表示"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[g]}} {{y|get}}"),
                Is.EqualTo("{{W|[g]}} {{y|拾う}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[h]}} {{y|chat}}"),
                Is.EqualTo("{{W|[h]}} {{y|話す}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[k]}} {{y|attack}}"),
                Is.EqualTo("{{W|[k]}} {{y|攻撃}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[l]}} {{y|look}}"),
                Is.EqualTo("{{W|[l]}} {{y|見る}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[x]}} {{y|examine}}"),
                Is.EqualTo("{{W|[x]}} {{y|調べる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[c]}} {{y|collect liquid}}"),
                Is.EqualTo("{{W|[c]}} {{y|液体を採取}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[Esc]}} {{y|Close Menu}}"),
                Is.EqualTo("{{W|[Esc]}} {{y|メニューを閉じる}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[g]}} {{y|get}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[g]}} {{y|拾う}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[o]}} {{y|open}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[o]}} {{y|開ける}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[d]}} {{y|drop}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[d]}} {{y|落とす}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[p]}} {{y|pour}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[p]}} {{y|注ぐ}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[a]}} {{y|{{hotkey|a}}pply}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[a]}} {{y|使用する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[w]}} {{y|show effects}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[w]}} {{y|効果を表示}}"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_CommonMenuActionDictionaryDoesNotLeakToGlobalTranslator()
    {
        WriteCommonMenuActionDictionary(("get", "拾う"));

        Assert.Multiple(() =>
        {
            Assert.That(Translator.Translate("get"), Is.EqualTo("get"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[g]}} {{y|get}}"),
                Is.EqualTo("{{W|[g]}} {{y|拾う}}"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_RouteSpecificDictionaryOverridesCommonMenuActions()
    {
        WriteCommonMenuActionDictionary(("remove", "COMMON-REMOVE-POISON"));
        WriteQudMenuItemDictionary(("remove", "QudMenuItem", "QUD-REMOVE"));
        WriteInventoryActionDictionary(("remove", "XRL.World.IInventoryActionsEvent", "外す"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[r]}} {{y|remove}}"),
                Is.EqualTo("{{W|[r]}} {{y|QUD-REMOVE}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[r]}} {{y|remove}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[r]}} {{y|外す}}"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_UsesInventoryActionOwnerDictionaryForPlainInventoryActionMenuRows()
    {
        WriteDictionary(("remove", "GLOBAL-REMOVE-POISON"));
        WriteQudMenuItemDictionary(("remove", "QudMenuItem", "QUD-MENU-REMOVE-POISON"));
        WriteInventoryActionDictionary(("remove", "XRL.World.IInventoryActionsEvent", "外す"));

        var translated = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "    {{y|remove}}",
            "InventoryActionMenu:ABC123");

        Assert.That(translated, Is.EqualTo("    {{y|外す}}"));
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_TranslatesRechargeWithCellNamePattern()
    {
        WriteDisplayNameDictionary(("chem cell", "ケムセル"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[R]}} {{y|{{hotkey|R}}echarge {{c|ケムセル}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[R]}} {{y|{{c|ケムセル}}を充電する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[r]}} {{y|{{hotkey|r}}echarge {{c|ケムセル}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[r]}} {{y|{{c|ケムセル}}を充電する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "{{W|[R]}} {{y|{{hotkey|R}}echarge {{c|chem cell}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("{{W|[R]}} {{y|{{c|ケムセル}}を充電する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "    {{y|Recharge {{c|chem cell}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("    {{y|{{c|ケムセル}}を充電する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "    {{y|recharge {{c|chem cell}}}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("    {{y|{{c|ケムセル}}を充電する}}"));
            Assert.That(
                SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                    "    {{y|Recharge &bchem cell}}",
                    "InventoryActionMenu:ABC123"),
                Is.EqualTo("    &bケムセル&yを充電する"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_DoesNotDuplicateDisabledColorWhenTranslationOwnsColor()
    {
        WriteInventoryActionDictionary(("remove", "XRL.World.IInventoryActionsEvent", "&K外す"));

        var translated = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "    &Kremove",
            "InventoryActionMenu:ABC123");

        Assert.That(translated, Is.EqualTo("    &K外す"));
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_RestoresDisabledColorWhenOtherColorRemainsAtSamePosition()
    {
        WriteInventoryActionDictionary(
            ("remove", "XRL.World.IInventoryActionsEvent", "&G外す"),
            ("drop", "XRL.World.IInventoryActionsEvent", "^r落とす"));

        var foreground = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "    &Kremove",
            "InventoryActionMenu:ABC123");
        var background = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "    &Kdrop",
            "InventoryActionMenu:ABC123");

        Assert.Multiple(() =>
        {
            Assert.That(foreground, Is.EqualTo("    &K&G外す"));
            Assert.That(background, Is.EqualTo("    &K^r落とす"));
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_UsesInventoryActionOwnerDictionaryForEmbeddedHotkeyInventoryActionMenuRows()
    {
        WriteDictionary(("mark important", "GLOBAL-MARK-POISON"));
        WriteQudMenuItemDictionary(("mark important", "QudMenuItem", "QUD-MENU-MARK-POISON"));
        WriteInventoryActionDictionary(("mark important", "XRL.World.IInventoryActionsEvent", "重要にする"));

        var translated = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
            "{{W|[i]}} {{y|mark {{hotkey|i}}mportant}}",
            "InventoryActionMenu:ABC123");

        Assert.That(translated, Is.EqualTo("{{W|[i]}} {{y|重要にする}}"));
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_StripsDirectMarkerWithoutRetranslatingInventoryActionRows()
    {
        WriteDictionary(("mark important", "GLOBAL-MARK-POISON"));
        WriteQudMenuItemDictionary(("mark important", "QudMenuItem", "QUD-MENU-MARK-POISON"));
        WriteInventoryActionDictionary(("mark important", "XRL.World.IInventoryActionsEvent", "重要にする"));

        Assert.Multiple(() =>
        {
            var embeddedHotkey = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                "{{W|[i]}} {{y|\x01mark {{hotkey|i}}mportant}}",
                "InventoryActionMenu:ABC123");
            Assert.That(embeddedHotkey, Is.EqualTo("{{W|[i]}} {{y|mark {{hotkey|i}}mportant}}"));
            Assert.That(embeddedHotkey.IndexOf(MessageFrameTranslator.DirectTranslationMarker), Is.EqualTo(-1));

            var exact = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                "\x01mark important",
                "InventoryActionMenu:ABC123");
            Assert.That(exact, Is.EqualTo("mark important"));

            var fallback = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                "\x01unknown action",
                "InventoryActionMenu:ABC123");
            Assert.That(fallback, Is.EqualTo("unknown action"));

            var empty = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
                MessageFrameTranslator.MarkDirectTranslation(string.Empty),
                "InventoryActionMenu:ABC123");
            Assert.That(empty, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Prefix_CoercesNullPickOptionEntriesToEmptyStrings()
    {
        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Options: new string[] { null!, "Continue" });

        Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { string.Empty, "Continue" }));
    }

    [Test]
    public void Prefix_TranslatesDynamicUntilTimeOfDayOption_FromCalendarLeafTranslation()
    {
        WriteDictionary(("Waxing Salt Sun", "塩の満ちる太陽"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Options: new[] { "Until Waxing Salt Sun" });

        Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "次の塩の満ちる太陽まで" }));
    }

    [Test]
    public void Prefix_TranslatesPickOptionSpacingText()
    {
        WriteDictionary(("Prompt", "案内"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(SpacingText: "Prompt");

        Assert.That(DummyPopupGenericTarget.LastPickOptionSpacingText, Is.EqualTo("案内"));
    }

    [Test]
    public void Prefix_TranslatesPickOptionButtons()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Buttons: new[] { new DummyPopupMenuItem("{{W|Cancel}}") });

        Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
        Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|キャンセル}}"));
    }

    [Test]
    public void Prefix_TranslatesReadOnlyPickOptionButtons()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        using var patch = PatchPickOption();

        var buttons = Array.AsReadOnly(new[] { new DummyPopupMenuItem("{{W|Cancel}}") });
        DummyPopupGenericTarget.PickOption(Buttons: buttons);

        Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
        Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|キャンセル}}"));
    }

    [Test]
    public void Prefix_LeavesAlreadyLocalizedTextUnchanged()
    {
        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Title: "セーブ一覧",
            Intro: "行き先を選んでください。",
            SpacingText: "案内",
            Options: new[] { "続ける", "キャンセル" },
            Buttons: new[] { new DummyPopupMenuItem("{{W|キャンセル}}") });

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("セーブ一覧"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionIntro, Is.EqualTo("行き先を選んでください。"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionSpacingText, Is.EqualTo("案内"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "続ける", "キャンセル" }));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|キャンセル}}"));
        });
    }

    [Test]
    public void Prefix_PreservesMarkupAndColorTags()
    {
        WriteDictionary(
            ("Warning!", "警告！"),
            ("Choose wisely.", "慎重に選んでください。"),
            ("Prompt", "案内"),
            ("Continue", "続ける"),
            ("Cancel", "キャンセル"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Title: "{{R|Warning!}}",
            Intro: "{{C|Choose wisely.}}",
            SpacingText: "{{K|Prompt}}",
            Options: new[] { "{{W|Continue}}" },
            Buttons: new[] { new DummyPopupMenuItem("{{W|Cancel}}") });

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("{{R|警告！}}"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionIntro, Is.EqualTo("{{C|慎重に選んでください。}}"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionSpacingText, Is.EqualTo("{{K|案内}}"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "{{W|続ける}}" }));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|キャンセル}}"));
        });
    }

    [Test]
    public void Prefix_TranslatesSiblingHotkeyOptionsConsistently()
    {
        WriteDictionary(
            ("[l] look", "[l] 見る"),
            ("[w] show effects", "[w] 効果を表示"),
            ("[n] detonate", "[n] 起爆"),
            ("Quit Without Saving", "セーブせずに終了"),
            ("[Esc] Cancel", "[Esc] キャンセル"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Options: new[]
            {
                "[l] look",
                "[w] show effects",
                "[n] detonate",
                "Quit Without Saving",
            },
            Buttons: new[] { new DummyPopupMenuItem("{{W|[Esc]}} {{y|Cancel}}") });

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionOptions,
                Is.EqualTo(new[]
                {
                    "[l] 見る",
                    "[w] 効果を表示",
                    "[n] 起爆",
                    "セーブせずに終了",
                }));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
        });
    }

    [Test]
    public void Prefix_TranslatesEmbeddedHotkeyLoadAndUnloadOptions()
    {
        WriteDictionary(("load", "GLOBAL-LOAD-POISON"), ("unload", "GLOBAL-UNLOAD-POISON"));
        WriteQudMenuItemDictionary(
            ("load", "QudMenuItem", "装填"),
            ("unload", "QudMenuItem", "装填解除"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Options: new[]
            {
                "l{{hotkey|o}}ad",
                "{{hotkey|u}}nload",
            },
            Buttons: new[]
            {
                new DummyPopupMenuItem("{{W|[o]}} {{y|l{{hotkey|o}}ad}}"),
                new DummyPopupMenuItem("{{W|[u]}} {{y|{{hotkey|u}}nload}}"),
            });

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "装填", "装填解除" }));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|[o]}} {{y|装填}}"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons[1].text, Is.EqualTo("{{W|[u]}} {{y|装填解除}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupPickOptionTranslationPatch),
                    "Popup.ProducerMenuItem.EmbeddedHotkeyLabel"),
                Is.GreaterThan(0));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupPickOptionTranslationPatch),
                    "Popup.ProducerMenuItem.HotkeyLabel"),
                Is.GreaterThan(0));
        });
    }

    private static IDisposable PatchPickOption()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Finalizer))));
        return new HarmonyPatchScope(harmony, harmonyId);
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
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "popup-pickoption.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteQudMenuItemDictionary(params (string key, string context, string text)[] entries)
    {
        var scopedDirectory = Path.Combine(dictionaryDirectory, "Scoped");
        Directory.CreateDirectory(scopedDirectory);

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
            builder.Append("\",\"context\":\"");
            builder.Append(EscapeJson(entries[index].context));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        File.WriteAllText(
            Path.Combine(scopedDirectory, "ui-popup-qud-menu-item.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteInventoryActionDictionary(params (string key, string context, string text)[] entries)
    {
        WriteScopedDictionary("ui-inventory-actions.ja.json", entries);
    }

    private void WriteCommonMenuActionDictionary(params (string key, string text)[] entries)
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
        Directory.CreateDirectory(Path.Combine(dictionaryDirectory, "Scoped"));
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "Scoped", "ui-menu-actions.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteDisplayNameDictionary(params (string key, string text)[] entries)
    {
        WriteFlatDictionary("ui-displayname-atomic.ja.json", entries);
    }

    private void WriteInventoryActionDictionaryContents(string contents)
    {
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "ui-inventory-actions.ja.json"),
            contents,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteScopedDictionary(string fileName, params (string key, string context, string text)[] entries)
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
            builder.Append("\",\"context\":\"");
            builder.Append(EscapeJson(entries[index].context));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, fileName),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteFlatDictionary(string fileName, params (string key, string text)[] entries)
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
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, fileName),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

    private sealed class DummySelectableTextMenuItemTarget
    {
        public DummySelectableTextMenuItemTarget(DummySelectableMenuData data)
        {
            this.data = data;
            itemText = data.text;
            ControlId = "QudTextMenuItem:" + data.simpleText;
        }

        public DummySelectableMenuData data { get; }

        public string itemText { get; }

        public string ControlId { get; }

        public bool selected { get; set; }

        public readonly DummySelectableItemSkin item = new();
    }

#pragma warning disable S1144
    private sealed class DummySelectableItemSkin
    {
        public string Text { get; private set; } = string.Empty;

        public int SetTextCallCount { get; private set; }

        public void SetText(string value)
        {
            SetTextCallCount++;
            Text = value;
        }
    }
#pragma warning restore S1144

    private sealed class DummyQudMenuBottomContext
    {
        public DummyQudMenuBottomContext(params object[] buttons)
        {
            this.buttons = new ArrayList(buttons);
        }

        public ArrayList buttons { get; }
    }

    private sealed class DummySelectableMenuData
    {
        public DummySelectableMenuData(string text, string simpleText, string command, string hotkey)
        {
            this.text = text;
            this.simpleText = simpleText;
            this.command = command;
            this.hotkey = hotkey;
        }

        public string text;
        public string simpleText;
        public string command;
        public string hotkey;
    }

    private sealed class HarmonyPatchScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyPatchScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
