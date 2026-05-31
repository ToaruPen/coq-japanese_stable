using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class UiMenuOptionDescriptionTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-uimenuoption-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyCharacterAttributeLineTarget.ResetMenuOptions();
        DummyAskNumberScreenTarget.ResetMenuOptions();
        DummyCharacterEffectLineTarget.ResetMenuOptions();
        DummyCharacterMutationLineTarget.ResetMenuOptions();
        DummyEquipmentLineTarget.ResetMenuOptions();
        ResetOptionsControlMenuOptions();
        ResetAdditionalStaticLineMenuOptions();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyCharacterAttributeLineTarget.ResetMenuOptions();
        DummyAskNumberScreenTarget.ResetMenuOptions();
        DummyCharacterEffectLineTarget.ResetMenuOptions();
        DummyCharacterMutationLineTarget.ResetMenuOptions();
        DummyEquipmentLineTarget.ResetMenuOptions();
        ResetOptionsControlMenuOptions();
        ResetAdditionalStaticLineMenuOptions();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesOptionsControlDescriptions_WhenOwnerRoutesRun()
    {
        WriteDictionaryFile(
            "ui-options.ja.json",
            ("Toggle Visibilty", "Qud.UI.OptionsCategoryControl.TOGGLE_OPTION.Description", "表示を切り替え"),
            ("Toggle Option", "Qud.UI.OptionsCheckboxControl.TOGGLE_OPTION.Description", "設定を切り替え"),
            ("Change Value", "Qud.UI.OptionsSliderControl.CHANGE_VALUE.Description", "値を変更"),
            ("Change Value", "Qud.UI.OptionsSliderControl.ARROWS_CHANGE_VALUE.Description", "値を変更（カーソル）"),
            ("Save", "Qud.UI.OptionsSliderControl.SAVE_VALUE.Description", "保存"),
            ("Cancel", "Qud.UI.OptionsSliderControl.CANCEL_VALUE.Description", "キャンセル"));

        WriteDictionary(("High", "高"), ("Low", "低"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPostfix(harmony, typeof(DummyOptionsCategoryControlTarget), nameof(DummyOptionsCategoryControlTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyOptionsCheckboxControlTarget), nameof(DummyOptionsCheckboxControlTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyOptionsSliderControlTarget), nameof(DummyOptionsSliderControlTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyOptionsComboBoxControlTarget), nameof(DummyOptionsComboBoxControlTarget.Render));

            new DummyOptionsCategoryControlTarget().SetupContexts(new object());
            new DummyOptionsCheckboxControlTarget().SetupContexts(new object());
            new DummyOptionsSliderControlTarget().SetupContexts(new object());

            var comboBox = new DummyOptionsComboBoxControlTarget
            {
                data = new DummyOptionsComboBoxRow(
                    new[] { "high", "low" },
                    new[] { "High", "Low" },
                    "high"),
            };
            comboBox.Render();

            Assert.Multiple(() =>
            {
                Assert.That(DummyOptionsCategoryControlTarget.TOGGLE_OPTION.Description, Is.EqualTo("表示を切り替え"));
                Assert.That(DummyOptionsCheckboxControlTarget.TOGGLE_OPTION.Description, Is.EqualTo("設定を切り替え"));
                Assert.That(DummyOptionsSliderControlTarget.CHANGE_VALUE.Description, Is.EqualTo("値を変更"));
                Assert.That(DummyOptionsSliderControlTarget.ARROWS_CHANGE_VALUE.Description, Is.EqualTo("値を変更（カーソル）"));
                Assert.That(DummyOptionsSliderControlTarget.SAVE_VALUE.Description, Is.EqualTo("保存"));
                Assert.That(DummyOptionsSliderControlTarget.CANCEL_VALUE.Description, Is.EqualTo("キャンセル"));
                Assert.That(comboBox.RenderOptions.Select(option => option.Description), Is.EqualTo(new[]
                {
                    "{{W|高}}",
                    "{{c|低}}",
                }));
                Assert.That(comboBox.optionsScroller.choices.Select(option => option.Description), Is.EqualTo(new[]
                {
                    "{{W|高}}",
                    "{{c|低}}",
                }));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.OptionsSliderControl.StaticMenuOptionDescription"),
                    Is.EqualTo(4));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.OptionsComboBoxControl.Render.DisplayOptionDescription"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesAdditionalFixedUiMenuOptionDescriptions_WhenOwnerRoutesRun()
    {
        WriteDictionary(
            ("Accept", "決定"),
            ("Cancel", "キャンセル"),
            ("navigate", "移動"),
            ("select", "選択"),
            ("Select", "選択"),
            ("Expand", "展開"),
            ("Collapse", "折りたたむ"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPostfix(harmony, typeof(DummyAskNumberScreenTarget), nameof(DummyAskNumberScreenTarget.SetupContext));
            PatchPostfix(harmony, typeof(DummySaveManagementTarget), nameof(DummySaveManagementTarget.UpdateMenuBars));
            PatchPostfix(harmony, typeof(DummyCharacterEffectLineTarget), nameof(DummyCharacterEffectLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyCharacterMutationLineTarget), nameof(DummyCharacterMutationLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyEquipmentLineTarget), nameof(DummyEquipmentLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyButtonBarButtonTarget), nameof(DummyButtonBarButtonTarget.setData));
            PatchPostfix(harmony, typeof(DummyFactionsLineTarget), nameof(DummyFactionsLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyJournalSultanStatueLineTarget), nameof(DummyJournalSultanStatueLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummySkillsAndPowersLineTarget), nameof(DummySkillsAndPowersLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyTinkeringBitsLineTarget), nameof(DummyTinkeringBitsLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyTinkeringDetailsLineTarget), nameof(DummyTinkeringDetailsLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyTinkeringLineTarget), nameof(DummyTinkeringLineTarget.SetupContexts));
            PatchPostfix(harmony, typeof(DummyTradeLineTarget), nameof(DummyTradeLineTarget.SetupContexts));

            var askNumber = new DummyAskNumberScreenTarget();
            askNumber.SetupContext();

            var saveManagement = new DummySaveManagementTarget();
            saveManagement.UpdateMenuBars();

            var effects = new DummyCharacterEffectLineTarget();
            effects.SetupContexts(new object());

            var mutations = new DummyCharacterMutationLineTarget();
            mutations.SetupContexts(new object());

            var equipment = new DummyEquipmentLineTarget();
            equipment.SetupContexts(new object());

            new DummyButtonBarButtonTarget().setData(new object());
            new DummyFactionsLineTarget().SetupContexts(new object());
            new DummyInventoryLineTarget().SetupContexts(new object());
            new DummyJournalSultanStatueLineTarget().SetupContexts(new object());
            new DummySkillsAndPowersLineTarget().SetupContexts(new object());
            new DummyTinkeringBitsLineTarget().SetupContexts(new object());
            new DummyTinkeringDetailsLineTarget().SetupContexts(new object());
            new DummyTinkeringLineTarget().SetupContexts(new object());
            new DummyTradeLineTarget().SetupContexts(new object());

            Assert.Multiple(() =>
            {
                Assert.That(DummyAskNumberScreenTarget.getItemMenuOptions[0].Description, Is.EqualTo("決定"));
                Assert.That(DummyAskNumberScreenTarget.getItemMenuOptions[1].Description, Is.EqualTo("キャンセル"));
                Assert.That(saveManagement.hotkeyBar.choices[0].Description, Is.EqualTo("移動"));
                Assert.That(saveManagement.hotkeyBar.choices[1].Description, Is.EqualTo("選択"));
                Assert.That(DummyCharacterEffectLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyCharacterEffectLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyCharacterMutationLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyCharacterMutationLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyEquipmentLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyEquipmentLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyButtonBarButtonTarget.itemOptions[0].Description, Is.EqualTo("選択"));
                Assert.That(DummyFactionsLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyFactionsLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyInventoryLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyInventoryLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyJournalSultanStatueLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyJournalSultanStatueLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummySkillsAndPowersLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummySkillsAndPowersLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyTinkeringBitsLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyTinkeringBitsLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyTinkeringDetailsLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyTinkeringDetailsLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyTinkeringLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyTinkeringLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyTradeLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyTradeLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("選択"));
                Assert.That(DummyTradeLineTarget.itemOptions[0].Description, Is.EqualTo("選択"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.AskNumberScreen.StaticMenuOptionDescription"),
                    Is.EqualTo(2));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.SaveManagement.UpdateMenuBars.MenuOptionDescription"),
                    Is.EqualTo(2));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.CharacterEffectLine.StaticMenuOptionDescription"),
                    Is.EqualTo(2));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.CharacterMutationLine.StaticMenuOptionDescription"),
                    Is.EqualTo(2));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.EquipmentLine.StaticMenuOptionDescription"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesOwnedMenuOptionDescriptions_WhenOwnerRoutesRun()
    {
        WriteDictionary(
            ("Sort Options", "並び替えオプション"),
            ("Filter", "絞り込み"),
            ("navigate", "移動"),
            ("select", "選択"),
            ("Expand All", "すべて展開"),
            ("Collapse All", "すべて折りたたむ"),
            ("Expand", "展開"),
            ("Collapse", "折りたたむ"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPostfix(harmony, typeof(DummyFactionsStatusScreenTarget), nameof(DummyFactionsStatusScreenTarget.ShowScreen));
            PatchPostfix(harmony, typeof(DummyHighScoresScreenTarget), nameof(DummyHighScoresScreenTarget.UpdateMenuBars));
            PatchPostfix(harmony, typeof(DummyKeybindsScreenTarget), nameof(DummyKeybindsScreenTarget.UpdateMenuBars));
            PatchPostfix(harmony, typeof(DummyCharacterAttributeLineTarget), nameof(DummyCharacterAttributeLineTarget.SetupContexts));

            var factions = new DummyFactionsStatusScreenTarget();
            factions.ShowScreen(new object(), new object());

            var highScores = new DummyHighScoresScreenTarget { CurrentMode = DummyHighScoresScreenTarget.Mode.Local };
            highScores.UpdateMenuBars();

            var keybinds = new DummyKeybindsScreenTarget();
            keybinds.UpdateMenuBars();

            var attributes = new DummyCharacterAttributeLineTarget();
            attributes.SetupContexts(new object());

            Assert.Multiple(() =>
            {
                Assert.That(factions.context.menuOptionDescriptions[0].Description, Is.EqualTo("すべて展開"));
                Assert.That(factions.context.menuOptionDescriptions[1].Description, Is.EqualTo("すべて折りたたむ"));
                Assert.That(factions.context.menuOptionDescriptions[2].Description, Is.EqualTo("並び替えオプション"));
                Assert.That(factions.context.menuOptionDescriptions[3].Description, Is.EqualTo("絞り込み"));
                Assert.That(highScores.hotkeyBar.choices[0].Description, Is.EqualTo("移動"));
                Assert.That(highScores.hotkeyBar.choices[1].Description, Is.EqualTo("選択"));
                Assert.That(keybinds.hotkeyBar.choices[0].Description, Is.EqualTo("移動"));
                Assert.That(keybinds.hotkeyBar.choices[1].Description, Is.EqualTo("選択"));
                Assert.That(DummyCharacterAttributeLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyCharacterAttributeLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.FactionsStatusScreen.ShowScreen.MenuOptionDescription"),
                    Is.EqualTo(4));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.HighScoresScreen.UpdateMenuBars.MenuOptionDescription"),
                    Is.EqualTo(2));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.KeybindsScreen.UpdateMenuBars.MenuOptionDescription"),
                    Is.EqualTo(2));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(UiMenuOptionDescriptionTranslationPatch),
                        "Qud.UI.CharacterAttributeLine.StaticMenuOptionDescription"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesUnknownOwnerAndUnknownStaticDescriptionUnchanged()
    {
        WriteDictionary(
            ("navigate", "移動"),
            ("Expand", "展開"));

        var otherOwner = new DummyOtherMenuOwner();
        otherOwner.UpdateMenuBars();

        DummyCharacterAttributeLineTarget.categoryExpandOptions.Add(new DummyMenuOption("Open Details", "CmdDetails"));
        UiMenuOptionDescriptionTranslationPatch.Postfix(otherOwner);
        UiMenuOptionDescriptionTranslationPatch.Postfix(new DummyCharacterAttributeLineTarget());

        Assert.Multiple(() =>
        {
            Assert.That(otherOwner.hotkeyBar.choices[0].Description, Is.EqualTo("navigate"));
            Assert.That(DummyCharacterAttributeLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
            Assert.That(DummyCharacterAttributeLineTarget.categoryExpandOptions[1].Description, Is.EqualTo("Open Details"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(UiMenuOptionDescriptionTranslationPatch),
                    "Qud.UI.HighScoresScreen.UpdateMenuBars.MenuOptionDescription"),
                Is.EqualTo(0));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(UiMenuOptionDescriptionTranslationPatch),
                    "Qud.UI.CharacterAttributeLine.StaticMenuOptionDescription"),
                Is.EqualTo(1));
        });
    }

    private static void PatchPostfix(Harmony harmony, Type targetType, string methodName)
    {
        harmony.Patch(
            original: RequireMethod(targetType, methodName),
            postfix: new HarmonyMethod(RequireMethod(typeof(UiMenuOptionDescriptionTranslationPatch), nameof(UiMenuOptionDescriptionTranslationPatch.Postfix))));
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("ui-menu-options.ja.json", entries.Select(entry => (entry.key, context: (string?)null, entry.text)).ToArray());
    }

    private void WriteDictionaryFile(string fileName, params (string key, string? context, string text)[] entries)
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
            builder.Append('"');
            if (entries[index].context is not null)
            {
                builder.Append(",\"context\":\"");
                builder.Append(EscapeJson(entries[index].context!));
                builder.Append('"');
            }

            builder.Append(",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void ResetAdditionalStaticLineMenuOptions()
    {
        DummyButtonBarButtonTarget.ResetMenuOptions();
        DummyFactionsLineTarget.ResetMenuOptions();
        DummyInventoryLineTarget.ResetMenuOptions();
        DummyJournalSultanStatueLineTarget.ResetMenuOptions();
        DummySkillsAndPowersLineTarget.ResetMenuOptions();
        DummyTinkeringBitsLineTarget.ResetMenuOptions();
        DummyTinkeringDetailsLineTarget.ResetMenuOptions();
        DummyTinkeringLineTarget.ResetMenuOptions();
        DummyTradeLineTarget.ResetMenuOptions();
    }

    private static void ResetOptionsControlMenuOptions()
    {
        DummyOptionsCategoryControlTarget.ResetMenuOptions();
        DummyOptionsCheckboxControlTarget.ResetMenuOptions();
        DummyOptionsSliderControlTarget.ResetMenuOptions();
    }

    private sealed class DummyFactionsStatusScreenTarget
    {
        public DummyFactionsStatusScreenContext context = new DummyFactionsStatusScreenContext();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public object ShowScreen(object gameObject, object parent)
        {
            _ = gameObject;
            _ = parent;
            context.menuOptionDescriptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Expand All", "V Positive"),
                new DummyMenuOption("Collapse All", "V Negative"),
                new DummyMenuOption("Sort Options", "CmdOptions"),
                new DummyMenuOption("Filter", "CmdFilter"),
            };
            return context;
        }
    }

    private sealed class DummyFactionsStatusScreenContext
    {
        public List<DummyMenuOption> menuOptionDescriptions = new List<DummyMenuOption>();
    }

    private sealed class DummyHighScoresScreenTarget
    {
        public enum Mode
        {
            Local,
            Daily,
        }

        public DummyHotkeyBar hotkeyBar = new DummyHotkeyBar();

        public Mode CurrentMode { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpdateMenuBars()
        {
            var options = new List<DummyMenuOption>
            {
                new DummyMenuOption("navigate", "NavigationXYAxis"),
            };

            if (CurrentMode == Mode.Local)
            {
                options.Add(new DummyMenuOption("select", "Accept"));
            }

            hotkeyBar.BeforeShow(null, options);
        }
    }

    private sealed class DummyKeybindsScreenTarget
    {
        public List<DummyMenuOption> keyMenuOptions = new List<DummyMenuOption>();

        public DummyHotkeyBar hotkeyBar = new DummyHotkeyBar();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpdateMenuBars()
        {
            keyMenuOptions.Clear();
            keyMenuOptions.Add(new DummyMenuOption("navigate", "NavigationXYAxis"));
            keyMenuOptions.Add(new DummyMenuOption("select", "Accept"));
            keyMenuOptions.Add(new DummyMenuOption("restore defaults", "V Positive"));
            hotkeyBar.BeforeShow(null, keyMenuOptions);
        }
    }

    private sealed class DummyCharacterAttributeLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            categoryExpandOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Expand", "Accept"),
            };
            categoryCollapseOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Collapse", "Accept"),
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyAskNumberScreenTarget
    {
        public static List<DummyMenuOption> getItemMenuOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            getItemMenuOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Accept", "Accept"),
                new DummyMenuOption("Cancel", "Cancel"),
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContext()
        {
        }
    }

    private sealed class DummySaveManagementTarget
    {
        public DummyHotkeyBar hotkeyBar = new DummyHotkeyBar();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpdateMenuBars()
        {
            hotkeyBar.BeforeShow(
                null,
                new List<DummyMenuOption>
                {
                    new DummyMenuOption("navigate", "NavigationXYAxis"),
                    new DummyMenuOption("select", "Accept"),
                });
        }
    }

    private sealed class DummyCharacterEffectLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            categoryExpandOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Expand", "Accept"),
            };
            categoryCollapseOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Collapse", "Accept"),
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyCharacterMutationLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            categoryExpandOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Expand", "Accept"),
            };
            categoryCollapseOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Collapse", "Accept"),
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyEquipmentLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            categoryExpandOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Expand", "Accept"),
            };
            categoryCollapseOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Collapse", "Accept"),
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyButtonBarButtonTarget
    {
        public static List<DummyMenuOption> itemOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            itemOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Select", "Accept"),
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void setData(object data)
        {
            _ = data;
        }
    }

    private sealed class DummyFactionsLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            ResetExpandCollapseOptions(ref categoryExpandOptions, ref categoryCollapseOptions);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyInventoryLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            ResetExpandCollapseOptions(ref categoryExpandOptions, ref categoryCollapseOptions);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyJournalSultanStatueLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            ResetExpandCollapseOptions(ref categoryExpandOptions, ref categoryCollapseOptions);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummySkillsAndPowersLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            ResetExpandCollapseOptions(ref categoryExpandOptions, ref categoryCollapseOptions);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyTinkeringBitsLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            ResetExpandCollapseOptions(ref categoryExpandOptions, ref categoryCollapseOptions);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyTinkeringDetailsLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            ResetExpandCollapseOptions(ref categoryExpandOptions, ref categoryCollapseOptions);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyTinkeringLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            ResetExpandCollapseOptions(ref categoryExpandOptions, ref categoryCollapseOptions);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyTradeLineTarget
    {
        public static List<DummyMenuOption> categoryExpandOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> categoryCollapseOptions = new List<DummyMenuOption>();

        public static List<DummyMenuOption> itemOptions = new List<DummyMenuOption>();

        public static void ResetMenuOptions()
        {
            categoryExpandOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Expand", "Accept"),
            };
            categoryCollapseOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Select", "Accept"),
            };
            itemOptions = new List<DummyMenuOption>
            {
                new DummyMenuOption("Select", "Accept"),
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyOptionsCategoryControlTarget
    {
        public static DummyMenuOption TOGGLE_OPTION = new DummyMenuOption("Toggle Visibilty", "Accept");

        public static void ResetMenuOptions()
        {
            TOGGLE_OPTION = new DummyMenuOption("Toggle Visibilty", "Accept");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyOptionsCheckboxControlTarget
    {
        public static DummyMenuOption TOGGLE_OPTION = new DummyMenuOption("Toggle Option", "Accept");

        public static void ResetMenuOptions()
        {
            TOGGLE_OPTION = new DummyMenuOption("Toggle Option", "Accept");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyOptionsSliderControlTarget
    {
        public static DummyMenuOption CHANGE_VALUE = new DummyMenuOption("Change Value", "Accept");

        public static DummyMenuOption ARROWS_CHANGE_VALUE = new DummyMenuOption("Change Value", "NavigationXYAxis");

        public static DummyMenuOption SAVE_VALUE = new DummyMenuOption("Save", "Accept");

        public static DummyMenuOption CANCEL_VALUE = new DummyMenuOption("Cancel", "Cancel");

        public static void ResetMenuOptions()
        {
            CHANGE_VALUE = new DummyMenuOption("Change Value", "Accept");
            ARROWS_CHANGE_VALUE = new DummyMenuOption("Change Value", "NavigationXYAxis");
            SAVE_VALUE = new DummyMenuOption("Save", "Accept");
            CANCEL_VALUE = new DummyMenuOption("Cancel", "Cancel");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetupContexts(object scrollContext)
        {
            _ = scrollContext;
        }
    }

    private sealed class DummyOptionsComboBoxControlTarget
    {
        public DummyOptionsComboBoxRow data = new DummyOptionsComboBoxRow(Array.Empty<string>(), Array.Empty<string>(), string.Empty);

        public DummyOptionsScroller optionsScroller = new DummyOptionsScroller();

        public List<DummyMenuOption> RenderOptions = new List<DummyMenuOption>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Render()
        {
            RenderOptions.Clear();
            for (var index = 0; index < data.Options.Length; index++)
            {
                var selected = data.Options[index] == data.Value;
                RenderOptions.Add(new DummyMenuOption((selected ? "{{W|" : "{{c|") + data.DisplayOptions[index] + "}}", data.Options[index]));
            }

            optionsScroller.BeforeShow(null, RenderOptions);
        }
    }

    private sealed class DummyOptionsComboBoxRow
    {
        public DummyOptionsComboBoxRow(string[] options, string[] displayOptions, string value)
        {
            Options = options;
            DisplayOptions = displayOptions;
            Value = value;
        }

        public string[] Options;

        public string[] DisplayOptions;

        public string Value;
    }

    private sealed class DummyOptionsScroller
    {
        public List<DummyMenuOption> choices = new List<DummyMenuOption>();

        public void BeforeShow(object? descriptor, IEnumerable<DummyMenuOption>? selections = null)
        {
            _ = descriptor;
            choices = selections?.ToList() ?? new List<DummyMenuOption>();
        }
    }

    private static void ResetExpandCollapseOptions(
        ref List<DummyMenuOption> categoryExpandOptions,
        ref List<DummyMenuOption> categoryCollapseOptions)
    {
        categoryExpandOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Expand", "Accept"),
        };
        categoryCollapseOptions = new List<DummyMenuOption>
        {
            new DummyMenuOption("Collapse", "Accept"),
        };
    }

    private sealed class DummyOtherMenuOwner
    {
        public DummyHotkeyBar hotkeyBar = new DummyHotkeyBar();

        public void UpdateMenuBars()
        {
            hotkeyBar.BeforeShow(
                null,
                new List<DummyMenuOption>
                {
                    new DummyMenuOption("navigate", "NavigationXYAxis"),
                });
        }
    }

    private sealed class DummyHotkeyBar
    {
        public List<DummyMenuOption> choices = new List<DummyMenuOption>();

        public void BeforeShow(object? descriptor, IEnumerable<DummyMenuOption>? selections = null)
        {
            _ = descriptor;
            choices = selections?.ToList() ?? new List<DummyMenuOption>();
        }
    }

    private sealed class DummyMenuOption
    {
        public DummyMenuOption()
        {
        }

        public DummyMenuOption(string description, string? inputCommand)
        {
            Description = description;
            _ = inputCommand;
        }

        public string Description = string.Empty;
    }
}
