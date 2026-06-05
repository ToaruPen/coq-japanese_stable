using System.Text;
using QudJP;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class InventoryActionDisplayTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-inventory-action-display-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        InventoryActionMenuCloseTimingObservability.ResetForTests();
        InventoryActionDisplayTranslationPatch.SetInventoryActionKeyMappedPredicateForTests(null);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        InventoryActionMenuCloseTimingObservability.ResetForTests();
        InventoryActionDisplayTranslationPatch.SetInventoryActionKeyMappedPredicateForTests(null);

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateActionTable_TranslatesCompanionFollowDistanceDisplay()
    {
        WriteInventoryActionDictionary(("direct to change follow distance", "追従距離を変更"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Change Follow Distance"] = new()
            {
                Display = "direct to change follow distance",
                Command = "CompanionChangeFollowDistance",
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.That(actions["Change Follow Distance"].Display, Is.EqualTo("追従距離を変更"));
    }

    [Test]
    public void TranslateActionTable_DoesNotEmbedExactDictionaryHotkey_WhenActionKeyIsProvided()
    {
        WriteInventoryActionDictionary(("direct to change follow distance", "追従距離を変更"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Change Follow Distance"] = new()
            {
                Display = "direct to change follow distance",
                Command = "CompanionChangeFollowDistance",
                Key = 'D',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["Change Follow Distance"].Display, Is.EqualTo("追従距離を変更"));
            Assert.That(actions["Change Follow Distance"].Key, Is.EqualTo('D'));
        });
    }

    [Test]
    public void TranslateActionTable_TranslatesRechargeCellDisplayDynamically()
    {
        WriteDisplayNameAtomicDictionary(("chem cell", "ケムセル"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["RechargeSlotted"] = new()
            {
                Display = "recharge {{c|chem cell}}",
                Command = "RechargeEnergyCell",
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.That(actions["RechargeSlotted"].Display, Is.EqualTo("{{c|ケムセル}}を充電する"));
    }

    [Test]
    public void TranslateActionTable_DoesNotEmbedRechargeCellHotkey_WhenActionKeyIsProvided()
    {
        WriteDisplayNameAtomicDictionary(("chem cell", "ケムセル"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["RechargeSlotted"] = new()
            {
                Display = "recharge chem cell",
                Command = "RechargeEnergyCell",
                Key = 'R',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["RechargeSlotted"].Display, Is.EqualTo("ケムセルを充電する"));
            Assert.That(actions["RechargeSlotted"].Key, Is.EqualTo('R'));
        });
    }

    [Test]
    public void TranslateActionTable_TranslatesRechargeAntimatterCell_WithoutUsingCellNameAsHotkey()
    {
        WriteDisplayNameAtomicDictionary(("antimatter cell", "反物質セル"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["RechargeSlotted"] = new()
            {
                Display = "recharge antimatter cell",
                Command = "RechargeEnergyCell",
                Key = 'R',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["RechargeSlotted"].Display, Is.EqualTo("反物質セルを充電する"));
            Assert.That(actions["RechargeSlotted"].Key, Is.EqualTo('R'));
        });
    }

    [Test]
    public void TranslateActionTable_RekeysTranslatedRechargeCell_WhenNativeRechargeHotkeyCollides()
    {
        WriteDisplayNameAtomicDictionary(("antimatter cell", "反物質セル"));
        WriteInventoryActionDictionary(("repair", "修理する"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Repair"] = new()
            {
                Display = "repair",
                Command = "Repair",
                Key = 'R',
                Default = 10,
            },
            ["RechargeSlotted"] = new()
            {
                Display = "recharge antimatter cell",
                Command = "RechargeEnergyCell",
                Key = 'R',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["Repair"].Display, Is.EqualTo("修理する"));
            Assert.That(actions["Repair"].Key, Is.EqualTo('R'));
            Assert.That(actions["RechargeSlotted"].Display, Is.EqualTo("反物質セルを充電する"));
            Assert.That(actions["RechargeSlotted"].Key, Is.EqualTo('e'));
        });
    }

    [Test]
    public void TranslateActionTable_RekeysExactRechargeAction_WhenNativeRechargeHotkeyCollides()
    {
        WriteInventoryActionDictionary(("repair", "修理する"), ("recharge", "充電する"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Repair"] = new()
            {
                Display = "repair",
                Command = "Repair",
                Key = 'R',
                Default = 10,
            },
            ["Recharge"] = new()
            {
                Display = "recharge",
                Command = "RechargeCapacitor",
                Key = 'R',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["Repair"].Display, Is.EqualTo("修理する"));
            Assert.That(actions["Repair"].Key, Is.EqualTo('R'));
            Assert.That(actions["Recharge"].Display, Is.EqualTo("充電する"));
            Assert.That(actions["Recharge"].Key, Is.EqualTo('e'));
        });
    }

    [Test]
    public void TranslateActionTable_RekeysAnyTranslatedAction_WhenNativeHotkeyCollides()
    {
        WriteInventoryActionDictionary(("remove", "外す"), ("repair", "修理する"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Remove"] = new()
            {
                Display = "remove",
                Command = "Unequip",
                Key = 'r',
                Default = 10,
            },
            ["Repair"] = new()
            {
                Display = "repair",
                Command = "Repair",
                Key = 'r',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["Remove"].Display, Is.EqualTo("外す"));
            Assert.That(actions["Remove"].Key, Is.EqualTo('r'));
            Assert.That(actions["Repair"].Display, Is.EqualTo("修理する"));
            Assert.That(actions["Repair"].Key, Is.EqualTo('e'));
        });
    }

    [Test]
    public void TranslateActionTable_PreservesDistinctUpperAndLowerNativeHotkeys()
    {
        WriteInventoryActionDictionary(("drop", "落とす"), ("drop all", "すべて落とす"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Drop"] = new()
            {
                Display = "drop",
                Command = "CommandDropObject",
                Key = 'd',
            },
            ["DropAll"] = new()
            {
                Display = "drop all",
                Command = "CommandDropAllObject",
                Key = 'D',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["Drop"].Display, Is.EqualTo("落とす"));
            Assert.That(actions["Drop"].Key, Is.EqualTo('d'));
            Assert.That(actions["DropAll"].Display, Is.EqualTo("すべて落とす"));
            Assert.That(actions["DropAll"].Key, Is.EqualTo('D'));
        });
    }

    [Test]
    public void TranslateActionTable_RekeysAlreadyLocalizedDisassembleAllFromEnglishCandidates_WhenDisassembleHotkeysCollide()
    {
        WriteDisassembleActionDictionary();
        var actions = CreateDisassembleActionMenuFixture("{{hotkey|す}}べて分解", 'す');

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["DisassembleAll"].Display, Is.EqualTo("すべて分解"));
            Assert.That(actions["DisassembleAll"].Key, Is.EqualTo('i'));
        });
    }

    [Test]
    public void TranslateActionTable_RekeysAlreadyLocalizedActionsFromEnglishCandidates_WhenJapaneseHotkeysWereEmbedded()
    {
        WriteInventoryActionDictionary(
            ("drop", "落とす"),
            ("sit", "座る"),
            ("add notes", "メモを追加"),
            ("show effects", "効果を表示"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Drop"] = new()
            {
                Display = "drop",
                Command = "CommandDropObject",
                Key = 'd',
            },
            ["Sit"] = new()
            {
                Display = "sit",
                Command = "Sit",
                Key = 's',
            },
            ["AddNotes"] = new()
            {
                Display = "{{hotkey|メ}}モを追加",
                Command = "AddNotes",
                Key = 'メ',
            },
            ["ShowEffects"] = new()
            {
                Display = "{{hotkey|効}}果を表示",
                Command = "ShowEffects",
                Key = '効',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["AddNotes"].Display, Is.EqualTo("メモを追加"));
            Assert.That(actions["AddNotes"].Key, Is.EqualTo('a'));
            Assert.That(actions["ShowEffects"].Display, Is.EqualTo("効果を表示"));
            Assert.That(actions["ShowEffects"].Key, Is.EqualTo('h'));
        });
    }

    [Test]
    public void ShowInventoryActionMenuPrefix_RekeysActionsAddedAfterOwnerGetInventoryActionsEvent()
    {
        WriteDisassembleActionDictionary();
        var actions = CreateDisassembleActionMenuFixture(
            "disassemble all",
            'm',
            includeSingleDisassemble: false,
            markImportantDefault: 200);
        var state = InventoryActionMenuCloseTimingObservability.TimingScope.Empty;

        InventoryActionMenuShowTimingPatch.Prefix(actions, ref state);

        Assert.Multiple(() =>
        {
            Assert.That(actions["DisassembleAll"].Display, Is.EqualTo("すべて分解"));
            Assert.That(actions["DisassembleAll"].Key, Is.EqualTo('i'));
        });
    }

    [Test]
    public void ShowInventoryActionMenuPrefix_TranslatesFallbackRelicTitle_WhenIntroIsNull()
    {
        WriteDictionary(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            context: null,
            ("analog", "アナログの"));
        var actions = new Dictionary<string, DummyInventoryAction>();
        var owner = new DummyInventoryActionMenuItem
        {
            DisplayName = "{{Y|カムシュルクール}}",
        };
        var item = new DummyInventoryActionMenuItem
        {
            DisplayName = "{{M|Chain of the Analog Sand}} \u00040 \t0 [6ドラムのゲル]",
        };
        string? intro = null;
        var state = InventoryActionMenuCloseTimingObservability.TimingScope.Empty;

        InventoryActionMenuShowTimingPatch.Prefix(actions, ref state);
        AssertTranslateIntroPrefixBindsToShowInventoryActionMenuGoArgument();
        InventoryActionMenuShowTimingPatch.TranslateIntroPrefix(item, ref intro);

        Assert.Multiple(() =>
        {
            Assert.That(owner.DisplayName, Is.EqualTo("{{Y|カムシュルクール}}"));
            Assert.That(intro, Is.EqualTo("{{M|アナログの砂の鎖}} \u00040 \t0 [6ドラムのゲル]"));
        });
    }

    [Test]
    public void TranslateActionTable_RekeysTranslatedActions_WhenNativeHotkeysAreConsumedByMenuNavigation()
    {
        WriteInventoryActionDictionary(("mark important", "重要にする"), ("look", "見る"));
        InventoryActionDisplayTranslationPatch.SetInventoryActionKeyMappedPredicateForTests(
            key => key is 'i' or 'l');
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Mark Important"] = new()
            {
                Display = "mark important",
                Command = "MarkImportant",
                Key = 'i',
            },
            ["Look"] = new()
            {
                Display = "look",
                Command = "Look",
                Key = 'l',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["Mark Important"].Display, Is.EqualTo("重要にする"));
            Assert.That(actions["Mark Important"].Key, Is.EqualTo('m'));
            Assert.That(actions["Look"].Display, Is.EqualTo("見る"));
            Assert.That(actions["Look"].Key, Is.EqualTo('o'));
        });
    }

    [Test]
    public void TranslateActionTable_CachesMappedKeyLookupsWithinActionTable()
    {
        WriteDisplayNameAtomicDictionary(("antimatter cell", "反物質セル"));
        WriteInventoryActionDictionary(("repair", "修理する"));
        var keyCheckCounts = new Dictionary<char, int>();
        InventoryActionDisplayTranslationPatch.SetInventoryActionKeyMappedPredicateForTests(
            key =>
            {
                keyCheckCounts[key] = keyCheckCounts.TryGetValue(key, out var count) ? count + 1 : 1;
                return false;
            });
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Repair"] = new()
            {
                Display = "repair",
                Command = "Repair",
                Key = 'R',
                Default = 10,
            },
            ["RechargeSlotted"] = new()
            {
                Display = "recharge antimatter cell",
                Command = "RechargeEnergyCell",
                Key = 'R',
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.Multiple(() =>
        {
            Assert.That(actions["RechargeSlotted"].Key, Is.EqualTo('e'));
            Assert.That(keyCheckCounts['R'], Is.EqualTo(1));
            Assert.That(keyCheckCounts['e'], Is.EqualTo(1));
        });
    }

    [Test]
    public void TranslateActionTable_DoesNotUseInventoryActionDictionaryForRechargeCellCapture()
    {
        WriteInventoryActionDictionary(("chem cell", "誤った経路"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["RechargeSlotted"] = new()
            {
                Display = "recharge {{c|chem cell}}",
                Command = "RechargeEnergyCell",
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.That(actions["RechargeSlotted"].Display, Is.EqualTo("{{c|chem cell}}を充電する"));
    }

    [Test]
    public void TranslateActionTable_TranslatesTreatAsScrapDisplay()
    {
        WriteInventoryActionDictionary(("treat these as scrap", "スクラップ扱いにする"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["TreatAsScrap"] = new()
            {
                Display = "treat these as scrap",
                Command = "TreatAsScrap",
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.That(actions["TreatAsScrap"].Display, Is.EqualTo("スクラップ扱いにする"));
    }

    [Test]
    public void TranslateActionTable_LeavesUnknownDisplaysUnchanged()
    {
        WriteInventoryActionDictionary(("direct to change follow distance", "追従距離を変更"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Unknown"] = new()
            {
                Display = "unknown custom action",
                Command = "UnknownCommand",
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.That(actions["Unknown"].Display, Is.EqualTo("unknown custom action"));
    }

    [Test]
    public void TranslateActionTable_LeavesEmptyDisplayUnchanged()
    {
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Empty"] = new()
            {
                Display = "",
                Command = "SomeCommand",
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.That(actions["Empty"].Display, Is.EqualTo(""));
    }

    [Test]
    public void TranslateActionTable_StripsDirectMarker_WhenDisplayIsAlreadyTranslated()
    {
        WriteInventoryActionDictionary(("既に翻訳済み", "誤った再翻訳"));
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["AlreadyTranslated"] = new()
            {
                Display = MessageFrameTranslator.MarkDirectTranslation("既に翻訳済み"),
                Command = "AlreadyTranslated",
            },
        };

        InventoryActionDisplayTranslationPatch.TranslateActionTableForTests(actions);

        Assert.That(actions["AlreadyTranslated"].Display, Is.EqualTo("既に翻訳済み"));
    }

    private void WriteDisassembleActionDictionary()
    {
        WriteInventoryActionDictionary(
            ("drop", "落とす"),
            ("equip (auto)", "自動で装備"),
            ("equip (manual)", "手動で装備"),
            ("mark important", "重要にする"),
            ("add notes", "メモを追加"),
            ("sit", "座る"),
            ("treat these as scrap", "スクラップ扱いにする"),
            ("mod with tinkering", "工作で改造"),
            ("show effects", "効果を表示"),
            ("disassemble all", "すべて分解"));
    }

    private static Dictionary<string, DummyInventoryAction> CreateDisassembleActionMenuFixture(
        string disassembleAllDisplay,
        char disassembleAllKey,
        bool includeSingleDisassemble = true,
        int markImportantDefault = 0)
    {
        var actions = new Dictionary<string, DummyInventoryAction>
        {
            ["Drop"] = new()
            {
                Display = "drop",
                Command = "CommandDropObject",
                Key = 'd',
            },
            ["EquipAuto"] = new()
            {
                Display = "equip (auto)",
                Command = "CommandEquipObject",
                Key = 'e',
            },
            ["EquipManual"] = new()
            {
                Display = "equip (manual)",
                Command = "CommandEquipObjectManual",
                Key = 'E',
            },
            ["MarkImportant"] = new()
            {
                Display = "mark important",
                Command = "MarkImportant",
                Key = 'm',
                Default = markImportantDefault,
            },
            ["AddNotes"] = new()
            {
                Display = "add notes",
                Command = "AddNotes",
                Key = 'n',
            },
            ["Sit"] = new()
            {
                Display = "sit",
                Command = "Sit",
                Key = 's',
            },
            ["TreatAsScrap"] = new()
            {
                Display = "treat these as scrap",
                Command = "TreatAsScrap",
                Key = 'S',
            },
            ["ModWithTinkering"] = new()
            {
                Display = "mod with tinkering",
                Command = "ModWithTinkering",
                Key = 't',
            },
            ["ShowEffects"] = new()
            {
                Display = "show effects",
                Command = "ShowEffects",
                Key = 'w',
            },
            ["DisassembleAll"] = new()
            {
                Display = disassembleAllDisplay,
                Command = "DisassembleAll",
                Key = disassembleAllKey,
                Default = -1,
                Priority = -1,
            },
        };

        if (includeSingleDisassemble)
        {
            actions["Disassemble"] = new DummyInventoryAction
            {
                Display = "disassemble",
                Command = "Disassemble",
                Key = 'm',
                Default = -1,
            };
        }

        return actions;
    }

    private void WriteInventoryActionDictionary(params (string key, string text)[] entries)
    {
        WriteDictionary("ui-inventory-actions.ja.json", "XRL.World.IInventoryActionsEvent", entries);
    }

    private void WriteDisplayNameAtomicDictionary(params (string key, string text)[] entries)
    {
        WriteDictionary("ui-displayname-atomic.ja.json", context: null, entries);
    }

    private void WriteDictionary(string fileName, string? context, params (string key, string text)[] entries)
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
            if (context is not null)
            {
                builder.Append("\",\"context\":\"");
                builder.Append(EscapeJson(context));
            }

            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        var path = Path.Combine(tempDirectory, fileName);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(
            path,
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

    private static void AssertTranslateIntroPrefixBindsToShowInventoryActionMenuGoArgument()
    {
        var parameter = typeof(InventoryActionMenuShowTimingPatch)
            .GetMethod(nameof(InventoryActionMenuShowTimingPatch.TranslateIntroPrefix))!
            .GetParameters()
            .FirstOrDefault();

        Assert.That(parameter?.Name, Is.EqualTo("__2"));
    }

    private sealed class DummyInventoryAction
    {
        public string? Display { get; set; }

        public string? Command { get; set; }

        public char Key { get; set; }

        public int Default { get; set; }

        public int Priority { get; set; }
    }

    private sealed class DummyInventoryActionMenuItem
    {
        public string? DisplayName { get; set; }
    }
}
