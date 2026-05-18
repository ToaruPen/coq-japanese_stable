using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfireRollIngredientsTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
        DummyCampfireRollIngredientsTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void RollIngredients_TranslatesGeneratedIngredientFragments_WhenPatched()
    {
        WithPatchedRollIngredients(() =>
        {
            DummyCampfireRollIngredientsTarget.NextResult =
            [
                "a pinch of salt",
                "some bread",
                "a dram of {{C|water}}",
                "a pinch of qwern",
            ];

            var result = DummyCampfireRollIngredientsTarget.RollIngredients(4);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(new[]
                {
                    "塩ひとつまみ",
                    "パン少々",
                    "{{C|水}}1ドラム",
                    "a pinch of qwern",
                }));
                Assert.That(HitCount(), Is.EqualTo(3));
            });
        });
    }

    private static void WithPatchedRollIngredients(Action action)
    {
        var harmonyId = "qudjp.tests.campfire-roll-ingredients." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyCampfireRollIngredientsTarget),
                    nameof(DummyCampfireRollIngredientsTarget.RollIngredients),
                    typeof(int)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(CampfireRollIngredientsTranslationPatch),
                    nameof(CampfireRollIngredientsTranslationPatch.Postfix),
                    typeof(string[]).MakeByRefType())));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(CampfireRollIngredientsTranslationPatch),
            nameof(CampfireRollIngredientsTranslationPatch) + ".IngredientFragment");
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

internal static class DummyCampfireRollIngredientsTarget
{
    public static string[] NextResult { get; set; } = [];

    public static void Reset()
    {
        NextResult = [];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string[] RollIngredients(int amount)
    {
        return NextResult;
    }
}
