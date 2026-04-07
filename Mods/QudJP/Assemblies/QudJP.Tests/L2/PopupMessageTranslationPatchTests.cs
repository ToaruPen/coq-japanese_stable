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
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DummyPopupMessageTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

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
            ("[Enter] Accept", "[Enter] 承認"),
            ("[Esc] Cancel", "[Esc] キャンセル"));

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
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("Yashurのセーブデータを本当に削除しますか？"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("Yashurを削除"));
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
    public void Prefix_PreservesColorCodes_WhenDeleteTemplatesReorderPlaceholder()
    {
        WriteDictionary(
            ("Are you sure you want to delete the save game for {0}?", "{0}のセーブデータを本当に削除しますか？"),
            ("Delete {0}", "{0}を削除"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(
                "Are you sure you want to delete the save game for {{W|Yashur}}?",
                title: "Delete {{W|Yashur}}");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("{{W|Yashur}}のセーブデータを本当に削除しますか？"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("{{W|Yashur}}を削除"));
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
        WriteDictionary(("[Esc] Cancel", "[Esc] キャンセル"));

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
        WriteDictionary(("B Cancel", "B キャンセル"));

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

    [Test]
    public void Prefix_RecordsProducerRouteTransforms_WithoutPopupSinkObservation_WhenPatched()
    {
        WriteDictionary(("Save Slots", "セーブ一覧"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            const string source = "Save Slots";
            new DummyPopupMessageTarget().ShowPopup(source);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("セーブ一覧"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupMessageTranslationPatch),
                        "Popup.ProducerText.Exact"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(PopupTranslationPatch),
                        nameof(PopupMessageTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_TranslatesQuitPayloadAcrossOwnedFields_WhenPatched()
    {
        WriteDictionary(
            ("Are you sure you want to quit?", "本当に終了しますか？"),
            ("Quit Without Saving", "セーブせずに終了"),
            ("Game Menu", "ゲームメニュー"),
            ("[Enter] Submit", "[Enter] 送信"),
            ("[Esc] Cancel", "[Esc] キャンセル"),
            ("Continue playing", "続行する"));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Enter]}} {{y|Submit}}", "Accept", "Accept"),
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };
        var items = new List<DummyPopupMessageItem>
        {
            new("Continue playing", "Space", "Continue"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup(
                "Are you sure you want to quit?",
                buttons,
                items: items,
                title: "Quit Without Saving",
                contextTitle: "Game Menu",
                WantsSpecificPrompt: "QUIT");

            var renderedMessage = DummyPopupMessageTarget.LastMessage;
            var renderedButton = DummyPopupMessageTarget.LastButtons![0].text;
            UITextSkinTranslationPatch.Prefix(ref renderedMessage);
            UITextSkinTranslationPatch.Prefix(ref renderedButton);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("本当に終了しますか？"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("セーブせずに終了"));
                Assert.That(DummyPopupMessageTarget.LastContextTitle, Is.EqualTo("ゲームメニュー"));
                Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Enter]}} {{y|送信}}"));
                Assert.That(DummyPopupMessageTarget.LastButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
                Assert.That(DummyPopupMessageTarget.LastItems![0].text, Is.EqualTo("続行する"));
                Assert.That(DummyPopupMessageTarget.LastWantsSpecificPrompt, Is.EqualTo("QUIT"));
                Assert.That(renderedMessage, Is.EqualTo("本当に終了しますか？"));
                Assert.That(renderedButton, Is.EqualTo("{{W|[Enter]}} {{y|送信}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_RendersTranslatedBodyAtFinalOwner_WhenPatched()
    {
        WriteDictionary(("You do not have a missile weapon equipped!", "射撃武器を装備していない！"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            new DummyPopupMessageTarget().ShowPopup("You do not have a missile weapon equipped!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("射撃武器を装備していない！"));
                Assert.That(DummyPopupMessageTarget.LastRenderedBodyText, Is.EqualTo("{{y|射撃武器を装備していない！}}"));
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
