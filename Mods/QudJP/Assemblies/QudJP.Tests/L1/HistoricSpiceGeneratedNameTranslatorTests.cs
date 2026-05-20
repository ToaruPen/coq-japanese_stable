using System.Text;
using QudJP;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class HistoricSpiceGeneratedNameTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-spice-generated-name-l1", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        ScopedDictionaryLookup.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslateSultanateYearName_TranslatesGeneratedAdjectiveAndNoun()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("shining", "輝く"), ("visage", "容貌"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateSultanateYearName(
            "Year of the Shining Visage",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("輝く容貌の年"));
        });
    }

    [Test]
    public void TryTranslateSultanateYearName_LeavesUnknownComponentUnchanged()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("shining", "輝く"));
        const string source = "Year of the Shining Visage";

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateSultanateYearName(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateSultanateYearName_StripsDirectTranslationMarker()
    {
        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateSultanateYearName(
            MessageFrameTranslator.DirectTranslationMarker + "Year of the Shining Visage",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("Year of the Shining Visage"));
        });
    }

    [TestCase("")]
    [TestCase("Era of the Shining Visage")]
    public void TryTranslateSultanateYearName_LeavesNonMatchingInputUnchanged(string source)
    {
        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateSultanateYearName(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [TestCase("Blessing of Sword", "剣の祝福")]
    [TestCase("the Blessing of Sword", "剣の祝福")]
    [TestCase("Sword's Blessing", "剣の祝福")]
    [TestCase("Sword's blessing", "剣の祝福")]
    [TestCase("Sword Blessing", "剣の祝福")]
    [TestCase("the Sword Blessing", "剣の祝福")]
    public void TryTranslateHistoricItemName_TranslatesGeneratedBlessingFrames(string source, string expected)
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("sword", "剣"));
        WriteDictionaryFile("world-gospels.ja.json", ("blessing", "祝福"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateHistoricItemName(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateHistoricItemName_TranslatesKnownBlessingWithLocalizedRoot()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("jewel", "宝玉"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateHistoricItemName(
            "クダング's Jewel",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("クダングの宝玉"));
        });
    }

    [Test]
    public void TryTranslateHistoricItemName_PreservesWholeColorBoundary()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("sword", "剣"));
        WriteDictionaryFile("world-gospels.ja.json", ("blessing", "祝福"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateHistoricItemName(
            "{{M|Sword's Blessing}}",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{M|剣の祝福}}"));
        });
    }

    [Test]
    public void TryTranslateHistoricItemName_StripsDirectMarkerWithoutRetranslating()
    {
        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateHistoricItemName(
            MessageFrameTranslator.DirectTranslationMarker + "Sword's Blessing",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("Sword's Blessing"));
        });
    }

    [TestCase("Sword's Qwern")]
    [TestCase("Swordicus")]
    [TestCase("")]
    public void TryTranslateHistoricItemName_LeavesUnknownOrSuffixNamesUnchanged(string source)
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("sword", "剣"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateHistoricItemName(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [TestCase("Cult of the Gleaming Ghost", "煌めき幽鬼の教団")]
    [TestCase("Gleaming Ghost Cult", "煌めき幽鬼の教団")]
    [TestCase("Cult of Resheph", "Reshephの教団")]
    [TestCase("Ibulian Cult", "Ibul派の教団")]
    [TestCase("Gleamingian Cult", "煌めき派の教団")]
    [TestCase("2nd Ibulian Cult", "第2 Ibul派の教団")]
    public void TryTranslateSultanCultName_TranslatesGeneratedCultNameFrames(string source, string expected)
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("gleaming", "煌めき"), ("ghost", "幽鬼"));
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateSultanCultName(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("Mystery of the Unknown Ghost")]
    [TestCase("Ibulian Unknown")]
    [TestCase("")]
    public void TryTranslateSultanCultName_LeavesUnknownCultNamesUnchanged(string source)
    {
        WriteDictionaryFile("world-gospels.ja.json", ("cult", "教団"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateSultanCultName(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateSultanCultName_StripsDirectMarkerWithoutRetranslating()
    {
        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateSultanCultName(
            MessageFrameTranslator.DirectTranslationMarker + "Cult of the Gleaming Ghost",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("Cult of the Gleaming Ghost"));
        });
    }

    [TestCase("Ibul wastes", "Ibul荒野")]
    [TestCase("the red Ibul", "赤のIbul")]
    [TestCase("The red Ibul", "赤のIbul")]
    [TestCase("red wastes Ibul", "赤の荒野Ibul")]
    [TestCase("red Salt Dunes", "赤のSalt Dunes")]
    public void TryTranslateRuinsSiteName_TranslatesGeneratedSiteModifierFrames(string source, string expected)
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("red", "赤"), ("wastes", "荒野"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateRuinsSiteName(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("Ibul")]
    [TestCase("some forgotten ruins")]
    [TestCase("Ibul unknown")]
    [TestCase("")]
    public void TryTranslateRuinsSiteName_LeavesProperFallbackAndUnknownSiteNamesUnchanged(string source)
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("red", "赤"), ("wastes", "荒野"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateRuinsSiteName(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateRuinsSiteName_StripsDirectMarkerWithoutRetranslating()
    {
        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateRuinsSiteName(
            MessageFrameTranslator.DirectTranslationMarker + "red wastes Ibul",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("red wastes Ibul"));
        });
    }

    [Test]
    public void TryTranslateCapture_ReordersDishOfIngredientName()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("broth", "ブロス"),
            ("glowfish", "グロウフィッシュ"));

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateCapture("Broth of Glowfish", out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("グロウフィッシュ入りブロス"));
        });
    }

    [Test]
    public void TryTranslateCapture_UnknownInput_ReturnsFalseAndOriginal()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("broth", "ブロス"),
            ("glowfish", "グロウフィッシュ"));
        const string source = "Mystery of Nothing";

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateCapture_DirectMarker_RemovedAndNotRetranslated()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("broth", "ブロス"),
            ("glowfish", "グロウフィッシュ"));
        const string source = "Broth of Glowfish";

        var ok = HistoricSpiceGeneratedNameTranslator.TryTranslateCapture(
            MessageFrameTranslator.DirectTranslationMarker + source,
            out var translated);

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
