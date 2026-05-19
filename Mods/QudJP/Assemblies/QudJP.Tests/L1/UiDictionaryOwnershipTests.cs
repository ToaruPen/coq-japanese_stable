using System.Text.Json;
using System.Text.RegularExpressions;
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
            ("mark important", "重要にする"),
            ("mark unimportant", "重要マークを外す"),
            ("add notes", "メモを追加"),
            ("remove notes", "メモを削除"),
            ("remove", "外す"),
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
        var localizedMessages = LoadPresetMealMessages(localizedFurniturePath);

        Assert.Multiple(() =>
        {
            Assert.That(baseMessages, Is.Not.Empty, "Base Furniture.xml should expose preset meal menu messages.");
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
            foreach (var message in localizedMessages)
            {
                Assert.That(
                    LooksLikeUntranslatedEnglish(message),
                    Is.False,
                    $"{message} should be localized in Furniture.jp.xml because Campfire reads PresetMealMessage directly.");
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

    private static bool LooksLikeUntranslatedEnglish(string value)
    {
        return Regex.IsMatch(value, @"\b(?:Eat|Drink)\b", RegexOptions.CultureInvariant)
            && Regex.IsMatch(value, "[A-Za-z]", RegexOptions.CultureInvariant);
    }

    private sealed record DictionaryEntry(string Key, string Context, string Text);
}
