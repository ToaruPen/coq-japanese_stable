using System.Text;

using FsCheck.Fluent;

using FsCheckProperty = FsCheck.Property;

using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class JournalTextTranslatorPropertyTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private const string ReplaySeed = "468135792,97531";

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-journal-text-pbt", Guid.NewGuid().ToString("N"));
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

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(JournalTextTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateBaseEntry_StripsDirectTranslationMarker(JournalMarkerText sample)
    {
        WritePatternDictionary();

        var entry = new DummyJournalObservation
        {
            Category = "general",
        };

        var ok = JournalTextTranslator.TryTranslateBaseEntry(
            entry,
            MessageFrameTranslator.MarkDirectTranslation(sample.Value),
            nameof(JournalTextTranslatorPropertyTests),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(sample.Value));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(JournalTextTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateAccomplishmentTextForStorage_MarksTranslatedPattern(JournalJourneyCase sample)
    {
        WriteJourneyExactDictionary();
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var ok = JournalTextTranslator.TryTranslateAccomplishmentTextForStorage(
            sample.Source,
            "general",
            nameof(JournalTextTranslatorPropertyTests),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(JournalTextTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateObservationRevealTextForStorage_MarksTranslatedPattern(JournalJourneyCase sample)
    {
        WriteJourneyExactDictionary();
        WritePatternDictionary(("^You journeyed to (.+?)\\.$", "{t0}に旅した。"));

        var ok = JournalTextTranslator.TryTranslateObservationRevealTextForStorage(
            sample.Source,
            nameof(JournalTextTranslatorPropertyTests),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(JournalTextTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateAccomplishmentTextForStorage_LeavesAlreadyMarkedTextUntouched(JournalMarkerText sample)
    {
        WritePatternDictionary();

        var source = MessageFrameTranslator.MarkDirectTranslation(sample.Value);
        var ok = JournalTextTranslator.TryTranslateAccomplishmentTextForStorage(
            source,
            "general",
            nameof(JournalTextTranslatorPropertyTests),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(source));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(JournalTextTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslateMapNoteTextForStorage_SkipsBlockedCategories(JournalSkippedCategoryCase sample)
    {
        WritePatternDictionary(("^A \"SATED\" baetyl$", "「満足した」ベテル"));

        var ok = JournalTextTranslator.TryTranslateMapNoteTextForStorage(
            sample.Source,
            sample.Category,
            nameof(JournalTextTranslatorPropertyTests),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(sample.Source));
        });

        return true.ToProperty();
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
        File.WriteAllText(Path.Combine(dictionaryDirectory, "journal-text-pbt.ja.json"), builder.ToString(), Utf8WithoutBom);
    }

    private void WriteJourneyExactDictionary()
    {
        WriteExactDictionary(
            ("Kyakukya", "キャクキャ"),
            ("Joppa", "ジョッパ"));
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
