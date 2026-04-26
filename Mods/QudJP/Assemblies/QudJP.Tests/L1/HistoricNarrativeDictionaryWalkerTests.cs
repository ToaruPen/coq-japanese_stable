using System.Text;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class HistoricNarrativeDictionaryWalkerTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-walker-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        WritePatternDictionary(); // ensure pattern file exists; tests can overwrite as needed
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

    private void WritePatternDictionary(params (string Pattern, string Template)[] entries)
    {
        var sb = new StringBuilder();
        sb.Append("{\"patterns\":[");
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var (pattern, template) = entries[i];
            sb.Append("{\"pattern\":\"").Append(EscapeJson(pattern))
              .Append("\",\"template\":\"").Append(EscapeJson(template)).Append("\"}");
        }
        sb.Append("]}");
        File.WriteAllText(patternFilePath, sb.ToString(), Utf8WithoutBom);
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

    // -- TranslateGospelEntry --

    [Test]
    public void TranslateGospelEntry_SplitsAndTranslatesProseOnly()
    {
        WritePatternDictionary(("^Hello world$", "こんにちは世界"));

        var result = HistoricNarrativeDictionaryWalker.TranslateGospelEntry("Hello world|42");

        Assert.That(result, Is.EqualTo("こんにちは世界|42"));
    }

    [Test]
    public void TranslateGospelEntry_NoSeparator_TranslatesEntireString()
    {
        WritePatternDictionary(("^Hello world$", "こんにちは世界"));

        var result = HistoricNarrativeDictionaryWalker.TranslateGospelEntry("Hello world");

        Assert.That(result, Is.EqualTo("こんにちは世界"));
    }

    [Test]
    public void TranslateGospelEntry_EmptyEventId_PreservesTrailingPipe()
    {
        var result = HistoricNarrativeDictionaryWalker.TranslateGospelEntry("Untranslated|");

        Assert.That(result, Is.EqualTo("Untranslated|"));
    }

    // -- TranslateEventPropertiesDict --

    [Test]
    public void TranslateEventPropertiesDict_TranslatesAllowlistedKeysOnly()
    {
        WritePatternDictionary(("^In a year, things happened\\.$", "ある年、何かが起こった。"));

        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gospel"] = "In a year, things happened.",
            ["tombInscriptionCategory"] = "CrownedSultan",
            ["region"] = "DesertCanyon-7-2",
            ["revealsRegion"] = "OldRegionName",
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        Assert.That(dict["gospel"], Is.EqualTo("ある年、何かが起こった。"));
        Assert.That(dict["tombInscriptionCategory"], Is.EqualTo("CrownedSultan"));
        Assert.That(dict["region"], Is.EqualTo("DesertCanyon-7-2"));
        Assert.That(dict["revealsRegion"], Is.EqualTo("OldRegionName"));
    }

    [Test]
    public void TranslateEventPropertiesDict_TranslatesTombInscription()
    {
        WritePatternDictionary(("^Here lies a forgotten one\\.$", "ここに忘れられし者眠る。"));

        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tombInscription"] = "Here lies a forgotten one.",
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        Assert.That(dict["tombInscription"], Is.EqualTo("ここに忘れられし者眠る。"));
    }

    [Test]
    public void TranslateEventPropertiesDict_SkipsLookalikeKeys()
    {
        WritePatternDictionary(("^Anything\\.$", "何でも。"));

        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gospelText"] = "Anything.", // not in allowlist (note the suffix)
            ["Gospel"] = "Anything.",     // case-sensitive miss
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        Assert.That(dict["gospelText"], Is.EqualTo("Anything."));
        Assert.That(dict["Gospel"], Is.EqualTo("Anything."));
    }

    [Test]
    public void TranslateEventPropertiesDict_PassthroughDoesNotMutateValue()
    {
        // No pattern dictionary: translator returns input unchanged.

        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gospel"] = "Untranslated gospel sentence.",
        };

        HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(dict);

        Assert.That(dict["gospel"], Is.EqualTo("Untranslated gospel sentence."));
    }

    // -- TranslateEntityViaCallbacks --

    [Test]
    public void TranslateEntityViaCallbacks_TranslatesEntityPropertiesViaWriteCallback()
    {
        WritePatternDictionary(("^A proverb\\.$", "ある格言。"));

        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["proverb"] = "A proverb.",
            ["worships_creature_id"] = "Snapjaw_creature_42",
        };
        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var writes = new List<(string Name, string Value)>();
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: name => snapshot.TryGetValue(name, out var v) ? v : null,
            readList: name => lists.TryGetValue(name, out var list) ? list : null,
            writeProperty: (name, value) => writes.Add((name, value)),
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(writes, Has.Count.EqualTo(1));
        Assert.That(writes[0].Name, Is.EqualTo("proverb"));
        Assert.That(writes[0].Value, Is.EqualTo("ある格言。"));
        // worships_creature_id (not in allowlist) must not generate a write event.
        Assert.That(writes.Any(w => w.Name == "worships_creature_id"), Is.False);
    }

    [Test]
    public void TranslateEntityViaCallbacks_AllPassthroughList_DoesNotInvokeMutateList()
    {
        // No pattern dictionary, all list elements unchanged after translate.

        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["sacredThings"] = new List<string> { "Untranslated A", "Untranslated B" },
        };
        var writes = new List<(string Name, string Value)>();
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: name => snapshot.TryGetValue(name, out var v) ? v : null,
            readList: name => lists.TryGetValue(name, out var list) ? list : null,
            writeProperty: (name, value) => writes.Add((name, value)),
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(mutations, Is.Empty,
            "All-passthrough lists should not call MutateListPropertyAtCurrentYear (no event noise).");
    }

    [Test]
    public void TranslateEntityViaCallbacks_PartiallyTranslatedList_InvokesMutateListOnce()
    {
        WritePatternDictionary(("^Sacred\\.$", "聖。"));

        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["sacredThings"] = new List<string> { "Sacred.", "Untranslated.", "Sacred." },
        };
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: _ => null,
            readList: name => lists.TryGetValue(name, out var list) ? list : null,
            writeProperty: (_, _) => { },
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(mutations, Has.Count.EqualTo(1));
        Assert.That(mutations[0].Name, Is.EqualTo("sacredThings"));
        // Verify the supplied mutation function does the right thing on each element.
        Assert.That(mutations[0].Mutation("Sacred."), Is.EqualTo("聖。"));
        Assert.That(mutations[0].Mutation("Untranslated."), Is.EqualTo("Untranslated."));
    }

    [Test]
    public void TranslateEntityViaCallbacks_GospelsListUsesGospelEntrySplit()
    {
        WritePatternDictionary(("^Hello world$", "こんにちは世界"));

        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Gospels"] = new List<string> { "Hello world|42" },
        };
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: _ => null,
            readList: name => lists.TryGetValue(name, out var list) ? list : null,
            writeProperty: (_, _) => { },
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(mutations, Has.Count.EqualTo(1));
        Assert.That(mutations[0].Name, Is.EqualTo("Gospels"));
        Assert.That(mutations[0].Mutation("Hello world|42"), Is.EqualTo("こんにちは世界|42"));
    }

    [Test]
    public void TranslateEntityViaCallbacks_NullList_NoOp()
    {
        var mutations = new List<(string Name, Func<string, string> Mutation)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: _ => null,
            readList: _ => null,
            writeProperty: (_, _) => { },
            mutateList: (name, mutation) => mutations.Add((name, mutation)));

        Assert.That(mutations, Is.Empty);
    }

    [Test]
    public void TranslateEntityViaCallbacks_NullOrEmptyProperty_NoWrite()
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["proverb"] = string.Empty,
            ["defaultSacredThing"] = string.Empty,
        };
        var writes = new List<(string Name, string Value)>();

        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: name => snapshot.TryGetValue(name, out var v) ? v : null,
            readList: _ => null,
            writeProperty: (name, value) => writes.Add((name, value)),
            mutateList: (_, _) => { });

        Assert.That(writes, Is.Empty);
    }
}
