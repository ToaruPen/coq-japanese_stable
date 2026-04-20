using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupShowSpaceTranslationPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-showspace-l2", Guid.NewGuid().ToString("N"));
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
    public void Prefix_TranslatesShowSpaceMessage()
    {
        WriteDictionary(("Game saved!", "ゲームをセーブしました！"));

        using var patch = PatchShowSpace();

        DummyPopupGenericTarget.ShowSpace("Game saved!");

        Assert.That(DummyPopupGenericTarget.LastShowSpaceMessage, Is.EqualTo("ゲームをセーブしました！"));
    }

    [Test]
    public void Prefix_TranslatesShowSpaceTitle()
    {
        WriteDictionary(
            ("Game saved!", "ゲームをセーブしました！"),
            ("Checkpoint", "チェックポイント"));

        using var patch = PatchShowSpace();

        DummyPopupGenericTarget.ShowSpace("Game saved!", Title: "Checkpoint");

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastShowSpaceMessage, Is.EqualTo("ゲームをセーブしました！"));
            Assert.That(DummyPopupGenericTarget.LastShowSpaceTitle, Is.EqualTo("チェックポイント"));
        });
    }

    [Test]
    public void Prefix_LeavesUnknownShowSpaceMessageUnchanged()
    {
        using var patch = PatchShowSpace();

        DummyPopupGenericTarget.ShowSpace("Untranslated popup");

        Assert.That(DummyPopupGenericTarget.LastShowSpaceMessage, Is.EqualTo("Untranslated popup"));
    }

    [Test]
    public void Prefix_LeavesAlreadyLocalizedShowSpaceTextUnchanged()
    {
        using var patch = PatchShowSpace();

        DummyPopupGenericTarget.ShowSpace("ゲームをセーブしました！", Title: "チェックポイント");

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastShowSpaceMessage, Is.EqualTo("ゲームをセーブしました！"));
            Assert.That(DummyPopupGenericTarget.LastShowSpaceTitle, Is.EqualTo("チェックポイント"));
        });
    }

    [Test]
    public void Prefix_PreservesMarkupAndColorTags()
    {
        WriteDictionary(
            ("Warning!", "警告！"),
            ("Alert", "警報"));

        using var patch = PatchShowSpace();

        DummyPopupGenericTarget.ShowSpace("{{R|Warning!}}", Title: "{{C|Alert}}");

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastShowSpaceMessage, Is.EqualTo("{{R|警告！}}"));
            Assert.That(DummyPopupGenericTarget.LastShowSpaceTitle, Is.EqualTo("{{C|警報}}"));
        });
    }

    [Test]
    public void Prefix_LeavesEmptyShowSpaceTextUnchanged()
    {
        using var patch = PatchShowSpace();

        DummyPopupGenericTarget.ShowSpace(string.Empty, Title: string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastShowSpaceMessage, Is.Empty);
            Assert.That(DummyPopupGenericTarget.LastShowSpaceTitle, Is.Empty);
        });
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarker_FromShowSpaceText()
    {
        WriteDictionary(
            ("既に翻訳済みの本文", "別訳本文"),
            ("既に翻訳済みのタイトル", "別訳タイトル"));

        using var patch = PatchShowSpace();

        DummyPopupGenericTarget.ShowSpace("\u0001既に翻訳済みの本文", Title: "\u0001既に翻訳済みのタイトル");

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastShowSpaceMessage, Is.EqualTo("既に翻訳済みの本文"));
            Assert.That(DummyPopupGenericTarget.LastShowSpaceTitle, Is.EqualTo("既に翻訳済みのタイトル"));
        });
    }

    [Test]
    public void Prefix_TranslatesDeathPopupWithLocalizedKiller_AndPreservesLeadingColor()
    {
        WriteDictionary(
            ("QudJP.DeathWrapper.Generic.Wrapped", "あなたは死んだ。\n\n{body}"),
            ("QudJP.DeathWrapper.KilledBy.Bare", "{killer}に殺された。"));

        using var patch = PatchShowSpace();

        DummyPopupGenericTarget.ShowSpace("&yYou died.\n\nYou were killed by タム、ドロマド商団 [座っている].");

        Assert.That(
            DummyPopupGenericTarget.LastShowSpaceMessage,
            Is.EqualTo("&yあなたは死んだ。\n\nタム、ドロマド商団 [座っている]に殺された。"));
    }

    private static IDisposable PatchShowSpace()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.ShowSpace)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowSpaceTranslationPatch), nameof(PopupShowSpaceTranslationPatch.Prefix))));
        return new HarmonyPatchScope(harmony, harmonyId);
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
            Path.Combine(dictionaryDirectory, "popup-showspace.ja.json"),
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
