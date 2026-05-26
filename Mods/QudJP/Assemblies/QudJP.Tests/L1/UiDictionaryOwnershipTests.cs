using System.Text.Json;
using System.Xml.Linq;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class UiDictionaryOwnershipTests
{
    [Test]
    public void InventoryActionMenuLabels_AreOwnedByCorrectScopedDictionary_NotQudMenuItemDictionary()
    {
        var dictionariesRoot = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries");
        var inventoryActionEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-inventory-actions.ja.json"));
        var commonMenuActionEntries = LoadEntries(Path.Combine(dictionariesRoot, "Scoped", "ui-menu-actions.ja.json"));
        var qudMenuItemEntries = LoadEntries(Path.Combine(dictionariesRoot, "Scoped", "ui-popup-qud-menu-item.ja.json"));

        var expectedInventoryOwnerEntries = new[]
        {
            ("equip (manual)", "手動で装備"),
            ("fight fire", "消火する"),
            ("mark important", "重要にする"),
            ("mark unimportant", "重要マークを外す"),
            ("add notes", "メモを追加"),
            ("remove notes", "メモを削除"),
            ("remove", "外す"),
            ("wake", "起こす"),
            ("cancel life drain", "生命吸収を中止する"),
            ("dismiss", "解散させる"),
            ("direct to stop flying", "飛行をやめるよう指示"),
            ("seal", "封をする"),
            ("unseal", "封を解く"),
            ("pray", "祈る"),
            ("desecrate", "冒涜する"),
            ("deactivate", "停止する"),
            ("repair", "修理する"),
            ("recharge", "充電する"),
            ("disassemble all", "すべて分解"),
            ("clean", "掃除する"),
            ("show internals", "内部構造を表示"),
            ("mod with tinkering", "工作で改造"),
            ("drop all", "すべて落とす"),
            ("stop auto-collecting liquid", "液体の自動採取をやめる"),
            ("auto-collect liquid", "液体を自動採取する"),
            ("clean all your items [1 dram]", "手持ちのアイテムをすべて洗う [1 ドラム]"),
            ("stand up", "立ち上がる"),
            ("replace cell", "セルを交換"),
            ("install cell", "セルを装着"),
            ("direct to attack target", "攻撃対象を指示"),
            ("direct to engage aggressively", "攻撃的に交戦させる"),
            ("direct to engage defensively only", "防御的に交戦させる"),
            ("direct to come along", "ついて来させる"),
            ("give items", "アイテムを渡してもらう"),
            ("direct to move", "移動を指示"),
            ("direct to change follow distance", "追従距離を変更"),
            ("direct to stay there", "その場で待機させる"),
            ("direct ability use", "能力使用設定を変更"),
            ("rename", "名前を変更"),
            ("untarget", "ターゲット解除"),
            ("telekinetically pull toward you", "念動力で引き寄せる"),
            ("telekinetically pull one toward you", "念動力で1つ引き寄せる"),
            ("telekinetically pull towards you and take", "念動力で引き寄せて拾う"),
            ("telekinetically pull one towards you and take", "念動力で1つ引き寄せて拾う"),
            ("telekinetically hurl", "念動力で投げる"),
            ("telekinetically move", "念動力で移動"),
            ("telekinetically hurl one", "念動力で1つ投げる"),
            ("telekinetically move one", "念動力で1つ移動"),
            ("Eat fresh apple matz.", "新鮮なアップルマッツァを食べる。"),
            ("Drink mulled mushroom cider.", "温めたマッシュルームサイダーを飲む。"),
            ("Eat goat in sweet leaf.", "甘葉包みのヤギ肉を食べる。"),
            ("Eat some Tongue and Cheek.", "タングアンドチークを食べる。"),
            ("Eat bone babka.", "ボーンバブカを食べる。"),
            ("Eat some Hot and Spiny.", "ホットアンドスパイニーを食べる。"),
            ("Eat mah lah soup.", "マーラースープを食べる。"),
            ("Eat the Porridge.", "粥を食べる。"),
            ("Whip up a meal.", "手早く食事を作る。"),
            ("Choose ingredients to cook with.", "料理に使う材料を選ぶ。"),
            ("Cook from a recipe.", "レシピから料理する。"),
            ("Preserve your fresh foods.", "新鮮な食材を保存食にする。"),
            ("Preserve your exotic foods.", "珍味を保存食にする。"),
            ("Stop bleeding.", "出血を止める。"),
            ("Treat poison.", "毒を治療する。"),
            ("Treat illness.", "病気を治療する。"),
            ("Treat disease onset.", "発症前の病を治療する。"),
        };
        var expectedCommonMenuActionEntries = new[]
        {
            ("drop", "落とす"),
            ("detonate", "起爆する"),
            ("look", "見る"),
            ("examine", "調べる"),
        };

        Assert.Multiple(() =>
        {
            foreach (var (key, text) in expectedInventoryOwnerEntries)
            {
                Assert.That(
                    inventoryActionEntries,
                    Has.Some.Matches<DictionaryEntry>(entry =>
                        string.Equals(entry.Key, key, StringComparison.Ordinal)
                        && string.Equals(entry.Context, "XRL.World.IInventoryActionsEvent", StringComparison.Ordinal)
                        && string.Equals(entry.Text, text, StringComparison.Ordinal)),
                    $"{key} should be owned by ui-inventory-actions.ja.json under XRL.World.IInventoryActionsEvent.");
                Assert.That(
                    qudMenuItemEntries,
                    Has.None.Matches<DictionaryEntry>(entry => string.Equals(entry.Key, key, StringComparison.Ordinal)),
                    $"{key} should not be duplicated into ui-popup-qud-menu-item.ja.json.");
            }

            foreach (var (key, text) in expectedCommonMenuActionEntries)
            {
                Assert.That(
                    commonMenuActionEntries,
                    Has.Some.Matches<DictionaryEntry>(entry =>
                        string.Equals(entry.Key, key, StringComparison.Ordinal)
                        && string.IsNullOrEmpty(entry.Context)
                        && string.Equals(entry.Text, text, StringComparison.Ordinal)),
                    $"{key} should be owned by scoped ui-menu-actions.ja.json.");
                Assert.That(
                    inventoryActionEntries,
                    Has.None.Matches<DictionaryEntry>(entry => string.Equals(entry.Key, key, StringComparison.Ordinal)),
                    $"{key} should not stay duplicated in ui-inventory-actions.ja.json.");
                Assert.That(
                    qudMenuItemEntries,
                    Has.None.Matches<DictionaryEntry>(entry => string.Equals(entry.Key, key, StringComparison.Ordinal)),
                    $"{key} should not be duplicated into ui-popup-qud-menu-item.ja.json.");
            }
        });
    }

    [Test]
    public void CampfirePresetMealMessages_AreLocalizedAndSinkCovered()
    {
        var repositoryRoot = TestProjectPaths.GetRepositoryRoot();
        var dictionariesRoot = Path.Combine(repositoryRoot, "Mods", "QudJP", "Localization", "Dictionaries");
        var inventoryActionEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-inventory-actions.ja.json"));

        var localizedFurniturePath = Path.Combine(
            repositoryRoot,
            "Mods",
            "QudJP",
            "Localization",
            "ObjectBlueprints",
            "Furniture.jp.xml");

        var baseMessages = new[]
        {
            "Eat fresh apple matz.",
            "Drink mulled mushroom cider.",
            "Eat goat in sweet leaf.",
            "Eat some Tongue and Cheek.",
            "Eat bone babka.",
            "Eat some Hot and Spiny.",
            "Eat mah lah soup.",
            "Eat the Porridge.",
        };
        var expectedLocalizedMessages = new[]
        {
            "新鮮なアップルマッツァを食べる。",
            "温めたマッシュルームサイダーを飲む。",
            "甘葉包みのヤギ肉を食べる。",
            "タングアンドチークを食べる。",
            "ボーンバブカを食べる。",
            "ホットアンドスパイニーを食べる。",
            "マーラースープを食べる。",
            "粥を食べる。",
        };
        var localizedMessages = LoadPresetMealMessages(localizedFurniturePath);

        Assert.Multiple(() =>
        {
            Assert.That(localizedMessages, Is.Not.Empty, "Furniture.jp.xml should expose preset meal menu messages.");
            foreach (var message in baseMessages)
            {
                Assert.That(
                    inventoryActionEntries,
                    Has.Some.Matches<DictionaryEntry>(entry =>
                        string.Equals(entry.Key, message, StringComparison.Ordinal)
                        && string.Equals(entry.Context, "XRL.World.IInventoryActionsEvent", StringComparison.Ordinal)),
                    $"{message} should have an InventoryActionMenu sink fallback.");
            }

            Assert.That(localizedMessages, Has.Count.EqualTo(baseMessages.Length));
            Assert.That(localizedMessages, Is.EqualTo(expectedLocalizedMessages));
        });
    }

    [Test]
    public void RuntimeObservedFixedUiLeaves_AreOwnedByNarrowDictionaries()
    {
        var dictionariesRoot = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries");
        var uiDefaultEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-default.ja.json"));
        var pickTargetEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-pick-target.ja.json"));
        var skillsAndPowersEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-skillsandpowers.ja.json"));
        var displayNameAdjectiveEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-displayname-adjectives.ja.json"));
        var displayNameAtomicEntries = LoadEntries(Path.Combine(dictionariesRoot, "ui-displayname-atomic.ja.json"));
        var creatureTitles = LoadCreatureTitles(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "ObjectBlueprints",
            "Creatures.jp.xml"));
        var creatureDescriptions = LoadCreatureDescriptions(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "ObjectBlueprints",
            "Creatures.jp.xml"));

        Assert.Multiple(() =>
        {
            Assert.That(
                uiDefaultEntries,
                Has.Some.Matches<DictionaryEntry>(entry =>
                    string.Equals(entry.Key, "Loading wish commands", StringComparison.Ordinal)
                    && string.IsNullOrEmpty(entry.Context)
                    && string.Equals(entry.Text, "wishコマンドを読み込み中", StringComparison.Ordinal)),
                "WishManager loading status should be owned by ui-default.ja.json.");
            Assert.That(
                uiDefaultEntries,
                Has.Some.Matches<DictionaryEntry>(entry =>
                    string.Equals(entry.Key, "Space-select", StringComparison.Ordinal)
                    && string.IsNullOrEmpty(entry.Context)
                    && string.Equals(entry.Text, "[Space] 選択", StringComparison.Ordinal)),
                "Runtime key hints that arrive as Space-select should keep their key brackets in ui-default.ja.json.");
            Assert.That(
                uiDefaultEntries,
                Has.Some.Matches<DictionaryEntry>(entry =>
                    string.Equals(entry.Key, "space-select", StringComparison.Ordinal)
                    && string.IsNullOrEmpty(entry.Context)
                    && string.Equals(entry.Text, "[Space] 選択", StringComparison.Ordinal)),
                "Runtime key hints that arrive as space-select should keep their key brackets in ui-default.ja.json.");
            Assert.That(
                pickTargetEntries,
                Has.Some.Matches<DictionaryEntry>(entry =>
                    string.Equals(entry.Key, "Pick Target", StringComparison.Ordinal)
                    && string.IsNullOrEmpty(entry.Context)
                    && string.Equals(entry.Text, "対象を選択", StringComparison.Ordinal)),
                "PickTarget.ShowPicker title and command bar header should share the ui-pick-target.ja.json leaf.");
            Assert.That(
                pickTargetEntries,
                Has.Some.Matches<DictionaryEntry>(entry =>
                    string.Equals(entry.Key, "Berate whom?", StringComparison.Ordinal)
                    && string.IsNullOrEmpty(entry.Context)
                    && string.Equals(entry.Text, "誰を罵倒する？", StringComparison.Ordinal)),
                "Persuasion berate target prompt should be owned by ui-pick-target.ja.json.");

            var expectedAbilityManagerFragments = new[]
            {
                ("disabled", "無効"),
                ("astrally tethered", "アストラル束縛"),
                ("turn cooldown", "ターンのクールダウン"),
                ("Toggled on", "オン"),
                ("Toggled off", "オフ"),
                ("high Willpower", "高い意志力"),
            };
            foreach (var (key, text) in expectedAbilityManagerFragments)
            {
                Assert.That(
                    skillsAndPowersEntries,
                    Has.Some.Matches<DictionaryEntry>(entry =>
                        string.Equals(entry.Key, key, StringComparison.Ordinal)
                        && string.Equals(entry.Context, "AbilityManagerLine.Fragment", StringComparison.Ordinal)
                        && string.Equals(entry.Text, text, StringComparison.Ordinal)),
                    $"{key} should be owned by AbilityManagerLine fragments in ui-skillsandpowers.ja.json.");
            }

            var expectedSkillsAndPowersLeaves = new[]
            {
                ("Toggle to enable or disable the harvesting of plants", "植物の収穫を有効/無効に切り替える"),
                ("DV penalty and no jumping removed due to Hurdle skill.", "障害物越えスキルにより、DVペナルティとジャンプ不可は取り除かれている。"),
            };
            foreach (var (key, text) in expectedSkillsAndPowersLeaves)
            {
                Assert.That(
                    skillsAndPowersEntries,
                    Has.Some.Matches<DictionaryEntry>(entry =>
                        string.Equals(entry.Key, key, StringComparison.Ordinal)
                        && string.IsNullOrEmpty(entry.Context)
                        && string.Equals(entry.Text, text, StringComparison.Ordinal)),
                    $"{key} should be owned by ui-skillsandpowers.ja.json.");
            }

            var expectedDisplayNameAdjectives = new[]
            {
                ("HE", "HE"),
            };
            foreach (var (key, text) in expectedDisplayNameAdjectives)
            {
                Assert.That(
                    displayNameAdjectiveEntries,
                    Has.Some.Matches<DictionaryEntry>(entry =>
                        string.Equals(entry.Key, key, StringComparison.Ordinal)
                        && string.Equals(entry.Context, "GetDisplayName.Adjective", StringComparison.Ordinal)
                        && string.Equals(entry.Text, text, StringComparison.Ordinal)),
                    $"{key} should be owned by GetDisplayName.Adjective in ui-displayname-adjectives.ja.json.");
            }

            var expectedDisplayNameAtomicLeaves = new[]
            {
                ("bronze long sword", "{{w|青銅の長剣}}"),
            };
            foreach (var (key, text) in expectedDisplayNameAtomicLeaves)
            {
                Assert.That(
                    displayNameAtomicEntries,
                    Has.Some.Matches<DictionaryEntry>(entry =>
                        string.Equals(entry.Key, key, StringComparison.Ordinal)
                        && string.IsNullOrEmpty(entry.Context)
                        && string.Equals(entry.Text, text, StringComparison.Ordinal)),
                    $"{key} should be owned by ui-displayname-atomic.ja.json so trade/inventory display-name routes do not split it into a partial material translation.");
            }

            var expectedDisplayNameTitles = new[]
            {
                ("disciple of the Coiled Lamb", "GetDisplayName.Title", "巻かれた仔羊の弟子"),
                ("hindren pariah", "GetDisplayName.Title", "ヒンドレンのパリア"),
            };
            foreach (var (key, context, text) in expectedDisplayNameTitles)
            {
                Assert.That(
                    displayNameAtomicEntries,
                    Has.Some.Matches<DictionaryEntry>(entry =>
                        string.Equals(entry.Key, key, StringComparison.Ordinal)
                        && string.Equals(entry.Context, context, StringComparison.Ordinal)
                        && string.Equals(entry.Text, text, StringComparison.Ordinal)),
                    $"{key} should be owned by GetDisplayName.Title in ui-displayname-atomic.ja.json.");
            }

            Assert.That(
                creatureTitles,
                Has.Some.Matches<CreatureTitle>(entry =>
                    string.Equals(entry.ObjectName, "Lulihart", StringComparison.Ordinal)
                    && string.Equals(entry.Kind, "Ordinary", StringComparison.Ordinal)
                    && string.Equals(entry.Text, "ヒンドレンのパリア", StringComparison.Ordinal)),
                "Lulihart's localized blueprint title should not leave the runtime display name to translate hindren pariah.");
            Assert.That(
                creatureTitles,
                Has.Some.Matches<CreatureTitle>(entry =>
                    string.Equals(entry.ObjectName, "Tszappur", StringComparison.Ordinal)
                    && string.Equals(entry.Kind, "Primary", StringComparison.Ordinal)
                    && string.Equals(entry.Text, "巻かれた仔羊の弟子", StringComparison.Ordinal)),
                "Tszappur's localized blueprint title should not leave the runtime display name to translate disciple of the Coiled Lamb.");

            var runtimeObservedDescriptionResidues = new[]
            {
                "galvanize",
                "moon time",
                " flare ",
            };
            foreach (var residue in runtimeObservedDescriptionResidues)
            {
                Assert.That(
                    creatureDescriptions,
                    Has.None.Matches<CreatureDescription>(entry =>
                        entry.ShortDescription.IndexOf(residue, StringComparison.Ordinal) >= 0),
                    $"Creature descriptions should not retain runtime-observed English residue: {residue}");
            }
        });
    }

    private static IReadOnlyList<DictionaryEntry> LoadEntries(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("entries")
            .EnumerateArray()
            .Select(static entry => new DictionaryEntry(
                entry.GetProperty("key").GetString() ?? string.Empty,
                entry.TryGetProperty("context", out var context) ? context.GetString() ?? string.Empty : string.Empty,
                entry.GetProperty("text").GetString() ?? string.Empty))
            .ToArray();
    }

    private static IReadOnlyList<string> LoadPresetMealMessages(string path)
    {
        var document = XDocument.Load(path);
        return document.Descendants("tag")
            .Where(static element => string.Equals((string?)element.Attribute("Name"), "PresetMealMessage", StringComparison.Ordinal))
            .Select(static element => (string?)element.Attribute("Value") ?? string.Empty)
            .ToArray();
    }

    private static IReadOnlyList<CreatureTitle> LoadCreatureTitles(string path)
    {
        var document = XDocument.Load(path);
        return document.Descendants("object")
            .SelectMany(static element =>
            {
                var objectName = (string?)element.Attribute("Name") ?? string.Empty;
                return element.Elements("part")
                    .Where(static part => string.Equals((string?)part.Attribute("Name"), "Titles", StringComparison.Ordinal))
                    .SelectMany(part => part.Attributes()
                        .Where(static attribute => !string.Equals(attribute.Name.LocalName, "Name", StringComparison.Ordinal))
                        .Select(attribute => new CreatureTitle(objectName, attribute.Name.LocalName, attribute.Value)));
            })
            .ToArray();
    }

    private static IReadOnlyList<CreatureDescription> LoadCreatureDescriptions(string path)
    {
        var document = XDocument.Load(path);
        return document.Descendants("object")
            .SelectMany(static element =>
            {
                var objectName = (string?)element.Attribute("Name") ?? string.Empty;
                return element.Elements("part")
                    .Where(static part => string.Equals((string?)part.Attribute("Name"), "Description", StringComparison.Ordinal))
                    .Select(part => new CreatureDescription(objectName, (string?)part.Attribute("Short") ?? string.Empty));
            })
            .ToArray();
    }

    private sealed record DictionaryEntry(string Key, string Context, string Text);

    private sealed record CreatureTitle(string ObjectName, string Kind, string Text);

    private sealed record CreatureDescription(string ObjectName, string ShortDescription);
}
