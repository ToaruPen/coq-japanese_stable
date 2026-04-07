using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupAskNumberTranslationPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-asknumber-l2", Guid.NewGuid().ToString("N"));
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
        DummyAskNumberScreenTarget.Reset();
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
    public void Prefix_TranslatesAskNumberPrompt()
    {
        WriteDictionary(("How many waterskins?", "ウォータースキンはいくつですか？"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumber));

        DummyPopupGenericTarget.AskNumber("How many waterskins?");

        Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo("ウォータースキンはいくつですか？"));
    }

    [Test]
    public void Prefix_TranslatesAskNumberAsyncPrompt()
    {
        WriteDictionary(("Select how many?", "いくつ選びますか？"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumberAsync));

        _ = DummyPopupGenericTarget.AskNumberAsync("Select how many?").GetAwaiter().GetResult();

        Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo("いくつ選びますか？"));
    }

    [Test]
    public void Prefix_HandsOffTranslatedAskNumberAsyncPrompt_ToAskNumberScreenRoute()
    {
        WriteDictionary(("Select how many?", "いくつ選びますか？"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumberAsyncGamepad));

        _ = DummyPopupGenericTarget.AskNumberAsyncGamepad("Select how many?").GetAwaiter().GetResult();

        var sinkText = DummyAskNumberScreenTarget.LastMessage;
        UITextSkinTranslationPatch.Prefix(ref sinkText);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo("いくつ選びますか？"));
            Assert.That(DummyAskNumberScreenTarget.LastMessage, Is.EqualTo("いくつ選びますか？"));
            Assert.That(sinkText, Is.EqualTo("いくつ選びますか？"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupAskNumberTranslationPatch),
                    "Popup.ProducerText.Exact"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Prefix_LeavesAlreadyLocalizedAskNumberPromptUnchanged()
    {
        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumber));

        DummyPopupGenericTarget.AskNumber("ウォータースキンはいくつですか？");

        Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo("ウォータースキンはいくつですか？"));
    }

    [Test]
    public void Prefix_LeavesUnknownAskNumberPromptUnchanged()
    {
        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumber));

        const string source = "Untranslated popup prompt";
        DummyPopupGenericTarget.AskNumber(source);

        Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo(source));
    }

    [Test]
    public void Prefix_LeavesEmptyAskNumberPromptUnchanged()
    {
        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumber));

        DummyPopupGenericTarget.AskNumber(string.Empty);

        Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.Empty);
    }

    [Test]
    public void Prefix_PreservesAskNumberMarkupAndColorTags()
    {
        WriteDictionary(("How many waterskins?", "ウォータースキンはいくつですか？"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumber));

        DummyPopupGenericTarget.AskNumber("{{R|How many waterskins?}}");

        Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo("{{R|ウォータースキンはいくつですか？}}"));
    }

    [Test]
    public void Prefix_PreservesColorTagsWithinTranslatedAskNumberPrompt()
    {
        WriteDictionary(("How many waterskins?", "ウォータースキンを{{C|いくつ}}選びますか？"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumber));

        DummyPopupGenericTarget.AskNumber("How many waterskins?");

        Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo("ウォータースキンを{{C|いくつ}}選びますか？"));
    }

    [Test]
    public void Prefix_TranslatesCampfirePreservePrompt()
    {
        WriteDictionary((
            "{0}: how many do you want to preserve? (max = {1})",
            "{0}: いくつ保存するか？ (最大 = {1})"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumber));

        DummyPopupGenericTarget.AskNumber("{{Y|fermented yuckwheat stem}}: how many do you want to preserve? (max = 3)");

        Assert.That(
            DummyPopupGenericTarget.LastAskNumberMessage,
            Is.EqualTo("{{Y|fermented yuckwheat stem}}: いくつ保存するか？ (最大 = 3)"));
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarker_FromAskNumberPrompt()
    {
        WriteDictionary(("既に翻訳済み", "別訳"));

        using var patch = PatchMethod(nameof(DummyPopupGenericTarget.AskNumber));

        DummyPopupGenericTarget.AskNumber("\u0001既に翻訳済み");

        Assert.That(DummyPopupGenericTarget.LastAskNumberMessage, Is.EqualTo("既に翻訳済み"));
    }

    private static IDisposable PatchMethod(string methodName)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), methodName),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupAskNumberTranslationPatch), nameof(PopupAskNumberTranslationPatch.Prefix))));
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
            Path.Combine(dictionaryDirectory, "popup-asknumber.ja.json"),
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
