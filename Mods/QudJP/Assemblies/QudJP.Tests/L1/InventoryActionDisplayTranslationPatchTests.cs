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
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();

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
    public void TranslateActionTable_PreservesExactDictionaryHotkey_WhenActionKeyIsProvided()
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

        Assert.That(actions["Change Follow Distance"].Display, Is.EqualTo("{{hotkey|D}}追従距離を変更"));
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
    public void TranslateActionTable_PreservesRechargeCellHotkey_WhenActionKeyIsProvided()
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

        Assert.That(actions["RechargeSlotted"].Display, Is.EqualTo("{{hotkey|R}}ケムセルを充電する"));
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
        File.WriteAllText(
            Path.Combine(tempDirectory, fileName),
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

    private sealed class DummyInventoryAction
    {
        public string? Display { get; set; }

        public string? Command { get; set; }

        public char Key { get; set; }
    }
}
