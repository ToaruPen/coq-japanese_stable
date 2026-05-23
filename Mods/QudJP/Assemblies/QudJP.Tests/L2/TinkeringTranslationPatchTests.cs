using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201OtherUiBindingPatchTests
{
    [Test]
    public void TinkeringStatusScreenPostfix_TranslatesModeToggleAndCategoryInfos_WhenPatched()
    {
        WriteDictionary(
            ("{{hotkey|[~Toggle]}} switch to modifications", "{{hotkey|[~Toggle]}} 改造に切り替え"),
            ("{{hotkey|[~Toggle]}} switch to build", "{{hotkey|[~Toggle]}} 製作に切り替え"),
            ("Build", "製作"),
            ("Mod", "改造"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTinkeringStatusScreenTarget), nameof(DummyTinkeringStatusScreenTarget.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TinkeringStatusScreenTranslationPatch), nameof(TinkeringStatusScreenTranslationPatch.Postfix))));

            var buildTarget = new DummyTinkeringStatusScreenTarget
            {
                CurrentCategory = 0,
            };
            buildTarget.UpdateViewFromData();

            var modTarget = new DummyTinkeringStatusScreenTarget
            {
                CurrentCategory = 1,
            };
            modTarget.UpdateViewFromData();

            Assert.Multiple(() =>
            {
                Assert.That(buildTarget.OriginalExecuted, Is.True);
                Assert.That(buildTarget.modeToggleText.Text, Is.EqualTo("{{hotkey|[~Toggle]}} 改造に切り替え"));
                Assert.That(buildTarget.categoryInfos[0].Name, Is.EqualTo("製作"));
                Assert.That(buildTarget.categoryInfos[1].Name, Is.EqualTo("改造"));
                Assert.That(modTarget.modeToggleText.Text, Is.EqualTo("{{hotkey|[~Toggle]}} 製作に切り替え"));
                Assert.That(modTarget.categoryInfos[0].Name, Is.EqualTo("製作"));
                Assert.That(modTarget.categoryInfos[1].Name, Is.EqualTo("改造"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringStatusScreenTranslationPatch), "TinkeringStatus.ModeToggleText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringStatusScreenTranslationPatch), "TinkeringStatus.CategoryName"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringLinePostfix_TranslatesNoSchematicsAndNoApplicableItems_WhenPatched()
    {
        WriteDictionary(
            ("{{K|You don't have any schematics.}}", "{{K|設計図がない。}}"),
            ("<no applicable items>", "<適用可能なアイテムなし>"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTinkeringLineTarget), nameof(DummyTinkeringLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TinkeringLineTranslationPatch), nameof(TinkeringLineTranslationPatch.Postfix))));

            var noSchematicsTarget = new DummyTinkeringLineTarget();
            noSchematicsTarget.setData(new DummyTinkeringLineDataTarget
            {
                category = true,
                categoryName = "~<none>",
            });

            var noApplicableItemsTarget = new DummyTinkeringLineTarget();
            noApplicableItemsTarget.setData(new DummyTinkeringLineDataTarget
            {
                mode = 1,
            });

            Assert.Multiple(() =>
            {
                Assert.That(noSchematicsTarget.OriginalExecuted, Is.True);
                Assert.That(noSchematicsTarget.categoryText.Text, Is.EqualTo("{{K|設計図がない。}}"));
                Assert.That(noApplicableItemsTarget.OriginalExecuted, Is.True);
                Assert.That(noApplicableItemsTarget.text.Text, Is.EqualTo("    <適用可能なアイテムなし>"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringLineTranslationPatch), "TinkeringLine.CategoryText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringLineTranslationPatch), "TinkeringLine.Text"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringDetailsLinePostfix_TranslatesBitCostIngredientsAndOr_WhenPatched()
    {
        WriteDictionary(
            ("build item", "製作品"),
            ("This contraption hums quietly.", "この装置は静かにうなっている。"),
            ("mod item", "改造品"),
            ("This item has been modified.", "このアイテムは改造されている。"),
            ("{{K|| Bit Cost |}}", "{{K|| ビットコスト |}}"),
            ("{{K || Bit Cost |}}", "{{K || ビットコスト |}}"),
            ("{{K|| Ingredients |}}", "{{K|| 素材 |}}"),
            ("-or-", "-または-"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTinkeringDetailsLineTarget), nameof(DummyTinkeringDetailsLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TinkeringDetailsLineTranslationPatch), nameof(TinkeringDetailsLineTranslationPatch.Postfix))));

            var buildTarget = new DummyTinkeringDetailsLineTarget();
            buildTarget.setData(new DummyTinkeringLineDataTarget
            {
                data = new DummyTinkeringRecipeData
                {
                    Type = "Build",
                    DisplayName = "build item",
                    UnclippedDescription = "This contraption hums quietly.",
                },
            });

            var modTarget = new DummyTinkeringDetailsLineTarget();
            modTarget.setData(new DummyTinkeringLineDataTarget
            {
                data = new DummyTinkeringRecipeData
                {
                    Type = "Mod",
                    DisplayName = "mod item",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(buildTarget.OriginalExecuted, Is.True);
                Assert.That(buildTarget.text.Text, Is.EqualTo("製作品"));
                Assert.That(buildTarget.descriptionText.Text, Is.EqualTo("この装置は静かにうなっている。"));
                Assert.That(buildTarget.modBitCostText.Text, Does.Contain("{{K|| ビットコスト |}}"));
                Assert.That(buildTarget.modBitCostText.Text, Does.Contain("{{R|A}}{{C|C}}"));
                Assert.That(buildTarget.modBitCostText.Text, Does.Contain("{{K|| 素材 |}}"));
                Assert.That(buildTarget.modBitCostText.Text, Does.Contain("-または-"));
                Assert.That(modTarget.text.Text, Is.EqualTo("改造品"));
                Assert.That(modTarget.descriptionText.Text, Is.EqualTo("このアイテムは改造されている。"));
                Assert.That(modTarget.modBitCostText.Text, Does.Contain("{{K || ビットコスト |}}"));
                Assert.That(modTarget.modBitCostText.Text, Does.Contain("{{R|A}}{{C|C}}"));
                Assert.That(modTarget.modBitCostText.Text, Does.Contain("{{K|| 素材 |}}"));
                Assert.That(modTarget.modBitCostText.Text, Does.Contain("-または-"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringDetailsLineTranslationPatch), "TinkeringDetails.Text"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringDetailsLineTranslationPatch), "TinkeringDetails.DescriptionText"),
                    Is.GreaterThan(0));
                Assert.That(
                    modTarget.modDescriptionText.Text,
                    Is.EqualTo("{{rules|巨大: この武器はダメージ+3、装甲切断でAV-3を与える。これは巨大な生物しか装備できない。}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringDetailsLineTranslationPatch), "TinkeringDetails.ModBitCostText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringDetailsLineTranslationPatch), "TinkeringDetails.ModDescriptionText"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringLinePostfix_PreservesColoredBitCostTags_WhenItemLineDoesNotNeedFragmentTranslation()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTinkeringLineTarget), nameof(DummyTinkeringLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TinkeringLineTranslationPatch), nameof(TinkeringLineTranslationPatch.Postfix))));

            var target = new DummyTinkeringLineTarget();
            target.setData(new DummyTinkeringLineDataTarget
            {
                mode = 0,
                costString = "{{R|A}}{{C|C}}",
                data = new DummyTinkeringRecipeData
                {
                    DisplayName = "チェーンピストル",
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Is.EqualTo("    チェーンピストル [{{R|A}}{{C|C}}]"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringBitsLinePostfix_TranslatesOnlyBitCategoryNames_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTinkeringBitsLineTarget), nameof(DummyTinkeringBitsLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TinkeringBitsLineTranslationPatch), nameof(TinkeringBitsLineTranslationPatch.Postfix))));

            var numericTarget = new DummyTinkeringBitsLineTarget();
            numericTarget.setData(new DummyTinkeringBitsLineDataTarget
            {
                bit = "{{R|1 scrap power systems}}",
            });

            var alphaTarget = new DummyTinkeringBitsLineTarget();
            alphaTarget.setData(new DummyTinkeringBitsLineDataTarget
            {
                bit = "{{Y|A AI microcontrollers}}",
            });

            var unknownTarget = new DummyTinkeringBitsLineTarget();
            unknownTarget.setData(new DummyTinkeringBitsLineDataTarget
            {
                bit = "{{R|? unknown component}}",
            });

            Assert.Multiple(() =>
            {
                Assert.That(numericTarget.OriginalExecuted, Is.True);
                Assert.That(numericTarget.text.Text, Is.EqualTo("{{R|1 スクラップ動力系}}"));
                Assert.That(alphaTarget.text.Text, Is.EqualTo("{{Y|A AIマイクロコントローラ}}"));
                Assert.That(unknownTarget.text.Text, Is.EqualTo("{{R|? unknown component}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringBitsLineTranslationPatch), "TinkeringBitsLine.Text"),
                    Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringBitsLinePostfix_StripsDirectMarker_WhenTextIsAlreadyTranslated()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTinkeringBitsLineTarget), nameof(DummyTinkeringBitsLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TinkeringBitsLineTranslationPatch), nameof(TinkeringBitsLineTranslationPatch.Postfix))));

            var target = new DummyTinkeringBitsLineTarget();
            target.setData(new DummyTinkeringBitsLineDataTarget
            {
                bit = MessageFrameTranslator.MarkDirectTranslation("{{R|? unknown component}}"),
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("{{R|? unknown component}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringBitsLineTranslationPatch), "TinkeringBitsLine.Text"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TinkeringDetailsLinePostfix_StripsDirectMarkers_WhenTextAlreadyTranslated()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyTinkeringDetailsLineTarget), nameof(DummyTinkeringDetailsLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(TinkeringDetailsLineTranslationPatch), nameof(TinkeringDetailsLineTranslationPatch.Postfix))));

            var target = new DummyTinkeringDetailsLineTarget();
            target.setData(new DummyTinkeringLineDataTarget
            {
                data = new DummyTinkeringRecipeData
                {
                    Type = "Build",
                    DisplayName = MessageFrameTranslator.MarkDirectTranslation("既訳名"),
                    UnclippedDescription = MessageFrameTranslator.MarkDirectTranslation("既訳説明"),
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.text.Text, Is.EqualTo("既訳名"));
                Assert.That(target.descriptionText.Text, Is.EqualTo("既訳説明"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringDetailsLineTranslationPatch), "TinkeringDetails.Text"),
                    Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(TinkeringDetailsLineTranslationPatch), "TinkeringDetails.DescriptionText"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
