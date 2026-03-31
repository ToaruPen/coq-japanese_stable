using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WorldMapUiTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-worldmap-ui-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyStatusScreensScreenTarget.ResetStaticMenuOptions();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyStatusScreensScreenTarget.ResetStaticMenuOptions();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void JournalStatusScreenPostfix_TranslatesCategoryChromeAndMenuOptions_WhenPatched()
    {
        WriteDictionary(
            "ui-journal.ja.json",
            ("Locations", "場所"),
            ("Gossip and Lore", "噂と伝承"),
            ("Sultan Histories", "スルタン史"),
            ("Village Histories", "村の歴史"),
            ("Chronology", "年代記"),
            ("General Notes", "雑記"),
            ("Recipes", "レシピ"),
            ("Add", "追加"),
            ("Delete", "削除"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyJournalStatusScreenTarget), nameof(DummyJournalStatusScreenTarget.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(JournalStatusScreenTranslationPatch), nameof(JournalStatusScreenTranslationPatch.Postfix))));

            var target = new DummyJournalStatusScreenTarget();
            var expected = new[]
            {
                ("Locations", "場所"),
                ("Gossip and Lore", "噂と伝承"),
                ("Sultan Histories", "スルタン史"),
                ("Village Histories", "村の歴史"),
                ("Chronology", "年代記"),
                ("General Notes", "雑記"),
                ("Recipes", "レシピ"),
            };

            foreach (var (source, translated) in expected)
            {
                target.NextCategoryText = source;
                target.UpdateViewFromData();
                Assert.That(target.categoryText.Text, Is.EqualTo(translated), source);
            }

            Assert.Multiple(() =>
            {
                Assert.That(target.CMD_INSERT.Description, Is.EqualTo("追加"));
                Assert.That(target.CMD_DELETE.Description, Is.EqualTo("削除"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(JournalStatusScreenTranslationPatch), "JournalStatusScreen.CategoryText"),
                    Is.EqualTo(expected.Length));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(JournalStatusScreenTranslationPatch), "JournalStatusScreen.MenuOption"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void StatusScreensScreenPostfix_TranslatesFilterNavigationAndAccept_WhenPatched()
    {
        WriteDictionary(
            "ui-statusscreens.ja.json",
            ("Filter", "絞り込み"),
            ("navigation", "移動"),
            ("Accept", "決定"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyStatusScreensScreenTarget), nameof(DummyStatusScreensScreenTarget.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(StatusScreensScreenTranslationPatch), nameof(StatusScreensScreenTranslationPatch.Postfix))));

            var target = new DummyStatusScreensScreenTarget();
            target.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(DummyStatusScreensScreenTarget.SET_FILTER.Description, Is.EqualTo("絞り込み"));
                Assert.That(DummyStatusScreensScreenTarget.SET_FILTER.KeyDescription, Is.EqualTo("絞り込み"));
                Assert.That(target.defaultMenuOptionOrder[0].Description, Is.EqualTo("移動"));
                Assert.That(target.defaultMenuOptionOrder[1].Description, Is.EqualTo("決定"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(StatusScreensScreenTranslationPatch), "StatusScreensScreen.MenuOption"),
                    Is.EqualTo(4));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, Type[]? parameterTypes = null)
    {
        var method = parameterTypes is null
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);
        return method
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(string fileName, params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"entries\": [");

        for (var index = 0; index < entries.Length; index++)
        {
            var suffix = index + 1 < entries.Length ? "," : string.Empty;
            builder.Append("    { \"key\": \"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\", \"text\": \"");
            builder.Append(EscapeJson(entries[index].text));
            builder.AppendLine("\" }" + suffix);
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");

        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
