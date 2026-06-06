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
        ColorShapeCaptureObservability.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        InventoryLineTranslationPatch.ClearTranslationCachesForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ColorShapeCaptureObservability.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        InventoryLineTranslationPatch.ClearTranslationCachesForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateItemDisplayNameForQudTest_CachesRepeatedDisplayName()
    {
        WriteDictionaryFile("ui-displayname-atomic.ja.json", ("water flask", "水袋"));

        var initialCacheCount = InventoryLineTranslationPatch.GetTranslationCacheCountForTests();
        var first = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest("water flask");
        var afterFirstCacheCount = InventoryLineTranslationPatch.GetTranslationCacheCountForTests();
        var second = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest("water flask");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("水袋"));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(afterFirstCacheCount, Is.EqualTo(initialCacheCount + 1));
            Assert.That(InventoryLineTranslationPatch.GetTranslationCacheCountForTests(), Is.EqualTo(afterFirstCacheCount));
        });
    }

    [Test]
    public void TranslateItemDisplayNameForQudTest_CachesFallbackDisplayNameAfterVisibleMiss()
    {
        WriteDictionaryFile("ui-displayname-atomic.ja.json", ("water flask", "水袋"));
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[empty]", "[空]"));

        var initialCacheCount = InventoryLineTranslationPatch.GetTranslationCacheCountForTests();
        var first = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest("water flask [empty]");
        var afterFirstCacheCount = InventoryLineTranslationPatch.GetTranslationCacheCountForTests();
        var second = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest("water flask [empty]");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("水袋 [空]"));
            Assert.That(second, Is.EqualTo("水袋 [空]"));
            Assert.That(afterFirstCacheCount, Is.EqualTo(initialCacheCount + 1));
            Assert.That(InventoryLineTranslationPatch.GetTranslationCacheCountForTests(), Is.EqualTo(afterFirstCacheCount));
        });
    }

    [Test]
    public void InventoryLinePostfix_TranslatesCategoryAndItemRows_AfterOriginalSetData()
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
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Postfix))));

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
                Assert.That(categoryTarget.OriginalExecuted, Is.True);
                Assert.That(categoryTarget.categoryWeightText.Text, Does.Contain("3 個"));
                Assert.That(categoryTarget.categoryWeightText.Text, Does.Contain("17 lbs."));
                Assert.That(categoryTarget.categoryExpandLabel.Text, Is.EqualTo("[-]"));
                Assert.That(itemTarget.text.Text, Is.EqualTo("レーザーライフル"));
                Assert.That(itemTarget.OriginalExecuted, Is.True);
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
    public void InventoryLinePostfix_UsesDisplayNameRouteForItemNames_WhenPatched()
    {
        WriteDictionary(
            ("items", "個"));
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("water flask", "水袋"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[空]"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Postfix))));

            var itemTarget = new DummyInventoryLineTarget();
            itemTarget.setData(new DummyInventoryLineDataTarget
            {
                category = false,
                displayName = "water flask [empty]",
                go = new DummyStatusGameObject { DisplayName = "water flask [empty]", Weight = 7 },
            });

            Assert.Multiple(() =>
            {
                Assert.That(itemTarget.OriginalExecuted, Is.True);
                Assert.That(itemTarget.text.Text, Is.EqualTo("水袋 [空]"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.ItemName"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryLinePostfix_UsesFreshGameObjectDisplayName_WhenLineDataDisplayNameIsStaleAfterIdentify()
    {
        WriteDictionaryFile(
            Path.Combine("Scoped", "historyspice-common.ja.json"),
            ("analog", "アナログの"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Postfix))));

            var go = new DummyStatusGameObject
            {
                DisplayName = "{{Y-Y-Y-G-Y-Y-g-Y sequence|Chain of the Analog Sand}} \u00040 \t0 [6ドラムのゲル]",
                Weight = 1,
            };
            var itemTarget = new DummyInventoryLineTarget();
            itemTarget.setData(new DummyInventoryLineDataTarget
            {
                category = false,
                displayName = "Weird Artifact",
                go = go,
            });

            Assert.Multiple(() =>
            {
                Assert.That(
                    itemTarget.text.Text,
                    Is.EqualTo("{{Y-Y-Y-G-Y-Y-g-Y sequence|アナログの砂の鎖}} \u00040 \t0 [6ドラムのゲル]"));
                Assert.That(go.LastRenderContext, Is.EqualTo("Inventory"));
                Assert.That(itemTarget.icon.LastRenderable, Is.Not.Null);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryLinePostfix_PreservesRuntimeToolkitColor_WhenDisplayNameRouteAddsBlueprintColor()
    {
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        LocalizationAssetResolver.SetLocalizationRootForTests(GetRepositoryLocalizationRoot());

        const string source = "高級工具セット [ケムセル (残量多) <BD1>]";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Postfix))));

            var itemTarget = new DummyInventoryLineTarget();
            itemTarget.setData(new DummyInventoryLineDataTarget
            {
                category = false,
                displayName = source,
                go = new DummyStatusGameObject { DisplayName = source, Weight = 7 },
            });

            Assert.Multiple(() =>
            {
                Assert.That(itemTarget.OriginalExecuted, Is.True);
                Assert.That(
                    itemTarget.text.Text,
                    Is.EqualTo("{{C|高級工具セット}} [{{c|ケムセル}} {{y|({{G|残量多}})}} {{y|<{{G|B}}{{C|D}}{{r|1}}>}}]"));
                Assert.That(itemTarget.text.Text, Does.Not.StartWith("高級工具セット ["));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.ItemName"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            LocalizationAssetResolver.SetLocalizationRootForTests(null);
        }
    }

    [Test]
    public void InventoryLinePostfix_TranslatesMerchantAdvertisementTitleAndStripsEmbeddedMarker()
    {
        WriteDictionary(("items", "個"));

        var source = "advertisement for "
            + MessageFrameTranslator.DirectTranslationMarker
            + "{{M|クユラミルの蒸留所, 伝説の樹液商}}";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Postfix))));

            var itemTarget = new DummyInventoryLineTarget();
            itemTarget.setData(new DummyInventoryLineDataTarget
            {
                category = false,
                displayName = source,
                go = new DummyStatusGameObject { DisplayName = source, Weight = 7 },
            });

            Assert.Multiple(() =>
            {
                Assert.That(itemTarget.OriginalExecuted, Is.True);
                Assert.That(itemTarget.text.Text, Is.EqualTo("{{M|クユラミルの蒸留所, 伝説の樹液商}}の広告"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.MerchantAdvertisementTitle"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryLinePostfix_TranslatesCompactWeaponTrailingStates_WhenPatched()
    {
        WriteDictionary(
            ("items", "個"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[rusted]", "[{{r|錆びた}}]"),
            ("[broken]", "[{{r|破損}}]"));

        var source =
            "クローム・リボルバー {{c|\u001a}}7 {{r|\u0003}}1d6 {{y|[鉛スラッグ x6]}} [{{r|rusted}}] [{{r|broken}}] {{y|<{{B|C}}{{B|C}}{{g|2}}>}}";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Postfix))));

            var itemTarget = new DummyInventoryLineTarget();
            itemTarget.setData(new DummyInventoryLineDataTarget
            {
                category = false,
                displayName = source,
                go = new DummyStatusGameObject { DisplayName = source, Weight = 7 },
            });

            Assert.That(
                itemTarget.text.Text,
                Is.EqualTo("クローム・リボルバー {{c|\u001a}}7 {{r|\u0003}}1d6 {{y|[鉛スラッグ x6]}} [{{r|錆びた}}] [{{r|破損}}] {{y|<{{B|C}}{{B|C}}{{g|2}}>}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryLinePostfix_EmitsColorShapeCaptureArtifact_ForProducerDisplayName()
    {
        WriteDictionary(
            ("items", "個"));
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("chem cell", "ケムセル"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[fresh water]", "[清水]"));

        const string source = "{{c|chem cell}} {{y|[{{g|fresh water}}]}}";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Postfix))));

            var itemTarget = new DummyInventoryLineTarget();
            var output = TestTraceHelper.CaptureTrace(() =>
                itemTarget.setData(new DummyInventoryLineDataTarget
                {
                    category = false,
                    go = new DummyStatusGameObject { DisplayName = source, Weight = 7 },
                }));

            Assert.Multiple(() =>
            {
                Assert.That(itemTarget.OriginalExecuted, Is.True);
                Assert.That(itemTarget.text.Text, Is.EqualTo("{{c|ケムセル}} {{y|[{{g|清水}}]}}"));
                Assert.That(output, Does.Contain("ColorShapeProbe/v1"));
                Assert.That(output, Does.Contain("producer='InventoryLine.GameObjectDisplayName'"));
                Assert.That(output, Does.Contain("source_visible='chem cell [fresh water]'"));
                Assert.That(output, Does.Contain("final_visible='ケムセル [清水]'"));
                Assert.That(itemTarget.text.Text, Does.Not.Contain("{{c|{{c|"));
                Assert.That(itemTarget.text.Text, Does.Not.Contain("[{{g|清水]}}"));
                Assert.That(
                    ColorShapeCaptureObservability.GetRouteProducerHitCountForTests(
                        nameof(InventoryLineTranslationPatch),
                        "InventoryLine.GameObjectDisplayName"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void InventoryLinePostfix_LeavesUnsupportedInputOnOriginalPath()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryLineTarget), nameof(DummyInventoryLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(InventoryLineTranslationPatch), nameof(InventoryLineTranslationPatch.Postfix))));

            var target = new DummyInventoryLineTarget();
            target.setData(new DummyFallbackInventoryLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("inventory fallback"));
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

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.Combine(
            GetRepositoryLocalizationRoot(),
            "Dictionaries");
    }

    private static string GetRepositoryLocalizationRoot()
    {
        return Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("issue201-status-batch2.ja.json", entries);
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
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
        var path = Path.Combine(tempDirectory, fileName);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(
            path,
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
