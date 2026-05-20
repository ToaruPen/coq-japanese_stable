using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class ImportedFoodOrDrinkFactionNameTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-imported-food-drink-faction-name-l1", Guid.NewGuid().ToString("N"));
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

    [TestCase("Cult of the Honeyed Bread", "ハチミツ風味のパンの教団")]
    [TestCase("Honeyed Bread Cult", "ハチミツ風味のパンの教団")]
    [TestCase("Honeyed Bread Cabal", "ハチミツ風味のパンの秘密結社")]
    public void TryTranslate_TranslatesGeneratedFactionNameFrames(string source, string expected)
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("honeyed", "ハチミツ風味の"),
            ("bread", "パン"));
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"), ("cabal", "秘密結社"));

        var ok = ImportedFoodOrDrinkFactionNameTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesColorBoundaryWrappers()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("bread", "パン"));
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"));

        var ok = ImportedFoodOrDrinkFactionNameTranslator.TryTranslate("{{Y|Cult of the Bread}}", out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{Y|パンの教団}}"));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var source = MessageFrameTranslator.DirectTranslationMarker + "Cult of the Bread";

        var ok = ImportedFoodOrDrinkFactionNameTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("Cult of the Bread"));
        });
    }

    [TestCase("")]
    [TestCase("Honeyed Bread")]
    [TestCase("Cult of Root")]
    public void TryTranslate_LeavesUnknownOrNonMatchingInputUnchanged(string source)
    {
        var ok = ImportedFoodOrDrinkFactionNameTranslator.TryTranslate(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    private void WriteDictionaryFile(string fileName, params (string Key, string Text)[] entries)
    {
        var path = Path.Combine(dictionaryDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].Key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].Text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(path, builder.ToString(), Utf8WithoutBom);
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
