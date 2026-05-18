using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfireDescribeMealTranslationPatchTests
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
    public void DescribeMeal_TranslatesGeneratedCookTemplate_WhenPatched()
    {
        WithPatchedDescribeMeal(() =>
        {
            DummyCampfireDescribeMealTarget.NextResult =
                "You gather some fixings: {{Y|starapple jam}}, some salt, and a dram of oil.\n\nYou toss them in a pot and stir.";

            var result = DummyCampfireDescribeMealTarget.DescribeMeal();

            Assert.Multiple(() =>
            {
                Assert.That(
                    result,
                    Is.EqualTo("いくつかの具材を集めた: {{Y|スターアップルジャム}}、塩少々と油1ドラム\n\nそれらを鍋に放り込み、かき混ぜた。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void DescribeMeal_LeavesUnknownResult_WhenPatched()
    {
        WithPatchedDescribeMeal(() =>
        {
            DummyCampfireDescribeMealTarget.NextResult = "{invalid meal: unknown}";

            var result = DummyCampfireDescribeMealTarget.DescribeMeal();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{invalid meal: unknown}"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void DescribeMeal_StripsDirectMarkerWithoutObservabilityHit_WhenPatched()
    {
        WithPatchedDescribeMeal(() =>
        {
            DummyCampfireDescribeMealTarget.NextResult =
                MessageFrameTranslator.DirectTranslationMarker + "You toss snapjaw haunch into a pot and stir.";

            var result = DummyCampfireDescribeMealTarget.DescribeMeal();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("You toss snapjaw haunch into a pot and stir."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedDescribeMeal(Action action)
    {
        var harmonyId = "qudjp.tests.campfire-describe-meal." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCampfireDescribeMealTarget), nameof(DummyCampfireDescribeMealTarget.DescribeMeal)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(CampfireDescribeMealTranslationPatch),
                    nameof(CampfireDescribeMealTranslationPatch.Postfix),
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
            nameof(CampfireDescribeMealTranslationPatch),
            nameof(CampfireDescribeMealTranslationPatch) + ".CookTemplate");
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal static class DummyCampfireDescribeMealTarget
{
    public static string NextResult { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string DescribeMeal()
    {
        return NextResult;
    }
}
