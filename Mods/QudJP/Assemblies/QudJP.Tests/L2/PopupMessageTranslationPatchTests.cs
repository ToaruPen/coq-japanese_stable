using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupMessageTranslationPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-message-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DummyPopupMessageTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesPopupContent_WhenPatched()
    {
        WriteDictionary(
            ("Are you sure you want to delete the save game for {0}?", "{0}のセーブデータを本当に削除しますか？"),
            ("Delete {0}", "{0}を削除"),
            ("Save Slots", "セーブ一覧"),
            ("Continue", "続ける"),
            ("Accept", "承認"),
            ("Cancel", "キャンセル"));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Enter]}} {{y|Accept}}", "Accept", "Accept"),
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };
        var items = new List<DummyPopupMessageItem>
        {
            new("Continue", "Space", "Continue"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(
                "Are you sure you want to delete the save game for Yashur?",
                buttons,
                commandCallback: null,
                items: items,
                title: "Delete Yashur",
                contextTitle: "Save Slots",
                WantsSpecificPrompt: "ABANDON");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("Are you sure you want to delete the save game for Yashur?"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("Delete Yashur"));
                Assert.That(DummyPopupMessageTarget.LastContextTitle, Is.EqualTo("セーブ一覧"));
                Assert.That(DummyPopupMessageTarget.LastButtons, Is.Not.Null);
                Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Enter]}} {{y|承認}}"));
                Assert.That(DummyPopupMessageTarget.LastButtons[0].hotkey, Is.EqualTo("Accept"));
                Assert.That(DummyPopupMessageTarget.LastButtons[0].command, Is.EqualTo("Accept"));
                Assert.That(DummyPopupMessageTarget.LastButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
                Assert.That(DummyPopupMessageTarget.LastItems, Is.Not.Null);
                Assert.That(DummyPopupMessageTarget.LastItems![0].text, Is.EqualTo("続ける"));
                Assert.That(DummyPopupMessageTarget.LastWantsSpecificPrompt, Is.EqualTo("ABANDON"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_IsIdempotentForSharedButtonLists()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        var sharedButtons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            target.ShowPopup("Prompt", sharedButtons);
            target.ShowPopup("Prompt", sharedButtons);

            Assert.That(sharedButtons[0].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_FallsBackToEnglish_WhenKeyNotInDictionary()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var target = new DummyPopupMessageTarget();
            target.ShowPopup("Unknown English Text");

            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("Unknown English Text"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_SkipsRetranslation_WhenDirectTranslationMarkerPresent()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            var markedMessage = "\u0001既に翻訳済み";
            var target = new DummyPopupMessageTarget();
            target.ShowPopup(markedMessage);

            // \x01 marker is stripped by TranslatePopupTextForRoute but translation is skipped
            Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("既に翻訳済み"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesPlainHotkeyButtons_WhenPatched()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|B}} Cancel", "Cancel", "Cancel"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup("Prompt", buttons);

            Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|B}} キャンセル"));
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
            Path.Combine(dictionaryDirectory, "test.ja.json"),
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
}
