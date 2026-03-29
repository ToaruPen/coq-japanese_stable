using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class Issue201StatusScreensBatch2Tests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-issue201-batch2-l2", Guid.NewGuid().ToString("N"));
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

    // ---------------------------------------------------------------
    // InventoryLineTranslationPatch
    // ---------------------------------------------------------------

    [Test]
    public void InventoryLinePrefix_TranslatesCategoryAndItemRows_WhenPatched()
    {
        WriteDictionary(
            ("Weapons", "武器"),
            ("items", "個"),
            ("lbs.", "ポンド"),
            ("Laser Rifle", "レーザーライフル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Prefix))));

            // Category row
            var categoryTarget = new DummyInventoryLineTarget();
            categoryTarget.setData(new DummyInventoryLineDataTarget
            {
                category = true,
                categoryName = "Weapons",
                categoryExpanded = true,
                categoryAmount = 3,
                categoryWeight = 17,
            });

            // Item row
            var itemTarget = new DummyInventoryLineTarget();
            itemTarget.setData(new DummyInventoryLineDataTarget
            {
                category = false,
                displayName = "Laser Rifle",
                go = new DummyStatusGameObject { DisplayName = "Laser Rifle", Weight = 7 },
            });

            Assert.Multiple(() =>
            {
                Assert.That(categoryTarget.categoryLabel.Text, Is.EqualTo("武器"));
                Assert.That(categoryTarget.categoryWeightText.Text, Does.Contain("3 個"));
                Assert.That(categoryTarget.categoryWeightText.Text, Does.Contain("17 ポンド"));
                Assert.That(categoryTarget.categoryExpandLabel.Text, Is.EqualTo("[-]"));
                Assert.That(itemTarget.text.Text, Is.EqualTo("レーザーライフル"));
                Assert.That(itemTarget.itemWeightText.Text, Is.EqualTo("[7 ポンド]"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.CategoryName"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.ItemName"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.WeightSummary"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.WeightLabel"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Prefix))));

            var target = new DummyInventoryLineTarget();
            target.setData(new DummyFallbackInventoryLineDataTarget());

            Assert.That(target.OriginalExecuted, Is.True);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    // ---------------------------------------------------------------
    // EquipmentLineTranslationPatch
    // ---------------------------------------------------------------

    [Test]
    public void EquipmentLinePrefix_TranslatesSlotAndItemTexts_WhenPatched()
    {
        WriteDictionary(
            ("Right Hand", "右手"),
            ("Chain Glove", "鎖の手袋"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEquipmentLineTarget), nameof(DummyEquipmentLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(EquipmentLineTranslationPatch), nameof(EquipmentLineTranslationPatch.Prefix))));

            var target = new DummyEquipmentLineTarget();
            target.setData(new DummyEquipmentLineDataTarget
            {
                bodyPart = new DummyBodyPart
                {
                    Name = "Hand",
                    Primary = true,
                    CardinalDescription = "Right Hand",
                    Equipped = new DummyStatusGameObject { DisplayName = "Chain Glove" },
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Does.Contain("右手"));
                Assert.That(target.text.Text, Does.StartWith("{{G|*}}"));
                Assert.That(target.itemText.Text, Is.EqualTo("鎖の手袋"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(EquipmentLineTranslationPatch),
                        "EquipmentLine.SlotName"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(EquipmentLineTranslationPatch),
                        "EquipmentLine.ItemName"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EquipmentLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEquipmentLineTarget), nameof(DummyEquipmentLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(EquipmentLineTranslationPatch), nameof(EquipmentLineTranslationPatch.Prefix))));

            var target = new DummyEquipmentLineTarget();
            target.setData(new DummyFallbackEquipmentLineDataTarget());

            Assert.That(target.OriginalExecuted, Is.True);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    // ---------------------------------------------------------------
    // InventoryAndEquipmentStatusScreenTranslationPatch
    // ---------------------------------------------------------------

    [Test]
    public void InventoryAndEquipmentScreenPostfix_TranslatesMenuOptionsAndWeightText_WhenPatched()
    {
        WriteDictionary(
            ("Toggle Cybernetics", "サイバネ切替"),
            ("Display Options", "表示オプション"),
            ("Set Primary Limb", "主肢設定"),
            ("Show Tooltip", "ツールチップ表示"),
            ("Quick Drop", "素早く落とす"),
            ("Quick Eat", "素早く食べる"),
            ("Quick Drink", "素早く飲む"),
            ("Quick Apply", "素早く適用"),
            ("lbs.", "ポンド"),
            ("show cybernetics", "サイバネティクス表示"),
            ("show equipment", "装備表示"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryAndEquipmentStatusScreen), nameof(DummyInventoryAndEquipmentStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), nameof(InventoryAndEquipmentStatusScreenTranslationPatch.Postfix))));

            var screen = new DummyInventoryAndEquipmentStatusScreen();
            screen.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(screen.CMD_SHOWCYBERNETICS.Description, Is.EqualTo("サイバネ切替"));
                Assert.That(screen.CMD_OPTIONS.Description, Is.EqualTo("表示オプション"));
                Assert.That(screen.SET_PRIMARY_LIMB.Description, Is.EqualTo("主肢設定"));
                Assert.That(screen.QUICK_DROP.Description, Is.EqualTo("素早く落とす"));
                Assert.That(screen.QUICK_EAT.Description, Is.EqualTo("素早く食べる"));
                Assert.That(screen.QUICK_DRINK.Description, Is.EqualTo("素早く飲む"));
                Assert.That(screen.QUICK_APPLY.Description, Is.EqualTo("素早く適用"));
                Assert.That(screen.weightText.Text, Does.Contain("ポンド"));
                Assert.That(screen.cyberneticsHotkeySkin.Text, Does.Contain("サイバネティクス表示"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryAndEquipmentScreenPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        // No dictionary entries written -- Postfix should not change anything meaningful
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryAndEquipmentStatusScreen), nameof(DummyInventoryAndEquipmentStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), nameof(InventoryAndEquipmentStatusScreenTranslationPatch.Postfix))));

            var screen = new DummyInventoryAndEquipmentStatusScreen();
            screen.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(screen.CMD_SHOWCYBERNETICS.Description, Is.EqualTo("Toggle Cybernetics"));
                Assert.That(screen.CMD_OPTIONS.Description, Is.EqualTo("Display Options"));
                Assert.That(screen.weightText.Text, Does.Contain("lbs."));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    // ---------------------------------------------------------------
    // HelpRowTranslationPatch
    // ---------------------------------------------------------------

    [Test]
    public void HelpRowPrefix_TranslatesCategoryAndHelpText_WhenPatched()
    {
        WriteDictionary(
            ("MENU", "メニュー"),
            ("Walk around freely", "自由に歩き回る"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHelpRowTarget), nameof(DummyHelpRowTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(HelpRowTranslationPatch), nameof(HelpRowTranslationPatch.Prefix))));

            var target = new DummyHelpRowTarget();
            target.setData(new DummyHelpDataRowTarget
            {
                Description = "Menu",
                HelpText = "Walk around freely",
                Collapsed = false,
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.categoryDescription.Text, Is.EqualTo("{{C|メニュー}}"));
                Assert.That(target.description.Text, Is.EqualTo("自由に歩き回る"));
                Assert.That(target.description.gameObject.Active, Is.True);
                Assert.That(target.categoryExpander.Text, Is.EqualTo("{{C|[-]}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(HelpRowTranslationPatch),
                        "HelpRow.CategoryDescription"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(HelpRowTranslationPatch),
                        "HelpRow.HelpText"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void HelpRowPrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyHelpRowTarget), nameof(DummyHelpRowTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(HelpRowTranslationPatch), nameof(HelpRowTranslationPatch.Prefix))));

            var target = new DummyHelpRowTarget();
            target.setData(new DummyFallbackHelpDataRowTarget());

            Assert.That(target.OriginalExecuted, Is.True);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    // ---------------------------------------------------------------
    // KeybindRowTranslationPatch
    // ---------------------------------------------------------------

    [Test]
    public void KeybindRowPrefix_TranslatesDataRowAndCategoryRow_WhenPatched()
    {
        WriteDictionary(
            ("Interact Nearby", "近くと交互作用"),
            ("MOVEMENT", "移動"),
            ("None", "なし"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindRowTarget), nameof(DummyKeybindRowTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(KeybindRowTranslationPatch), nameof(KeybindRowTranslationPatch.Prefix))));

            // Keybind data row (no bindings -> None)
            var dataRowTarget = new DummyKeybindRowTarget();
            dataRowTarget.setData(new DummyKeybindDataRowTarget
            {
                CategoryId = "General",
                KeyId = "InteractNearby",
                KeyDescription = "Interact Nearby",
                SearchWords = "General Interact Nearby",
            });

            // Category row
            var categoryTarget = new DummyKeybindRowTarget();
            categoryTarget.setData(new DummyKeybindCategoryRowTarget
            {
                CategoryId = "Movement",
                CategoryDescription = "Movement",
                Collapsed = true,
            });

            Assert.Multiple(() =>
            {
                Assert.That(dataRowTarget.description.Text, Is.EqualTo("{{C|近くと交互作用}}"));
                Assert.That(dataRowTarget.box1.boxText, Is.EqualTo("{{K|なし}}"));
                Assert.That(dataRowTarget.box1.forceUpdate, Is.True);
                Assert.That(dataRowTarget.bindingDisplay.Active, Is.True);
                Assert.That(dataRowTarget.categoryDisplay.Active, Is.False);
                Assert.That(categoryTarget.categoryDescription.Text, Is.EqualTo("{{C|移動}}"));
                Assert.That(categoryTarget.categoryExpander.Text, Is.EqualTo("{{C|[+]}}"));
                Assert.That(categoryTarget.categoryDisplay.Active, Is.True);
                Assert.That(categoryTarget.bindingDisplay.Active, Is.False);
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(KeybindRowTranslationPatch),
                        "KeybindRow.KeyDescription"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(KeybindRowTranslationPatch),
                        "KeybindRow.CategoryDescription"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(KeybindRowTranslationPatch),
                        "KeybindRow.NoneBinding"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void KeybindRowPrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindRowTarget), nameof(DummyKeybindRowTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(KeybindRowTranslationPatch), nameof(KeybindRowTranslationPatch.Prefix))));

            var target = new DummyKeybindRowTarget();
            target.setData(new DummyFallbackKeybindRowDataTarget());

            Assert.That(target.OriginalExecuted, Is.True);
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    // ---------------------------------------------------------------
    // KeybindsScreenTranslationPatch
    // ---------------------------------------------------------------

    [Test]
    public void KeybindsScreenPostfix_TranslatesInputTypeAndMenuItems_WhenPatched()
    {
        WriteDictionary(
            ("Configuring Controller:", "設定中のコントローラー:"),
            ("Keyboard && Mouse", "キーボード＆マウス"),
            ("General", "一般"),
            ("Interact Nearby", "近くと交互作用"),
            ("remove keybind", "キーバインド削除"),
            ("Delete", "削除"),
            ("restore defaults", "デフォルトに戻す"),
            ("R", "R"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindsScreenTarget), nameof(DummyKeybindsScreenTarget.QueryKeybinds)),
                postfix: new HarmonyMethod(RequireMethod(typeof(KeybindsScreenTranslationPatch), nameof(KeybindsScreenTranslationPatch.Postfix))));

            var screen = new DummyKeybindsScreenTarget();
            screen.QueryKeybinds();

            Assert.Multiple(() =>
            {
                Assert.That(screen.OriginalExecuted, Is.True);
                Assert.That(screen.inputTypeText.Text, Is.EqualTo("{{C|設定中のコントローラー:}} {{c|キーボード＆マウス}}"));
                Assert.That(DummyKeybindsScreenTarget.REMOVE_BIND.Description, Is.EqualTo("キーバインド削除"));
                Assert.That(DummyKeybindsScreenTarget.RESTORE_DEFAULTS.Description, Is.EqualTo("デフォルトに戻す"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(KeybindsScreenTranslationPatch),
                        "KeybindsScreen.InputType"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(KeybindsScreenTranslationPatch),
                        "KeybindsScreen.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            // Reset static fields that the postfix mutated
            ResetKeybindsScreenStaticFields();
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void KeybindsScreenPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindsScreenTarget), nameof(DummyKeybindsScreenTarget.QueryKeybinds)),
                postfix: new HarmonyMethod(RequireMethod(typeof(KeybindsScreenTranslationPatch), nameof(KeybindsScreenTranslationPatch.Postfix))));

            var screen = new DummyKeybindsScreenTarget();
            screen.QueryKeybinds();

            Assert.Multiple(() =>
            {
                Assert.That(screen.OriginalExecuted, Is.True);
                Assert.That(screen.inputTypeText.Text, Does.Contain("Keyboard"));
            });
        }
        finally
        {
            ResetKeybindsScreenStaticFields();
            harmony.UnpatchAll(harmonyId);
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static void ResetKeybindsScreenStaticFields()
    {
        DummyKeybindsScreenTarget.REMOVE_BIND = new DummyMenuOption("remove keybind", "CmdDelete", "Delete");
        DummyKeybindsScreenTarget.RESTORE_DEFAULTS = new DummyMenuOption("restore defaults", "Restore", "R");
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
        File.WriteAllText(
            Path.Combine(tempDirectory, "issue201-status-batch2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
