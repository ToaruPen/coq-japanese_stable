using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupAskStringTranslationPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-askstring-l2", Guid.NewGuid().ToString("N"));
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
        DummyPopupGenericTarget.Reset();
        DummyPopupMessageTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupMessageTarget.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_TranslatesAskStringPrompt()
    {
        WriteDictionary(("Name your pet.", "ペットに名前を付けてください。"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString("Name your pet.");

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("ペットに名前を付けてください。"));
    }

    [Test]
    public void Prefix_TranslatesAskStringAsyncPrompt()
    {
        WriteDictionary(("What do you call this build?", "このビルドを何と呼びますか？"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskStringAsync));

        _ = DummyPopupGenericTarget.AskStringAsync("What do you call this build?").GetAwaiter().GetResult();

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("このビルドを何と呼びますか？"));
    }

    [Test]
    public void Prefix_TranslatesFrameworkSearchPrompt()
    {
        WriteDictionary(("Enter search text", "検索テキストを入力"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskStringAsync));

        _ = DummyPopupGenericTarget.AskStringAsync("Enter search text").GetAwaiter().GetResult();

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("検索テキストを入力"));
    }

    [Test]
    public void Prefix_RepositoryDictionary_TranslatesCodaEndGamePrompt()
    {
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString("Leaving the village will end the game.\n\nType END GAME to confirm.");

        Assert.That(
            DummyPopupGenericTarget.LastAskStringMessage,
            Is.EqualTo("村を出るとゲームが終了する。\n\n確認するには END GAME と入力。"));
    }

    [Test]
    public void Prefix_RepositoryDictionary_TranslatesBuildLibraryPrompts()
    {
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskStringAsync));

        _ = DummyPopupGenericTarget.AskStringAsync("Paste build code:").GetAwaiter().GetResult();
        var pastePrompt = DummyPopupGenericTarget.LastAskStringMessage;

        _ = DummyPopupGenericTarget.AskStringAsync("Name this build:").GetAwaiter().GetResult();
        var namePrompt = DummyPopupGenericTarget.LastAskStringMessage;

        Assert.Multiple(() =>
        {
            Assert.That(pastePrompt, Is.EqualTo("ビルドコードを貼り付け："));
            Assert.That(namePrompt, Is.EqualTo("このビルドに名前を付ける："));
        });
    }

    [Test]
    public void Prefix_TranslatesGenderCustomizeNamePromptTemplate()
    {
        WriteDictionary((
            "What name should be used for your {0}? (Male, female, etc.)",
            "あなたの{0}に使う名前は？（Male、female など）"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskStringAsync));

        _ = DummyPopupGenericTarget.AskStringAsync(
            "What name should be used for your gender? (Male, female, etc.)").GetAwaiter().GetResult();

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("あなたのgenderに使う名前は？（Male、female など）"));
    }

    [Test]
    public void Prefix_TranslatesEndGameConversationConfirmPromptTemplate()
    {
        WriteDictionary(("End game?\n\nType {0} to confirm.", "ゲームを終了しますか？\n\n確認するには {0} と入力。"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString("End game?\n\nType ASCEND to confirm.");

        Assert.That(
            DummyPopupGenericTarget.LastAskStringMessage,
            Is.EqualTo("ゲームを終了しますか？\n\n確認するには ASCEND と入力。"));
    }

    [TestCase(
        "Launch spaceship and end game? (type LAUNCH to confirm)",
        "宇宙船を発射してゲームを終了しますか？（確認するには LAUNCH と入力）")]
    [TestCase(
        "Opening the ark will expose its nondeterministic core to the chamber's ambient normality and irrevocably damage Resheph. Continue?\n\nType OPEN ARK to confirm.",
        "方舟を開くと、その非決定論的コアが部屋の周囲正常性にさらされ、レシェフに取り返しのつかない損傷を与える。続けますか？\n\n確認するには OPEN ARK と入力。")]
    public void Prefix_TranslatesShipArkConfirmationPrompts(string source, string expected)
    {
        WriteDictionary((source, expected));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString(source);

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo(expected));
    }

    [Test]
    public void Prefix_LeavesAlreadyLocalizedAskStringPromptUnchanged()
    {
        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString("ペットに名前を付けてください。");

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("ペットに名前を付けてください。"));
    }

    [Test]
    public void Prefix_LeavesUnknownAskStringPromptUnchanged()
    {
        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        const string source = "Untranslated popup prompt";
        DummyPopupGenericTarget.AskString(source);

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo(source));
    }

    [Test]
    public void Prefix_LeavesEmptyAskStringPromptUnchanged()
    {
        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString(string.Empty);

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.Empty);
    }

    [Test]
    public void Prefix_PreservesAskStringMarkupAndColorTags()
    {
        WriteDictionary(("Name your pet.", "ペットに名前を付けてください。"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString("{{R|Name your pet.}}");

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("{{R|ペットに名前を付けてください。}}"));
    }

    [Test]
    public void Prefix_PreservesColorTagsWithinTranslatedAskStringPrompt()
    {
        WriteDictionary(("Name your pet.", "ペットに{{G|名前}}を付けてください。"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString("Name your pet.");

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("ペットに{{G|名前}}を付けてください。"));
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarker_FromAskStringPrompt()
    {
        WriteDictionary(("既に翻訳済み", "別訳"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskString));

        DummyPopupGenericTarget.AskString("\u0001既に翻訳済み");

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("既に翻訳済み"));
    }

    [Test]
    public void Prefix_PreservesQuitPromptThroughPopupMessageHandoff()
    {
        WriteDictionary(
            ("Are you sure you want to quit?", "本当に終了しますか？"),
            ("Quit Without Saving", "セーブせずに終了"),
            ("[Enter] Submit", "[Enter] 送信"),
            ("[Esc] Cancel", "[Esc] キャンセル"));

        var buttons = new List<DummyPopupMessageItem>
        {
            new("{{W|[Enter]}} {{y|Submit}}", "Accept", "Accept"),
            new("{{W|[Esc]}} {{y|Cancel}}", "Cancel", "Cancel"),
        };

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.AskStringAsync)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupAskStringTranslationPatch), nameof(PopupAskStringTranslationPatch.Prefix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
                prefix: new HarmonyMethod(RequireMethod(typeof(PopupMessageTranslationPatch), nameof(PopupMessageTranslationPatch.Prefix))));

            _ = DummyPopupGenericTarget.AskStringAsync(
                "Are you sure you want to quit?",
                WantsSpecificPrompt: "QUIT").GetAwaiter().GetResult();

            var clippedMessage = DummyPopupGenericTarget.LastAskStringMessage;
            new DummyPopupMessageTarget().ShowPopup(
                clippedMessage,
                buttons,
                title: "Quit Without Saving",
                WantsSpecificPrompt: "QUIT");

            var renderedMessage = DummyPopupMessageTarget.LastMessage;
            var renderedButton = DummyPopupMessageTarget.LastButtons![0].text;
            UITextSkinTranslationPatch.Prefix(ref renderedMessage);
            UITextSkinTranslationPatch.Prefix(ref renderedButton);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("本当に終了しますか？"));
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("本当に終了しますか？"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("セーブせずに終了"));
                Assert.That(DummyPopupMessageTarget.LastButtons![0].text, Is.EqualTo("{{W|[Enter]}} {{y|送信}}"));
                Assert.That(DummyPopupMessageTarget.LastButtons[1].text, Is.EqualTo("{{W|[Esc]}} {{y|キャンセル}}"));
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

    private static IDisposable PatchMethod(string methodName)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupAskStringTranslationPatch), nameof(PopupAskStringTranslationPatch.Prefix))));
        return new HarmonyPatchScope(harmony, harmonyId);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");

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
            Path.Combine(dictionaryDirectory, "popup-askstring.ja.json"),
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

    private sealed class HarmonyPatchScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        public HarmonyPatchScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
