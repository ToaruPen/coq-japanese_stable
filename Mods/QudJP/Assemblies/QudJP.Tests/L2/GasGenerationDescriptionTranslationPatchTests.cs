using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GasGenerationDescriptionTranslationPatchTests
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
    public void SyncFromBlueprint_TranslatesGeneratedDescription_WhenPatched()
    {
        WithPatchedSyncFromBlueprint(() =>
        {
            var mutation = new DummyGasGeneration
            {
                SourceDescription = "You release a burst of {{G|corrosive gas}} around yourself.",
            };

            mutation.SyncFromBlueprint();

            Assert.Multiple(() =>
            {
                Assert.That(mutation.GetDescription(), Is.EqualTo("周囲に{{G|腐食性ガス}}を噴出する。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void SyncFromBlueprint_TranslatesFallbackGeneratedDescription_WhenPatched()
    {
        WithPatchedSyncFromBlueprint(() =>
        {
            var mutation = new DummyGasGeneration
            {
                SourceDescription = "You release a gaseous burst around yourself.",
            };

            mutation.SyncFromBlueprint();

            Assert.That(mutation.GetDescription(), Is.EqualTo("周囲にガスを噴出する。"));
        });
    }

    [Test]
    public void SyncFromBlueprint_LeavesUnrelatedDescriptionUnchanged_WhenPatched()
    {
        WithPatchedSyncFromBlueprint(() =>
        {
            var mutation = new DummyGasGeneration
            {
                SourceDescription = "You breathe a cone of gas.",
            };

            mutation.SyncFromBlueprint();

            Assert.Multiple(() =>
            {
                Assert.That(mutation.GetDescription(), Is.EqualTo("You breathe a cone of gas."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedSyncFromBlueprint(Action action)
    {
        var harmonyId = "qudjp.tests.gas-generation-description." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGasGeneration), nameof(DummyGasGeneration.SyncFromBlueprint)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(GasGenerationDescriptionTranslationPatch),
                    nameof(GasGenerationDescriptionTranslationPatch.Postfix),
                    typeof(object))));
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
            GasGenerationDescriptionTranslationPatch.Context,
            GasGenerationDescriptionTranslationPatch.Family);
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

internal sealed class DummyGasGeneration
{
    public string SourceDescription { get; init; } = string.Empty;

    private string Description = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncFromBlueprint()
    {
        Description = SourceDescription;
    }

    public string GetDescription() => Description;
}
