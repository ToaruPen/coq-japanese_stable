using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed partial class Issue201StatusScreensBatch2Tests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-issue201-batch2-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void InventoryLinePrefix_TranslatesCategoryAndItemRows_WhenPatched()
    {
        WriteDictionary(
            ("Weapons", "武器"),
            ("items", "個"),
            ("lbs.", "ポンド"),
            ("Laser Rifle", "レーザーライフル"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Prefix))));

            var categoryTarget = new DummyInventoryLineTarget();
            categoryTarget.setData(new DummyInventoryLineDataTarget
            {
                category = true,
                categoryName = "Weapons",
                categoryExpanded = true,
                categoryAmount = 3,
                categoryWeight = 17,
            });

            var itemTarget = new DummyInventoryLineTarget();
            itemTarget.setData(new DummyInventoryLineDataTarget
            {
                category = false,
                displayName = "Laser Rifle",
                go = new DummyStatusGameObject { DisplayName = "Laser Rifle", Weight = 7 },
            });

            Assert.Multiple(() =>
            {
                Assert.That(categoryTarget.categoryLabel.Text, Is.EqualTo("武器"));
                Assert.That(categoryTarget.categoryWeightText.Text, Does.Contain("3 個"));
                Assert.That(categoryTarget.categoryWeightText.Text, Does.Contain("17 lbs."));
                Assert.That(categoryTarget.categoryExpandLabel.Text, Is.EqualTo("[-]"));
                Assert.That(itemTarget.text.Text, Is.EqualTo("レーザーライフル"));
                Assert.That(itemTarget.itemWeightText.Text, Is.EqualTo("[7 lbs.]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("lbs."), Is.EqualTo(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.CategoryName"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.ItemName"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.WeightSummary"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.WeightLabel"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryLinePrefix_FallsBackToOriginal_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Prefix))));

            var target = new DummyInventoryLineTarget();
            target.setData(new DummyFallbackInventoryLineDataTarget());

            Assert.That(target.OriginalExecuted, Is.True);
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

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(
            Path.Combine(tempDirectory, "issue201-status-batch2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
