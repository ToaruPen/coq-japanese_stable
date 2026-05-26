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

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
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
                Assert.That(factions.context.menuOptionDescriptions[0].Description, Is.EqualTo("Expand All"));
                Assert.That(factions.context.menuOptionDescriptions[1].Description, Is.EqualTo("Collapse All"));
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
                    Is.EqualTo(2));
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

        var path = Path.Combine(tempDirectory, "ui-menu-options.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
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
        public DummyMenuOption(string description, string? inputCommand)
        {
            Description = description;
            _ = inputCommand;
        }

        public string Description;
    }
}
