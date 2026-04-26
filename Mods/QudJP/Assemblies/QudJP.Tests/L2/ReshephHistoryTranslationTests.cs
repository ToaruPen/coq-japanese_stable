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
        foreach (var needle in sample.ExpectedJapaneseContains)
        {
            Assert.That(actual, Does.Contain(needle),
                $"sample {sample.CandidateId}: translated output '{actual}' missing expected substring '{needle}'");
        }
    }
}
