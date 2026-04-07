using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PopupGetPopupOptionTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-popup-getpopupoption-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesFinalHotkeyOptionText()
    {
        WriteDictionary(
            ("[n] detonate", "[n] 起爆"),
            ("[q] quit without saving", "[q] セーブせずに終了"));

        using var patch = PatchGetPopupOption();

        var detonate = DummyPopupGenericTarget.GetPopupOption(0, new[] { "Detonate" }, new[] { 'n' });
        var quit = DummyPopupGenericTarget.GetPopupOption(0, new[] { "Quit Without Saving" }, new[] { 'q' });

        Assert.Multiple(() =>
        {
            Assert.That(detonate.text, Is.EqualTo("{{W|[n]}} {{y|起爆}}"));
            Assert.That(quit.text, Is.EqualTo("{{W|[q]}} {{y|セーブせずに終了}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupGetPopupOptionTranslationPatch),
                    "Popup.ProducerMenuItem.Exact"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void Postfix_TranslatesInlineHotkeyPopupOptionText()
    {
        WriteDictionary(("[n] detonate", "[n] 起爆"));

        using var patch = PatchGetPopupOption();

        var option = DummyPopupGenericTarget.GetPopupOption(0, new[] { "deto{{hotkey|n}}ate" }, new[] { 'n' });

        Assert.That(option.text, Is.EqualTo("{{W|[n]}} {{y|起爆}}"));
    }

    [Test]
    public void Postfix_LeavesUnknownHotkeyOptionTextUnchanged()
    {
        WriteDictionary(("[n] detonate", "[n] 起爆"));

        using var patch = PatchGetPopupOption();

        var option = DummyPopupGenericTarget.GetPopupOption(0, new[] { "Untranslated" }, new[] { 'x' });

        Assert.That(option.text, Is.EqualTo("{{W|[x]}} {{y|Untranslated}}"));
    }

    private static IDisposable PatchGetPopupOption()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.GetPopupOption)),
            postfix: new HarmonyMethod(RequireMethod(typeof(PopupGetPopupOptionTranslationPatch), nameof(PopupGetPopupOptionTranslationPatch.Postfix))));
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
            Path.Combine(tempDirectory, "popup-getpopupoption.ja.json"),
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
