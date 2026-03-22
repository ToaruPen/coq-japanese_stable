using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class HotkeyLabelFamilyTranslatorTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-hotkey-family-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslateBracketedLabel_TranslatesSharedPopupAndUiFamily()
    {
        WriteDictionary(("Cancel", "キャンセル"));

        var popupTranslated = HotkeyLabelFamilyTranslator.TryTranslateBracketedLabel(
            "[Esc] Cancel",
            nameof(PopupTranslationPatch),
            "HotkeyLabel",
            rejectNumericHotkeys: true,
            out var popupLabel);
        var uiTranslated = HotkeyLabelFamilyTranslator.TryTranslateBracketedLabel(
            "[Esc] Cancel",
            nameof(UITextSkinTranslationPatch),
            "UITextSink.HotkeyLabel",
            rejectNumericHotkeys: false,
            out var uiLabel);

        Assert.Multiple(() =>
        {
            Assert.That(popupTranslated, Is.True);
            Assert.That(uiTranslated, Is.True);
            Assert.That(popupLabel, Is.EqualTo("[Esc] キャンセル"));
            Assert.That(uiLabel, Is.EqualTo("[Esc] キャンセル"));
        });
    }

    [Test]
    public void TryTranslateBracketedLabel_RejectsNumericHotkeysWhenRequested()
    {
        WriteDictionary(("Continue", "続ける"));

        var popupTranslated = HotkeyLabelFamilyTranslator.TryTranslateBracketedLabel(
            "[1] Continue",
            nameof(PopupTranslationPatch),
            "HotkeyLabel",
            rejectNumericHotkeys: true,
            out var popupLabel);
        var uiTranslated = HotkeyLabelFamilyTranslator.TryTranslateBracketedLabel(
            "[1] Continue",
            nameof(UITextSkinTranslationPatch),
            "UITextSink.HotkeyLabel",
            rejectNumericHotkeys: false,
            out var uiLabel);

        Assert.Multiple(() =>
        {
            Assert.That(popupTranslated, Is.False);
            Assert.That(popupLabel, Is.EqualTo("[1] Continue"));
            Assert.That(uiTranslated, Is.True);
            Assert.That(uiLabel, Is.EqualTo("[1] 続ける"));
        });
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
