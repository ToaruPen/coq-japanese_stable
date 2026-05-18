using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ItemNamingGeneratedNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
        DummyItemNamingGeneratedNameTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void GenerateRelicStyleName_TranslatesZoneStyleName_WhenPatched()
    {
        WithPatchedGenerateRelicStyleName(() =>
        {
            DummyItemNamingGeneratedNameTarget.NextResult = "The Dominant Sword of the Asphalt Mines";
            var element = "might";
            var type = "LongBlade";

            var result = DummyItemNamingGeneratedNameTarget.GenerateRelicStyleName(
                item: new object(),
                owner: new object(),
                kill: null,
                influencedBy: null,
                zoneId: "JoppaWorld.1.1.10.10.10",
                ref element,
                ref type);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("アスファルト鉱山の支配的な剣"));
                Assert.That(element, Is.EqualTo("might"));
                Assert.That(type, Is.EqualTo("LongBlade"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void GenerateRelicStyleName_LeavesCultureNameUnchanged_WhenPatched()
    {
        WithPatchedGenerateRelicStyleName(() =>
        {
            DummyItemNamingGeneratedNameTarget.NextResult = "Qwernalax";
            var element = "might";
            var type = "LongBlade";

            var result = DummyItemNamingGeneratedNameTarget.GenerateRelicStyleName(
                item: new object(),
                owner: new object(),
                kill: null,
                influencedBy: null,
                zoneId: null,
                ref element,
                ref type);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Qwernalax"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedGenerateRelicStyleName(Action action)
    {
        var harmonyId = "qudjp.tests.item-naming-generated-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyItemNamingGeneratedNameTarget),
                    nameof(DummyItemNamingGeneratedNameTarget.GenerateRelicStyleName),
                    typeof(object),
                    typeof(object),
                    typeof(object),
                    typeof(object),
                    typeof(string),
                    typeof(string).MakeByRefType(),
                    typeof(string).MakeByRefType()),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(ItemNamingGeneratedNameTranslationPatch),
                    nameof(ItemNamingGeneratedNameTranslationPatch.Postfix),
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
            ItemNamingGeneratedNameTranslationPatch.Context,
            ItemNamingGeneratedNameTranslationPatch.Family);
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

internal static class DummyItemNamingGeneratedNameTarget
{
    public static string NextResult { get; set; } = "The Dominant Sword of the Asphalt Mines";

    public static void Reset()
    {
        NextResult = "The Dominant Sword of the Asphalt Mines";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GenerateRelicStyleName(
        object item,
        object owner,
        object? kill,
        object? influencedBy,
        string? zoneId,
        ref string element,
        ref string type)
    {
        _ = item;
        _ = owner;
        _ = kill;
        _ = influencedBy;
        _ = zoneId;
        _ = element;
        _ = type;
        return NextResult;
    }
}
