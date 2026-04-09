using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class AbilityManagerScreenTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-ability-manager-screen-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DummyAbilityManagerScreenTarget.ResetMenuOptions();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesRowsBeforeScreenConsumesFilteredItems()
    {
        WriteDictionary(
            ("Sprint", "スプリント"),
            ("Maneuvers", "戦技"),
            ("search: ", "検索: "));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerScreenTarget), nameof(DummyAbilityManagerScreenTarget.FilterItems)),
                postfix: new HarmonyMethod(RequirePatchPostfix()));

            var screen = new DummyAbilityManagerScreenTarget();
            screen.leftSideItems.Add(new DummyAbilityManagerScreenLineData
            {
                Id = "category",
                category = "Maneuvers",
            });
            screen.leftSideItems.Add(new DummyAbilityManagerScreenLineData
            {
                Id = "ability",
                ability = new DummyAbilityManagerEntryTarget
                {
                    DisplayName = "Sprint",
                    Class = "Maneuvers",
                    Description = "素早く移動する。",
                },
            });

            screen.searchText = "Sprint";
            screen.FilterItems();

            Assert.Multiple(() =>
            {
                Assert.That(DummyAbilityManagerScreenTarget.FILTER_ITEMS.Description, Is.EqualTo("検索: {{w|Sprint}}"));
                Assert.That(screen.filteredItems[0].category, Is.EqualTo("戦技"));
                Assert.That(screen.filteredItems[1].ability?.DisplayName, Is.EqualTo("スプリント"));
                Assert.That(screen.filteredItems[1].ability?.Class, Is.EqualTo("戦技"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesHotkeyBarDescriptions_WhenUpdateMenuBarsRuns()
    {
        WriteDictionary(
            ("Close Menu", "メニューを閉じる"),
            ("navigate", "移動"),
            ("Activate Selected Ability", "選択中の能力を起動"),
            ("Toggle Sort", "並び替え切替"),
            ("sort: ", "並び替え: "),
            ("custom", "任意"),
            ("by class", "クラス別"),
            ("search", "検索"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerScreenTarget), nameof(DummyAbilityManagerScreenTarget.UpdateMenuBars)),
                postfix: new HarmonyMethod(RequirePatchPostfix()));

            var screen = new DummyAbilityManagerScreenTarget();
            screen.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(screen.hotkeyBar.choices.Select(static choice => choice.Description), Is.EqualTo(new[]
                {
                    "メニューを閉じる",
                    "移動",
                    "並び替え: {{w|任意}}/{{y|クラス別}}",
                    "選択中の能力を起動",
                    "検索",
                }));
                Assert.That(screen.hotkeyBar.choices.Select(static choice => choice.KeyDescription), Is.EqualTo(new string?[]
                {
                    null,
                    null,
                    "並び替え切替",
                    null,
                    null,
                }));
                Assert.That(screen.hotkeyBar.renderedDescriptions, Is.EqualTo(new[]
                {
                    "メニューを閉じる",
                    "移動",
                    "並び替え: {{w|任意}}/{{y|クラス別}}",
                    "選択中の能力を起動",
                    "検索",
                }));
                Assert.That(screen.hotkeyBar.renderedKeyDescriptions, Is.EqualTo(new string?[]
                {
                    null,
                    null,
                    "並び替え切替",
                    null,
                    null,
                }));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_FallsBackToEnglishForFilterAndHotkey_WhenDictionaryEntriesAreMissing()
    {
        WriteDictionary();

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerScreenTarget), nameof(DummyAbilityManagerScreenTarget.FilterItems)),
                postfix: new HarmonyMethod(RequirePatchPostfix()));
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerScreenTarget), nameof(DummyAbilityManagerScreenTarget.UpdateMenuBars)),
                postfix: new HarmonyMethod(RequirePatchPostfix()));

            var screen = new DummyAbilityManagerScreenTarget();
            screen.searchText = "Sprint";
            screen.FilterItems();
            screen.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(DummyAbilityManagerScreenTarget.FILTER_ITEMS.Description, Is.EqualTo("search: {{w|Sprint}}"));
                Assert.That(screen.hotkeyBar.choices.Select(static choice => choice.Description), Is.EqualTo(new[]
                {
                    "Close Menu",
                    "navigate",
                    "sort: {{w|custom}}/{{y|by class}}",
                    "Activate Selected Ability",
                    "search: {{w|Sprint}}",
                }));
                Assert.That(screen.hotkeyBar.choices.Select(static choice => choice.KeyDescription), Is.EqualTo(new string?[]
                {
                    null,
                    null,
                    "Toggle Sort",
                    null,
                    null,
                }));
                Assert.That(screen.hotkeyBar.renderedDescriptions, Is.EqualTo(new[]
                {
                    "Close Menu",
                    "navigate",
                    "sort: {{w|custom}}/{{y|by class}}",
                    "Activate Selected Ability",
                    "search: {{w|Sprint}}",
                }));
                Assert.That(screen.hotkeyBar.renderedKeyDescriptions, Is.EqualTo(new string?[]
                {
                    null,
                    null,
                    "Toggle Sort",
                    null,
                    null,
                }));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesHeaderAndTypePrefix_WhenHighlightChanges()
    {
        WriteDictionary(
            ("Sprint", "スプリント"),
            ("Type: ", "種別: "),
            ("Maneuvers", "戦技"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerScreenTarget), nameof(DummyAbilityManagerScreenTarget.HandleHighlightLeft)),
                postfix: new HarmonyMethod(RequirePatchPostfix()));

            var screen = new DummyAbilityManagerScreenTarget();
            screen.HandleHighlightLeft(new DummyAbilityManagerScreenLineData
            {
                Id = "ability",
                ability = new DummyAbilityManagerEntryTarget
                {
                    DisplayName = "Sprint",
                    Class = "Maneuvers",
                    Description = "素早く移動する。",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(screen.rightSideHeaderText.text, Is.EqualTo("スプリント"));
                Assert.That(screen.rightSideDescriptionArea.text, Is.EqualTo("{{y|種別: }}戦技\n\n素早く移動する。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_FallsBackToEnglish_WhenDictionaryEntriesAreMissing()
    {
        WriteDictionary(("Maneuvers", "戦技"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerScreenTarget), nameof(DummyAbilityManagerScreenTarget.HandleHighlightLeft)),
                postfix: new HarmonyMethod(RequirePatchPostfix()));

            var screen = new DummyAbilityManagerScreenTarget();
            screen.HandleHighlightLeft(new DummyAbilityManagerScreenLineData
            {
                Id = "ability",
                ability = new DummyAbilityManagerEntryTarget
                {
                    DisplayName = "Sprint",
                    Class = "Maneuvers",
                    Description = "素早く移動する。",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(screen.rightSideHeaderText.text, Is.EqualTo("Sprint"));
                Assert.That(screen.rightSideDescriptionArea.text, Is.EqualTo("{{y|Type: }}戦技\n\n素早く移動する。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesExistingDetailsPaneOnFirstPaint_WhenHighlightArgIsNull()
    {
        WriteDictionary(
            ("Sprint", "スプリント"),
            ("Type: ", "種別: "),
            ("Mutations", "変異"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityManagerScreenTarget), nameof(DummyAbilityManagerScreenTarget.HandleHighlightLeft)),
                postfix: new HarmonyMethod(RequirePatchPostfix()));

            var screen = new DummyAbilityManagerScreenTarget();
            screen.rightSideHeaderText.SetText("Sprint");
            screen.rightSideDescriptionArea.SetText("{{y|Type: }}Mutations\n\n素早く移動する。");

            screen.HandleHighlightLeft(null!);

            Assert.Multiple(() =>
            {
                Assert.That(screen.rightSideHeaderText.text, Is.EqualTo("スプリント"));
                Assert.That(screen.rightSideDescriptionArea.text, Is.EqualTo("{{y|種別: }}変異\n\n素早く移動する。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static MethodInfo RequirePatchPostfix()
    {
        return AccessTools.Method(typeof(AbilityManagerScreenTranslationPatch), nameof(AbilityManagerScreenTranslationPatch.Postfix))
            ?? throw new InvalidOperationException("AbilityManagerScreenTranslationPatch.Postfix not found.");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");

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
            Path.Combine(tempDirectory, "ability-manager-screen-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
