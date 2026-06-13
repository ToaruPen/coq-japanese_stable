using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CookingRecipeDisplayNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void GetDisplayName_TranslatesGeneratedDishName_WhenPatched()
    {
        WithPatchedDisplayName(() =>
        {
            var target = new DummyCookingRecipeDisplayNameTarget
            {
                DisplayNameResult = "{{W|Fried Wafers}}",
            };

            var result = target.GetDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{W|揚げウェハー}}"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [TestCase("{{W|Apple Matz}}", "{{W|アップルマッツァ}}")]
    [TestCase("{{C|Apple Matz}}", "{{C|アップルマッツァ}}")]
    [TestCase("{{W|Bone Babka}}", "{{W|ボーンバブカ}}")]
    [TestCase("{{W|Cloaca Surprise}}", "{{W|クロアカ・サプライズ}}")]
    [TestCase("{{W|Crystal Delight}}", "{{W|クリスタル・ディライト}}")]
    [TestCase("{{W|Goat in Sweet Leaf}}", "{{W|甘葉包みのヤギ肉}}")]
    [TestCase("{{W|Hot and Spiny}}", "{{W|ホットアンドスパイニー}}")]
    [TestCase("{{W|Mah Lah Soup}}", "{{W|マーラースープ}}")]
    [TestCase("{{W|Mulled Mushroom Cider}}", "{{W|温めたマッシュルームサイダー}}")]
    [TestCase("{{W|The Porridge}}", "{{W|粥}}")]
    [TestCase("{{W|Tongue and Cheek}}", "{{W|タングアンドチーク}}")]
    public void GetDisplayName_TranslatesPresetRecipeName_WhenPatched(string source, string expected)
    {
        WithPatchedDisplayName(() =>
        {
            var target = new DummyCookingRecipeDisplayNameTarget
            {
                DisplayNameResult = source,
            };

            var result = target.GetDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GetDisplayName_TranslatesGeneratedDishPrepositionName_WhenPatched()
    {
        WithPatchedDisplayName(() =>
        {
            var target = new DummyCookingRecipeDisplayNameTarget
            {
                DisplayNameResult = "{{W|Bread With Salt}}",
            };

            var result = target.GetDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{W|パン：塩入り}}"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GetDisplayName_TranslatesObservedMixedLocalizedGeneratedDishName_WhenPatched()
    {
        WithPatchedDisplayName(() =>
        {
            var target = new DummyCookingRecipeDisplayNameTarget
            {
                DisplayNameResult = "{{W|カムシュルウールの Yogurt with 甲虫ジャーキー, Meat Rice, and Meat Kugel}}",
            };

            var result = target.GetDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{W|カムシュルウールのヨーグルト：甲虫ジャーキー、肉飯、肉クーゲル入り}}"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GetDisplayName_LeavesUnknownDishNameUnchanged_WhenPatched()
    {
        WithPatchedDisplayName(() =>
        {
            var target = new DummyCookingRecipeDisplayNameTarget
            {
                DisplayNameResult = "{{W|Qwern Wafers}}",
            };

            var result = target.GetDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{W|Qwern Wafers}}"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void GetDisplayName_StripsDirectMarkerWithoutRecordingTransform_WhenPatched()
    {
        WithPatchedDisplayName(() =>
        {
            var target = new DummyCookingRecipeDisplayNameTarget
            {
                DisplayNameResult = "\x01{{W|Fried Wafers}}",
            };

            var result = target.GetDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{W|Fried Wafers}}"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void GenerateRecipeTile_SuppressesDisplayNameTranslationForTileMatching()
    {
        WithPatchedDisplayNameAndTileScope(() =>
        {
            var target = new DummyCookingRecipeDisplayNameTarget
            {
                DisplayNameResult = "{{W|Fried Wafers}}",
            };

            var tileName = DummyCookingRecipeDisplayNameTarget.GenerateRecipeTile(target);
            var displayNameAfterTileScope = target.GetDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(tileName, Is.EqualTo("{{W|Fried Wafers}}"));
                Assert.That(displayNameAfterTileScope, Is.EqualTo("{{W|揚げウェハー}}"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    private static void WithPatchedDisplayName(Action action)
    {
        var harmonyId = "qudjp.tests.cooking-recipe-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchDisplayName(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedDisplayNameAndTileScope(Action action)
    {
        var harmonyId = "qudjp.tests.cooking-recipe-display-name-tile." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchDisplayName(harmony);
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyCookingRecipeDisplayNameTarget),
                    nameof(DummyCookingRecipeDisplayNameTarget.GenerateRecipeTile),
                    typeof(DummyCookingRecipeDisplayNameTarget)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(CookingRecipeGenerateRecipeTileTranslationScopePatch),
                    nameof(CookingRecipeGenerateRecipeTileTranslationScopePatch.Prefix),
                    typeof(int).MakeByRefType())),
                finalizer: new HarmonyMethod(RequireMethod(
                    typeof(CookingRecipeGenerateRecipeTileTranslationScopePatch),
                    nameof(CookingRecipeGenerateRecipeTileTranslationScopePatch.Finalizer),
                    typeof(int))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchDisplayName(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyCookingRecipeDisplayNameTarget),
                nameof(DummyCookingRecipeDisplayNameTarget.GetDisplayName)),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(CookingRecipeDisplayNameTranslationPatch),
                nameof(CookingRecipeDisplayNameTranslationPatch.Postfix),
                typeof(string).MakeByRefType())));
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            CookingRecipeDisplayNameTranslationPatch.Context,
            CookingRecipeDisplayNameTranslationPatch.Family);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }
}

internal sealed class DummyCookingRecipeDisplayNameTarget
{
    public string DisplayNameResult { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GetDisplayName()
    {
        return DisplayNameResult;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GenerateRecipeTile(DummyCookingRecipeDisplayNameTarget recipe)
    {
        return recipe.GetDisplayName();
    }
}
