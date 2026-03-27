using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class InventoryAndEquipmentStatusScreenTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-inventory-status-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_ObservationOnly_LeavesMenuOptionDescriptionsUnchanged_WhenPatched()
    {
        WriteDictionary(
            ("Display Options", "表示オプション"),
            ("Set Primary Limb", "主肢に設定"),
            ("Show Tooltip", "ツールチップ表示"),
            ("Quick Drop", "クイック投棄"),
            ("Quick Eat", "クイック食事"),
            ("Quick Drink", "クイック飲用"),
            ("Quick Apply", "クイック使用"));

        var target = new DummyInventoryAndEquipmentStatusScreen();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryAndEquipmentStatusScreen), nameof(DummyInventoryAndEquipmentStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), nameof(InventoryAndEquipmentStatusScreenTranslationPatch.Postfix))));

            target.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(target.CMD_OPTIONS.Description, Is.EqualTo("Display Options"));
                Assert.That(target.SET_PRIMARY_LIMB.Description, Is.EqualTo("Set Primary Limb"));
                Assert.That(target.SHOW_TOOLTIP.Description, Is.EqualTo("[{{W|Alt}}] Show Tooltip"));
                Assert.That(target.QUICK_DROP.Description, Is.EqualTo("Quick Drop"));
                Assert.That(target.QUICK_EAT.Description, Is.EqualTo("Quick Eat"));
                Assert.That(target.QUICK_DRINK.Description, Is.EqualTo("Quick Drink"));
                Assert.That(target.QUICK_APPLY.Description, Is.EqualTo("Quick Apply"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_ObservationOnly_LogsUnclaimedMenuOptionDescriptions_WhenPatched()
    {
        var target = new DummyInventoryAndEquipmentStatusScreen();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryAndEquipmentStatusScreen), nameof(DummyInventoryAndEquipmentStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), nameof(InventoryAndEquipmentStatusScreenTranslationPatch.Postfix))));

            target.UpdateViewFromData();

            const string source = "Display Options";
            Assert.Multiple(() =>
            {
                Assert.That(target.CMD_OPTIONS.Description, Is.EqualTo(source));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(InventoryAndEquipmentStatusScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.GreaterThan(0));
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

        var path = Path.Combine(tempDirectory, "ui-inventory-status.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
