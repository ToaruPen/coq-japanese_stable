using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201StatusScreensBatch2Tests
{
    [Test]
    public void KeybindsScreenPostfix_TranslatesInputTypeAndMenuItems_WhenPatched()
    {
        WriteDictionary(
            ("Configuring Controller:", "設定中のコントローラー:"),
            ("Keyboard && Mouse", "キーボード＆マウス"),
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
            var categoryRow = screen.menuItems.OfType<DummyKeybindCategoryRowTarget>().Single();
            var dataRow = screen.menuItems.OfType<DummyKeybindDataRowTarget>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(screen.OriginalExecuted, Is.True);
                Assert.That(screen.inputTypeText.Text, Is.EqualTo("{{C|設定中のコントローラー:}} {{c|キーボード＆マウス}}"));
                Assert.That(categoryRow.CategoryId, Is.EqualTo("Basic Move / Attack"));
                Assert.That(categoryRow.CategoryDescription, Is.EqualTo("基本移動／攻撃"));
                Assert.That(dataRow.CategoryId, Is.EqualTo("Basic Move / Attack"));
                Assert.That(dataRow.KeyDescription, Is.EqualTo("近くと交互作用"));
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

    private static void ResetKeybindsScreenStaticFields()
    {
        DummyKeybindsScreenTarget.REMOVE_BIND = new DummyMenuOption("remove keybind", "CmdDelete", "Delete");
        DummyKeybindsScreenTarget.RESTORE_DEFAULTS = new DummyMenuOption("restore defaults", "Restore", "R");
    }
}
