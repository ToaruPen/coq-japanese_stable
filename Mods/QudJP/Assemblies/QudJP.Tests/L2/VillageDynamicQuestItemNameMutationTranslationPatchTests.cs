using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VillageDynamicQuestItemNameMutationTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
        DummyVillageDynamicQuestContextTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void GetQuestItemNameMutation_TranslatesGeneratedMutation_WhenPatched()
    {
        WithPatchedGetQuestItemNameMutation(() =>
        {
            DummyVillageDynamicQuestContextTarget.NextResult = "Copper Nugget of the Holy Wheel";
            var target = new DummyVillageDynamicQuestContextTarget();

            var result = target.getQuestItemNameMutation("Copper Nugget");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("聖なる車輪の銅塊"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GetQuestItemNameMutation_DoesNotRecordHit_ForDirectMarker()
    {
        WithPatchedGetQuestItemNameMutation(() =>
        {
            DummyVillageDynamicQuestContextTarget.NextResult =
                MessageFrameTranslator.DirectTranslationMarker + "holy copper nugget";
            var target = new DummyVillageDynamicQuestContextTarget();

            var result = target.getQuestItemNameMutation("copper nugget");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("holy copper nugget"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedGetQuestItemNameMutation(Action action)
    {
        var harmonyId = "qudjp.tests.village-dynamic-quest-item-name-mutation." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyVillageDynamicQuestContextTarget),
                    nameof(DummyVillageDynamicQuestContextTarget.getQuestItemNameMutation),
                    typeof(string)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(VillageDynamicQuestItemNameMutationTranslationPatch),
                    nameof(VillageDynamicQuestItemNameMutationTranslationPatch.Postfix),
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
            nameof(VillageDynamicQuestItemNameMutationTranslationPatch),
            nameof(VillageDynamicQuestItemNameMutationTranslationPatch) + ".getQuestItemNameMutation");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.GetFullPath(
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

internal sealed class DummyVillageDynamicQuestContextTarget
{
    public static string NextResult { get; set; } = "Copper Nugget of the Holy Wheel";

    public static void Reset()
    {
        NextResult = "Copper Nugget of the Holy Wheel";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string getQuestItemNameMutation(string name)
    {
        _ = GetType();
        _ = name;
        return NextResult;
    }
}
