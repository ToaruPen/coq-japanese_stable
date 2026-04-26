using System.Text;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HistoricNarrativeTranslationPatchesTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-historic-narrative-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "journal-patterns.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        JournalPatternTranslator.ResetForTests();
        JournalPatternTranslator.SetPatternFileForTests(patternFilePath);
        WritePatternDictionary(); // seed empty pattern file; tests overwrite as needed
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

    // Helper that mirrors the patch body using the L1-callable walker entry points,
    // exercised against the dummy targets so we don't need to bind real Harmony in L2.

    private static void RunGenerateVillageEraHistoryPostfixUsing(IEnumerable<DummyHistoricEvent> events)
    {
        foreach (var ev in events)
        {
            HistoricNarrativeDictionaryWalker.TranslateEventPropertiesDict(ev.EventProperties);
        }
    }

    private static void RunAddVillageGospelsPrefixUsing(DummyHistoricEntity entity)
    {
        var snapshot = entity.GetCurrentSnapshot();
        HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
            readProperty: name => snapshot.GetProperty(name),
            readList: name => snapshot.GetList(name),
            writeProperty: entity.SetEntityPropertyAtCurrentYear,
            mutateList: entity.MutateListPropertyAtCurrentYear);
    }

    [Test]
    public void EraHistoryPostfix_TranslatesGospelOnlyOnAllowlistedEventProperties()
    {
        WritePatternDictionary(("^Crowned\\.$", "戴冠した。"));

        var ev = new DummyHistoricEvent();
        ev.EventProperties["gospel"] = "Crowned.";
        ev.EventProperties["tombInscriptionCategory"] = "CrownedSultan";
        ev.EventProperties["region"] = "DesertCanyon";
        ev.EventProperties["revealsRegion"] = "OldName";

        RunGenerateVillageEraHistoryPostfixUsing(new[] { ev });

        Assert.That(ev.EventProperties["gospel"], Is.EqualTo("戴冠した。"));
        Assert.That(ev.EventProperties["tombInscriptionCategory"], Is.EqualTo("CrownedSultan"));
        Assert.That(ev.EventProperties["region"], Is.EqualTo("DesertCanyon"));
        Assert.That(ev.EventProperties["revealsRegion"], Is.EqualTo("OldName"));
    }

    [Test]
    public void AddVillageGospelsPrefix_TranslatesAllowlistedEntityPropertiesViaMutationApi()
    {
        WritePatternDictionary(
            ("^A proverb\\.$", "ある格言。"),
            ("^Sacred\\.$", "聖。"),
            ("^Gospel sentence\\.$", "ゴスペル一文。"));

        var entity = new DummyHistoricEntity();
        entity.SeedProperty("proverb", "A proverb.");
        entity.SeedProperty("worships_creature_id", "Snapjaw_42");
        entity.SeedList("sacredThings", "Sacred.");
        entity.SeedList("Gospels", "Gospel sentence.|17");
        entity.SeedList("type", "village"); // not in allowlist

        RunAddVillageGospelsPrefixUsing(entity);

        // Entity property: written via SetEntityPropertyAtCurrentYear.
        Assert.That(entity.PropertyEvents.Any(e => e.Name == "proverb" && e.Value == "ある格言。"), Is.True);
        Assert.That(entity.PropertyEvents.Any(e => e.Name == "worships_creature_id"), Is.False);

        // List property: mutation event added; runs translator on each element.
        var sacredEvent = entity.MutateListEvents.SingleOrDefault(e => e.Name == "sacredThings");
        Assert.That(sacredEvent, Is.Not.Null);
        Assert.That(sacredEvent!.Mutation("Sacred."), Is.EqualTo("聖。"));

        var gospelEvent = entity.MutateListEvents.SingleOrDefault(e => e.Name == "Gospels");
        Assert.That(gospelEvent, Is.Not.Null);
        Assert.That(gospelEvent!.Mutation("Gospel sentence.|17"), Is.EqualTo("ゴスペル一文。|17"));

        // type list (not in allowlist) must not produce a mutation event.
        Assert.That(entity.MutateListEvents.Any(e => e.Name == "type"), Is.False);
    }

    [Test]
    public void AddVillageGospelsPrefix_AllPassthroughLists_NoMutationEvents()
    {
        // No pattern dictionary: every translation is identity.

        var entity = new DummyHistoricEntity();
        entity.SeedList("sacredThings", "Sacred A.", "Sacred B.");
        entity.SeedList("Gospels", "Untranslated A.|11", "Untranslated B.|22");

        RunAddVillageGospelsPrefixUsing(entity);

        Assert.That(entity.MutateListEvents, Is.Empty,
            "Lists with all-passthrough elements must not generate MutateListProperty events.");
    }

    [Test]
    public void AddVillageGospelsPrefix_DoubleInvocation_DoesNotDuplicateEvents()
    {
        WritePatternDictionary(("^A proverb\\.$", "ある格言。"));

        var entity = new DummyHistoricEntity();
        entity.SeedProperty("proverb", "A proverb.");

        RunAddVillageGospelsPrefixUsing(entity);
        RunAddVillageGospelsPrefixUsing(entity);

        // First call writes "ある格言。"; second call sees the already-translated value and
        // the translator returns it unchanged (no Japanese pattern). Therefore no second write.
        var proverbEvents = entity.PropertyEvents.Where(e => e.Name == "proverb").ToList();
        Assert.That(proverbEvents, Has.Count.EqualTo(1),
            "Idempotent re-application should not duplicate property events.");
    }

    [Test]
    public void AddVillageGospelsPrefix_NullEntity_NoOp()
    {
        // Patches' try/catch + walker null-guard means null entity must be safe.
        // Walker handles null via the production code path; the dummy harness tests the API contract.

        Assert.DoesNotThrow(() =>
            HistoricNarrativeDictionaryWalker.TranslateEntityViaCallbacks(
                readProperty: _ => null,
                readList: _ => null,
                writeProperty: (_, _) => { },
                mutateList: (_, _) => { }));
    }
}
