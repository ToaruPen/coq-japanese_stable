using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class LocalizationCoverageTests
{
    private static readonly HashSet<string> CommandCategoryKeys = new(StringComparer.Ordinal)
    {
        "Ability Bar",
        "Advanced Adventuring",
        "Adventuring",
        "Basic Move / Attack",
        "Character Creation",
        "Character Sheet",
        "Debug",
        "Menus",
        "Mouse-specific",
        "Shortcuts to Character Sheet",
        "System",
        "Targeting",
        "Trade",
        "UI",
    };

    private static readonly IReadOnlyDictionary<string, string> CommandCategoryLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Ability Bar"] = "アビリティバー",
        ["Advanced Adventuring"] = "高度な冒険操作",
        ["Adventuring"] = "冒険操作",
        ["Basic Move / Attack"] = "基本移動／攻撃",
        ["Character Creation"] = "キャラクター作成",
        ["Character Sheet"] = "キャラクターシート",
        ["Debug"] = "デバッグ",
        ["Menus"] = "メニュー操作",
        ["Mouse-specific"] = "マウス操作",
        ["Shortcuts to Character Sheet"] = "キャラクターシートショートカット",
        ["System"] = "システム",
        ["Targeting"] = "ターゲティング",
        ["Trade"] = "取引",
        ["UI"] = "UI",
    };

    private static readonly string[] ExpectedActiveEffectProducerClassifications =
    {
        "owner-translated",
        "fixed-leaf translated",
        "generated/composed route translated",
        "intentional pass-through",
        "deferred with reason",
    };

    private static readonly string[] ExpectedIssue739ObservedProducerIds =
    {
        "XRL.World.Effects/ProceduralCookingEffect.cs::ProceduralCookingEffect.ProceduralCookingEffect()",
        "XRL.World.Effects/ProceduralCookingEffect.cs::ProceduralCookingEffect.GetDescription()",
        "XRL.World.Effects/LongbladeStance_Defensive.cs::LongbladeStance_Defensive.LongbladeStance_Defensive()",
        "XRL.World.Effects/LongbladeStance_Aggressive.cs::LongbladeStance_Aggressive.LongbladeStance_Aggressive()",
        "XRL.World.Effects/LongbladeStance_Dueling.cs::LongbladeStance_Dueling.LongbladeStance_Dueling()",
        "XRL.World.Effects/LongbladeStance_Defensive.cs::LongbladeStance_Defensive.GetDetails()",
        "XRL.World.Effects/LongbladeStance_Aggressive.cs::LongbladeStance_Aggressive.GetDetails()",
        "XRL.World.Effects/LongbladeStance_Dueling.cs::LongbladeStance_Dueling.GetDetails()",
    };

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
    public void SkillsXml_CoversAllSkillAndPowerDescriptions_ForFormattedDescriptionPopups()
    {
        var skillsDocument = XDocument.Load(Path.Combine(localizationRoot, "Skills.jp.xml"));
        var describedEntries = skillsDocument.Root!
            .Elements("skill")
            .Concat(skillsDocument.Root!.Descendants("power"))
            .Select(static element => new
            {
                Name = element.Attribute("Name")?.Value ?? "<missing name>",
                Description = element.Attribute("Description")?.Value,
            })
            .ToArray();
        var missingDescriptions = describedEntries
            .Where(static entry => string.IsNullOrWhiteSpace(entry.Description))
            .Select(static entry => entry.Name)
            .ToArray();
        var englishDescriptions = describedEntries
            .Where(static entry => Regex.IsMatch(entry.Description ?? string.Empty, "[A-Za-z]{5,}"))
            .Select(static entry => $"{entry.Name}: {entry.Description}")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(missingDescriptions, Is.Empty, "SkillsAndPowersScreen.Show formatted-description popups require localized Description attributes.");
            Assert.That(englishDescriptions, Is.Empty, "Skill/power descriptions should not retain untranslated long English words.");
        });
    }

    [Test]
    public void CommandsXml_CommandCategoriesRemainStableGroupingKeys()
    {
        var commandsDocument = XDocument.Load(Path.Combine(localizationRoot, "Commands.jp.xml"));
        var categoryValues = commandsDocument.Root!
            .Elements("command")
            .Select(element => element.Attribute("Category")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var unsupportedCategories = categoryValues
            .Where(category => !CommandCategoryKeys.Contains(category))
            .ToArray();

        Assert.That(
            unsupportedCategories,
            Is.Empty,
            "Command Category is a stable grouping key consumed by Control Mapping. "
            + "Translate category labels at the UI route instead of localizing Category attributes.");
    }

    [Test]
    public void TranslateCommandCategoryLabel_CoversStableCategoryKeysAndFallbacks()
    {
        const string route = "Qud.UI.KeybindsScreen:category";
        const string family = "ui:keybind-category";

        Assert.Multiple(() =>
        {
            foreach (var (source, expected) in CommandCategoryLabels)
            {
                Assert.That(
                    UiBindingTranslationHelpers.TranslateCommandCategoryLabel(source, route, family),
                    Is.EqualTo(expected),
                    source);

                Assert.That(
                    UiBindingTranslationHelpers.TranslateCommandCategoryLabel(source.ToUpperInvariant(), route, family),
                    Is.EqualTo(expected),
                    source.ToUpperInvariant());
            }

            Assert.That(UiBindingTranslationHelpers.TranslateCommandCategoryLabel("Unknown Category", route, family), Is.EqualTo("Unknown Category"));
            Assert.That(UiBindingTranslationHelpers.TranslateCommandCategoryLabel("{{C|Basic Move / Attack}}", route, family), Is.EqualTo("{{C|Basic Move / Attack}}"));
            Assert.That(UiBindingTranslationHelpers.TranslateCommandCategoryLabel("\x01Basic Move / Attack", route, family), Is.EqualTo("\x01Basic Move / Attack"));
            Assert.That(UiBindingTranslationHelpers.TranslateCommandCategoryLabel(string.Empty, route, family), Is.EqualTo(string.Empty));
            Assert.That(UiBindingTranslationHelpers.TranslateCommandCategoryLabel(null!, route, family), Is.Null);
        });
    }

    [Test]
    public void CommandsXml_DirectionalBindingsRemainAlignedWithCommandIds()
    {
        var commandsDocument = XDocument.Load(Path.Combine(localizationRoot, "Commands.jp.xml"));

        Assert.Multiple(() =>
        {
            Assert.That(
                CommandElement(commandsDocument, "CmdAttackU")
                    .Elements("keyboardBind")
                    .Any(static element =>
                        string.Equals(element.Attribute("Modifier")?.Value, "ctrl,shift", StringComparison.Ordinal)
                        && string.Equals(element.Attribute("Key")?.Value, "comma", StringComparison.Ordinal)),
                Is.True,
                "CmdAttackU should keep the upstream Shift+, vertical-up bind.");

            Assert.That(
                CommandElement(commandsDocument, "CmdAttackD")
                    .Elements("keyboardBind")
                    .Any(static element =>
                        string.Equals(element.Attribute("Modifier")?.Value, "ctrl,shift", StringComparison.Ordinal)
                        && string.Equals(element.Attribute("Key")?.Value, "period", StringComparison.Ordinal)),
                Is.True,
                "CmdAttackD should keep the upstream Shift+. vertical-down bind.");

            Assert.That(
                CommandElement(commandsDocument, "UI:DetailsNavigate/left").Attribute("CanShareBindsWith")?.Value,
                Is.EqualTo("IndicateDirection/left,CmdMoveW"));
            Assert.That(
                CommandElement(commandsDocument, "UI:DetailsNavigate/left").Attribute("UpgradeFrom")?.Value,
                Is.EqualTo("CmdMoveW"));
            Assert.That(
                CommandElement(commandsDocument, "UI:DetailsNavigate/right").Attribute("CanShareBindsWith")?.Value,
                Is.EqualTo("IndicateDirection/right,CmdMoveE"));
            Assert.That(
                CommandElement(commandsDocument, "UI:DetailsNavigate/right").Attribute("UpgradeFrom")?.Value,
                Is.EqualTo("CmdMoveE"));
        });
    }

    [Test]
    public void ScopedSkillNameDictionaries_StayOnTheirOwnSurfaceAndOutOfFlatFamilies()
    {
        var scopedFamilies = new[]
        {
            (flatFile: "ui-chargen.ja.json", scopedFile: Path.Combine("Scoped", "ui-chargen-skill-context.ja.json"), expectedContext: "Chargen.SkillName"),
            (flatFile: "ui-skillsandpowers.ja.json", scopedFile: Path.Combine("Scoped", "ui-skillsandpowers-skill-names.ja.json"), expectedContext: "TMP.Skill Name"),
        };

        Assert.Multiple(() =>
        {
            foreach (var (flatFile, scopedFile, expectedContext) in scopedFamilies)
            {
                var flatKeys = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", flatFile))
                    .Select(static entry => entry.Key)
                    .ToHashSet(StringComparer.Ordinal);
                var scopedEntries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", scopedFile));

                var wrongContextEntries = scopedEntries
                    .Where(entry => !string.Equals(entry.Context, expectedContext, StringComparison.Ordinal))
                    .Select(entry => $"{entry.Context}:{entry.Key}")
                    .ToArray();
                var leakedKeys = scopedEntries
                    .Select(static entry => entry.Key)
                    .Where(flatKeys.Contains)
                    .ToArray();
                var duplicateTexts = scopedEntries
                    .GroupBy(static entry => entry.Text, StringComparer.Ordinal)
                    .Where(static group => group.Count() > 1)
                    .Select(static group => group.Key)
                    .ToArray();

                Assert.That(
                    wrongContextEntries,
                    Is.Empty,
                    $"{scopedFile} should stay on the {expectedContext} ownership surface.");
                Assert.That(
                    leakedKeys,
                    Is.Empty,
                    $"{scopedFile} should stay in the scoped tier instead of duplicating flat-family keys.");
                Assert.That(
                    duplicateTexts,
                    Is.Empty,
                    $"{scopedFile} should not duplicate the same text on the same ownership surface.");
            }
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
    public void ChargenPregenNames_CoverTutorialObservedMutantPresets()
    {
        var requiredNames = new[]
        {
            "Marsh Taur",
            "Dream Tortoise",
            "Gunwing",
            "Star-Eye Esper",
            "Firefrond",
            "bzzzt",
        };

        var chargenSupplementPath = Path.Combine(localizationRoot, "Dictionaries", "ui-chargen-supplement.ja.json");
        var pregenNameEntries = LoadEntries(chargenSupplementPath)
            .Where(static entry => string.Equals(entry.Context, "Chargen.Pregen.Name", StringComparison.Ordinal))
            .ToDictionary(static entry => entry.Key, static entry => entry.Text, StringComparer.Ordinal);

        var missing = requiredNames
            .Where(name => !pregenNameEntries.ContainsKey(name))
            .ToArray();
        var untranslated = requiredNames
            .Where(name => pregenNameEntries.TryGetValue(name, out var text)
                           && string.Equals(text, name, StringComparison.Ordinal))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.Empty, "Missing tutorial-observed pregen names in ui-chargen-supplement.");
            Assert.That(untranslated, Is.Empty, "Tutorial-observed pregen names should not remain identical to English.");
        });
    }

    [Test]
    public void ChargenPregenNames_UseCanonicalJapaneseNamesAcrossVisibleRoutes()
    {
        var expectedNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Marsh Taur"] = "湿地のタウル",
            ["Dream Tortoise"] = "夢見のトータス",
            ["Gunwing"] = "ガンウィング",
            ["Star-Eye Esper"] = "星眼のエスパー",
            ["Firefrond"] = "ファイアフロンド",
            ["bzzzt"] = "ビズズズト",
        };
        var forbiddenFragments = new[]
        {
            "マーシュ・ター",
            "マーシュ・タウル",
            "夢見る亀",
            "ブズズズト",
        };

        var chargenSupplementPath = Path.Combine(localizationRoot, "Dictionaries", "ui-chargen-supplement.ja.json");
        var pregenNameEntries = LoadEntries(chargenSupplementPath)
            .Where(static entry => string.Equals(entry.Context, "Chargen.Pregen.Name", StringComparison.Ordinal))
            .ToDictionary(static entry => entry.Key, static entry => entry.Text, StringComparer.Ordinal);
        var staleEntries = Directory
            .EnumerateFiles(Path.Combine(localizationRoot, "Dictionaries"), "*.ja.json", SearchOption.AllDirectories)
            .SelectMany(path => LoadEntries(path).Select(entry => (Path: path, Entry: entry)))
            .Where(row => forbiddenFragments.Any(fragment => row.Entry.Text.Contains(fragment, StringComparison.Ordinal)))
            .Select(row => $"{Path.GetRelativePath(localizationRoot, row.Path)}:{row.Entry.Key}:{row.Entry.Text}")
            .ToArray();

        Assert.Multiple(() =>
        {
            foreach (var expected in expectedNames)
            {
                Assert.That(
                    pregenNameEntries.TryGetValue(expected.Key, out var actual) ? actual : null,
                    Is.EqualTo(expected.Value),
                    $"Unexpected canonical pregen translation for '{expected.Key}'.");
            }

            Assert.That(staleEntries, Is.Empty, "Visible dictionary routes must not keep stale pregen-name variants.");
        });
    }

    [Test]
    public void TutorialDictionary_CoversStaticHighlightObjectRouteKeys()
    {
        var expectedEntries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["You regain hitpoints naturally as turns pass. You can pass a few turns by waiting, or if there are no hostile creatures around, you can {{W|rest until healed}}.\n\nPress ~CmdWaitUntilHealed"] = "ターンが経過すると、ヒットポイントは自然に回復します。数ターン待機することもできますし、周囲に敵対的な生き物がいなければ、{{W|完全に回復するまで休む}}こともできます。\n\n~CmdWaitUntilHealed を押してください。",
            ["You picked up the odd trinket automatically because it is an artifact.\n\nPress ~CmdCharacter to investigate it."] = "奇妙な小物はアーティファクトなので、自動的に拾いました。\n\n調べるには ~CmdCharacter を押してください。",
            ["You picked up the odd trinket automatically because it is an artifact.\n\nPress ~CmdInventory to investigate it."] = "奇妙な小物はアーティファクトなので、自動的に拾いました。\n\n調べるには ~CmdInventory を押してください。",
            ["The bear is dead! Looks like it dropped something, too."] = "熊は死にました！ 何かを落としたようです。",
            ["Use the campfire."] = "キャンプファイアを使ってください。",
            ["Let's not be rude. Talk to the beetle.\n\nPress ~CmdUse or ~AdventureMouseContextAction."] = "失礼にならないようにしましょう。甲虫に話しかけてください。\n\n~CmdUse または ~AdventureMouseContextAction を押してください。",
            ["Let's not be rude. Talk to the beetle."] = "失礼にならないようにしましょう。甲虫に話しかけてください。",
        };

        var entries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-tutorial.ja.json"))
            .Where(static entry => string.Equals(entry.Context, "Qud.UI.TutorialManager.HighlightCell", StringComparison.Ordinal))
            .ToDictionary(static entry => entry.Key, static entry => entry.Text, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            foreach (var expected in expectedEntries)
            {
                Assert.That(
                    entries.TryGetValue(expected.Key, out var actual) ? actual : null,
                    Is.EqualTo(expected.Value),
                    $"Missing or mismatched tutorial HighlightObject route text for '{expected.Key}'.");
            }
        });
    }

    [Test]
    public void ChargenAttributeHelpText_CoversRuntimeObservedTrueKinEgoDescription()
    {
        const string trueKinEgoDescription =
            "Your {{W|Ego}} score determines the potency of your ability to haggle with merchants, and your ability to dominate the wills of other living creatures.";

        var chargenDictionaryPath = Path.Combine(localizationRoot, "Dictionaries", "ui-chargen.ja.json");
        var matchingEntries = LoadEntries(chargenDictionaryPath)
            .Where(static entry => entry.Key == trueKinEgoDescription)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(matchingEntries, Has.Length.EqualTo(1), "Missing runtime True Kin Ego help text in ui-chargen.");
            Assert.That(
                matchingEntries.Select(static entry => entry.Context),
                Is.EquivalentTo(new[] { "Chargen.Attributes.HelpText" }));
            Assert.That(
                matchingEntries.Select(static entry => entry.Text),
                Is.EquivalentTo(new[] { "あなたの{{W|自我}}は、商人との値引き交渉力、および他の生物の意志を支配する能力を決定します。" }));
        });
    }

    [Test]
    public void CallingSubtypeExtraInfoOverrides_RemoveBaseEnglishExtraInfo()
    {
        var subtypesDocument = XDocument.Load(Path.Combine(localizationRoot, "Subtypes.jp.xml"));
        var expectedRemovedExtraInfo = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Arconaut"] = new[] { "Starts with random junk and artifacts" },
            ["Nomad"] = new[] { "Starts with a {{B|recycling suit}}" },
            ["Tinker"] = new[] { "Begins with a number of random artifacts and scrap" },
            ["Water Merchant"] = new[]
            {
                "Allowed entrance to many settlements for purposes of trade",
                "Starts with trade goods",
            },
            ["Watervine Farmer"] = new[] { "Starts with random cooking ingredients" },
        };

        var missing = expectedRemovedExtraInfo
            .SelectMany(pair =>
            {
                var subtype = subtypesDocument.Root!
                    .Descendants("subtype")
                    .FirstOrDefault(element => string.Equals(element.Attribute("Name")?.Value, pair.Key, StringComparison.Ordinal));

                if (subtype is null)
                {
                    return pair.Value.Select(value => $"{pair.Key}:{value} (subtype missing)");
                }

                var removedValues = subtype.Elements("removeextrainfo")
                    .Select(element => element.Value)
                    .ToHashSet(StringComparer.Ordinal);

                return pair.Value
                    .Where(value => !removedValues.Contains(value))
                    .Select(value => $"{pair.Key}:{value}");
            })
            .ToArray();

        Assert.That(
            missing,
            Is.Empty,
            "Localized calling subtype extrainfo overrides must remove the base English extrainfo first.");
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
    public void MutationDescriptionsDictionary_CoversAllMutationEntriesFromAssets()
    {
        var mutationNames = LoadMutationNamesWithDisplayName(Path.Combine(localizationRoot, "Mutations.jp.xml"))
            .Concat(LoadMutationNamesWithDisplayName(Path.Combine(localizationRoot, "HiddenMutations.jp.xml")))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var descriptionKeys = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "mutation-descriptions.ja.json"))
            .Select(static entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        var missing = mutationNames
            .Where(name => !descriptionKeys.Contains($"mutation:{name}"))
            .ToArray();

        Assert.That(
            missing,
            Is.Empty,
            "Every mutation asset entry should have a mutation:<Name> long-description key for popup and menu owner routes.");
    }

    [Test]
    public void ChargenStructuredTextTranslator_CoversRuntimeObservedSkillLeaves()
    {
        var expectedTranslations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Axe"] = "斧",
            ["Butchery"] = "解体術",
            ["Cleave"] = "裂断",
            ["Deploy Turret"] = "タレット展開",
            ["Harvestry"] = "収穫術",
            ["Nostrums"] = "秘薬",
            ["Shield Slam"] = "シールドスラム",
            ["Snake Oiler"] = "蛇油売り",
            ["Tactics"] = "戦術",
            ["Tank"] = "戦車乗り",
            ["Tinker I"] = "工匠 I",
            ["Tinker II"] = "工匠 II",
            ["Weak Spotter"] = "急所狙い",
            ["Wilderness Lore: Canyons"] = "荒地巡り：峡谷",
            ["Wilderness Lore: Hills and Mountains"] = "荒地巡り：丘陵と山",
            ["Wilderness Lore: Jungles"] = "荒地巡り：ジャングル",
        };

        Assert.Multiple(() =>
        {
            foreach (var expected in expectedTranslations)
            {
                Assert.That(
                    ChargenStructuredTextTranslator.Translate(expected.Key),
                    Is.EqualTo(expected.Value),
                    expected.Key);
            }
        });
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
    public void MutationAndAbilityStaticText_BatchA_DoesNotRegressKnownEnglishResidueOrMechanics()
    {
        var descriptions = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "mutation-descriptions.ja.json"))
            .ToDictionary(static entry => entry.Key, static entry => entry.Text, StringComparer.Ordinal);
        var rankTexts = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "mutation-ranktext.ja.json"))
            .ToDictionary(static entry => entry.Key, static entry => entry.Text, StringComparer.Ordinal);
        var abilitiesDocument = XDocument.Load(Path.Combine(localizationRoot, "ActivatedAbilities.jp.xml"));
        var swoopDescription = abilitiesDocument.Root!
            .Elements("ability")
            .Single(element => string.Equals(element.Attribute("Command")?.Value, "CommandSwoopAttack", StringComparison.Ordinal))
            .Element("description")!
            .Value;

        Assert.Multiple(() =>
        {
            Assert.That(
                swoopDescription,
                Does.Contain("1ターンかけて攻撃し、もう1ターンで上空へ戻る"),
                "CommandSwoopAttack must preserve the source meaning: one turn to attack, one turn to return.");

            Assert.That(
                descriptions["mutation:Metamorphosis"],
                Is.EqualTo("触れたあらゆるクリーチャーの姿をとる。"),
                "Metamorphosis long description should not add unsupported equipment or self-level claims.");
            Assert.That(
                descriptions["mutation:Blinking Tic"],
                Does.Contain("戦闘中、毎ラウンド低確率で近くの場所へランダムに瞬間移動する。"),
                "Blinking Tic must preserve the combat-per-round random nearby teleport behavior.");

            Assert.That(descriptions["mutation:Photosynthetic Skin"], Does.Not.Contain("{{rules|1 day}}"));
            Assert.That(descriptions["mutation:Photosynthetic Skin"], Does.Not.Contain("Consortium of Phyta"));

            for (var rank = 1; rank <= 10; rank++)
            {
                var burrowingText = rankTexts[$"mutation:Burrowing Claws:rank:{rank}"];
                Assert.That(burrowingText, Does.Not.Contain("penetrating hits"));
                Assert.That(burrowingText, Does.Not.Contain("base damage to non-walls"));
                Assert.That(burrowingText, Does.Contain("爪で4回貫通すると壁を破壊する。"));

                var electricalText = rankTexts[$"mutation:Electrical Generation:rank:{rank}"];
                Assert.That(electricalText, Does.Contain("1000チャージごとに1d4ダメージ"));
                Assert.That(electricalText, Does.Contain("1000チャージごとに最大1体へ連鎖する。"));
                Assert.That(electricalText, Does.Not.Contain("チャージ1点ごとに4d1000"));
                Assert.That(electricalText, Does.Not.Contain("最大1チャージあたり1000体"));
            }
        });
    }

    [Test]
    public void SkulkTonicRulesDescription_DoesNotRegressToEnglishRulesText()
    {
        var itemsDocument = XDocument.Load(Path.Combine(localizationRoot, "ObjectBlueprints", "Items.jp.xml"));
        var rulesDescription = itemsDocument.Root!
            .Elements("object")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "SkulkTonic", StringComparison.Ordinal))
            .Elements("part")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "RulesDescription", StringComparison.Ordinal));

        var text = rulesDescription.Attribute("Text")?.Value ?? string.Empty;
        var genotypeAlt = rulesDescription.Attribute("GenotypeAlt")?.Value ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("持続：1001-1200ラウンド"));
            Assert.That(genotypeAlt, Does.Contain("持続：1001-1200ラウンド"));
            Assert.That(text, Does.Not.Contain("Duration:"));
            Assert.That(genotypeAlt, Does.Not.Contain("Duration:"));
            Assert.That(text, Does.Not.Contain("Your movement speed"));
            Assert.That(genotypeAlt, Does.Not.Contain("Your movement speed"));
        });
    }

    [Test]
    public void HulkHoneyTonicRulesDescription_DoesNotRegressToEnglishRulesText()
    {
        var itemsDocument = XDocument.Load(Path.Combine(localizationRoot, "ObjectBlueprints", "Items.jp.xml"));
        var rulesDescription = itemsDocument.Root!
            .Elements("object")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "HulkHoneyTonic", StringComparison.Ordinal))
            .Elements("part")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "RulesDescription", StringComparison.Ordinal));

        var text = rulesDescription.Attribute("Text")?.Value ?? string.Empty;
        var genotypeAlt = rulesDescription.Attribute("GenotypeAlt")?.Value ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("持続：41-50ラウンド"));
            Assert.That(genotypeAlt, Does.Contain("持続：41-50ラウンド"));
            Assert.That(text, Does.Contain("筋力 +6"));
            Assert.That(genotypeAlt, Does.Contain("筋力 +9"));
            Assert.That(text, Does.Not.Contain("Duration:"));
            Assert.That(genotypeAlt, Does.Not.Contain("Duration:"));
            Assert.That(text, Does.Not.Contain("Strength"));
            Assert.That(genotypeAlt, Does.Not.Contain("Strength"));
        });
    }

    [Test]
    public void CtesiphusPetResponse_DoesNotRegressToEnglishMeow()
    {
        var creaturesDocument = XDocument.Load(Path.Combine(localizationRoot, "ObjectBlueprints", "Creatures.jp.xml"));
        var pettable = creaturesDocument.Root!
            .Elements("object")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "Ctesiphus", StringComparison.Ordinal))
            .Elements("part")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "Pettable", StringComparison.Ordinal));
        var petResponseTag = creaturesDocument.Root!
            .Elements("object")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "Ctesiphus", StringComparison.Ordinal))
            .Elements("tag")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "PetResponse", StringComparison.Ordinal));

        var petResponse = petResponseTag.Attribute("Value")?.Value ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(pettable.Attribute("PetResponse"), Is.Null);
            Assert.That(petResponse, Is.EqualTo("=subject.T=がにゃあと鳴く。"));
            Assert.That(petResponse, Does.Not.Contain("meow"));
            Assert.That(petResponse, Does.Not.Contain("meows"));
        });
    }

    [Test]
    public void ObjectBlueprintDescriptionText_DoesNotUsePoliteNarration()
    {
        var objectBlueprintRoot = Path.Combine(localizationRoot, "ObjectBlueprints");
        var politePattern = new Regex(
            "(?:でした|ました|でしょう|ません|ください|かもしれません|です(?=[。！？?？、,]|$))",
            RegexOptions.CultureInvariant);
        var offenders = Directory.EnumerateFiles(objectBlueprintRoot, "*.jp.xml")
            .SelectMany(file => XDocument.Load(file).Root!
                .Elements("object")
                .SelectMany(obj => obj.Elements("part")
                    .Where(static part =>
                        string.Equals(part.Attribute("Name")?.Value, "Description", StringComparison.Ordinal)
                        || string.Equals(part.Attribute("Name")?.Value, "RulesDescription", StringComparison.Ordinal))
                    .SelectMany(part => new[] { "Short", "Text", "GenotypeAlt" }
                        .Select(attributeName => new
                        {
                            FileName = Path.GetFileName(file),
                            ObjectName = obj.Attribute("Name")?.Value ?? string.Empty,
                            PartName = part.Attribute("Name")?.Value ?? string.Empty,
                            AttributeName = attributeName,
                            Value = part.Attribute(attributeName)?.Value ?? string.Empty,
                        }))))
            .Where(row => !string.IsNullOrEmpty(row.Value) && politePattern.IsMatch(row.Value))
            .Select(row => $"{row.FileName}#{row.ObjectName}:{row.PartName}.{row.AttributeName}={row.Value}")
            .ToArray();

        Assert.That(
            offenders,
            Is.Empty,
            "Object and rules descriptions should use plain descriptive narration; dialogue/chat commands are outside this check.");
    }

    [Test]
    public void CrackedLensHistorySpiceToken_MatchesObjectDisplayName()
    {
        var itemsDocument = XDocument.Load(Path.Combine(localizationRoot, "ObjectBlueprints", "Items.jp.xml"));
        var crackedLensDisplayName = itemsDocument.Root!
            .Elements("object")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "Scrap Crystal", StringComparison.Ordinal))
            .Elements("part")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "Render", StringComparison.Ordinal))
            .Attribute("DisplayName")?.Value;
        var historySpiceTokenText = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "world-gospels.ja.json"))
            .Single(entry => string.Equals(entry.Key, "cracked lens", StringComparison.Ordinal))
            .Text;

        Assert.Multiple(() =>
        {
            Assert.That(crackedLensDisplayName, Is.EqualTo("ひび割れたレンズ"));
            Assert.That(historySpiceTokenText, Is.EqualTo(crackedLensDisplayName));
        });
    }

    [Test]
    public void TreatAsSolidMessages_AreLocalizedInObjectBlueprintOverlays()
    {
        var objectBlueprintRoot = Path.Combine(localizationRoot, "ObjectBlueprints");
        var treatAsSolidMessages = Directory.EnumerateFiles(objectBlueprintRoot, "*.jp.xml")
            .SelectMany(file => XDocument.Load(file).Root!
                .Elements("object")
                .SelectMany(obj => obj
                    .Elements("part")
                    .Where(static part => string.Equals(
                        part.Attribute("Name")?.Value,
                        "TreatAsSolid",
                        StringComparison.Ordinal))
                    .Select(part => new
                    {
                        FileName = Path.GetFileName(file),
                        ObjectName = obj.Attribute("Name")?.Value ?? string.Empty,
                        Message = part.Attribute("Message")?.Value ?? string.Empty,
                    })))
            .Where(static row => !string.IsNullOrWhiteSpace(row.Message))
            .ToArray();

        Assert.That(treatAsSolidMessages, Has.Length.EqualTo(20));
        Assert.Multiple(() =>
        {
            foreach (var row in treatAsSolidMessages)
            {
                Assert.That(row.Message, Does.Contain("=subject."),
                    $"{row.FileName}#{row.ObjectName} must preserve the subject placeholder.");
                Assert.That(row.Message, Does.Not.Contain("The darkness consumes"),
                    $"{row.FileName}#{row.ObjectName} must not keep the English darkness template.");
                Assert.That(row.Message, Does.Not.Contain("under the pressure of normality"),
                    $"{row.FileName}#{row.ObjectName} must not keep the English normality template.");
                Assert.That(row.Message, Does.Not.Contain("=verb:collapse="),
                    $"{row.FileName}#{row.ObjectName} must not keep the English verb slot.");
            }

            Assert.That(treatAsSolidMessages.Select(static row => row.Message), Has.Some.Contains("闇"));
            Assert.That(treatAsSolidMessages.Select(static row => row.Message), Has.Some.Contains("常態"));
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
    public void DynamicProducerRoutes_DoNotKeepKnownConcreteExactKeys()
    {
        var dictionariesRoot = Path.Combine(localizationRoot, "Dictionaries");
        var popupConcreteKeys = LoadEntries(Path.Combine(dictionariesRoot, "ui-popup.ja.json"))
            .Where(static entry =>
                IsConcreteOutOfRange(entry.Key)
                || IsConcreteTargetOutOfRange(entry.Key)
                || IsConcreteSultanHistoryJournalNotification(entry.Key))
            .Select(static entry => entry.Key)
            .ToArray();
        var cookingConcreteKeys = LoadEntries(Path.Combine(dictionariesRoot, "world-effects-cooking.ja.json"))
            .Where(static entry => IsConcreteHpIncreaseDescription(entry.Key) || IsConcreteHpIncreaseDetails(entry.Key))
            .Select(static entry => entry.Key)
            .ToArray();
        var cookingDynamicPrefixKeys = LoadEntries(Path.Combine(dictionariesRoot, "world-effects-cooking.ja.json"))
            .Where(static entry =>
                IsCookingDynamicPrefixFragment(entry.Key)
                || IsCookingMutationDynamicFragment(entry.Key)
                || IsBasicCookingEffectDetailsFragment(entry.Key))
            .Select(static entry => entry.Key)
            .ToArray();
        var messageLogConcreteKeys = LoadEntries(Path.Combine(dictionariesRoot, "ui-messagelog-leaf.ja.json"))
            .Where(static entry => IsConcreteFallToGround(entry.Key) || IsConcreteFallAsleep(entry.Key))
            .Select(static entry => entry.Key)
            .ToArray();
        var worldPartsDynamicFragments = LoadEntries(Path.Combine(dictionariesRoot, "world-parts.ja.json"))
            .Where(static entry => IsWorldPartsDynamicFragment(entry.Key))
            .Select(static entry => entry.Key)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                popupConcreteKeys,
                Is.Empty,
                "Known dynamic popup strings should be translated by popup or journal producer routes, not concrete exact keys.");
            Assert.That(
                cookingConcreteKeys,
                Is.Empty,
                "Known dynamic cooking HP strings should be translated by CookingEffectTranslationPatch, not concrete exact keys.");
            Assert.That(
                cookingDynamicPrefixKeys,
                Is.Empty,
                "Dynamic cooking producer fragments should be translated by owner routes, not broad prefix-like exact dictionary keys.");
            Assert.That(
                messageLogConcreteKeys,
                Is.Empty,
                "Known dynamic fall/asleep messages should be translated by message patterns, not concrete leaf exact keys.");
            Assert.That(
                worldPartsDynamicFragments,
                Is.Empty,
                "Known dynamic world-parts discovery messages should be translated by popup or journal producer routes, not prefix/suffix exact fragments.");
        });
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
        var worldEffectsStatusKeys = LoadEntries(Path.Combine(dictionariesRoot, "world-effects-status.ja.json"))
            .Select(static entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(skillsAndPowersKeys, Does.Contain("ABILITIES"));
            Assert.That(skillsAndPowersKeys, Does.Contain("page {0} of {1}"));
            Assert.That(uiDefaultKeys, Does.Contain("Active Effects - {0}"));
            Assert.That(uiDefaultKeys, Does.Contain("No active effects."));
            Assert.That(worldEffectsStatusKeys, Does.Contain("corrected vision"));
        });
    }

    [Test]
    public void WorldEffectsStatusDictionary_CoversStaticActiveEffectDescriptionLeaves()
    {
        var worldEffectsStatusKeys = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "world-effects-status.ja.json"))
            .Select(static entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        var expectedKeys = new[]
        {
            "&minhabited",
            "astrally burdened",
            "covered in liquid",
            "demolishing",
            "distracted by a decoy",
            "emboldened",
            "emptying the clips",
            "entranced",
            "greased",
            "hooked",
            "immobilized",
            "incommunicado",
            "locked in psychic battle",
            "meditating",
            "shaken",
            "springing",
            "sprinting",
            "stained by liquid",
            "suppressed",
            "synapse snap",
            "syphoned",
            "waking dream",
            "{{B|flying}}",
            "{{B|mobility impaired}}",
            "{{B|projecting consciousness}}",
            "{{B|pulsed}}",
            "{{B|stressed}}",
            "{{B|submerged}}",
            "{{B|swimming}}",
            "{{B|wading}}",
            "{{C|hobbled}}",
            "{{C|interdicted}}",
            "{{C|piloting}}",
            "{{C|rebuked}}",
            "{{C|sitting}}",
            "{{C|stunned by gas}}",
            "{{C|unpiloted}}",
            "{{C|warming up}}",
            "{{G|ecstatic}}",
            "{{G|poisoned by gas}}",
            "{{G|refreshed}}",
            "{{K|disguised}}",
            "{{K|gleaming}}",
            "{{K|overburdened}}",
            "{{K|unpowered}}",
            "{{M|in stasis}}",
            "{{M|inspired}}",
            "{{M|mutating}}",
            "{{O|shimmering}}",
            "{{R|FURIOUS}}",
            "{{R|crippled}}",
            "{{R|famished}}",
            "{{R|wilted}}",
            "{{W|cardiac arrest}}",
            "{{W|covered in spores}}",
            "{{W|crackling}}",
            "{{W|dashing}}",
            "{{W|safe mode}}",
            "{{W|well fed}}",
            "{{Y|omniphase}}",
            "{{Y|proselytized}}",
            "{{Y|reflectively shielded}}",
            "{{Y|sizzling}}",
            "{{Y|tomb-tethered}}",
            "{{b|deep dreaming}}",
            "{{camouflage|camouflaged}}",
            "{{coated in plasma|coated in plasma}}",
            "{{c|cybernetic rejection syndrome}}",
            "{{g|nullphased}}",
            "{{g|phase spider venom}}",
            "{{g|shield wall}}",
            "{{g|vitalized}}",
            "{{lovesickness|lovesick}}",
            "{{m|phosphorescent}}",
            "{{m|quantum-locked}}",
            "{{rainbow|irisdual molting}}",
            "{{rainbow|scintillating}}",
            "{{r|broken}}",
            "{{r|budding}}",
            "{{r|callow}}",
            "{{r|cracked}}",
            "{{r|disoriented}}",
            "{{r|drained}}",
            "{{r|flagging}}",
            "{{r|latched onto}}",
            "{{r|nosebleed}}",
            "{{r|prowling}}",
            "{{r|shamed}}",
            "{{r|war trance}}",
            "{{urban camouflage|urban camouflage}}",
            "{{w|burrowed}}",
            "{{w|grounded}}",
        };

        Assert.That(
            expectedKeys.Where(key => !worldEffectsStatusKeys.Contains(key)).ToArray(),
            Is.Empty,
            "Static active-effect description leaves from the decompiled effect inventory must stay covered.");
    }

    [Test]
    public void ActiveEffectDictionaries_CoverIssue739ObservedRuntimeSamples()
    {
        var dictionariesRoot = Path.Combine(localizationRoot, "Dictionaries");
        var cookingEntries = LoadEntries(Path.Combine(dictionariesRoot, "world-effects-cooking.ja.json"));
        var statusEntries = LoadEntries(Path.Combine(dictionariesRoot, "world-effects-status.ja.json"));
        var generatedEntries = LoadEntries(Path.Combine(dictionariesRoot, "Scoped", "world-effects-generated-templates.ja.json"));

        Assert.Multiple(() =>
        {
            Assert.That(
                cookingEntries,
                Does.Contain(new DictionaryEntry("metabolizing", "XRL.World.Effects.ProceduralCookingEffect.DisplayName", "代謝中")));
            Assert.That(
                cookingEntries,
                Does.Contain(new DictionaryEntry("{{W|metabolizing}}", "XRL.World.Effects.ProceduralCookingEffect.GetDescription", "{{W|代謝中}}")));
            Assert.That(
                cookingEntries,
                Does.Contain(new DictionaryEntry("{{w|metabolized effect}}", "XRL.World.Effects.BasicTriggeredCookingEffect.GetDescription", "{{w|代謝効果}}")));
            Assert.That(
                cookingEntries,
                Does.Contain(new DictionaryEntry(
                    "@thisCreature thirst@s at half rate.",
                    "XRL.World.Effects.ProceduralCookingEffectUnit_LessThirst.GetDescription",
                    "喉の渇きが半減する。")));
            Assert.That(
                cookingEntries,
                Does.Contain(new DictionaryEntry("You thirst at half rate.", "XRL.World.Skills.Cooking.AppleMatz.GetDescription", "喉の渇きが半減する。")));
            Assert.That(
                generatedEntries,
                Does.Contain(new DictionaryEntry(
                    "+{0} DV while wielding a long blade in the primary hand.",
                    "XRL.World.Effects.LongbladeStance_Defensive.GetDetails",
                    "主手に長剣を装備しているあいだDV+{0}。")));
            Assert.That(
                generatedEntries,
                Does.Contain(new DictionaryEntry(
                    "+{0} to your penetration roll and -{1} to hit while wielding a long blade in the primary hand.",
                    "XRL.World.Effects.LongbladeStance_Aggressive.GetDetails",
                    "主手に長剣を装備しているあいだ貫通判定+{0}、命中-{1}。")));
            Assert.That(
                generatedEntries,
                Does.Contain(new DictionaryEntry(
                    "+{0} to hit while wielding a long blade in the primary hand.",
                    "XRL.World.Effects.LongbladeStance_Dueling.GetDetails",
                    "主手に長剣を装備しているあいだ命中+{0}。")));
            Assert.That(
                statusEntries,
                Does.Contain(new DictionaryEntry(
                    "{{G|defensive stance}}",
                    "XRL.World.Effects.LongbladeStance_Defensive.DisplayName",
                    "{{G|防御姿勢}}")));
            Assert.That(
                statusEntries,
                Does.Contain(new DictionaryEntry(
                    "aggressive stance",
                    "XRL.World.Effects.LongbladeStance_Aggressive.DisplayName",
                    "攻撃姿勢")));
            Assert.That(
                statusEntries,
                Does.Contain(new DictionaryEntry(
                    "{{R|aggressive stance}}",
                    "XRL.World.Effects.LongbladeStance_Aggressive.DisplayName",
                    "{{R|攻撃姿勢}}")));
            Assert.That(
                statusEntries,
                Does.Contain(new DictionaryEntry(
                    "dueling stance",
                    "XRL.World.Effects.LongbladeStance_Dueling.DisplayName",
                    "決闘姿勢")));
            Assert.That(
                statusEntries,
                Does.Contain(new DictionaryEntry(
                    "{{W|dueling stance}}",
                    "XRL.World.Effects.LongbladeStance_Dueling.DisplayName",
                    "{{W|決闘姿勢}}")));
        });
    }

    [Test]
    public void ActiveEffectProducerInventory_ClassifiesCurrentEffectDisplayFamilies()
    {
        var repoRoot = TestProjectPaths.GetRepositoryRoot();
        var inventoryPath = Path.Combine(repoRoot, "docs", "active-effect-producer-inventory.json");
        using var document = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        var root = document.RootElement;
        var classificationValues = root.GetProperty("classification_values")
            .EnumerateArray()
            .Select(static value => value.GetString() ?? string.Empty)
            .ToArray();
        var observedProducerIds = root.GetProperty("observed_issue_739_producers")
            .EnumerateArray()
            .Select(static value => value.GetString() ?? string.Empty)
            .ToArray();
        var allowedClassifications = ExpectedActiveEffectProducerClassifications.ToHashSet(StringComparer.Ordinal);
        var items = root.GetProperty("items").EnumerateArray().ToArray();
        var invalidClassifications = items
            .Select(static item => item.GetProperty("classification").GetString() ?? string.Empty)
            .Where(classification => !allowedClassifications.Contains(classification))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var itemIds = items
            .Select(static item => item.GetProperty("family_id").GetString() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("issue").GetInt32(), Is.EqualTo(739));
            Assert.That(root.GetProperty("totals").GetProperty("family_count").GetInt32(), Is.EqualTo(466));
            Assert.That(items, Has.Length.EqualTo(466));
            Assert.That(classificationValues, Is.EquivalentTo(ExpectedActiveEffectProducerClassifications));
            Assert.That(observedProducerIds, Is.EquivalentTo(ExpectedIssue739ObservedProducerIds));
            Assert.That(invalidClassifications, Is.Empty);
            Assert.That(
                observedProducerIds.Where(id => !itemIds.Contains(id)).ToArray(),
                Is.Empty,
                "Issue #739 observed XRL.World.Effects producers must remain present in the checked-in inventory.");
            Assert.That(
                root.GetProperty("adjacent_route_producers")
                    .EnumerateArray()
                    .Select(static item => item.GetProperty("family_id").GetString() ?? string.Empty)
                    .ToArray(),
                Does.Contain("XRL.World.Skills.Cooking/AppleMatz.cs::AppleMatz.GetDescription()"));
        });
    }

    [Test]
    public void SkillsAndPowersDictionary_ContainsStaticAbilityBarBaseLeaves()
    {
        var entries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-skillsandpowers.ja.json"));
        var expectedEntries = new[]
        {
            new DictionaryEntry("Sunder Mind", "AbilityBar.ButtonText", "精神断裂"),
            new DictionaryEntry("Lay Mine", "AbilityBar.ButtonText", "地雷設置"),
            new DictionaryEntry("Set Bomb", "AbilityBar.ButtonText", "爆弾設置"),
            new DictionaryEntry("Recharge", "AbilityBar.ButtonText", "充電"),
            new DictionaryEntry("Spit Slime", "AbilityBar.ButtonText", "粘液吐き"),
            new DictionaryEntry("Discharge", "AbilityBar.ButtonText", "放電"),
            new DictionaryEntry("Lase", "AbilityBar.ButtonText", "レーザー照射"),
            new DictionaryEntry("Recoil", "AbilityBar.ButtonText", "帰還"),
            new DictionaryEntry("Mark", "AbilityBar.ButtonText", "マーク"),
            new DictionaryEntry("Precognition - Start vision", "AbilityBar.ButtonText", "予知 - 予知視開始"),
            new DictionaryEntry("Precognition - End vision", "AbilityBar.ButtonText", "予知 - 予知視終了"),
            new DictionaryEntry("Force Wall", "AbilityBar.ButtonText", "力場壁"),
            new DictionaryEntry("Burgeoning", "AbilityBar.ButtonText", "繁茂"),
            new DictionaryEntry("Disintegration", "AbilityBar.ButtonText", "分解"),
            new DictionaryEntry("Fear Aura", "AbilityBar.ButtonText", "恐怖のオーラ"),
            new DictionaryEntry("Flaming Ray", "AbilityBar.ButtonText", "炎線"),
            new DictionaryEntry("Force Bubble", "AbilityBar.ButtonText", "力場泡"),
            new DictionaryEntry("Freezing Ultraray", "AbilityBar.ButtonText", "凍結超光線"),
            new DictionaryEntry("Infiltrate", "AbilityBar.ButtonText", "潜入"),
            new DictionaryEntry("Irisdual Beam", "AbilityBar.ButtonText", "アイリスデュアル光線"),
            new DictionaryEntry("Kindle", "AbilityBar.ButtonText", "着火"),
            new DictionaryEntry("Magnetic Pulse", "AbilityBar.ButtonText", "磁気パルス"),
            new DictionaryEntry("Mental Mirror", "AbilityBar.ButtonText", "精神鏡"),
            new DictionaryEntry("Metamorphosis", "AbilityBar.ButtonText", "変容"),
            new DictionaryEntry("Psychometry", "AbilityBar.ButtonText", "サイコメトリー"),
            new DictionaryEntry("Spacetime Vortex", "AbilityBar.ButtonText", "時空渦"),
            new DictionaryEntry("Stunning Force", "AbilityBar.ButtonText", "衝撃念力"),
            new DictionaryEntry("Syphon Vim", "AbilityBar.ButtonText", "活力吸収"),
            new DictionaryEntry("Telepathy", "AbilityBar.ButtonText", "テレパシー"),
            new DictionaryEntry("Teleport Other", "AbilityBar.ButtonText", "他者転送"),
            new DictionaryEntry("Time Dilation", "AbilityBar.ButtonText", "時間延伸"),
            new DictionaryEntry("Blow Aji Conch", "AbilityBar.ButtonText", "アジ族の法螺貝を吹く"),
            new DictionaryEntry("Recomposite", "AbilityBar.ButtonText", "再構築"),
            new DictionaryEntry("Inflate Axons", "AbilityBar.ButtonText", "軸索膨張"),
            new DictionaryEntry("Emergency Recomposite", "AbilityBar.ButtonText", "緊急再構築"),
            new DictionaryEntry("Imprint with current location", "AbilityBar.ButtonText", "現在位置を刻印"),
            new DictionaryEntry("Project Stasis Field", "AbilityBar.ButtonText", "静止場を展開"),
            new DictionaryEntry("Eject", "AbilityBar.ButtonText", "射出"),
            new DictionaryEntry("Quicken Mind", "AbilityBar.ButtonText", "クイックマインド"),
            new DictionaryEntry("Spit Acid", "AbilityBar.ButtonText", "酸吐き"),
            new DictionaryEntry("Beguile Creature", "AbilityBar.ButtonText", "クリーチャーを魅了"),
            new DictionaryEntry("Burrow", "AbilityBar.ButtonText", "穴掘り"),
            new DictionaryEntry("Excavate up", "AbilityBar.ButtonText", "上階へ掘削"),
            new DictionaryEntry("Excavate down", "AbilityBar.ButtonText", "下階へ掘削"),
            new DictionaryEntry("Clairvoyance", "AbilityBar.ButtonText", "千里眼"),
            new DictionaryEntry("Confusion", "AbilityBar.ButtonText", "混乱"),
            new DictionaryEntry("Chill", "AbilityBar.ButtonText", "冷却"),
            new DictionaryEntry("Decarbonize", "AbilityBar.ButtonText", "脱炭素化"),
            new DictionaryEntry("Emit Pulse", "AbilityBar.ButtonText", "パルス放出"),
            new DictionaryEntry("Knit Frosty Webs", "AbilityBar.ButtonText", "氷結糸を編む"),
            new DictionaryEntry("Ley Shift", "AbilityBar.ButtonText", "レイシフト"),
            new DictionaryEntry("Ambient Light", "AbilityBar.ButtonText", "環境光"),
            new DictionaryEntry("Spit Liquid", "AbilityBar.ButtonText", "液体吐き"),
            new DictionaryEntry("End Metamorphosis", "AbilityBar.ButtonText", "変容を終える"),
            new DictionaryEntry("Bask", "AbilityBar.ButtonText", "日光浴"),
            new DictionaryEntry("Sting", "AbilityBar.ButtonText", "刺突"),
            new DictionaryEntry("Spew", "AbilityBar.ButtonText", "吐き出す"),
            new DictionaryEntry("Scintillate", "AbilityBar.ButtonText", "きらめく"),
            new DictionaryEntry("Phase", "AbilityBar.ButtonText", "フェイズ化"),
            new DictionaryEntry("Boost Agility", "AbilityBar.ButtonText", "敏捷強化"),
            new DictionaryEntry("Boost Strength", "AbilityBar.ButtonText", "筋力強化"),
            new DictionaryEntry("Boost Toughness", "AbilityBar.ButtonText", "頑健強化"),
            new DictionaryEntry("Release Adrenaline", "AbilityBar.ButtonText", "アドレナリン放出"),
            new DictionaryEntry("Spin Webs", "AbilityBar.ButtonText", "網を張る"),
            new DictionaryEntry("Tap the Mass Mind", "AbilityBar.ButtonText", "集合精神に接続"),
            new DictionaryEntry("Telekinesis", "AbilityBar.ButtonText", "念動力"),
            new DictionaryEntry("Telekinetic Throwing", "AbilityBar.ButtonText", "念動投擲"),
            new DictionaryEntry("Toast", "AbilityBar.ButtonText", "加熱"),
            new DictionaryEntry("Tongue", "AbilityBar.ButtonText", "粘着舌"),
            new DictionaryEntry("Waveform Dash", "AbilityBar.ButtonText", "ウェーブフォーム・ダッシュ"),
            new DictionaryEntry("Serenity", "AbilityBar.ButtonText", "安らぎ"),
            new DictionaryEntry("Wrecking Charge", "AbilityBar.ButtonText", "破壊的な突進"),
        };

        Assert.Multiple(() =>
        {
            foreach (var expectedEntry in expectedEntries)
            {
                Assert.That(
                    entries,
                    Does.Contain(expectedEntry),
                    $"Fresh Player.log static_leaf evidence requires an exact AbilityBar.ButtonText base leaf for {expectedEntry.Key}.");
            }
        });
    }

    [Test]
    public void TurretTinkerDictionaries_ContainCommandDirectionAndFailureLeaves()
    {
        var pickTargetEntries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-pick-target.ja.json"));
        var popupEntries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-popup.ja.json"));

        Assert.Multiple(() =>
        {
            Assert.That(
                pickTargetEntries,
                Does.Contain(new DictionaryEntry("Tinker Turret", "PickTarget.DirectionPrompt", "タレット製作")));
            Assert.That(
                popupEntries,
                Does.Contain(new DictionaryEntry(
                    "You are out of turrets to place.",
                    "XRL.World.Parts.TurretTinker.CommandTinkerTurret.ShowFailure",
                    "設置できるタレットが残っていない。")));
        });
    }

    [Test]
    public void BandageMedicationDictionaries_ContainPromptAndFailureLeaves()
    {
        var pickTargetEntries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-pick-target.ja.json"));
        var messagePatterns = JsonDocument
            .Parse(File.ReadAllText(Path.Combine(localizationRoot, "Dictionaries", "messages.ja.json")))
            .RootElement
            .GetProperty("patterns")
            .EnumerateArray()
            .Select(static element => element.GetProperty("pattern").GetString() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(
                pickTargetEntries,
                Does.Contain(new DictionaryEntry("Bandage whom?", "PickTarget.DirectionPrompt", "誰に包帯を巻く？")));
            Assert.That(
                messagePatterns,
                Does.Contain("^You cannot reach (.+?) to bandage (?:his|her|its|their) wounds[.!]?$"));
            Assert.That(
                messagePatterns,
                Does.Contain("^There's no one there[.!]?$"));
            Assert.That(
                messagePatterns,
                Does.Contain("^All of (.+?) wounds that can be staunched have been already[.!]?$"));
            Assert.That(
                messagePatterns,
                Does.Contain("^(.+?) wounds have been bandaged[.!]?$"));
            Assert.That(
                messagePatterns,
                Does.Contain("^(.+?) wounds are too deep to bandage[.!]?$"));
        });
    }

    [Test]
    public void DisplayNameAtomicDictionary_ContainsMultiHornsRuntimeMutationDisplayNames()
    {
        var entries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-displayname-atomic.ja.json"));
        var expectedEntries = new[]
        {
            new DictionaryEntry("Triple Horn", string.Empty, "三本角"),
            new DictionaryEntry("Horns", string.Empty, "角"),
            new DictionaryEntry("Horn", string.Empty, "単角"),
            new DictionaryEntry("Antlers", string.Empty, "枝角"),
        };

        Assert.Multiple(() =>
        {
            foreach (var expectedEntry in expectedEntries)
            {
                Assert.That(
                    entries,
                    Does.Contain(expectedEntry),
                    $"MultiHorns.Mutate runtime SetDisplayName leaf should be available to status display sinks: {expectedEntry.Key}.");
            }
        });
    }

    [Test]
    public void SkillsAndPowersDictionary_AbilityBarDuplicateLeavesShareAbilityManagerTranslations()
    {
        var entries = LoadEntries(Path.Combine(localizationRoot, "Dictionaries", "ui-skillsandpowers.ja.json"));
        var sharedAbilityBarLeaves = new[]
        {
            new DictionaryEntry("Dominate Creature", "AbilityManager.Name", "支配"),
            new DictionaryEntry("Power Devices", "AbilityManager.Name", "発電"),
        };

        Assert.Multiple(() =>
        {
            foreach (var sharedLeaf in sharedAbilityBarLeaves)
            {
                var matchingEntries = entries
                    .Where(entry => string.Equals(entry.Key, sharedLeaf.Key, StringComparison.Ordinal))
                    .ToArray();

                Assert.That(
                    matchingEntries,
                    Does.Contain(sharedLeaf),
                    $"{sharedLeaf.Key} should keep using the existing AbilityManager.Name translation.");
                Assert.That(
                    matchingEntries.Where(static entry => string.Equals(entry.Context, "AbilityBar.ButtonText", StringComparison.Ordinal)),
                    Is.Empty,
                    $"{sharedLeaf.Key} intentionally avoids a duplicate AbilityBar.ButtonText entry while runtime lookup remains flat by key.");
            }
        });
    }

    [Test]
    public void KnownRuntimeNoisyDuplicateKeys_AreExplicitlyAudited()
    {
        var dictionariesRoot = Path.Combine(localizationRoot, "Dictionaries");
        var duplicateEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-default.ja.json"))
            .Concat(LoadEntries(Path.Combine(dictionariesRoot, "ui-phase3c-labels.ja.json")))
            .Concat(LoadEntries(Path.Combine(dictionariesRoot, "ui-auto-generated.ja.json")))
            .Concat(LoadEntries(Path.Combine(dictionariesRoot, "ui-chargen.ja.json")))
            .Where(static entry => entry.Key is "Randomize Selection" or "Reset Selection" or "Sated" or "Quenched" or "Hostile")
            .GroupBy(static entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(duplicateEntries.TryGetValue("Randomize Selection", out var randomizeSelectionEntries), Is.True);
            Assert.That(randomizeSelectionEntries!.Select(static entry => entry.Text).Distinct(StringComparer.Ordinal),
                Is.EquivalentTo(new[] { "ランダムに選択", "選択をランダムにする" }));
            Assert.That(randomizeSelectionEntries, Has.Length.EqualTo(3));

            Assert.That(duplicateEntries.TryGetValue("Reset Selection", out var resetSelectionEntries), Is.True);
            Assert.That(resetSelectionEntries!.Select(static entry => entry.Text).Distinct(StringComparer.Ordinal),
                Is.EquivalentTo(new[] { "選択をリセット" }));
            Assert.That(resetSelectionEntries, Has.Length.EqualTo(3));

            Assert.That(duplicateEntries.TryGetValue("Sated", out var satedEntries), Is.True);
            Assert.That(satedEntries!.Select(static entry => entry.Text).Distinct(StringComparer.Ordinal),
                Is.EquivalentTo(new[] { "満腹" }));
            Assert.That(satedEntries, Has.Length.EqualTo(2));

            Assert.That(duplicateEntries.TryGetValue("Quenched", out var quenchedEntries), Is.True);
            Assert.That(quenchedEntries!.Select(static entry => entry.Text).Distinct(StringComparer.Ordinal),
                Is.EquivalentTo(new[] { "潤っている", "潤沢" }));
            Assert.That(quenchedEntries, Has.Length.EqualTo(3));

            Assert.That(duplicateEntries.TryGetValue("Hostile", out var hostileEntries), Is.True);
            Assert.That(hostileEntries!.Select(static entry => entry.Text).Distinct(StringComparer.Ordinal),
                Is.EquivalentTo(new[] { "敵対", "敵対的" }));
            Assert.That(hostileEntries, Has.Length.EqualTo(2));
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

    private static bool IsConcreteOutOfRange(string key)
    {
        const string prefix = "That is out of range! (";
        return TryGetOutOfRangeDistance(key, prefix, out _);
    }

    private static bool IsConcreteTargetOutOfRange(string key)
    {
        const string prefix = "That target is out of range! (";
        return TryGetOutOfRangeDistance(key, prefix, out _);
    }

    private static bool IsConcreteSultanHistoryJournalNotification(string key)
    {
        const string prefix = "You note this piece of information in the Sultan Histories > ";
        const string suffix = " section of your journal.";
        if (!key.StartsWith(prefix, StringComparison.Ordinal)
            || !key.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var section = key.Substring(prefix.Length, key.Length - prefix.Length - suffix.Length);
        return section.Length > 0 && !section.Contains('{', StringComparison.Ordinal);
    }

    private static bool TryGetOutOfRangeDistance(string key, string prefix, out string distance)
    {
        distance = string.Empty;
        const string singularSuffix = " square)";
        const string pluralSuffix = " squares)";
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = string.Empty;
        if (key.EndsWith(pluralSuffix, StringComparison.Ordinal))
        {
            suffix = pluralSuffix;
        }
        else if (key.EndsWith(singularSuffix, StringComparison.Ordinal))
        {
            suffix = singularSuffix;
        }
        if (suffix.Length == 0)
        {
            return false;
        }

        distance = key.Substring(prefix.Length, key.Length - prefix.Length - suffix.Length);
        return IsAsciiDigits(distance);
    }

    private static bool IsConcreteHpIncreaseDescription(string key)
    {
        const string prefix = "@they get +";
        const string suffix = "% max HP for 1 hour.";
        return key.StartsWith(prefix, StringComparison.Ordinal)
               && key.EndsWith(suffix, StringComparison.Ordinal)
               && IsAsciiDigits(key.Substring(prefix.Length, key.Length - prefix.Length - suffix.Length));
    }

    private static bool IsConcreteHpIncreaseDetails(string key)
    {
        const string prefix = "+";
        const string suffix = "% max HP";
        return key.StartsWith(prefix, StringComparison.Ordinal)
               && key.EndsWith(suffix, StringComparison.Ordinal)
               && IsAsciiDigits(key.Substring(prefix.Length, key.Length - prefix.Length - suffix.Length));
    }

    private static bool IsCookingDynamicPrefixFragment(string key)
    {
        return key is "@they get +"
            or "@they expel quills per the Quills mutation at level"
            or "@they expel quills per the Quills mutation at level "
            or "Reflect"
            or "Reflect "
            or "whenever @thisCreature take@s damage, there's a"
            or "whenever @thisCreature take@s damage, there's a "
            or "Whenever @thisCreature take@s avoidable damage, there's a"
            or "Whenever @thisCreature take@s avoidable damage, there's a "
            or "Can use"
            or "Can use ";
    }

    private static bool IsCookingMutationDynamicFragment(string key)
    {
        return key is "Can use {MutationDisplayName} at level {0}."
            or "+{0} level to {MutationDisplayName}."
            or "+{0} levels to {MutationDisplayName}."
            or "Can use {MutationDisplayName} at level {0}-{1}. If @they already have {MutationDisplayName}, it's enhanced by {2}-{3} levels."
            or "Can use Intimidate."
            or "+{0} bonus on Ego roll when using Intimidate."
            or "+2 bonus on Ego roll when using Intimidate."
            or "Can use Intimidate. If @they already have Intimidate, gain a +2 bonus on the Ego roll when using Intimidate."
            || IsConcreteCookingMutationUseTemplate(key);
    }

    private static bool IsConcreteCookingMutationUseTemplate(string key)
    {
        return key.StartsWith("Can use ", StringComparison.Ordinal)
               && key.IndexOf(" at level ", StringComparison.Ordinal) >= 0
               && key.IndexOf(". If @they already have ", StringComparison.Ordinal) >= 0
               && key.EndsWith(" levels.", StringComparison.Ordinal);
    }

    private static bool IsBasicCookingEffectDetailsFragment(string key)
    {
        return key is "+10% hit points"
            or "+1 MA"
            or "+6% Move Speed"
            or "+3% Quickness"
            or "+1 to hit"
            or "+5% XP gained";
    }

    private static bool IsConcreteFallToGround(string key)
    {
        return key is "You fall to the ground!"
            or "You falls to the ground!"
            or "You fell to the ground!"
            or "You fall to the ground."
            or "You falls to the ground."
            or "You fell to the ground."
            or "You fall to the ground"
            or "You falls to the ground"
            or "You fell to the ground";
    }

    private static bool IsConcreteFallAsleep(string key)
    {
        return key is "You fall asleep!"
            or "You falls asleep!"
            or "You fell asleep!"
            or "You fall asleep."
            or "You falls asleep."
            or "You fell asleep."
            or "You fall asleep"
            or "You falls asleep"
            or "You fell asleep";
    }

    private static bool IsWorldPartsDynamicFragment(string key)
    {
        return key is "You discover "
            or "You discovered "
            or "You discover something about "
            or " that was hidden!";
    }

    private static bool IsAsciiDigits(string value)
    {
        return value.Length > 0 && value.All(static ch => ch is >= '0' and <= '9');
    }

    private static XElement CommandElement(XDocument document, string id)
    {
        return document.Root!
            .Elements("command")
            .Single(element => string.Equals(element.Attribute("ID")?.Value, id, StringComparison.Ordinal));
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
