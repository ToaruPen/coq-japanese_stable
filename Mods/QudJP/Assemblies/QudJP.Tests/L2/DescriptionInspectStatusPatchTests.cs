using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DescriptionInspectStatusPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-description-inspect-status-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        WriteStatusDictionary();
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

    [TestCase("Friendly", "友好")]
    [TestCase("Hostile", "敵対")]
    [TestCase("Neutral", "中立")]
    [TestCase("Impossible", "不可能")]
    [TestCase("Very Tough", "非常に困難")]
    [TestCase("Tough", "困難")]
    [TestCase("Average", "普通")]
    [TestCase("Easy", "容易")]
    [TestCase("Trivial", "取るに足りない")]
    [TestCase("Badly Wounded", "瀕死")]
    [TestCase("Wounded", "負傷")]
    [TestCase("Injured", "軽傷")]
    [TestCase("Fine", "良好")]
    [TestCase("Perfect", "完全")]
    [TestCase("Badly Damaged", "大破")]
    [TestCase("Damaged", "損傷")]
    [TestCase("Lightly Damaged", "軽微な損傷")]
    public void TranslateInspectStatusText_TranslatesKnownInspectStatusFamilies(string source, string expected)
    {
        var translated = DescriptionInspectStatusPatch.TranslateInspectStatusTextForTests(source);

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslateInspectStatusText_PreservesFeelingAndDifficultyColorTags()
    {
        var feeling = DescriptionInspectStatusPatch.TranslateInspectStatusTextForTests("{{G|Friendly}}");
        var difficulty = DescriptionInspectStatusPatch.TranslateInspectStatusTextForTests("{{R|Impossible}}");
        var woundLevel = DescriptionInspectStatusPatch.TranslateInspectStatusTextForTests("{{W|Injured}}");

        Assert.Multiple(() =>
        {
            Assert.That(feeling, Is.EqualTo("{{G|友好}}"));
            Assert.That(difficulty, Is.EqualTo("{{R|不可能}}"));
            Assert.That(woundLevel, Is.EqualTo("{{W|軽傷}}"));
        });
    }

    [Test]
    public void TranslateInspectStatusText_EmptyInputReturnsEmptyWithoutMissingKeyNoise()
    {
        var translated = DescriptionInspectStatusPatch.TranslateInspectStatusTextForTests(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(string.Empty));
            Assert.That(Translator.GetMissingKeyHitCountForTests(string.Empty), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateInspectStatusText_PreservesDirectTranslationMarkerAndTranslatesVisibleStatus()
    {
        var translated = DescriptionInspectStatusPatch.TranslateInspectStatusTextForTests("\x01{{G|Friendly}}");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("\x01{{G|友好}}"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("\x01{{G|Friendly}}"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("\x01Friendly"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateInspectStatusText_LeavesUnknownTextUnchangedWithoutMissingKeyNoise()
    {
        var translated = DescriptionInspectStatusPatch.TranslateInspectStatusTextForTests("Unknown Status");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("Unknown Status"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Unknown Status"), Is.EqualTo(0));
        });
    }

    private void WriteStatusDictionary()
    {
        WriteDictionaryFile(
            "ui-phase3c-labels.ja.json",
            ("Friendly", "友好"),
            ("Hostile", "敵対"),
            ("Neutral", "中立"),
            ("Impossible", "不可能"),
            ("Very Tough", "非常に困難"),
            ("Tough", "困難"),
            ("Average", "普通"),
            ("Easy", "容易"),
            ("Trivial", "取るに足りない"),
            ("Badly Wounded", "瀕死"),
            ("Wounded", "負傷"),
            ("Injured", "軽傷"),
            ("Fine", "良好"),
            ("Perfect", "完全"),
            ("Badly Damaged", "大破"),
            ("Damaged", "損傷"),
            ("Lightly Damaged", "軽微な損傷"));
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var path = Path.Combine(tempDirectory, fileName);
        var document = new DictionaryDocument
        {
            Entries = entries.Select(static entry => new DictionaryEntry { Key = entry.key, Text = entry.text }).ToList(),
        };

        using var stream = File.Create(path);
        var serializer = new DataContractJsonSerializer(typeof(DictionaryDocument));
        serializer.WriteObject(stream, document);
    }

    [DataContract]
    private sealed class DictionaryDocument
    {
        [DataMember(Name = "entries")]
        public List<DictionaryEntry> Entries { get; set; } = new List<DictionaryEntry>();
    }

    [DataContract]
    private sealed class DictionaryEntry
    {
        [DataMember(Name = "key")]
        public string Key { get; set; } = string.Empty;

        [DataMember(Name = "text")]
        public string Text { get; set; } = string.Empty;
    }
}
