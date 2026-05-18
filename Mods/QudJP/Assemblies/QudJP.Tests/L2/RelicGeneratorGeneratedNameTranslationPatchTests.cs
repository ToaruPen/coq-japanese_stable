using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class RelicGeneratorGeneratedNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
        DummyRelicGeneratorGeneratedNameTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void GenerateRelicName_TranslatesNormalGeneratedNameAndClearsArticle_WhenPatched()
    {
        WithPatchedGenerateRelicName(() =>
        {
            DummyRelicGeneratorGeneratedNameTarget.NextResult = "Edge of the Dominant Sword";
            DummyRelicGeneratorGeneratedNameTarget.NextArticle = "the";

            var result = DummyRelicGeneratorGeneratedNameTarget.GenerateRelicName(
                "Axe",
                SnapRegion: null,
                "might",
                out var article);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("支配的な剣の刃"));
                Assert.That(article, Is.Empty);
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GenerateRelicName_TranslatesRegionGeneratedNameAndClearsArticle_WhenPatched()
    {
        WithPatchedGenerateRelicName(() =>
        {
            DummyRelicGeneratorGeneratedNameTarget.NextResult = "Dominant Sword of Bethesda Susa";
            DummyRelicGeneratorGeneratedNameTarget.NextArticle = "the";

            var result = DummyRelicGeneratorGeneratedNameTarget.GenerateRelicName(
                "Axe",
                SnapRegion: new object(),
                "might",
                out var article);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Bethesda Susaの支配的な剣"));
                Assert.That(article, Is.Empty);
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GenerateRelicName_LeavesUnknownGeneratedNameUnchanged_WhenPatched()
    {
        WithPatchedGenerateRelicName(() =>
        {
            DummyRelicGeneratorGeneratedNameTarget.NextResult = "Edge of the Qwern Sword";
            DummyRelicGeneratorGeneratedNameTarget.NextArticle = "the";

            var result = DummyRelicGeneratorGeneratedNameTarget.GenerateRelicName(
                "Axe",
                SnapRegion: null,
                "might",
                out var article);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Edge of the Qwern Sword"));
                Assert.That(article, Is.EqualTo("the"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedGenerateRelicName(Action action)
    {
        var harmonyId = "qudjp.tests.relic-generated-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyRelicGeneratorGeneratedNameTarget),
                    nameof(DummyRelicGeneratorGeneratedNameTarget.GenerateRelicName),
                    typeof(string),
                    typeof(object),
                    typeof(string),
                    typeof(string).MakeByRefType()),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(RelicGeneratorGeneratedNameTranslationPatch),
                    nameof(RelicGeneratorGeneratedNameTranslationPatch.Postfix),
                    typeof(object),
                    typeof(string).MakeByRefType(),
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
            RelicGeneratorGeneratedNameTranslationPatch.Context,
            RelicGeneratorGeneratedNameTranslationPatch.Family);
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

internal static class DummyRelicGeneratorGeneratedNameTarget
{
    public static string NextResult { get; set; } = "Edge of the Dominant Sword";

    public static string NextArticle { get; set; } = "the";

    public static void Reset()
    {
        NextResult = "Edge of the Dominant Sword";
        NextArticle = "the";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GenerateRelicName(string Type, object? SnapRegion, string Element, out string Article)
    {
        Article = NextArticle;
        return NextResult;
    }
}
