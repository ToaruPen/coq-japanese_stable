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
    public void Prefix_TranslatesPickOptionOptions()
    {
        WriteDictionary(("Continue", "続ける"), ("Cancel", "キャンセル"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Options: new[] { "Continue", "Cancel" });

        Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "続ける", "キャンセル" }));
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
            ("mark important", "XRL.World.IInventoryActionsEvent", "重要にする"),
            ("add notes", "XRL.World.IInventoryActionsEvent", "メモを追加"),
            ("remove", "XRL.World.IInventoryActionsEvent", "外す"),
            ("drop", "XRL.World.IInventoryActionsEvent", "落とす"),
            ("detonate", "XRL.World.IInventoryActionsEvent", "起爆する"));

        Assert.Multiple(() =>
        {
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
        });
    }

    [Test]
    public void SelectableTextMenuItemDisplayTranslation_DoesNotUseInventoryActionOwnerDictionaryWithoutInventoryActionMenuRoute()
    {
        WriteInventoryActionDictionary(("remove", "XRL.World.IInventoryActionsEvent", "外す"));

        var translated = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[r]}} {{y|remove}}");

        Assert.That(translated, Is.EqualTo("{{W|[r]}} {{y|remove}}"));
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
            ("look", "調べる"),
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
                Is.EqualTo("{{W|[l]}} {{y|調べる}}"));
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
            ("[l] look", "[l] 調べる"),
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
                    "[l] 調べる",
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
                    "Popup.ProducerText.EmbeddedHotkeyLabel"),
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

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
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
