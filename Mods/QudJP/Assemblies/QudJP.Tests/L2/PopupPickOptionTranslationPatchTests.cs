using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupPickOptionTranslationPatchTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-pickoption-l2", Guid.NewGuid().ToString("N"));
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
    public void Prefix_TranslatesPickOptionTitle()
    {
        WriteDictionary(("Save Slots", "セーブ一覧"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Title: "Save Slots");

        Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("セーブ一覧"));
    }

    [Test]
    public void Prefix_TranslatesPickOptionIntro()
    {
        WriteDictionary(("Choose a destination.", "行き先を選んでください。"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Intro: "Choose a destination.");

        Assert.That(DummyPopupGenericTarget.LastPickOptionIntro, Is.EqualTo("行き先を選んでください。"));
    }

    [Test]
    public void Prefix_TranslatesPickOptionOptions()
    {
        WriteDictionary(("Continue", "続ける"), ("Cancel", "キャンセル"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Options: new[] { "Continue", "Cancel" });

        Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "続ける", "キャンセル" }));
    }

    [Test]
    public void Prefix_TranslatesDynamicUntilTimeOfDayOption_FromCalendarLeafTranslation()
    {
        WriteDictionary(("Waxing Salt Sun", "塩の満ちる太陽"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Options: new[] { "Until Waxing Salt Sun" });

        Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "次の塩の満ちる太陽まで" }));
    }

    [Test]
    public void Prefix_TranslatesPickOptionSpacingText()
    {
        WriteDictionary(("Prompt", "案内"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(SpacingText: "Prompt");

        Assert.That(DummyPopupGenericTarget.LastPickOptionSpacingText, Is.EqualTo("案内"));
    }

    [Test]
    public void Prefix_TranslatesPickOptionButtons()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(Buttons: new[] { new DummyPopupMenuItem("{{W|Cancel}}") });

        Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
        Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|キャンセル}}"));
    }

    [Test]
    public void Prefix_LeavesAlreadyLocalizedTextUnchanged()
    {
        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Title: "セーブ一覧",
            Intro: "行き先を選んでください。",
            SpacingText: "案内",
            Options: new[] { "続ける", "キャンセル" },
            Buttons: new[] { new DummyPopupMenuItem("{{W|キャンセル}}") });

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("セーブ一覧"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionIntro, Is.EqualTo("行き先を選んでください。"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionSpacingText, Is.EqualTo("案内"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "続ける", "キャンセル" }));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|キャンセル}}"));
        });
    }

    [Test]
    public void Prefix_PreservesMarkupAndColorTags()
    {
        WriteDictionary(
            ("Warning!", "警告！"),
            ("Choose wisely.", "慎重に選んでください。"),
            ("Prompt", "案内"),
            ("Continue", "続ける"),
            ("Cancel", "キャンセル"));

        using var patch = PatchPickOption();

        DummyPopupGenericTarget.PickOption(
            Title: "{{R|Warning!}}",
            Intro: "{{C|Choose wisely.}}",
            SpacingText: "{{K|Prompt}}",
            Options: new[] { "{{W|Continue}}" },
            Buttons: new[] { new DummyPopupMenuItem("{{W|Cancel}}") });

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("{{R|警告！}}"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionIntro, Is.EqualTo("{{C|慎重に選んでください。}}"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionSpacingText, Is.EqualTo("{{K|案内}}"));
            Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.EqualTo(new[] { "{{W|続ける}}" }));
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons, Is.Not.Null);
            Assert.That(DummyPopupGenericTarget.LastPickOptionButtons![0].text, Is.EqualTo("{{W|キャンセル}}"));
        });
    }

    private static IDisposable PatchPickOption()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Prefix))));
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
            Path.Combine(dictionaryDirectory, "popup-pickoption.ja.json"),
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
