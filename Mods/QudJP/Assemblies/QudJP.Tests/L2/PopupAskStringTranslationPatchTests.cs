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

        Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("{{R|ペットに名前を付けてください}}。"));
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
