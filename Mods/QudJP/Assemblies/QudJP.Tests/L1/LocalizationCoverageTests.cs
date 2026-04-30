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
    public void KnownRuntimeNoisyDuplicateKeys_AreExplicitlyAudited()
    {
        var dictionariesRoot = Path.Combine(localizationRoot, "Dictionaries");
        var duplicateEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-default.ja.json"))
            .Concat(LoadEntries(Path.Combine(dictionariesRoot, "ui-phase3c-labels.ja.json")))
            .Concat(LoadEntries(Path.Combine(dictionariesRoot, "ui-auto-generated.ja.json")))
            .Concat(LoadEntries(Path.Combine(dictionariesRoot, "ui-chargen.ja.json")))
            .Where(static entry => entry.Key is "Randomize Selection" or "Reset Selection" or "Sated" or "Quenched")
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
