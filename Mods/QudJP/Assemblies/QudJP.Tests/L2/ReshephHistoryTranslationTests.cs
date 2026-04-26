using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[DataContract]
public sealed class ReshephSampleEntry
{
    [DataMember(Name = "candidate_id")]
    public string CandidateId { get; set; } = "";
    [DataMember(Name = "event_property")]
    public string EventProperty { get; set; } = "";
    [DataMember(Name = "input_source")]
    public string InputSource { get; set; } = "";
    [DataMember(Name = "expected_japanese_contains")]
    public List<string> ExpectedJapaneseContains { get; set; } = new();
    [DataMember(Name = "expected_japanese_exact")]
    public string? ExpectedJapaneseExact { get; set; }
    [DataMember(Name = "expected_unchanged")]
    public bool ExpectedUnchanged { get; set; }
}

[DataContract]
internal sealed class ReshephSampleDocument
{
    [DataMember(Name = "schema_version")]
    public string SchemaVersion { get; set; } = "";
    [DataMember(Name = "samples")]
    public List<ReshephSampleEntry> Samples { get; set; } = new();
}

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ReshephHistoryTranslationTests
{
    private const string ExpectedSchemaVersion = "1";

    private static string GetFixturePath()
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "annals-samples.json");
    }

    public static IEnumerable<TestCaseData> Samples()
    {
        var path = GetFixturePath();
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fixture file not found: {path}", path);
        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(ReshephSampleDocument));
        var doc = serializer.ReadObject(stream) as ReshephSampleDocument;
        if (doc is null)
            throw new InvalidDataException($"Failed to deserialize fixture: {path}");
        if (doc.SchemaVersion != ExpectedSchemaVersion)
            throw new InvalidDataException($"Fixture schema_version mismatch: expected '{ExpectedSchemaVersion}', got '{doc.SchemaVersion}' in {path}");
        foreach (var sample in doc.Samples)
        {
            yield return new TestCaseData(sample).SetName($"Resheph_{sample.CandidateId}");
        }
    }

    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        var dictionaryDir = Path.Combine(localizationRoot, "Dictionaries");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDir);
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFilesForTests(null);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        JournalPatternTranslator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [TestCaseSource(nameof(Samples))]
    public void TranslateEventPropertiesDict_ProducesExpectedJapanese(ReshephSampleEntry sample)
    {
        var dict = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            [sample.EventProperty] = sample.InputSource,
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        var actual = dict[sample.EventProperty];

        var hasContains = sample.ExpectedJapaneseContains is { Count: > 0 };
        if (sample.ExpectedUnchanged && (sample.ExpectedJapaneseExact is not null || hasContains))
        {
            Assert.Fail(
                $"sample {sample.CandidateId}: expected_unchanged は expected_japanese_exact / expected_japanese_contains と同時に設定できません。");
            return;
        }
        if (!sample.ExpectedUnchanged && sample.ExpectedJapaneseExact is not null && hasContains)
        {
            Assert.Fail(
                $"sample {sample.CandidateId}: expected_japanese_exact と expected_japanese_contains の同時設定は不可です。どちらか一方を設定してください。");
            return;
        }

        if (sample.ExpectedUnchanged)
        {
            Assert.That(actual, Is.EqualTo(sample.InputSource),
                $"sample {sample.CandidateId}: expected unchanged passthrough but got '{actual}'");
            return;
        }
        if (sample.ExpectedJapaneseExact is not null)
        {
            Assert.That(actual, Is.EqualTo(sample.ExpectedJapaneseExact),
                $"sample {sample.CandidateId}: exact match failed. actual='{actual}'");
            return;
        }
        if (sample.ExpectedJapaneseContains is null || sample.ExpectedJapaneseContains.Count == 0)
        {
            Assert.Fail(
                $"sample {sample.CandidateId}: 期待値が未設定です。expected_unchanged / expected_japanese_exact / expected_japanese_contains のいずれかを設定してください。");
            return;
        }
        if (sample.ExpectedJapaneseContains.Exists(static needle => string.IsNullOrWhiteSpace(needle)))
        {
            Assert.Fail(
                $"sample {sample.CandidateId}: expected_japanese_contains に空文字または空白要素は設定できません。");
            return;
        }
        foreach (var needle in sample.ExpectedJapaneseContains)
        {
            Assert.That(actual, Does.Contain(needle),
                $"sample {sample.CandidateId}: translated output '{actual}' missing expected substring '{needle}'");
        }
    }
}
