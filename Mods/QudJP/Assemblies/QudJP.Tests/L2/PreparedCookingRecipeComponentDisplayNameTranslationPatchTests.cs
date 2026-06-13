using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PreparedCookingRecipeComponentDisplayNameTranslationPatchTests
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
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "{{C|1}} serving of {{w|山羊肉ジャーキー}}",
        "{{w|山羊肉ジャーキー}}{{C|1}}食分")]
    [TestCase(
        "1 dram of {{w|honey}}",
        "{{w|ハチミツ}}1ドラム")]
    public void GetDisplayName_TranslatesRecipeComponentQuantity_WhenPatched(string source, string expected)
    {
        WithPatchedDisplayName(() =>
        {
            var target = new DummyPreparedCookingRecipeComponentDisplayNameTarget
            {
                DisplayNameResult = source,
            };

            var result = target.getDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GetDisplayName_LeavesUnknownRecipeComponentQuantity_WhenPatched()
    {
        WithPatchedDisplayName(() =>
        {
            var target = new DummyPreparedCookingRecipeComponentDisplayNameTarget
            {
                DisplayNameResult = "{{C|1}} serving of mystery chunk",
            };

            var result = target.getDisplayName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{C|1}} serving of mystery chunk"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedDisplayName(Action action)
    {
        var harmonyId = "qudjp.tests.prepared-cooking-component-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyPreparedCookingRecipeComponentDisplayNameTarget),
                    nameof(DummyPreparedCookingRecipeComponentDisplayNameTarget.getDisplayName)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(PreparedCookingRecipeComponentDisplayNameTranslationPatch),
                    nameof(PreparedCookingRecipeComponentDisplayNameTranslationPatch.Postfix),
                    typeof(string).MakeByRefType())));
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
            PreparedCookingRecipeComponentDisplayNameTranslationPatch.Context,
            PreparedCookingRecipeComponentDisplayNameTranslationPatch.Family);
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

internal sealed class DummyPreparedCookingRecipeComponentDisplayNameTarget
{
    public string DisplayNameResult { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string getDisplayName()
    {
        return DisplayNameResult;
    }
}
