using System;
using System.IO;
using System.Text;
using QudJP;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class TranslatorTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-translator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
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
    public void Translate_ReturnsJapanese_WhenKeyExists()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        var translated = Translator.Translate("Hello");

        Assert.That(translated, Is.EqualTo("こんにちは"));
    }

    [Test]
    public void Translate_ReturnsOriginal_WhenKeyIsMissing()
    {
        WriteDictionary("ui-test.ja.json", "Hello", "こんにちは");

        var translated = Translator.Translate("Goodbye");

        Assert.That(translated, Is.EqualTo("Goodbye"));
    }

    [Test]
    public void Translate_LoadsDictionaryOnlyOnce_WhenCalledRepeatedly()
    {
        WriteDictionary("ui-test.ja.json", "Inventory", "インベントリ");

        var first = Translator.Translate("Inventory");
        var second = Translator.Translate("Inventory");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("インベントリ"));
            Assert.That(second, Is.EqualTo("インベントリ"));
            Assert.That(Translator.LoadInvocationCount, Is.EqualTo(1));
        });
    }

    private void WriteDictionary(string fileName, string key, string text)
    {
        var content =
            "{"
            + "\"entries\":["
            + "{\"key\":\"" + EscapeJson(key) + "\",\"text\":\"" + EscapeJson(text) + "\"}"
            + "]}"
            + Environment.NewLine;

        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
