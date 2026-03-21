using System.Text.Json;
using System.Xml.Linq;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class LocalizationCoverageTests
{
    private string localizationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        localizationRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.ResetForTests();
        ChargenStructuredTextTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ChargenStructuredTextTranslator.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [Test]
    public void SkillsAndPowersDictionary_CoversAllSkillAndPowerNames()
    {
        var skillsDocument = XDocument.Load(Path.Combine(localizationRoot, "Skills.jp.xml"));
        var skillNames = skillsDocument.Root!
            .Elements("skill")
            .Select(element => element.Attribute("Name")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var powerNames = skillsDocument.Root!
            .Descendants("power")
            .Select(element => element.Attribute("Name")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        var skillDictionaryPath = Path.Combine(localizationRoot, "Dictionaries", "ui-skillsandpowers.ja.json");
        var skillNameKeys = LoadKeysByContext(skillDictionaryPath, "TMP.Skill Name");

        Assert.Multiple(() =>
        {
            Assert.That(skillNames.Except(skillNameKeys).ToArray(), Is.Empty, "Missing skill-name entries in ui-skillsandpowers.");
            Assert.That(powerNames.Except(skillNameKeys).ToArray(), Is.Empty, "Missing power-name entries in ui-skillsandpowers.");
        });
    }

    [Test]
    public void WorldFactionsDictionary_CoversAllFactionNames()
    {
        var factionsDocument = XDocument.Load(Path.Combine(localizationRoot, "Factions.jp.xml"));
        var factionNames = factionsDocument.Root!
            .Elements("faction")
            .Select(element => element.Attribute("Name")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        var factionDictionaryPath = Path.Combine(localizationRoot, "Dictionaries", "world-factions.ja.json");
        var factionKeys = LoadKeysByContextSubstring(factionDictionaryPath, "Faction.Name");

        Assert.That(factionNames.Except(factionKeys).ToArray(), Is.Empty, "Missing faction-name entries in world-factions.");
    }

    [Test]
    public void ChargenTitles_CoverAllCallingSubtypeNames()
    {
        var subtypesDocument = XDocument.Load(Path.Combine(localizationRoot, "Subtypes.jp.xml"));
        var callingNames = subtypesDocument.Root!
            .Descendants("class")
            .Where(element => string.Equals(element.Attribute("ID")?.Value, "Callings", StringComparison.Ordinal))
            .Descendants("subtype")
            .Select(element => element.Attribute("Name")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        var chargenDictionaryPath = Path.Combine(localizationRoot, "Dictionaries", "ui-chargen.ja.json");
        var entries = LoadEntries(chargenDictionaryPath);
        var titlePairs = entries
            .Where(static entry => entry.Context.StartsWith("Chargen.Subtype.", StringComparison.Ordinal)
                                   && entry.Context.EndsWith(".Title", StringComparison.Ordinal))
            .Select(static entry => (entry.Context, entry.Key))
            .ToHashSet();

        var missing = callingNames
            .Where(name => !titlePairs.Contains(($"Chargen.Subtype.{name}.Title", name)))
            .ToArray();

        Assert.That(missing, Is.Empty, "Missing calling title entries in ui-chargen.");
    }

    [Test]
    public void ChargenStructuredTextTranslator_CoversAllMutationOptionNamesFromAssets()
    {
        var mutationNames = LoadMutationNamesWithDisplayName(Path.Combine(localizationRoot, "Mutations.jp.xml"))
            .Concat(LoadMutationNamesWithDisplayName(Path.Combine(localizationRoot, "HiddenMutations.jp.xml")))
            .Concat(LoadMutationOptionEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-chargen-supplement.ja.json")))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var untranslated = mutationNames
            .Where(name => string.Equals(ChargenStructuredTextTranslator.Translate(name), name, StringComparison.Ordinal))
            .ToArray();

        Assert.That(untranslated, Is.Empty, "Mutation option names are not covered by the exact-leaf chargen route.");
    }

    private static string[] LoadMutationNamesWithDisplayName(string path)
    {
        var document = XDocument.Load(path);
        return document.Root!
            .Descendants("mutation")
            .Where(element => element.Attribute("DisplayName") is not null)
            .Select(element => element.Attribute("Name")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    private static string[] LoadMutationOptionEntries(string path)
    {
        return LoadEntries(path)
            .Where(entry => string.Equals(entry.Context, "Chargen.Mutation.Option", StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToArray();
    }

    private static HashSet<string> LoadKeysByContext(string path, string context)
    {
        return LoadEntries(path)
            .Where(entry => string.Equals(entry.Context, context, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> LoadKeysByContextSubstring(string path, string contextFragment)
    {
        return LoadEntries(path)
            .Where(entry => entry.Context.Contains(contextFragment, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<DictionaryEntry> LoadEntries(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("entries")
            .EnumerateArray()
            .Select(static element => new DictionaryEntry(
                element.GetProperty("key").GetString() ?? string.Empty,
                element.TryGetProperty("context", out var contextProperty) ? contextProperty.GetString() ?? string.Empty : string.Empty))
            .ToArray();
    }

    private sealed record DictionaryEntry(string Key, string Context);
}
