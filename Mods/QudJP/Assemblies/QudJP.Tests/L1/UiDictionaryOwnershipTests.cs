using System.Text.Json;

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

    private sealed record DictionaryEntry(string Key, string Context, string Text);
}
