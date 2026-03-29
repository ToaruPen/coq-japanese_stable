using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class Issue201OtherUiBindingPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-issue201-other-ui-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        DummyBookScreenTarget.ResetStaticMenuOptions();
        BookUI.Reset();
        DummyAbilityManagerLineTarget.ResetStaticMenuOptions();
        DummyPickGameObjectLineTarget.ResetStaticMenuOptions();
        PickGameObjectScreen.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyBookScreenTarget.ResetStaticMenuOptions();
        BookUI.Reset();
        DummyAbilityManagerLineTarget.ResetStaticMenuOptions();
        DummyPickGameObjectLineTarget.ResetStaticMenuOptions();
        PickGameObjectScreen.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void BookLinePrefix_TranslatesPageText_WhenPatched()
    {
        WriteDictionary(("Page One", "1ページ目"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBookLineTarget), nameof(DummyBookLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(BookLineTranslationPatch), nameof(BookLineTranslationPatch.Prefix))));

            var target = new DummyBookLineTarget();
            target.setData(new DummyBookLineDataTarget { text = "{{K|Page One}}" });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("{{K|1ページ目}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(BookLineTranslationPatch), "Book.LineText"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void BookLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBookLineTarget), nameof(DummyBookLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(BookLineTranslationPatch), nameof(BookLineTranslationPatch.Prefix))));

            var target = new DummyBookLineTarget();
            target.setData(new DummyFallbackBookLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("book line fallback"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void BookScreenPrefix_TranslatesTitleAndMenuOptions_ForBookObjectOverload()
    {
        WriteDictionary(
            ("Codex of Leaves", "葉の宝典"),
            ("Previous Page", "前のページ"),
            ("Next Page", "次のページ"),
            ("Close book", "本を閉じる"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBookScreenTarget), nameof(DummyBookScreenTarget.showScreen), new[] { typeof(DummyBookTarget), typeof(string), typeof(Action<int>), typeof(Action<int>) }),
                prefix: new HarmonyMethod(RequireMethod(typeof(BookScreenTranslationPatch), nameof(BookScreenTranslationPatch.Prefix))));

            var target = new DummyBookScreenTarget();
            target.showScreen(new DummyBookTarget { Title = "Codex of Leaves" });

            Assert.Multiple(() =>
            {
                Assert.That(target.titleText.Text, Is.EqualTo("葉の宝典"));
                Assert.That(DummyBookScreenTarget.PREV_PAGE.Description, Is.EqualTo("前のページ"));
                Assert.That(DummyBookScreenTarget.PREV_PAGE.KeyDescription, Is.EqualTo("前のページ"));
                Assert.That(DummyBookScreenTarget.NEXT_PAGE.Description, Is.EqualTo("次のページ"));
                Assert.That(DummyBookScreenTarget.getItemMenuOptions[2].Description, Is.EqualTo("本を閉じる"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(BookScreenTranslationPatch), "BookScreen.TitleText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(BookScreenTranslationPatch), "BookScreen.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void BookScreenPrefix_TranslatesTitle_ForBookIdOverload()
    {
        WriteDictionary(("Leaf Journal", "葉の日誌"));
        BookUI.Books["leaf-journal"] = new DummyBookTarget { Title = "Leaf Journal" };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyBookScreenTarget), nameof(DummyBookScreenTarget.showScreen), new[] { typeof(string), typeof(string), typeof(Action<int>), typeof(Action<int>) }),
                prefix: new HarmonyMethod(RequireMethod(typeof(BookScreenTranslationPatch), nameof(BookScreenTranslationPatch.Prefix))));

            var target = new DummyBookScreenTarget();
            target.showScreen("leaf-journal");

            Assert.That(target.titleText.Text, Is.EqualTo("葉の日誌"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void JournalLinePrefix_TranslatesCategoryRecipeAndEntryRows_WhenPatched()
    {
        WriteDictionary(
            ("Legends", "伝承"),
            ("Blue Bananas", "青いバナナ"),
            ("Ingredients:", "材料:"),
            ("Salt, Vinegar", "塩, 酢"),
            ("Effects:", "効果:"),
            ("Restores thirst.", "喉の渇きを癒やす。"),
            ("Found a relic", "遺物を見つけた"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalLineTarget), nameof(DummyJournalLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalLineTranslationPatch), nameof(JournalLineTranslationPatch.Prefix))));

            var screen = new DummyJournalStatusScreenTarget();

            var categoryTarget = new DummyJournalLineTarget();
            categoryTarget.setData(new DummyJournalLineDataTarget
            {
                category = true,
                categoryExpanded = false,
                categoryName = "Legends",
                screen = screen,
            });

            var recipeTarget = new DummyJournalLineTarget();
            recipeTarget.setData(new DummyJournalLineDataTarget
            {
                screen = screen,
                entry = new DummyJournalRecipeNoteEntry
                {
                    Recipe = new DummyJournalRecipeTarget
                    {
                        DisplayName = "Blue Bananas",
                        Ingredients = "Salt, Vinegar",
                        Description = "Restores thirst.",
                    },
                },
            });

            var entryTarget = new DummyJournalLineTarget();
            entryTarget.setData(new DummyJournalLineDataTarget
            {
                screen = screen,
                entry = new DummyJournalObservationEntry
                {
                    Text = "Found a relic",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(categoryTarget.headerText.Text, Is.EqualTo("[+] 伝承"));
                Assert.That(recipeTarget.headerText.Text, Is.EqualTo("青いバナナ"));
                Assert.That(recipeTarget.text.Text, Does.Contain("材料:"));
                Assert.That(recipeTarget.text.Text, Does.Contain("塩, 酢"));
                Assert.That(recipeTarget.text.Text, Does.Contain("効果:"));
                Assert.That(recipeTarget.text.Text, Does.Contain("喉の渇きを癒やす。"));
                Assert.That(entryTarget.text.Text, Is.EqualTo("{{K|$}} 遺物を見つけた"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(JournalLineTranslationPatch), "JournalLine.CategoryHeader"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(JournalLineTranslationPatch), "JournalLine.RecipeBody"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(JournalLineTranslationPatch), "JournalLine.EntryText"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void JournalLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalLineTarget), nameof(DummyJournalLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(JournalLineTranslationPatch), nameof(JournalLineTranslationPatch.Prefix))));

            var target = new DummyJournalLineTarget();
            target.setData(new DummyFallbackJournalLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("journal fallback"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityManagerLinePrefix_TranslatesCategoryAbilityAndMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("Mental Mutations", "精神変異"),
            ("Force Bubble", "力場球"),
            ("Move Down", "下へ移動"),
            ("Move Up", "上へ移動"),
            ("Bind Key", "キー割り当て"),
            ("Unbind Key", "キー解除"),
            ("attack", "攻撃"),
            ("turn cooldown", "ターンのクールダウン"),
            ("Toggled on", "オン"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerLineTarget), nameof(DummyAbilityManagerLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(AbilityManagerLineTranslationPatch), nameof(AbilityManagerLineTranslationPatch.Prefix))));

            var categoryTarget = new DummyAbilityManagerLineTarget();
            categoryTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                category = "Mental Mutations",
                collapsed = false,
            });

            var abilityTarget = new DummyAbilityManagerLineTarget();
            abilityTarget.setData(new DummyAbilityManagerLineDataTarget
            {
                ability = new DummyAbilityEntryTarget
                {
                    DisplayName = "Force Bubble",
                    Cooldown = 3,
                    CooldownRounds = 3,
                    Toggleable = true,
                    ToggleState = true,
                },
                hotkeyDescription = "F",
            });

            Assert.Multiple(() =>
            {
                Assert.That(categoryTarget.text.Text, Is.EqualTo("[-] 精神変異"));
                Assert.That(abilityTarget.text.Text, Does.Contain("力場球"));
                Assert.That(abilityTarget.text.Text, Does.Contain("ターンのクールダウン"));
                Assert.That(abilityTarget.text.Text, Does.Contain("オン"));
                Assert.That(DummyAbilityManagerLineTarget.MOVE_DOWN.Description, Is.EqualTo("下へ移動"));
                Assert.That(DummyAbilityManagerLineTarget.MOVE_UP.Description, Is.EqualTo("上へ移動"));
                Assert.That(DummyAbilityManagerLineTarget.BIND_KEY.Description, Is.EqualTo("キー割り当て"));
                Assert.That(DummyAbilityManagerLineTarget.UNBIND_KEY.Description, Is.EqualTo("キー解除"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AbilityManagerLineTranslationPatch), "AbilityManagerLine.AbilityText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(AbilityManagerLineTranslationPatch), "AbilityManagerLine.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AbilityManagerLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerLineTarget), nameof(DummyAbilityManagerLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(AbilityManagerLineTranslationPatch), nameof(AbilityManagerLineTranslationPatch.Prefix))));

            var target = new DummyAbilityManagerLineTarget();
            target.setData(new DummyFallbackAbilityManagerLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("ability fallback"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PickGameObjectLinePrefix_TranslatesCategoryItemAndMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("Artifacts", "遺物"),
            ("Laser Pistol", "レーザーピストル"),
            ("owned by you", "あなたの所有"),
            ("readied", "装備中"),
            ("Expand", "展開"),
            ("Collapse", "折りたたむ"),
            ("Select", "選択"));
        PickGameObjectScreen.NotePlayerOwned = true;
        PickGameObjectScreen.ShowContext = true;

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPickGameObjectLineTarget), nameof(DummyPickGameObjectLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PickGameObjectLineTranslationPatch), nameof(PickGameObjectLineTranslationPatch.Prefix))));

            var categoryTarget = new DummyPickGameObjectLineTarget();
            categoryTarget.setData(new DummyPickGameObjectLineDataTarget
            {
                category = "Artifacts",
                collapsed = true,
            });

            var itemTarget = new DummyPickGameObjectLineTarget();
            itemTarget.setData(new DummyPickGameObjectLineDataTarget
            {
                go = new DummyPickGameObjectTargetObject
                {
                    DisplayName = "Laser Pistol",
                    OwnedByPlayer = true,
                    Weight = 7,
                    ListDisplayContext = "readied",
                },
                hotkeyDescription = "a",
            });

            Assert.Multiple(() =>
            {
                Assert.That(categoryTarget.text.Text, Is.EqualTo("[+] {{K|遺物}}"));
                Assert.That(itemTarget.text.Text, Does.Contain("レーザーピストル"));
                Assert.That(itemTarget.text.Text, Does.Contain("あなたの所有"));
                Assert.That(itemTarget.text.Text, Does.Contain("装備中"));
                Assert.That(itemTarget.rightFloatText.Text, Is.EqualTo("{{K|7#}}"));
                Assert.That(itemTarget.hotkey.Text, Is.EqualTo("{{Y|{{w|a}}}} "));
                Assert.That(DummyPickGameObjectLineTarget.categoryExpandOptions[0].Description, Is.EqualTo("展開"));
                Assert.That(DummyPickGameObjectLineTarget.categoryCollapseOptions[0].Description, Is.EqualTo("折りたたむ"));
                Assert.That(DummyPickGameObjectLineTarget.itemOptions[0].Description, Is.EqualTo("選択"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(PickGameObjectLineTranslationPatch), "PickGameObjectLine.CategoryText"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(PickGameObjectLineTranslationPatch), "PickGameObjectLine.ItemText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(PickGameObjectLineTranslationPatch), "PickGameObjectLine.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PickGameObjectLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPickGameObjectLineTarget), nameof(DummyPickGameObjectLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PickGameObjectLineTranslationPatch), nameof(PickGameObjectLineTranslationPatch.Prefix))));

            var target = new DummyPickGameObjectLineTarget();
            target.setData(new DummyFallbackPickGameObjectLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("pick fallback"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void FilterBarCategoryButtonPostfix_TranslatesAllMappingAndTooltip_WhenPatched()
    {
        WriteDictionary(("ALL", "すべて"), ("*All", "すべて"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFilterBarCategoryButtonTarget), nameof(DummyFilterBarCategoryButtonTarget.SetCategory), new[] { typeof(string), typeof(string) }),
                postfix: new HarmonyMethod(RequireMethod(typeof(FilterBarCategoryButtonTranslationPatch), nameof(FilterBarCategoryButtonTranslationPatch.Postfix))));

            var target = new DummyFilterBarCategoryButtonTarget();
            target.SetCategory("*All", null);

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("すべて"));
                Assert.That(target.tooltipText.Text, Is.EqualTo("すべて"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(FilterBarCategoryButtonTranslationPatch), "FilterBarCategoryButton.Text"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void FilterBarCategoryButtonPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFilterBarCategoryButtonTarget), nameof(DummyFilterBarCategoryButtonTarget.SetCategory), new[] { typeof(string), typeof(string) }),
                postfix: new HarmonyMethod(RequireMethod(typeof(FilterBarCategoryButtonTranslationPatch), nameof(FilterBarCategoryButtonTranslationPatch.Postfix))));

            var target = new DummyFilterBarCategoryButtonTarget();
            target.SetCategory("Artifacts", "Artifacts");

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("Artifacts"));
                Assert.That(target.tooltipText.Text, Is.EqualTo("Artifacts"));
                Assert.That(target.OriginalExecuted, Is.True);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CyberneticsTerminalScreenPostfix_TranslatesFooterAndMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("System ready", "システム準備完了"),
            ("navigate", "移動"),
            ("accept", "決定"),
            ("quit", "終了"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCyberneticsTerminalScreenTarget), nameof(DummyCyberneticsTerminalScreenTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CyberneticsTerminalScreenTranslationPatch), nameof(CyberneticsTerminalScreenTranslationPatch.Postfix))));

            var target = new DummyCyberneticsTerminalScreenTarget
            {
                FooterText = "System ready",
            };
            target.Show();

            Assert.Multiple(() =>
            {
                Assert.That(target.footerTextSkin.Text, Is.EqualTo("システム準備完了"));
                Assert.That(target.keyMenuOptions[0].Description, Is.EqualTo("移動"));
                Assert.That(target.keyMenuOptions[1].Description, Is.EqualTo("決定"));
                Assert.That(target.keyMenuOptions[2].Description, Is.EqualTo("終了"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(CyberneticsTerminalScreenTranslationPatch), "CyberneticsTerminal.FooterText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(CyberneticsTerminalScreenTranslationPatch), "CyberneticsTerminal.MenuOption"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void CyberneticsTerminalScreenPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCyberneticsTerminalScreenTarget), nameof(DummyCyberneticsTerminalScreenTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CyberneticsTerminalScreenTranslationPatch), nameof(CyberneticsTerminalScreenTranslationPatch.Postfix))));

            var target = new DummyCyberneticsTerminalScreenTarget
            {
                FooterText = "System ready",
            };
            target.Show();

            Assert.Multiple(() =>
            {
                Assert.That(target.footerTextSkin.Text, Is.EqualTo("System ready"));
                Assert.That(target.keyMenuOptions[0].Description, Is.EqualTo("navigate"));
                Assert.That(target.keyMenuOptions[1].Description, Is.EqualTo("accept"));
                Assert.That(target.keyMenuOptions[2].Description, Is.EqualTo("quit"));
                Assert.That(target.OriginalExecuted, Is.True);
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

    private static MethodInfo RequireMethod(Type type, string methodName, Type[]? parameterTypes = null)
    {
        var method = parameterTypes is null
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
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

        var path = Path.Combine(tempDirectory, "issue201-other-ui.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
