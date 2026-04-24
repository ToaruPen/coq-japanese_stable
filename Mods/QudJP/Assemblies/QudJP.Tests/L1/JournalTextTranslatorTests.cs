using System.Text;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class JournalTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-journal-text-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslateBaseEntry_StripsDirectTranslationMarker()
    {
        WritePatternDictionary();

        var entry = new DummyJournalObservation
        {
            Category = "general",
        };

        var ok = JournalTextTranslator.TryTranslateBaseEntry(
            entry,
            MessageFrameTranslator.MarkDirectTranslation("伝聞を記録した。"),
            "JournalTextTranslatorTests",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("伝聞を記録した。"));
        });
    }

    [Test]
    public void TryTranslateAccomplishmentTextForStorage_MarksTranslatedPattern()
    {
        WriteExactDictionary(("Kyakukya", "キャクキャ"));
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var ok = JournalTextTranslator.TryTranslateAccomplishmentTextForStorage(
            "You journeyed to Kyakukya.",
            "general",
            "JournalTextTranslatorTests",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("\u0001キャクキャに旅した。"));
        });
    }

    [Test]
    public void TryTranslateMapNoteTextForStorage_SkipsMiscellaneousCategory()
    {
        WritePatternDictionary(("^A \"SATED\" baetyl$", "「満足した」ベテル"));

        var ok = JournalTextTranslator.TryTranslateMapNoteTextForStorage(
            "A \"SATED\" baetyl",
            "Miscellaneous",
            "JournalTextTranslatorTests",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo("A \"SATED\" baetyl"));
        });
    }

    [Test]
    public void TryTranslateObservationRevealTextForStorage_MarksTranslatedText()
    {
        WriteExactDictionary(("Kyakukya", "キャクキャ"));
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var ok = JournalTextTranslator.TryTranslateObservationRevealTextForStorage(
            "You journeyed to Kyakukya.",
            "JournalTextTranslatorTests",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("\u0001キャクキャに旅した。"));
        });
    }

    [Test]
    public void TryTranslateMapNoteTextForStorage_TranslatesZoneDisplayNamesAndLairFamilies()
    {
        WriteExactDictionary(
            ("slime bog", "スライム沼"),
            ("flaming tar pits", "燃えるタール沼"),
            ("dromad caravan", "ドロマド商隊"));
        WritePatternDictionary();

        Assert.Multiple(() =>
        {
            AssertTranslatedMapNote("a slime bog", "\u0001スライム沼");
            AssertTranslatedMapNote("some flaming tar pits", "\u0001燃えるタール沼");
            AssertTranslatedMapNote("a dromad caravan", "\u0001ドロマド商隊");
            AssertTranslatedMapNote("{{Y|a slime bog}}", "\u0001{{Y|スライム沼}}");
            AssertTranslatedMapNote("the lair of Mamon Souldrinker", "\u0001Mamon Souldrinkerの巣");
            AssertTranslatedMapNote("the village lair of Mamon Souldrinker", "\u0001Mamon Souldrinkerの村落の巣");
            AssertTranslatedMapNote("the cradle of Girsh Rermadon", "\u0001Girsh Rermadonの揺籃");
            AssertTranslatedMapNote("the chuppah of Girsh Qas and Girsh Qon", "\u0001Girsh Qas and Girsh Qonのフッパー");
            AssertTranslatedMapNote("\u0001a slime bog", "\u0001a slime bog");
            AssertUntranslatedMapNote("an unknown landmark");
            AssertUntranslatedMapNote(string.Empty);
        });
    }

    [Test]
    public void TryTranslateObservationRevealTextForStorage_UsesJournalMarkOfDeathPatterns()
    {
        WritePatternDictionary(
            ("^The lost Mark of Death from the late sultanate was (.+?)\\.$", "亡きスルタンの死の刻印は{0}だった。"),
            ("^You recover the Mark of Death from (.+?)\\.$", "{0}から死の刻印を回収した。"));

        Assert.Multiple(() =>
        {
            AssertTranslatedObservation(
                "The lost Mark of Death from the late sultanate was %*//*%.",
                "\u0001亡きスルタンの死の刻印は%*//*%だった。");
            AssertTranslatedObservation(
                "The lost Mark of Death from the late sultanate was {{R|%*//*%}}.",
                "\u0001亡きスルタンの死の刻印は{{R|%*//*%}}だった。");
            AssertTranslatedObservation(
                "You recover the Mark of Death from the ruins.",
                "\u0001the ruinsから死の刻印を回収した。");
            AssertTranslatedObservation(
                "\u0001You recover the Mark of Death from the ruins.",
                "\u0001You recover the Mark of Death from the ruins.");
            AssertUntranslatedObservation("The Mark of Death is gone.");
            AssertUntranslatedObservation(string.Empty);
        });
    }

    private void WritePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[],\"patterns\":[");
        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(patternFilePath, builder.ToString(), Utf8WithoutBom);
    }

    private static void AssertTranslatedMapNote(string source, string expected)
    {
        var ok = JournalTextTranslator.TryTranslateMapNoteTextForStorage(
            source,
            "Locations",
            "JournalTextTranslatorTests",
            out var translated);

        Assert.That(ok, Is.True, source);
        Assert.That(translated, Is.EqualTo(expected), source);
    }

    private static void AssertTranslatedObservation(string source, string expected)
    {
        var ok = JournalTextTranslator.TryTranslateObservationRevealTextForStorage(
            source,
            "JournalTextTranslatorTests",
            out var translated);

        Assert.That(ok, Is.True, source);
        Assert.That(translated, Is.EqualTo(expected), source);
    }

    private static void AssertUntranslatedMapNote(string source)
    {
        var ok = JournalTextTranslator.TryTranslateMapNoteTextForStorage(
            source,
            "Locations",
            "JournalTextTranslatorTests",
            out var translated);

        Assert.That(ok, Is.False, source);
        Assert.That(translated, Is.EqualTo(source), source);
    }

    private static void AssertUntranslatedObservation(string source)
    {
        var ok = JournalTextTranslator.TryTranslateObservationRevealTextForStorage(
            source,
            "JournalTextTranslatorTests",
            out var translated);

        Assert.That(ok, Is.False, source);
        Assert.That(translated, Is.EqualTo(source), source);
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
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
        builder.AppendLine();
        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "journal-text-l1.ja.json"),
            builder.ToString(),
            Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
