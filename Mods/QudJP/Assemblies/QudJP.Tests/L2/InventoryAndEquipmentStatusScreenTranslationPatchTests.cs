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
    public void Postfix_TranslatesMenuOptionDescriptions_WhenPatched()
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
                Assert.That(target.CMD_OPTIONS.Description, Is.EqualTo("表示オプション"));
                Assert.That(target.SET_PRIMARY_LIMB.Description, Is.EqualTo("主肢に設定"));
                Assert.That(target.SHOW_TOOLTIP.Description, Is.EqualTo("[{{W|Alt}}] ツールチップ表示"));
                Assert.That(target.QUICK_DROP.Description, Is.EqualTo("クイック投棄"));
                Assert.That(target.QUICK_EAT.Description, Is.EqualTo("クイック食事"));
                Assert.That(target.QUICK_DRINK.Description, Is.EqualTo("クイック飲用"));
                Assert.That(target.QUICK_APPLY.Description, Is.EqualTo("クイック使用"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsInventoryRouteTransforms_WithoutUITextSkinSinkObservation()
    {
        WriteDictionary(("Display Options", "表示オプション"));

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
                Assert.That(target.CMD_OPTIONS.Description, Is.EqualTo("表示オプション"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryAndEquipmentStatusScreenTranslationPatch),
                        "InventoryAndEquipment.ExactLookup"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(InventoryAndEquipmentStatusScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsHotkeyLabelRouteTransform_WithoutUITextSkinSinkObservation()
    {
        WriteDictionary(("Show Tooltip", "ツールチップ表示"));

        var target = new DummyInventoryAndEquipmentStatusScreen();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryAndEquipmentStatusScreen), nameof(DummyInventoryAndEquipmentStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), nameof(InventoryAndEquipmentStatusScreenTranslationPatch.Postfix))));

            target.UpdateViewFromData();

            const string source = "[{{W|Alt}}] Show Tooltip";
            Assert.Multiple(() =>
            {
                Assert.That(target.SHOW_TOOLTIP.Description, Is.EqualTo("[{{W|Alt}}] ツールチップ表示"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryAndEquipmentStatusScreenTranslationPatch),
                        "InventoryAndEquipment.HotkeyLabel"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(InventoryAndEquipmentStatusScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryAndEquipmentScreenPostfix_TranslatesMenuOptionsAndWeightText_WhenPatched()
    {
        WriteDictionary(
            ("Toggle Cybernetics", "サイバネ切替"),
            ("Display Options", "表示オプション"),
            ("Set Primary Limb", "主肢設定"),
            ("Show Tooltip", "ツールチップ表示"),
            ("Quick Drop", "素早く落とす"),
            ("Quick Eat", "素早く食べる"),
            ("Quick Drink", "素早く飲む"),
            ("Quick Apply", "素早く適用"),
            ("lbs.", "ポンド"),
            ("show cybernetics", "サイバネティクス表示"),
            ("show equipment", "装備表示"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryAndEquipmentStatusScreen), nameof(DummyInventoryAndEquipmentStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), nameof(InventoryAndEquipmentStatusScreenTranslationPatch.Postfix))));

            var screen = new DummyInventoryAndEquipmentStatusScreen();
            screen.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(screen.CMD_SHOWCYBERNETICS.Description, Is.EqualTo("サイバネ切替"));
                Assert.That(screen.CMD_OPTIONS.Description, Is.EqualTo("表示オプション"));
                Assert.That(screen.SET_PRIMARY_LIMB.Description, Is.EqualTo("主肢設定"));
                Assert.That(screen.QUICK_DROP.Description, Is.EqualTo("素早く落とす"));
                Assert.That(screen.QUICK_EAT.Description, Is.EqualTo("素早く食べる"));
                Assert.That(screen.QUICK_DRINK.Description, Is.EqualTo("素早く飲む"));
                Assert.That(screen.QUICK_APPLY.Description, Is.EqualTo("素早く適用"));
                Assert.That(screen.weightText.Text, Does.Contain("lbs."));
                Assert.That(Translator.GetMissingKeyHitCountForTests("lbs."), Is.EqualTo(0));
                Assert.That(screen.cyberneticsHotkeySkin.Text, Does.Contain("サイバネティクス表示"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryAndEquipmentScreenPostfix_LeavesOriginalValues_WhenNoDictionaryEntries()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryAndEquipmentStatusScreen), nameof(DummyInventoryAndEquipmentStatusScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryAndEquipmentStatusScreenTranslationPatch), nameof(InventoryAndEquipmentStatusScreenTranslationPatch.Postfix))));

            var screen = new DummyInventoryAndEquipmentStatusScreen();
            screen.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(screen.CMD_SHOWCYBERNETICS.Description, Is.EqualTo("Toggle Cybernetics"));
                Assert.That(screen.CMD_OPTIONS.Description, Is.EqualTo("Display Options"));
                Assert.That(screen.weightText.Text, Does.Contain("lbs."));
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
