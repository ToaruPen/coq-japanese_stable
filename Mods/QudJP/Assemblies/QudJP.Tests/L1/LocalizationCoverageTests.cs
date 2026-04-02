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

        var skillsDictionaryPath = Path.Combine(localizationRoot, "Dictionaries", "ui-skillsandpowers.ja.json");
        var scopedSkillsDictionaryPath = Path.Combine(localizationRoot, "Dictionaries", "Scoped", "ui-skillsandpowers-skill-names.ja.json");
        var skillNameKeys = LoadKeysByContext(skillsDictionaryPath, "TMP.Skill Name");
        skillNameKeys.UnionWith(LoadKeysByContext(scopedSkillsDictionaryPath, "TMP.Skill Name"));

        Assert.Multiple(() =>
        {
            Assert.That(skillNames.Except(skillNameKeys).ToArray(), Is.Empty, "Missing skill-name entries in skills dictionaries.");
            Assert.That(powerNames.Except(skillNameKeys).ToArray(), Is.Empty, "Missing power-name entries in skills dictionaries.");
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

    [Test]
    public void MutationDescriptionsDictionary_UsesCanonicalEsperKeyOnly()
    {
        const string legacyEsperKey = "You only manifest mental mutations, and all of your mutation choices when manifesting a new mutation are mental.";
        var entries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "mutation-descriptions.ja.json"));
        var esperEntries = entries
            .Where(static entry => string.Equals(entry.Key, "mutation:Esper", StringComparison.Ordinal))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(esperEntries.Length, Is.EqualTo(1), "mutation-descriptions should keep a single canonical mutation:Esper entry.");
            Assert.That(entries.Any(static entry => string.Equals(entry.Key, legacyEsperKey, StringComparison.Ordinal)), Is.False, "mutation-descriptions should not retain the legacy full-sentence Esper key.");
        });
    }

    [Test]
    public void WorldPartsDictionary_DoesNotReuseCookingOwnerKeys()
    {
        var dictionariesRoot = Path.Combine(localizationRoot, "Dictionaries");
        var cookingKeys = LoadEntries(Path.Combine(dictionariesRoot, "world-effects-cooking.ja.json"))
            .Select(static entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        var worldPartsCookingKeys = LoadEntries(Path.Combine(dictionariesRoot, "world-parts.ja.json"))
            .Where(static entry => entry.Context.StartsWith("XRL.World.XRL.World.Effects.CookingDomain", StringComparison.Ordinal))
            .Select(static entry => entry.Key)
            .Where(cookingKeys.Contains)
            .ToArray();

        Assert.That(
            worldPartsCookingKeys,
            Is.Empty,
            "world-parts should not duplicate cooking owner keys because Translator currently loads dictionaries by key only.");
    }

    [Test]
    public void WorldEffectsCookingDictionary_DoesNotContainSameTextDuplicateKeys()
    {
        var dictionariesRoot = Path.Combine(localizationRoot, "Dictionaries");
        var sameTextDuplicateKeys = LoadEntries(Path.Combine(dictionariesRoot, "world-effects-cooking.ja.json"))
            .GroupBy(static entry => entry.Key, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Where(static group => group.Select(static entry => entry.Text).Distinct(StringComparer.Ordinal).Count() == 1)
            .Select(static group => group.Key)
            .ToArray();

        Assert.That(
            sameTextDuplicateKeys,
            Is.Empty,
            "world-effects-cooking should not keep duplicate keys with the same text because Translator currently loads dictionaries by key only.");
    }

    [Test]
    public void WorldEffectsCookingDictionary_DoesNotContainQuestionMarkOnlyKeys()
    {
        var dictionariesRoot = Path.Combine(localizationRoot, "Dictionaries");
        var invalidKeys = LoadEntries(Path.Combine(dictionariesRoot, "world-effects-cooking.ja.json"))
            .Where(static entry => entry.Key.Length > 0 && entry.Key.All(static ch => ch == '?'))
            .Select(static entry => $"{entry.Context}:{entry.Key}")
            .ToArray();

        Assert.That(
            invalidKeys,
            Is.Empty,
            "world-effects-cooking should not contain mojibake question-mark keys when a concrete English source key exists.");
    }

    [Test]
    public void ConfirmedOwnerRouteDictionaries_ContainCurrentAbilityAndActiveEffectKeys()
    {
        var dictionariesRoot = Path.Combine(localizationRoot, "Dictionaries");
        var skillsAndPowersKeys = LoadEntries(Path.Combine(dictionariesRoot, "ui-skillsandpowers.ja.json"))
            .Select(static entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        var uiDefaultKeys = LoadEntries(Path.Combine(dictionariesRoot, "ui-default.ja.json"))
            .Select(static entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(skillsAndPowersKeys, Does.Contain("ABILITIES"));
            Assert.That(skillsAndPowersKeys, Does.Contain("page {0} of {1}"));
            Assert.That(uiDefaultKeys, Does.Contain("Active Effects - {0}"));
            Assert.That(uiDefaultKeys, Does.Contain("No active effects."));
        });
    }

    [Test]
    public void UiDefaultDictionary_ContainsCurrentCalendarStatusKeys()
    {
        var uiDefaultKeys = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-default.ja.json"))
            .Select(static entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        var expectedCalendarKeys = new[]
        {
            "Beetle Moon Zenith",
            "Waning Beetle Moon",
            "The Shallows",
            "Harvest Dawn",
            "Waxing Salt Sun",
            "High Salt Sun",
            "Waning Salt Sun",
            "Hindsun",
            "Jeweled Dusk",
            "Waxing Beetle Moon",
            "Zero Hour",
            "Nivvun Ut",
            "Iyur Ut",
            "Simmun Ut",
            "Tuum Ut",
            "Ubu Ut",
            "Uulu Ut",
            "Ut yara Ux",
            "Tishru i Ux",
            "Tishru ii Ux",
            "Kisu Ux",
            "Tebet Ux",
            "Shwut Ux",
            "Uru Ux",
        };

        Assert.That(
            expectedCalendarKeys.Except(uiDefaultKeys).ToArray(),
            Is.Empty,
            "ui-default should contain the full canonical calendar time-of-day and month key set.");
    }

    [Test]
    public void ConversationsOverlay_DefinesKindrishSharedChoiceIdsForCurrentInherits()
    {
        var conversationsDocument = XDocument.Load(Path.Combine(localizationRoot, "Conversations.jp.xml"));
        var expectedChoices = new[]
        {
            ("StayLong", "KindrishReturnChoice", "KindrishReturn"),
            ("Fate", "KindrishReturnChoice", "KindrishReturn"),
            ("Doomed", "KindrishReturnAfterChoice", "KindrishReturnAfter"),
            ("MocksFate", "KindrishReturnAfterChoice", "KindrishReturnAfter"),
        };

        Assert.Multiple(() =>
        {
            foreach (var (startId, choiceId, gotoId) in expectedChoices)
            {
                var choice = conversationsDocument.Root!
                    .Descendants("start")
                    .Where(element => string.Equals(element.Attribute("ID")?.Value, startId, StringComparison.Ordinal))
                    .Elements("choice")
                    .SingleOrDefault(element =>
                        string.Equals(element.Attribute("ID")?.Value, choiceId, StringComparison.Ordinal)
                        && string.Equals(element.Attribute("GotoID")?.Value, gotoId, StringComparison.Ordinal));

                Assert.That(
                    choice,
                    Is.Not.Null,
                    $"{startId}.{choiceId} should exist in Conversations.jp.xml so current Kindrish inherits resolve.");
            }
        });
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

    [Test]
    public void ChiliadFactions_DisplayNameOmissions_AreCoveredByFactionsXml()
    {
        var factionsDocument = XDocument.Load(Path.Combine(localizationRoot, "Factions.jp.xml"));
        var chiliadDocument = XDocument.Load(Path.Combine(localizationRoot, "ChiliadFactions.jp.xml"));

        var chiliadWithoutDisplayName = chiliadDocument.Root!
            .Elements("faction")
            .Where(element => element.Attribute("DisplayName") is null)
            .Select(element => element.Attribute("Name")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        var factionsDisplayNames = factionsDocument.Root!
            .Elements("faction")
            .Where(element => element.Attribute("DisplayName") is not null)
            .Select(element => element.Attribute("Name")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        var uncovered = chiliadWithoutDisplayName
            .Where(name => !factionsDisplayNames.Contains(name))
            .ToArray();

        Assert.That(
            uncovered,
            Is.Empty,
            "ChiliadFactions entries without DisplayName must be covered by Factions.jp.xml DisplayName. "
            + "The game's LoadFactionNode skips null/empty DisplayName, so Factions.jp.xml values are preserved.");
    }

    private static IReadOnlyList<DictionaryEntry> LoadEntries(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("entries")
            .EnumerateArray()
            .Select(static element => new DictionaryEntry(
                element.GetProperty("key").GetString() ?? string.Empty,
                element.TryGetProperty("context", out var contextProperty) ? contextProperty.GetString() ?? string.Empty : string.Empty,
                element.GetProperty("text").GetString() ?? string.Empty))
            .ToArray();
    }

    private sealed record DictionaryEntry(string Key, string Context, string Text);
}
